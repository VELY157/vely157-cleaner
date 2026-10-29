# Instala el servidor de licencias VELY157 como servicio de Windows y actualiza la página "Muy pronto".
# Solo toca velymediallc.com. Ejecutar como Administrador.

$ErrorActionPreference = 'Stop'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Este script necesita permisos de Administrador." -ForegroundColor Red
    Read-Host "Enter para salir"; exit 1
}

$here     = Split-Path -Parent $MyInvocation.MyCommand.Path
$source   = Join-Path $here 'VelyLicense\publish'
$target   = 'C:\VelyLicense\app'
$svcName  = 'VelyLicense'
$webSrc   = Join-Path $here 'velymediallc-web\index.html'
$webDst   = 'C:\inetpub\velymediallc\index.html'

# 1. Servidor de licencias
Write-Host "`n[1/2] Servidor de licencias (api.velymediallc.com -> localhost:5157)" -ForegroundColor Cyan
if (Get-Service $svcName -ErrorAction SilentlyContinue) { Stop-Service $svcName -Force }
# La copia de prueba que dejó Claude corriendo
Get-Process VelyLicense -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item "$source\*" $target -Recurse -Force

if (-not (Get-Service $svcName -ErrorAction SilentlyContinue)) {
    New-Service -Name $svcName -DisplayName 'VELY157 License Server' -BinaryPathName "`"$target\VelyLicense.exe`"" -StartupType Automatic -Description 'Servidor de licencias de VELY157 Cleaner (api.velymediallc.com)' | Out-Null
    sc.exe failure $svcName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null
}
Start-Service $svcName
Start-Sleep -Seconds 3
try {
    $h = Invoke-RestMethod 'http://localhost:5157/api/v1/health' -TimeoutSec 10
    Write-Host "  Servicio $svcName -> $((Get-Service $svcName).Status), responde: $($h.ok)" -ForegroundColor Green
} catch {
    Write-Host "  El servicio no responde: $($_.Exception.Message)" -ForegroundColor Red
}

# 2. Limpiador para auto-actualización (lo descarga api.velymediallc.com/download/cleaner)
Write-Host "`n[2/3] Limpiador para auto-actualizacion" -ForegroundColor Cyan
$cleanerSrc = Join-Path $here 'VelyCleaner\publish\VELY157 Cleaner.exe'
$cleanerDir = Join-Path $env:ProgramData 'VelyLicense\cleaner'
if (Test-Path $cleanerSrc) {
    New-Item -ItemType Directory -Force $cleanerDir | Out-Null
    Copy-Item $cleanerSrc (Join-Path $cleanerDir 'VELY157 Cleaner.exe') -Force
    $ver = (Get-Item $cleanerSrc).VersionInfo.ProductVersion
    if (-not $ver) { $ver = '1.0.0' }
    Set-Content (Join-Path $cleanerDir 'version.txt') ($ver.Split('+')[0]) -NoNewline
    Write-Host "  Publicada version $ver para las actualizaciones." -ForegroundColor Green
} else {
    Write-Host "  (No encontre el limpiador compilado; omito las actualizaciones por ahora.)" -ForegroundColor Yellow
}

# 3. Página "Muy pronto" animada
Write-Host "`n[3/3] Pagina 'Muy pronto' de velymediallc.com" -ForegroundColor Cyan
Copy-Item $webSrc $webDst -Force
Write-Host "  Actualizada." -ForegroundColor Green

Write-Host "`nListo. Avisale a Claude." -ForegroundColor Green
Read-Host "Enter para cerrar"
