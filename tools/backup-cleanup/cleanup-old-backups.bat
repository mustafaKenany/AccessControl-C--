@echo off
REM ============================================================================
REM AccessControlPro - Backup folder cleanup
REM ============================================================================
REM Deletes .bak files older than N days from the backup folder.
REM
REM AutoBackup already does this every 02:24 / 14:24 (keeps last 3 days).
REM This script is for the rare case where the folder fills up between runs
REM or you want to free up disk on demand.
REM
REM Usage:
REM   cleanup-old-backups.bat                 ^(uses default 3-day retention^)
REM   set BACKUP_DAYS=7 ^&^& cleanup-old-backups.bat   ^(keep 7 days instead^)
REM   set BACKUP_PATH=E:\Backups ^&^& cleanup-old-backups.bat   ^(custom folder^)
REM ============================================================================

setlocal enabledelayedexpansion
title AccessControlPro - Backup Cleanup

REM ----- Defaults ------------------------------------------------------------
if "%BACKUP_DAYS%"=="" set BACKUP_DAYS=3

REM Try common backup paths if BACKUP_PATH is not set
if "%BACKUP_PATH%"=="" (
    if exist "D:\AccessControlBackups" (
        set "BACKUP_PATH=D:\AccessControlBackups"
    ) else if exist "C:\AccessControlBackups" (
        set "BACKUP_PATH=C:\AccessControlBackups"
    ) else if exist "D:\Backups\AccessControlPro" (
        set "BACKUP_PATH=D:\Backups\AccessControlPro"
    ) else (
        echo.
        echo  ERROR: No backup folder found.
        echo  Set BACKUP_PATH manually, e.g.:
        echo    set BACKUP_PATH=D:\MyBackups ^&^& cleanup-old-backups.bat
        echo.
        pause
        exit /b 1
    )
)

echo.
echo  ============================================================
echo   AccessControlPro - Backup Cleanup
echo  ============================================================
echo   Folder:    %BACKUP_PATH%
echo   Keep:      Last %BACKUP_DAYS% days
echo  ============================================================
echo.

REM ----- Count files BEFORE --------------------------------------------------
set /a BEFORE_COUNT=0
for %%F in ("%BACKUP_PATH%\AccessControlPro_*.bak") do set /a BEFORE_COUNT+=1
echo  Files before: %BEFORE_COUNT% .bak files
echo.

REM ----- Confirm -------------------------------------------------------------
choice /C YN /M "Proceed with cleanup"
if errorlevel 2 (
    echo Cancelled.
    pause
    exit /b 0
)

REM ----- Delete --------------------------------------------------------------
echo.
echo Deleting files older than %BACKUP_DAYS% days...
forfiles /p "%BACKUP_PATH%" /m "AccessControlPro_*.bak" /d -%BACKUP_DAYS% /c "cmd /c echo Deleting @file && del /q @path" 2>nul
if %errorLevel% neq 0 (
    echo  No files older than %BACKUP_DAYS% days found ^(nothing to delete^).
)

REM ----- Count files AFTER ---------------------------------------------------
set /a AFTER_COUNT=0
for %%F in ("%BACKUP_PATH%\AccessControlPro_*.bak") do set /a AFTER_COUNT+=1
set /a DELETED=BEFORE_COUNT-AFTER_COUNT

echo.
echo  ============================================================
echo   Done. Deleted %DELETED% file^(s^). %AFTER_COUNT% remaining.
echo  ============================================================
echo.

REM ----- Show what's left ----------------------------------------------------
echo  Remaining backups:
dir "%BACKUP_PATH%\AccessControlPro_*.bak" /b /od 2>nul
echo.

pause
endlocal
