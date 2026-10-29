# Publica una nueva versión del limpiador para que los clientes la reciban con el botón "Actualizar".
# Uso:  powershell -ExecutionPolicy Bypass -File publicar-actualizacion.ps1 -Version 1.0.1
param([Parameter(Mandatory)][string]$Version)

$ErrorActionPreference = 'Stop'
$env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'
$root = $PSScriptRoot

if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "La version debe ser como 1.0.1" }

Write-Host "Poniendo version $Version en el limpiador..." -ForegroundColor Cyan
$csproj = "$root\VelyCleaner\VelyCleaner.csproj"
(Get-Content $csproj -Raw) -replace '<Version>[\d.]+</Version>', "<Version>$Version</Version>" | Set-Content $csproj -NoNewline

Write-Host "Compilando limpiador e instalador..." -ForegroundColor Cyan
& "$root\build-all.ps1"

$cleanerDir = Join-Path $env:ProgramData 'VelyLicense\cleaner'
if (-not (Test-Path $cleanerDir)) {
    Write-Host "El servidor de licencias no esta instalado aun. Ejecuta install-velylicense.ps1 primero." -ForegroundColor Yellow
    exit 1
}
Copy-Item "$root\VelyCleaner\publish\VELY157 Cleaner.exe" (Join-Path $cleanerDir 'VELY157 Cleaner.exe') -Force
Set-Content (Join-Path $cleanerDir 'version.txt') $Version -NoNewline

# Actualizar tambien el instalador del escritorio para que las instalaciones nuevas traigan la ultima version
$newInstaller = "$root\VelyInstaller\publish\Instalar VELY157 Cleaner.exe"
$desk = 'C:\Users\odavely\Desktop\VELY157 Cleaner'
if (Test-Path $desk) { Copy-Item $newInstaller (Join-Path $desk 'Instalar VELY157 Cleaner.exe') -Force }

# Mantener al dia la copia de descarga de la pagina web (si existe esa carpeta)
$webDl = 'C:\Users\odavely\Desktop\skril\velymediallc-web\descargas'
if (Test-Path $webDl) {
    Copy-Item $newInstaller (Join-Path $webDl 'VELY157-Cleaner-Setup.exe') -Force
    Write-Host "  Copia de descarga de la web actualizada. Corre publicar-web.cmd (como admin) para publicarla." -ForegroundColor Yellow
}

Write-Host "`nListo. Los clientes con version anterior veran el boton 'Actualizar' (version $Version)." -ForegroundColor Green
