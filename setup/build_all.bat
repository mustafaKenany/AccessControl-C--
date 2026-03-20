@echo off
echo ========================================
echo AccessControlPro - Production Build
echo MainSetup (WPF+Admin) + POS (Separate)
echo ========================================
echo.

set "ROOT=%~dp0.."
set "DEPLOY=%ROOT%\Release"
set "SDK_DLLS=%ROOT%\src\AccessControlPro.SDK\NativeDlls"

:: Clean previous release
if exist "%DEPLOY%" rmdir /s /q "%DEPLOY%"
mkdir "%DEPLOY%\MainSetup"
mkdir "%DEPLOY%\POS"

:: Step 1: Publish WPF (main gym app) into MainSetup
echo [1/3] Publishing WPF (this takes a few minutes)...
dotnet publish "%ROOT%\src\AccessControlPro.WPF\AccessControlPro.WPF.csproj" -c Release -o "%DEPLOY%\MainSetup"
if errorlevel 1 goto :error
echo   WPF OK

:: Step 2: Publish Admin into same MainSetup folder
echo [2/3] Publishing Admin...
dotnet publish "%ROOT%\src\AccessControlPro.Admin\AccessControlPro.Admin.csproj" -c Release -o "%DEPLOY%\MainSetup"
if errorlevel 1 goto :error
echo   Admin OK

:: Step 3: Publish POS separately
echo [3/3] Publishing POS...
dotnet publish "%ROOT%\src\AccessControlPro.POS\AccessControlPro.POS.csproj" -c Release -o "%DEPLOY%\POS"
if errorlevel 1 goto :error
echo   POS OK

:: Copy config files
echo Copying config files...
copy /Y "%ROOT%\src\AccessControlPro.WPF\appsettings.json" "%DEPLOY%\MainSetup\" >nul 2>nul
copy /Y "%ROOT%\src\AccessControlPro.WPF\SubscriptionPlans.json" "%DEPLOY%\MainSetup\" >nul 2>nul
copy /Y "%ROOT%\src\AccessControlPro.POS\appsettings.json" "%DEPLOY%\POS\" >nul 2>nul

:: Copy native SDK DLLs (REQUIRED - these cannot be embedded in SingleFile)
echo Copying native SDK DLLs...
copy /Y "%SDK_DLLS%\*.dll" "%DEPLOY%\MainSetup\" >nul 2>nul
copy /Y "%SDK_DLLS%\*.dll" "%DEPLOY%\POS\" >nul 2>nul

:: Copy user manuals if they exist
if exist "%ROOT%\docs\UserManual_EN.pdf" copy /Y "%ROOT%\docs\UserManual_EN.pdf" "%DEPLOY%\MainSetup\" >nul 2>nul
if exist "%ROOT%\docs\UserManual_AR.pdf" copy /Y "%ROOT%\docs\UserManual_AR.pdf" "%DEPLOY%\MainSetup\" >nul 2>nul

:: Clean up debug/leftover files and setup markers (CRITICAL for fresh install)
del /q "%DEPLOY%\MainSetup\.setup_complete" 2>nul
del /q "%DEPLOY%\POS\.setup_complete" 2>nul
del /q "%DEPLOY%\MainSetup\sdk_log.txt" 2>nul
del /q "%DEPLOY%\POS\sdk_log.txt" 2>nul
if exist "%DEPLOY%\MainSetup\Logs" rmdir /s /q "%DEPLOY%\MainSetup\Logs" 2>nul
if exist "%DEPLOY%\POS\Logs" rmdir /s /q "%DEPLOY%\POS\Logs" 2>nul
del /q "%DEPLOY%\MainSetup\*.pdb" 2>nul
del /q "%DEPLOY%\POS\*.pdb" 2>nul
del /q "%DEPLOY%\MainSetup\AccessControlPro.WPF.runtimeconfig.json" 2>nul
del /q "%DEPLOY%\POS\AccessControlPro.WPF.runtimeconfig.json" 2>nul
del /q "%DEPLOY%\POS\SubscriptionPlans.json" 2>nul

echo.
echo ========================================
echo BUILD COMPLETE!
echo ========================================
echo.
echo Output: %DEPLOY%
echo.
echo   MainSetup files (WPF + Admin):
for %%f in ("%DEPLOY%\MainSetup\*") do echo     %%~nxf (%%~zf bytes)
echo.
echo   POS files (Separate):
for %%f in ("%DEPLOY%\POS\*") do echo     %%~nxf (%%~zf bytes)
echo.
echo ========================================
echo DEPLOYMENT:
echo   Main Install:
echo     1. Copy MainSetup folder to customer PC
echo     2. Run AccessControlPro.WPF.exe
echo     3. Setup Wizard handles everything
echo   POS Install (optional, separate):
echo     1. Copy POS folder to customer PC
echo     2. Run AccessControlPro.POS.exe
echo     3. Enter DB connection only
echo   No .NET install needed (self-contained)
echo ========================================
pause
exit /b 0

:error
echo.
echo ========================================
echo BUILD FAILED! Check errors above.
echo ========================================
pause
exit /b 1
