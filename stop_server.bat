@echo off
powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name='mish-mash-note.exe'\" | ForEach-Object { Stop-Process -Id $_.ProcessId -Force; Write-Output ('Stopped mish-mash-note (pid ' + $_.ProcessId + ').') }"
