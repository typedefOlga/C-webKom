@echo off

start "ComputeService HTTPS" cmd /k "cd /d C:\Users\user\olga\cs\KomPloblem2\WebApplication1 && dotnet run --launch-profile https"

start "ComputeService HTTP" cmd /k "cd /d C:\Users\user\olga\cs\KomPloblem2\WebApplication1 && dotnet run --launch-profile http"