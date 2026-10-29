@echo off
color a
title Vely Cache Cleaner

call :checkPermissions
call :VelyLogo
echo Press any key to clean cache...
pause > nul
cls

rd /s /f /q c:\windows\temp\*.*
rd /s /q c:\windows\temp
md c:\windows\temp
del /s /f /q C:\WINDOWS\Prefetch
del /s /f /q %temp%\*.*
rd /s /q %temp%
md %temp%
deltree /y c:\windows\tempor~1
deltree /y c:\windows\temp
deltree /y c:\windows\tmp
deltree /y c:\windows\ff*.tmp
deltree /y c:\windows\history
deltree /y c:\windows\cookies
deltree /y c:\windows\recent
deltree /y c:\windows\spool\printers
del c:\WIN386.SWP

for /F "tokens=*" %%G in ('wevtutil.exe el') DO (call :do_clear "%%G")

cls
call :paragonLogo
echo Logs and Cache have been Deleted!
echo.
echo Press any key to exit...
pause > nul
exit

:checkPermissions
fsutil dirty query %systemdrive% >nul
if %errorLevel% NEQ 0 (
	echo Try again as Administrator.
	echo.
	echo Press any key to exit...
	pause > nul
	exit
)
exit /b

:do_clear
echo clearing %1
wevtutil.exe cl %1
goto :eof

:VelyLogo
echo    157    888     888 8888888888 888    Y88b   Y88b      157
echo    157    888     888 888        888     Y88b   Y88b     157 
echo    157    888     888 888        888      Y88b   Y88b    157
echo    157    Y88b   d88P 8888888    888       Y88b 88Y88b   157
echo    157     Y88b d88P  888        888        Y88b         157
echo    157      Y88o88P   888        888         Y88b        157
echo    157       Y888P    888        888          Y88b       157
echo    157        Y8P     8888888888 8888888888    Y88b      157
echo.
exit /b