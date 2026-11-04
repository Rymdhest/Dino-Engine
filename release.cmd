@echo off
rem Sends what's committed to everyone with a launcher. Anything given here goes to Dino Release: see LAUNCHER.md.
cd /d "%~dp0"
dotnet run --project "Dino Release" -c Release -- publish %*
if "%~1"=="" pause
