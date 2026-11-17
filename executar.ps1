# Roda o RadialShift a partir do código. Uso:
#   .\executar.ps1            (normal)
#   .\executar.ps1 -Diagnostico   (log em nível DEBUG)
#   .\executar.ps1 -Testes        (só roda os testes unitários)
param([switch]$Diagnostico, [switch]$Testes)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

# SDK instalado só para o usuário, fora do PATH (PC antigo). Se não existir, usa o SDK global do PATH.
$sdkUsuario = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
if (Test-Path (Join-Path $sdkUsuario 'dotnet.exe')) {
    $env:DOTNET_ROOT = $sdkUsuario
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

if ($Testes) {
    dotnet test src/RadialShift.Tests
    dotnet test src/RadialShift.Ferramentas.Tests
    return
}

$env:RADIALSHIFT_LOG_DEBUG = if ($Diagnostico) { '1' } else { '' }
dotnet run --project src/RadialShift.App
