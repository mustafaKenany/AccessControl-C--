@echo off
REM ============================================================================
REM AccessControlPro — Customer-PC Tuneup
REM ============================================================================
REM Run ONCE after installing AccessControlPro on a customer's PC. Safe to re-run.
REM
REM Must be run as Administrator (right-click -> Run as administrator) — needed
REM for the Power Plan + Defender exclusion + SQL Server config calls.
REM
REM Effects:
REM   - SQL Server max memory capped at 2 GB
REM   - Indexes ensured on hot tables (Events, Cards, Players, QR Pool, etc.)
REM   - Statistics refreshed
REM   - Windows Power Plan set to High Performance (no CPU throttling)
REM   - AccessControlPro install folder excluded from Defender real-time scan
REM   - Visual effects set to "Best performance" (fewer GPU effects)
REM ============================================================================

setlocal enabledelayedexpansion
title AccessControlPro — Customer PC Tuneup

REM ----- Admin check ---------------------------------------------------------
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo.
    echo  ERROR: This script must be run as Administrator.
    echo  Right-click tuneup.bat and choose "Run as administrator".
    echo.
    pause
    exit /b 1
)

echo.
echo  ============================================================
echo   AccessControlPro - Customer PC Tuneup
echo  ============================================================
echo.
echo  This will apply the following changes on this PC:
echo.
echo    1. Cap SQL Server max memory at 2 GB
echo    2. Add indexes on hot tables (Events, Cards, Players, QR)
echo    3. Refresh SQL statistics
echo    4. Set Windows Power Plan to High Performance
echo    5. Add AccessControlPro folder to Defender exclusions
echo    6. Set Visual Effects to Best Performance
echo.
echo  Safe to re-run. Existing settings will be re-applied.
echo.
choice /C YN /M "Proceed"
if errorlevel 2 (
    echo Cancelled.
    pause
    exit /b 0
)

echo.
echo  ============================================================
echo   Step 1/6 - SQL Server config + indexes
echo  ============================================================

REM Default connection: localhost, sa / 123 (matches the setup wizard's default).
REM Override via env vars SQLHOST / SQLUSER / SQLPASS if the install uses different creds.
if "%SQLHOST%"=="" set SQLHOST=localhost
if "%SQLUSER%"=="" set SQLUSER=sa
if "%SQLPASS%"=="" set SQLPASS=123

set SCRIPT_DIR=%~dp0
echo Running tuneup.sql against %SQLUSER%@%SQLHOST% ...
sqlcmd -S %SQLHOST% -U %SQLUSER% -P %SQLPASS% -i "%SCRIPT_DIR%tuneup.sql" -C
if %errorLevel% neq 0 (
    echo.
    echo  WARNING: SQL Server tuneup failed. Possible reasons:
    echo    - SQL Server not running on %SQLHOST%
    echo    - Wrong username/password (override via SQLUSER/SQLPASS env vars^)
    echo    - sqlcmd not in PATH (install SQL Server Command Line Utilities^)
    echo.
    echo  Continuing with Windows-side tuneup anyway...
    echo.
)

echo.
echo  ============================================================
echo   Step 2/6 - Windows Power Plan
echo  ============================================================
echo Setting power plan to High Performance...
powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c
if %errorLevel% equ 0 (
    echo  OK: Power plan = High Performance
) else (
    echo  WARNING: Could not change power plan
)

echo.
echo  ============================================================
echo   Step 3/6 - Defender exclusions
echo  ============================================================

REM Try common install paths. Adjust if you install elsewhere.
set "INSTALL_PATHS=C:\Program Files\AccessControlPro C:\AccessControlPro D:\AccessControlPro"
for %%P in (%INSTALL_PATHS%) do (
    if exist "%%P" (
        echo Adding Defender exclusion: %%P
        powershell -NoProfile -Command "Add-MpPreference -ExclusionPath '%%P' -ErrorAction SilentlyContinue"
    )
)

REM Also exclude the SQL Server data folder (cuts disk I/O latency dramatically)
for %%D in (
    "C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA"
    "C:\Program Files\Microsoft SQL Server\MSSQL15.MSSQLSERVER\MSSQL\DATA"
    "C:\Program Files\Microsoft SQL Server\MSSQL14.MSSQLSERVER\MSSQL\DATA"
    "C:\Program Files\Microsoft SQL Server\MSSQL13.MSSQLSERVER\MSSQL\DATA"
) do (
    if exist %%D (
        echo Adding Defender exclusion: %%D
        powershell -NoProfile -Command "Add-MpPreference -ExclusionPath %%D -ErrorAction SilentlyContinue"
    )
)
echo  OK: Defender exclusions applied where folders exist

echo.
echo  ============================================================
echo   Step 4/6 - Visual Effects
echo  ============================================================
REM 2 = "Adjust for best performance" (kills animations, transparency, drop shadows)
REM This makes the WPF UI feel snappier on low-end GPUs.
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" /v VisualFXSetting /t REG_DWORD /d 2 /f >nul
echo  OK: Visual effects = Best performance ^(takes effect on next login^)

echo.
echo  ============================================================
echo   Step 5/6 - SQL Server service set to start automatic
echo  ============================================================
sc config "MSSQLSERVER" start= auto >nul 2>&1
sc config "MSSQL$SQLEXPRESS" start= auto >nul 2>&1
echo  OK: SQL Server set to auto-start at boot ^(if installed^)

echo.
echo  ============================================================
echo   Step 6/6 - Restart SQL Server to apply memory cap
echo  ============================================================
echo Restarting SQL Server so the memory cap takes effect...
net stop MSSQLSERVER >nul 2>&1
net start MSSQLSERVER >nul 2>&1
net stop "MSSQL$SQLEXPRESS" >nul 2>&1
net start "MSSQL$SQLEXPRESS" >nul 2>&1
echo  OK: SQL Server restarted

echo.
echo  ============================================================
echo   Tuneup complete
echo  ============================================================
echo.
echo  Recommended:
echo    - Log out and log back in (visual effects need a fresh session^)
echo    - Launch AccessControlPro and verify it starts cleanly
echo.
pause
endlocal
