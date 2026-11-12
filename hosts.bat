@echo off

set "PROJECT_DIR=%~dp0WebApplication1"

start "ComputeService HTTPS" cmd /k "cd /d "%PROJECT_DIR%" && dotnet run --launch-profile https"

start "ComputeService HTTP" cmd /k "cd /d "%PROJECT_DIR%" && dotnet run --launch-profile http"
