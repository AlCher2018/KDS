@echo off
rem set key HKEY_CURRENT_USER\Software\Integra\ClientOrderQueue\Update\Enable to "1"

reg add HKCU\Software\Integra\ClientOrderQueue\Update\ /v Enable /t REG_SZ /d "1" /f

pause