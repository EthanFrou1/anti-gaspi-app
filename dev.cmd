@echo off
rem Raccourci : lance scripts\dev.ps1 sans avoir à modifier la politique d'exécution PowerShell.
rem Exemples : dev.cmd   |   dev.cmd -Target emulator   |   dev.cmd -Claude   |   dev.cmd -Test   |   dev.cmd -Stop
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\dev.ps1" %*
