@echo off
pwsh -ExecutionPolicy Bypass -NoProfile -Command "& '%~dp0github-release.ps1' %*"

