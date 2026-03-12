@echo off
echo ========================================
echo AccessControlPro - Build All Installers
echo ========================================
echo.

:: Step 1: Publish all projects
echo [1/4] Publishing AccessControlPro.WPF...
dotnet publish ..\src\AccessControlPro.WPF\AccessControlPro.WPF.csproj -c Release -r win-x86 --self-contained false
if errorlevel 1 goto :error

echo [2/4] Publishing AccessControlPro.Admin...
dotnet publish ..\src\AccessControlPro.Admin\AccessControlPro.Admin.csproj -c Release -r win-x86 --self-contained false
if errorlevel 1 goto :error

echo [3/4] Publishing AccessControlPro.POS...
dotnet publish ..\src\AccessControlPro.POS\AccessControlPro.POS.csproj -c Release -r win-x86 --self-contained false
if errorlevel 1 goto :error

echo.
echo ========================================
echo All 3 apps published successfully!
echo ========================================
echo.
echo Published folders:
echo   WPF:   ..\src\AccessControlPro.WPF\bin\Release\net8.0-windows\win-x86\publish\
echo   Admin: ..\src\AccessControlPro.Admin\bin\Release\net8.0-windows\win-x86\publish\
echo   POS:   ..\src\AccessControlPro.POS\bin\Release\net8.0-windows\win-x86\publish\
echo.

:: Step 2: Build Inno Setup installers (optional)
echo [4/4] Looking for Inno Setup...
if not exist output mkdir output

set ISCC=
where iscc >nul 2>&1
if not errorlevel 1 (
    set ISCC=iscc
) else if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"
) else (
    echo NOTE: Inno Setup not found - skipping .exe installer creation.
    echo To create installers, install Inno Setup 6 from: https://jrsoftware.org/isdl.php
    echo You can still run apps directly from the publish folders above.
    goto :done
)

echo Building WPF installer...
"%ISCC%" AccessControlPro_WPF_Setup.iss
echo Building Admin installer...
"%ISCC%" AccessControlPro_Admin_Setup.iss
echo Building POS installer...
"%ISCC%" AccessControlPro_POS_Setup.iss

echo.
echo Installers created in: setup\output\

:done
echo.
echo ========================================
echo DONE!
echo ========================================
pause
exit /b 0

:error
echo.
echo ========================================
echo BUILD FAILED! Check the errors above.
echo ========================================
pause
exit /b 1
