#Requires -Version 5.1
<#
.SYNOPSIS
  Valida fluxo Docker: API + EMQX + auth/ACL hooks + heartbeat/status.

.NOTES
  Pré-requisito: Docker Desktop / docker CLI no PATH.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$baseUrl = $env:DOMUS_API_URL
if ([string]::IsNullOrWhiteSpace($baseUrl)) { $baseUrl = "http://localhost:8080" }

function Assert-Docker {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw "Docker CLI não encontrado no PATH. Instale o Docker Desktop e reexecute este script."
    }
}

function Wait-Http($url, $timeoutSec = 120) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $timeoutSec) {
        try {
            $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5
            if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 300) { return }
        } catch { Start-Sleep -Seconds 3 }
    }
    throw "Timeout aguardando $url"
}

Assert-Docker

if (-not (Test-Path "$root\.env")) {
    Copy-Item "$root\.env.example" "$root\.env"
}

Push-Location "$root\docker"
try {
    Write-Host "==> Subindo stack..." -ForegroundColor Cyan
    docker compose --env-file "$root\.env" up -d --build

    Write-Host "==> Aguardando /health (Postgres)..."
    Wait-Http "$baseUrl/health"

    Write-Host "==> Aguardando /health/ready (Postgres+EMQX)..."
    Wait-Http "$baseUrl/health/ready" 180

    $email = "mqtt-val-$([guid]::NewGuid().ToString('N').Substring(0,8))@domus.test"
    $register = @{
        email = $email
        password = "SenhaForte1!"
        name = "MQTT Validator"
        tenantName = "MqttVal"
        residenceName = "Casa"
        timezone = "America/Sao_Paulo"
    } | ConvertTo-Json

    $auth = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/auth/register" -ContentType "application/json" -Body $register
    $headers = @{ Authorization = "Bearer $($auth.accessToken)" }

    $deviceBody = @{ type = "Gate"; name = "Portao Validacao" } | ConvertTo-Json
    $device = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/residences/$($auth.residenceId)/devices" -Headers $headers -ContentType "application/json" -Body $deviceBody

    $prov = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/devices/$($device.id)/provisioning" -Headers $headers -ContentType "application/json" -Body "{}"
    $activateBody = @{
        provisioningCode = $prov.provisioningCode
        hardwareId = "hw-val-$([guid]::NewGuid().ToString('N').Substring(0,12))"
        firmwareVersion = "1.0.0-val"
    } | ConvertTo-Json

    $activated = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/devices/activate" -ContentType "application/json" -Body $activateBody

    Write-Host "==> Device ativado: $($activated.deviceId)"
    Write-Host "    MQTT user: $($activated.mqttUsername)"
    Write-Host "    Topics: $($activated.topicHeartbeat) / $($activated.topicStatus)"

    Write-Host "==> Testando hooks auth/ACL..."
    $hookSecret = (Get-Content "$root\.env" | Where-Object { $_ -match '^MQTT_HOOK_SECRET=(.+)$' } | ForEach-Object { $Matches[1] })
    if (-not $hookSecret) { $hookSecret = "domus_mqtt_hook_dev_secret" }
    $hookHeaders = @{ "X-Domus-Mqtt-Hook" = $hookSecret; "Content-Type" = "application/json" }

    $authOk = Invoke-RestMethod -Method Post -Uri "$baseUrl/internal/mqtt/auth" -Headers $hookHeaders -Body (@{
        username = $activated.mqttUsername
        password = $activated.mqttPassword
    } | ConvertTo-Json)
    if ($authOk.result -ne "allow") { throw "Auth hook negou device válido" }

    $aclOk = Invoke-RestMethod -Method Post -Uri "$baseUrl/internal/mqtt/acl" -Headers $hookHeaders -Body (@{
        username = $activated.mqttUsername
        topic = $activated.topicStatus
        action = "publish"
    } | ConvertTo-Json)
    if ($aclOk.result -ne "allow") { throw "ACL hook negou publish status próprio" }

    $aclDeny = Invoke-RestMethod -Method Post -Uri "$baseUrl/internal/mqtt/acl" -Headers $hookHeaders -Body (@{
        username = $activated.mqttUsername
        topic = "domus/$([guid]::NewGuid())/$([guid]::NewGuid())/status"
        action = "publish"
    } | ConvertTo-Json)
    if ($aclDeny.result -ne "deny") { throw "ACL hook deveria negar outro tenant/device" }

    Write-Host "==> OK: health, activate, auth hook, ACL allow/deny" -ForegroundColor Green
    Write-Host "Próximo (manual/MQTT client): publicar heartbeat/status com as credenciais e conferir GET /api/devices/$($device.id)"
    Write-Host "Contrato: docs/mqtt-contract.md (messageId, QoS, retain)"
}
finally {
    Pop-Location
}
