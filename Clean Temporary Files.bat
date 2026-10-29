@echo off
@echo code VELY157
@echo.
@echo Delete All Temporary Files?
@echo.
pause

color 3
Del /S /F /Q %temp%
Del /S /F /Q %Windir%\Temp
Del /S /F /Q C:\WINDOWS\Prefetch
Del /S /F /Q temp
@echo.
@echo All Temporary Files Sucessfully Deleted!
@echo. 
pause 