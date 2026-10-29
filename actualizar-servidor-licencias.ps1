# Actualiza el servidor de licencias (servicio VelyLicense) con una versión nueva ya compilada.
# Uso (como Administrador): .\actualizar-servidor-licencias.ps1 -Source <carpeta publicada>
# Guarda una copia de la versión anterior y de la base de datos antes de reemplazar.
param([Parameter(Mandatory)][string]$Source)

$ErrorActionPreference = 'Stop'
$target  = 'C:\VelyLicense\app'
$svc     = 'VelyLicense'
$dataDir = Join-Path $env:ProgramData 'VelyLicense'
$stamp   = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup  = "C:\VelyLicense\backup-$stamp"
$log     = Join-Path $PSScriptRoot 'actualizar-servidor-licencias.log'

Start-Transcript $log -Force | Out-Null
try {
    New-Item -ItemType Directory -Force $backup | Out-Null
    Copy-Item "$target\*" "$backup\app" -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item "$dataDir\licenses.db*" $backup -Force

    Stop-Service $svc -Force
    Start-Sleep -Seconds 2
    Copy-Item "$Source\*" $target -Recurse -Force
    Start-Service $svc
    Start-Sleep -Seconds 3

    $h = Invoke-RestMethod 'http://localhost:5157/api/v1/health' -TimeoutSec 10
    Write-Host "OK servicio=$((Get-Service $svc).Status) inicio=$((Get-Service $svc).StartType) responde=$($h.ok) copia=$backup"
} catch {
    Write-Host "ERROR $($_.Exception.Message). Copia anterior en $backup"
    if ((Get-Service $svc).Status -ne 'Running') { Copy-Item "$backup\app\*" $target -Recurse -Force; Start-Service $svc }
} finally {
    Stop-Transcript | Out-Null
}
