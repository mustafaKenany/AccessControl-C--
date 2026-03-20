@echo off
echo ========================================
echo AccessControlPro - Portable Deploy
echo ========================================
echo.

:: Use script's own location to find project root
set "ROOT=%~dp0.."
set "DEPLOY=D:\AccessControlPro_Deploy"

:: Step 1: Publish
echo [1/3] Publishing all apps (Release)...

dotnet publish "%ROOT%\src\AccessControlPro.WPF\AccessControlPro.WPF.csproj" -c Release -r win-x86 --self-contained false -o "%DEPLOY%\WPF"
if errorlevel 1 goto :error
echo   WPF OK

dotnet publish "%ROOT%\src\AccessControlPro.Admin\AccessControlPro.Admin.csproj" -c Release -r win-x86 --self-contained false -o "%DEPLOY%\Admin"
if errorlevel 1 goto :error
echo   Admin OK

dotnet publish "%ROOT%\src\AccessControlPro.POS\AccessControlPro.POS.csproj" -c Release -r win-x86 --self-contained false -o "%DEPLOY%\POS"
if errorlevel 1 goto :error
echo   POS OK

:: Step 2: Copy native SDK DLLs
echo [2/3] Copying native SDK DLLs...
copy /Y "%ROOT%\src\AccessControlPro.SDK\NativeDlls\*.dll" "%DEPLOY%\WPF\" >nul
copy /Y "%ROOT%\src\AccessControlPro.SDK\NativeDlls\*.dll" "%DEPLOY%\Admin\" >nul
copy /Y "%ROOT%\src\AccessControlPro.SDK\NativeDlls\*.dll" "%DEPLOY%\POS\" >nul
echo   Done

:: Step 3: Copy config files (if not already there from publish)
echo [3/3] Ensuring config files...
if not exist "%DEPLOY%\WPF\appsettings.json" copy /Y "%ROOT%\src\AccessControlPro.WPF\appsettings.json" "%DEPLOY%\WPF\" >nul
if not exist "%DEPLOY%\WPF\SubscriptionPlans.json" copy /Y "%ROOT%\src\AccessControlPro.WPF\SubscriptionPlans.json" "%DEPLOY%\WPF\" >nul
if not exist "%DEPLOY%\Admin\appsettings.json" copy /Y "%ROOT%\src\AccessControlPro.Admin\appsettings.json" "%DEPLOY%\Admin\" >nul
if not exist "%DEPLOY%\POS\appsettings.json" copy /Y "%ROOT%\src\AccessControlPro.POS\appsettings.json" "%DEPLOY%\POS\" >nul
echo   Done

echo.
echo ========================================
echo DEPLOY COMPLETE!
echo ========================================
echo.
echo Folder: %DEPLOY%
echo.
echo To install on user PC:
echo   1. Install .NET 8 Desktop Runtime (x86) on their PC
echo   2. Copy the WPF folder to their PC
echo   3. Edit appsettings.json with their SQL Server connection string
echo   4. Run AccessControlPro.WPF.exe
echo.
echo Apps:
echo   WPF:   %DEPLOY%\WPF\AccessControlPro.WPF.exe
echo   Admin: %DEPLOY%\Admin\AccessControlPro.Admin.exe
echo   POS:   %DEPLOY%\POS\AccessControlPro.POS.exe
echo ========================================
pause
exit /b 0

:error
echo.
echo BUILD FAILED! Check errors above.
pause
exit /b 1
