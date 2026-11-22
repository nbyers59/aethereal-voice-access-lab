@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "ROOT=%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "SPEECH="
set "UICLIENT="
set "UITYPES="
if not exist "%ROOT%bin" mkdir "%ROOT%bin"
if not exist "%CSC%" (
 echo ERROR: Windows .NET Framework64 C# compiler not found.
 exit /b 2
)
for /d %%D in ("%WINDIR%\Microsoft.NET\assembly\GAC_MSIL\System.Speech\*") do if exist "%%~fD\System.Speech.dll" if not defined SPEECH set "SPEECH=%%~fD\System.Speech.dll"
for /d %%D in ("%WINDIR%\Microsoft.NET\assembly\GAC_MSIL\UIAutomationClient\*") do if exist "%%~fD\UIAutomationClient.dll" if not defined UICLIENT set "UICLIENT=%%~fD\UIAutomationClient.dll"
for /d %%D in ("%WINDIR%\Microsoft.NET\assembly\GAC_MSIL\UIAutomationTypes\*") do if exist "%%~fD\UIAutomationTypes.dll" if not defined UITYPES set "UITYPES=%%~fD\UIAutomationTypes.dll"
if not defined SPEECH (
 echo ERROR: System.Speech.dll missing.
 exit /b 3
)
if not defined UICLIENT (
 echo ERROR: UIAutomationClient.dll missing.
 exit /b 4
)
if not defined UITYPES (
 echo ERROR: UIAutomationTypes.dll missing.
 exit /b 5
)
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /out:"%ROOT%bin\Aethereal-Voice-Access-Lab-v0.4.0-rc1.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:"%SPEECH%" /r:"%UICLIENT%" /r:"%UITYPES%" "%ROOT%src\Program.cs" "%ROOT%src\ActionPolicy.cs" "%ROOT%src\DemoForm.cs" "%ROOT%src\SelfTest.cs" "%ROOT%src\NotepadBridge.cs" "%ROOT%src\NotepadLabForm.cs" "%ROOT%src\NotepadSelfTest.cs" "%ROOT%src\UnifiedForm.cs"
if errorlevel 1 (
 echo BUILD FAILED. Older compiled files remain unchanged.
 exit /b 6
)
echo BUILD PASS: "%ROOT%bin\Aethereal-Voice-Access-Lab-v0.4.0-rc1.exe"
exit /b 0
