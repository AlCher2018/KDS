IF EXIST "%ProgramFiles(x86)%" (
   "%~dp0InstallUtil64.exe" "%~dp0..\KDSWinSvcHost.exe" /u
) else (
   "%~dp0InstallUtil.exe" "%~dp0..\KDSWinSvcHost.exe" /u
)


@echo off
echo.
pause