@echo off
REM ============================================================================
REM AccessControlPro - Data Integrity Cleanup
REM ============================================================================
REM Run on a customer's PC after install to clean up legacy data issues:
REM
REM   - Dangling AccessCard rows (cards pointing to deleted players)
REM   - Duplicate AccessCard rows (same CardNumber on multiple rows)
REM   - Empty CardNumber rows (junk from crashed mid-add operations)
REM   - Orphaned FreezeHistory rows
REM   - Orphaned Transaction.RelatedEmployeeId refs (detached, not deleted)
REM
REM Safe to re-run.  Idempotent.
REM Does NOT delete "Migrated" players - those clean naturally on renewal.
REM
REM Backup is taken automatically before any changes.
REM ============================================================================

setlocal enabledelayedexpansion
title AccessControlPro - Data Integrity Cleanup

REM ---- Admin check (helpful but not required - SQL Server logins handle access) ----
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo.
    echo  NOTE: Running without admin elevation. This is OK for this script,
    echo  but if it fails due to backup-path permissions, right-click ^"Run as administrator^".
    echo.
)

echo.
echo  ============================================================
echo   AccessControlPro - Data Integrity Cleanup
echo  ============================================================
echo.
echo  This will clean up legacy garbage rows in the local SQL Server:
echo.
echo    1. Dangling AccessCard rows
echo    2. Duplicate-CardNumber rows
echo    3. Empty-CardNumber rows
echo    4. Orphan FreezeHistory rows
echo    5. Orphan Transaction RelatedEmployeeId refs
echo.
echo  Migrated players are NOT touched.
echo  A SQL backup will be taken first (separate from the daily auto-backup).
echo.
choice /C YN /M "Proceed"
if errorlevel 2 (
    echo Cancelled.
    pause
    exit /b 0
)

if "%SQLHOST%"=="" set SQLHOST=localhost
if "%SQLUSER%"=="" set SQLUSER=sa
if "%SQLPASS%"=="" set SQLPASS=123
if "%SQLDB%"==""   set SQLDB=AccessControlPro

set SCRIPT_DIR=%~dp0
set TIMESTAMP=%date:~-4%%date:~-10,2%%date:~-7,2%-%time:~0,2%%time:~3,2%
set TIMESTAMP=%TIMESTAMP: =0%
set BACKUP_FILE=D:\Backups\AccessControlPro\PreCleanup_%TIMESTAMP%.bak

echo.
echo  ============================================================
echo   Step 1 / 2 - Pre-cleanup backup
echo  ============================================================
echo Creating safety backup: %BACKUP_FILE%
sqlcmd -S %SQLHOST% -U %SQLUSER% -P %SQLPASS% -C -Q "BACKUP DATABASE [%SQLDB%] TO DISK = N'%BACKUP_FILE%' WITH INIT, COMPRESSION, STATS = 10;"
if %errorLevel% neq 0 (
    echo.
    echo  ERROR: Pre-cleanup backup failed. Aborting cleanup.
    echo  Check that D:\Backups\AccessControlPro\ exists and is writable.
    echo.
    pause
    exit /b 1
)
echo  OK: backup written

echo.
echo  ============================================================
echo   Step 2 / 2 - Running cleanup.sql
echo  ============================================================
sqlcmd -S %SQLHOST% -U %SQLUSER% -P %SQLPASS% -C -d %SQLDB% -i "%SCRIPT_DIR%cleanup.sql"
if %errorLevel% neq 0 (
    echo.
    echo  ERROR: Cleanup script failed.  Backup is at: %BACKUP_FILE%
    echo  Restore via: sqlcmd -E -Q "RESTORE DATABASE [%SQLDB%] FROM DISK=N'%BACKUP_FILE%' WITH REPLACE"
    pause
    exit /b 1
)

echo.
echo  ============================================================
echo   Cleanup complete
echo  ============================================================
echo.
echo  Safety backup is at: %BACKUP_FILE%
echo  (Keep it for ~7 days, then it can be deleted.)
echo.
pause
endlocal
