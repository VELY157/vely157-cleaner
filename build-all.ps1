# Compila todo: limpiador, instalador y servidor de licencias.
$ErrorActionPreference = 'Stop'
$env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'
$root = $PSScriptRoot

function Pub($proj, $argsExtra) {
    & dotnet publish $proj -c Release @argsExtra -p:DebugType=none 2>&1 |
        Select-String 'error|warning CS|->' | ForEach-Object { $_.Line.Trim() }
    if ($LASTEXITCODE -ne 0) { throw "falló $proj" }
}

Write-Host "`n== Limpiador ==" -ForegroundColor Cyan
Pub "$root\VelyCleaner\VelyCleaner.csproj" @('-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true','-p:EnableCompressionInSingleFile=true','-o',"$root\VelyCleaner\publish")
$cleaner = "$root\VelyCleaner\publish\VELY157 Cleaner.exe"

Write-Host "`n== Comprimiendo limpiador para el instalador ==" -ForegroundColor Cyan
$payloadDir = "$root\VelyInstaller\payload"
New-Item -ItemType Directory -Force $payloadDir | Out-Null
$gz = "$payloadDir\cleaner.exe.gz"
$in = [System.IO.File]::OpenRead($cleaner)
$out = [System.IO.File]::Create($gz)
$zip = New-Object System.IO.Compression.GZipStream($out, [System.IO.Compression.CompressionLevel]::SmallestSize)
$in.CopyTo($zip); $zip.Dispose(); $out.Dispose(); $in.Dispose()
"{0:N1} MB exe  ->  {1:N1} MB comprimido" -f ((Get-Item $cleaner).Length/1MB), ((Get-Item $gz).Length/1MB)

Write-Host "`n== Instalador ==" -ForegroundColor Cyan
Pub "$root\VelyInstaller\VelyInstaller.csproj" @('-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true','-p:EnableCompressionInSingleFile=true','-o',"$root\VelyInstaller\publish")
$installer = "$root\VelyInstaller\publish\Instalar VELY157 Cleaner.exe"
"instalador: {0:N1} MB" -f ((Get-Item $installer).Length/1MB)

Write-Host "`n== Servidor de licencias ==" -ForegroundColor Cyan
Pub "$root\VelyLicense\VelyLicense.csproj" @('-r','win-x64','--self-contained','false','-o',"$root\VelyLicense\publish")

Write-Host "`nTodo compilado." -ForegroundColor Green
