IF EXIST "%ProgramFiles(x86)%" (
   InstallUtil64.exe ..\KDSWinSvcHost.exe
) else (
   InstallUtil.exe ..\KDSWinSvcHost.exe
)


@echo off
echo.
pause