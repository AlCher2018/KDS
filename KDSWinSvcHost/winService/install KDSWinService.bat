IF EXIST "%ProgramFiles(x86)%" (
   "%~dp0InstallUtil64.exe" "%~dp0..\KDSWinSvcHost.exe"
) else (
   "%~dp0InstallUtil.exe" "%~dp0..\KDSWinSvcHost.exe"
)

@echo off
echo.
pause