#Requires -Version 5.1
$ErrorActionPreference = "Stop"
$backend = Join-Path (Split-Path -Parent $PSScriptRoot) "src\backend"

Push-Location $backend
try {
    dotnet ef database update --project Domus.Infrastructure --startup-project Domus.Api
}
finally {
    Pop-Location
}
