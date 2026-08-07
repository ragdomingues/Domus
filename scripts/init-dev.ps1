#Requires -Version 5.1
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Resolve-DockerExe {
    $cmd = Get-Command docker -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source) {
        return $cmd.Source
    }

    $candidates = @(
        "$env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin\docker.exe",
        "$env:ProgramFiles\Docker\Docker\resources\bin\docker.exe",
        "${env:ProgramFiles(x86)}\Docker\Docker\resources\bin\docker.exe"
    )

    foreach ($path in $candidates) {
        if (Test-Path $path) {
            return $path
        }
    }

    return $null
}

Write-Host "==> Domus FASE 1 - init dev" -ForegroundColor Cyan

$docker = Resolve-DockerExe
if (-not $docker) {
    Write-Host "ERROR: docker.exe nao encontrado no PATH." -ForegroundColor Red
    Write-Host "Docker Desktop parece instalado, mas o CLI nao esta no PATH."
    Write-Host "1) Abra o Docker Desktop e espere ficar 'Running'"
    Write-Host "2) Feche e reabra o terminal (ou o Cursor)"
    Write-Host "3) Ou adicione ao PATH do usuario:"
    Write-Host "   $env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin"
    exit 1
}

$dockerBin = Split-Path -Parent $docker
if ($env:PATH -notlike "*$dockerBin*") {
    $env:PATH = "$dockerBin;$env:PATH"
    Write-Host "Usando Docker CLI: $docker"
}

if (-not (Test-Path (Join-Path $root ".env"))) {
    Copy-Item (Join-Path $root ".env.example") (Join-Path $root ".env")
    Write-Host "Created .env from .env.example"
}

# Preflight: daemon precisa estar no ar (no Windows isso exige WSL2).
$info = & $docker info 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $info -match "unable to start|error during connect|Is the docker daemon running") {
    Write-Host ""
    Write-Host "ERROR: Docker Desktop instalado, mas o engine NAO esta rodando." -ForegroundColor Red
    Write-Host "No Windows, Docker Desktop precisa do WSL 2."
    Write-Host ""
    Write-Host "Faça isto (PowerShell como Administrador):"
    Write-Host "  wsl --install"
    Write-Host "Depois reinicie o PC, abra o Docker Desktop, espere 'Running' e rode de novo:"
    Write-Host "  .\scripts\init-dev.ps1"
    Write-Host ""
    Write-Host "Detalhe tecnico: $info" -ForegroundColor DarkGray
    exit 1
}

Push-Location (Join-Path $root "docker")
try {
    & $docker compose --env-file (Join-Path $root ".env") up -d --build
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose falhou com codigo $LASTEXITCODE"
    }
    Write-Host "Stack starting."
    Write-Host "API:     http://127.0.0.1:8080"
    Write-Host "Health:  http://127.0.0.1:8080/health"
    Write-Host "Swagger: http://127.0.0.1:8080/swagger"
    Write-Host "(No Windows, use 127.0.0.1 em vez de localhost - IPv6 pode travar.)"
}
finally {
    Pop-Location
}
