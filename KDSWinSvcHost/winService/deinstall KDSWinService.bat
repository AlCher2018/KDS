IF EXIST "%ProgramFiles(x86)%" (
   InstallUtil64.exe ..\KDSWinSvcHost.exe /u
) else (
   InstallUtil.exe ..\KDSWinSvcHost.exe /u
)


@echo off
echo.
pause