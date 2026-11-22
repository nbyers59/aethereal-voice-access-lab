@echo off
setlocal
set "APP=%~dp0bin\Aethereal-Voice-Access-Lab-v0.4.0-rc1.exe"
if not exist "%APP%" (
 echo Executable missing. Run build.cmd first.
 pause
 exit /b 1
)
start "" "%APP%"
