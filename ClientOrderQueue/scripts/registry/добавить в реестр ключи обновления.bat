@echo off

reg add HKCU\Software\Integra\ClientOrderQueue\Update\ /v Enable /t REG_SZ /d "1" /f
reg add HKCU\Software\Integra\ClientOrderQueue\Update\ /v Source /t REG_SZ /d "ftp://82.207.112.88/IT Department/!Soft_dev/KDS/ClientQueue/" /f
reg add HKCU\Software\Integra\ClientOrderQueue\Update\ /v FTPLogin /t REG_SZ /d "integra-its\ftp" /f
reg add HKCU\Software\Integra\ClientOrderQueue\Update\ /v FTPPassword /t REG_SZ /d "Qwerty1234" /f

pause