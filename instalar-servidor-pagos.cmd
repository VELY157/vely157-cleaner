@echo off
:: Instala en el servidor de licencias la version con pagos de NOWPayments (pide permiso de administrador).
:: Guarda una copia de la version anterior y de la base de datos antes de reemplazar.
net session >nul 2>&1
if %errorlevel% neq 0 (
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0actualizar-servidor-licencias.ps1" -Source "%~dp0VelyLicense\publish-pagos"
echo.
echo Si dice OK, el servidor ya tiene los pagos. Ahora pon tus claves en:
echo   C:\ProgramData\VelyLicense\nowpayments.json
echo.
pause
