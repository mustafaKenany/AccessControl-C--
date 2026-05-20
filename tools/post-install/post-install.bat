@echo off
REM ============================================================================
REM AccessControlPro - Combined post-install script
REM ============================================================================
REM Run ONCE on a customer's PC after installing the WPF app.  This wraps:
REM
REM   1. customer-pc-tuneup    - SQL memory cap + 13 indexes + Windows tuneup
REM   2. data-integrity        - Cleans dangling cards, duplicates, orphan rows
REM
REM Both scripts are idempotent so it's safe to re-run.  Pre-cleanup safety
REM backup is taken automatically by the data-integrity step.
REM
REM Must be run as Administrator (right-click -^> Run as administrator) so the
REM tuneup can set the Windows Power Plan + Defender exclusions.
REM ============================================================================

setlocal enabledelayedexpansion
title AccessControlPro - Post-Install Setup

REM ---- Admin check ----------------------------------------------------------
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo.
    echo  ERROR: This script must be run as Administrator.
    echo  Right-click post-install.bat and choose "Run as administrator".
    echo.
    pause
    exit /b 1
)

echo.
echo  ============================================================
echo   AccessControlPro - Post-Install Setup
echo  ============================================================
echo.
echo  This will configure the customer's PC for AccessControlPro:
echo.
echo    PART 1 - PC tuneup
echo      - Cap SQL Server max memory at 2 GB
echo      - Add indexes to hot tables ^(Events, Cards, Players, QR^)
echo      - Refresh SQL statistics
echo      - Set Windows Power Plan to High Performance
echo      - Defender exclusions for app + SQL data folder
echo      - Visual effects = Best Performance
echo      - Restart SQL Server so memory cap takes effect
echo.
echo    PART 2 - Data integrity cleanup
echo      - Safety backup of the DB
echo      - Remove dangling AccessCard rows
echo      - Remove duplicate CardNumber rows
echo      - Remove empty-number card rows
echo      - Remove orphan FreezeHistory rows
echo      - Detach orphan Transaction.RelatedEmployeeId refs
echo.
echo  Safe to re-run.  Total time: about 2 minutes.
echo.
choice /C YN /M "Proceed"
if errorlevel 2 (
    echo Cancelled.
    pause
    exit /b 0
)

set SCRIPT_DIR=%~dp0

echo.
echo  ============================================================
echo   PART 1 / 2 - PC tuneup
echo  ============================================================
call "%SCRIPT_DIR%..\customer-pc-tuneup\tuneup.bat"
if %errorLevel% neq 0 (
    echo.
    echo  WARNING: tuneup.bat reported a non-zero exit.  Continuing with data-integrity step.
    echo.
)

echo.
echo  ============================================================
echo   PART 2 / 2 - Data integrity cleanup
echo  ============================================================
call "%SCRIPT_DIR%..\data-integrity\cleanup.bat"
if %errorLevel% neq 0 (
    echo.
    echo  WARNING: cleanup.bat reported a non-zero exit.  Check the output above.
    echo.
)

echo.
echo  ============================================================
echo   Post-install setup complete
echo  ============================================================
echo.
echo  Recommended:
echo    - Log out and log back in ^(visual-effects change needs a fresh session^)
echo    - Launch AccessControlPro and verify it starts cleanly
echo    - Open admin panel -^> Subscription Plans, adjust prices for this gym
echo.
pause
endlocal
