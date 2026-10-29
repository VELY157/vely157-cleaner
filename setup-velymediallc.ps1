# Setup de velymediallc.com (solo este dominio; no toca los otros sitios ni tuneles)
# Ejecutar como Administrador.

$ErrorActionPreference = 'Stop'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Este script necesita permisos de Administrador." -ForegroundColor Red
    Write-Host "Clic derecho -> Ejecutar con PowerShell como administrador."
    Read-Host "Enter para salir"; exit 1
}

$cloudflared = 'C:\Program Files (x86)\cloudflared\cloudflared.exe'
$config      = 'C:\Users\odavely\.cloudflared\velymedia.yml'
$svcName     = 'Cloudflared-VelyMedia'
$appcmd      = "$env:windir\System32\inetsrv\appcmd.exe"
$siteName    = 'velymediallc'
$sitePath    = 'C:\inetpub\velymediallc'
$sitePort    = 802

# 1. Servicio de Windows para el tunel velymedia
Write-Host "`n[1/3] Servicio del tunel velymedia" -ForegroundColor Cyan
if (Get-Service $svcName -ErrorAction SilentlyContinue) {
    Write-Host "  Ya existe, lo reinicio."
    Restart-Service $svcName
} else {
    $binPath = "`"$cloudflared`" --config `"$config`" tunnel run"
    New-Service -Name $svcName -DisplayName 'Cloudflared VelyMedia' -BinaryPathName $binPath -StartupType Automatic | Out-Null
    sc.exe failure $svcName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null
    Start-Service $svcName
}
Write-Host "  $svcName -> $((Get-Service $svcName).Status)" -ForegroundColor Green

# 2. Sitio IIS en el puerto 802
Write-Host "`n[2/3] Sitio IIS '$siteName' en el puerto $sitePort" -ForegroundColor Cyan
New-Item -ItemType Directory -Force $sitePath | Out-Null
if (-not (Test-Path "$sitePath\index.html")) {
    @'
<!doctype html>
<html lang="es">
<head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Vely Media LLC</title>
<style>body{margin:0;min-height:100vh;display:grid;place-items:center;background:#0b0f14;color:#e6edf3;font-family:Segoe UI,system-ui,sans-serif}h1{font-size:2.4rem;margin:0}p{color:#8b949e}</style>
</head>
<body><div style="text-align:center"><h1>Vely Media LLC</h1><p>Muy pronto.</p></div></body>
</html>
'@ | Set-Content -Encoding UTF8 "$sitePath\index.html"
}
$existing = & $appcmd list site /name:$siteName 2>$null
if ($existing) {
    Write-Host "  El sitio ya existe."
} else {
    & $appcmd add apppool /name:$siteName | Out-Null
    & $appcmd add site /name:$siteName /bindings:"http/*:${sitePort}:" /physicalPath:$sitePath | Out-Null
    & $appcmd set app "$siteName/" /applicationPool:$siteName | Out-Null
}
& $appcmd start site /site.name:$siteName 2>$null | Out-Null
Write-Host "  $(& $appcmd list site /name:$siteName)" -ForegroundColor Green

# 3. .NET 8 SDK (para el servidor de licencias y el limpiador)
Write-Host "`n[3/3] .NET 8 SDK" -ForegroundColor Cyan
if ((Get-Command dotnet -ErrorAction SilentlyContinue) -and (dotnet --list-sdks | Select-String '^8\.')) {
    Write-Host "  Ya instalado."
} else {
    winget install --id Microsoft.DotNet.SDK.8 --exact --silent --accept-package-agreements --accept-source-agreements
}

Write-Host "`nListo. Avisale a Claude." -ForegroundColor Green
Read-Host "Enter para cerrar"
