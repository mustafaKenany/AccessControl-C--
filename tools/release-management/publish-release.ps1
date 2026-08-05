# ============================================================================
# AccessControlPro - Automated release publisher
# ============================================================================
# What it does:
#   1. Asks you for: version number, release notes (EN + AR), mandatory flag
#   2. Builds the WPF + Updater
#   3. ZIPs the publish output
#   4. Calculates SHA-256 hash
#   5. Uploads ZIP to VPS via scp
#   6. Writes latest.json on the VPS via ssh
#   7. Verifies the manifest is live
#
# Prerequisites:
#   - OpenSSH client (built into Windows 10/11)
#   - SSH key configured for root@hmtech.solutions (or you type password 2x)
#
# Usage:
#   cd D:\AccessControlPro\tools\release-management
#   .\publish-release.ps1
# ============================================================================

$ErrorActionPreference = "Stop"

# ----- Config -----
$VpsHost        = "root@89.116.39.155"
# IMPORTANT (post "face"-project nginx reorg, 2026-08): the ZIP and the manifest
# now live in DIFFERENT directories.
#   * ZIP -> /var/www/releases/  — nginx serves it via
#            `location /releases/ { alias /var/www/releases/; }` on hmtech.solutions.
#            This dir is SHARED with the face project's zips.
#   * manifest (latest.json) -> /var/www/gymapp/wwwroot/releases/  — read by the
#            Blazor /api/version/latest endpoint, which is what the desktop
#            UpdateCheckService actually polls. Do NOT drop a gymapp latest.json
#            into the shared /var/www/releases/ (it would collide with face's).
# The static URL https://hmtech.solutions/releases/latest.json 404s BY DESIGN —
# we verify via the API endpoint + a direct GET of the download zip instead.
$VpsZipDir      = "/var/www/releases"
$VpsManifestDir = "/var/www/gymapp/wwwroot/releases"
$VerifyUrl      = "https://hmtech.solutions/api/version/latest"
$ZipUrlBase     = "https://hmtech.solutions/releases"
$RepoRoot       = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

# All 3 customer apps publish into the SAME folder so they share DLLs.
# WPF builds first (also pulls in Updater.exe via its ProjectReference).
# Admin + POS then add their own .exe files; shared DLLs are identical so
# overwrites are a no-op.
$WpfCsproj      = Join-Path $RepoRoot "src\AccessControlPro.WPF\AccessControlPro.WPF.csproj"
$AdminCsproj    = Join-Path $RepoRoot "src\AccessControlPro.Admin\AccessControlPro.Admin.csproj"
$PosCsproj      = Join-Path $RepoRoot "src\AccessControlPro.POS\AccessControlPro.POS.csproj"

# Bullet character used in release notes (built at runtime so the script source
# stays pure ASCII and PowerShell 5.1 can parse it without BOM)
$Bullet = [char]0x2022

# ----- Banner -----
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  AccessControlPro - Release Publisher" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# ----- Verify prerequisites -----
$scp = Get-Command scp -ErrorAction SilentlyContinue
$ssh = Get-Command ssh -ErrorAction SilentlyContinue
if (-not $scp -or -not $ssh) {
    Write-Host "ERROR: scp or ssh not found." -ForegroundColor Red
    Write-Host "Install OpenSSH Client: Settings -> Apps -> Optional Features -> 'OpenSSH Client'"
    exit 1
}
if (-not (Test-Path $WpfCsproj)) {
    Write-Host "ERROR: WPF csproj not found at $WpfCsproj" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $AdminCsproj)) {
    Write-Host "ERROR: Admin csproj not found at $AdminCsproj" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $PosCsproj)) {
    Write-Host "ERROR: POS csproj not found at $PosCsproj" -ForegroundColor Red
    exit 1
}

# ----- Ask for version -----
Write-Host "Step 1/7 - Version info" -ForegroundColor Yellow
Write-Host "----------------------------------------"
$version = Read-Host "New version number (e.g. 4.5.0)"
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "ERROR: Version must be in the form MAJOR.MINOR.PATCH (e.g. 4.5.0)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Step 2/7 - Release notes" -ForegroundColor Yellow
Write-Host "----------------------------------------"
Write-Host "Enter release notes in English. Use semicolons to separate bullets."
Write-Host "Example: Fixed Events page;Faster card search;New backup splash"
$notesEnRaw = Read-Host "English notes"
$notesEn = ($notesEnRaw -split ';' | ForEach-Object { "$Bullet $($_.Trim())" }) -join '\n'

Write-Host ""
Write-Host "Enter release notes in Arabic. Use semicolons to separate bullets."
$notesArRaw = Read-Host "Arabic notes"
$notesAr = ($notesArRaw -split ';' | ForEach-Object { "$Bullet $($_.Trim())" }) -join '\n'

Write-Host ""
$mandatoryAns = Read-Host "Is this update MANDATORY? Customers cannot skip it. (y/N)"
$mandatory = ($mandatoryAns -eq 'y' -or $mandatoryAns -eq 'Y')

# ----- Summary + confirmation -----
Write-Host ""
Write-Host "----------------------------------------"
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "----------------------------------------"
Write-Host "  Version:       $version"
Write-Host "  Mandatory:     $mandatory"
Write-Host "  English notes: $notesEn"
Write-Host "  Arabic notes:  $notesAr"
Write-Host "  VPS:           $VpsHost"
Write-Host "  ZIP folder:    $VpsZipDir  (shared /releases/ alias)"
Write-Host "  Manifest:      $VpsManifestDir/latest.json  (read by /api/version/latest)"
Write-Host ""
$confirm = Read-Host "Proceed? (Y/n)"
if ($confirm -eq 'n' -or $confirm -eq 'N') {
    Write-Host "Cancelled."
    exit 0
}

# ----- Step 3: Build -----
Write-Host ""
Write-Host "Step 3/7 - Building the WPF + Updater" -ForegroundColor Yellow
Write-Host "----------------------------------------"
$publishDir = Join-Path $RepoRoot "publish\v$version"
if (Test-Path $publishDir) {
    Write-Host "Cleaning previous publish folder..."
    Remove-Item $publishDir -Recurse -Force
}

# Bake the version into the build via MSBuild parameter so each .exe's
# Assembly.GetName().Version returns the same number the manifest advertises.
# Without this, the app reports 1.0.0.0 and the update prompt loops forever.
$versionArgs = @("-p:Version=$version", "-p:AssemblyVersion=$version.0", "-p:FileVersion=$version.0")

# Build 1/3: Main WPF app (also produces Updater.exe via ProjectReference)
Write-Host "  Building Main WPF app + Updater..."
dotnet publish $WpfCsproj -c Release -o $publishDir --nologo @versionArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: dotnet publish (WPF) failed." -ForegroundColor Red
    exit 1
}

# Build 2/3: Admin back-office app (publishes into same folder, shared DLLs overwrite identically)
Write-Host "  Building Admin app..."
dotnet publish $AdminCsproj -c Release -o $publishDir --nologo @versionArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: dotnet publish (Admin) failed." -ForegroundColor Red
    exit 1
}

# Build 3/3: POS cashier app
Write-Host "  Building POS app..."
dotnet publish $PosCsproj -c Release -o $publishDir --nologo @versionArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: dotnet publish (POS) failed." -ForegroundColor Red
    exit 1
}

# Verify all 3 .exe files landed
$expectedExes = @("AccessControlPro.WPF.exe", "AccessControlPro.Admin.exe", "AccessControlPro.POS.exe", "Updater.exe")
$missing = @()
foreach ($exe in $expectedExes) {
    if (-not (Test-Path (Join-Path $publishDir $exe))) { $missing += $exe }
}
if ($missing.Count -gt 0) {
    Write-Host "ERROR: Expected executables not found in publish folder: $($missing -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "  OK - all 4 .exe files published (WPF + Admin + POS + Updater), Version=$version" -ForegroundColor Green

# ----- Step 4: ZIP -----
Write-Host ""
Write-Host "Step 4/7 - Creating ZIP" -ForegroundColor Yellow
Write-Host "----------------------------------------"
$zipName = "AccessControlPro-v$version.zip"
$zipPath = Join-Path $RepoRoot "publish\$zipName"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "  Compressing... (this takes ~30 sec for 250 MB)"
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
$zipInfo = Get-Item $zipPath
$zipSize = $zipInfo.Length
Write-Host "  OK - $zipName ($([math]::Round($zipSize / 1MB, 1)) MB)" -ForegroundColor Green

# ----- Step 5: SHA-256 -----
Write-Host ""
Write-Host "Step 5/7 - Calculating SHA-256" -ForegroundColor Yellow
Write-Host "----------------------------------------"
$sha = (Get-FileHash $zipPath -Algorithm SHA256).Hash
Write-Host "  OK - $sha" -ForegroundColor Green

# ----- Step 6: Upload to VPS -----
Write-Host ""
Write-Host "Step 6/7 - Uploading to VPS" -ForegroundColor Yellow
Write-Host "----------------------------------------"

# Ensure both target folders exist on the VPS before scp (idempotent — safe to re-run)
Write-Host "  Ensuring release folders exist on VPS..."
ssh $VpsHost "mkdir -p $VpsZipDir $VpsManifestDir"
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: could not create $VpsZipDir / $VpsManifestDir on VPS." -ForegroundColor Red
    exit 1
}

Write-Host "  Uploading ZIP to $VpsZipDir (may prompt for VPS password if no SSH key)..."
scp $zipPath "${VpsHost}:${VpsZipDir}/"
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: scp upload failed." -ForegroundColor Red
    exit 1
}

# Verify the server-side SHA-256 matches what we built locally BEFORE we publish
# the manifest — guards against a truncated/corrupted upload pointing customers
# at a zip whose hash won't match (their updater would reject it fleet-wide).
Write-Host "  Verifying server-side SHA-256 matches local..."
$serverSha = (ssh $VpsHost "sha256sum $VpsZipDir/$zipName").Split(' ')[0]
if ($serverSha -and ($serverSha.ToLower() -eq $sha.ToLower())) {
    Write-Host "  OK - ZIP uploaded, server SHA matches local ($($sha.Substring(0,12))...)" -ForegroundColor Green
} else {
    Write-Host "ERROR: server SHA ($serverSha) != local ($sha)." -ForegroundColor Red
    Write-Host "       Upload is corrupt/incomplete — aborting BEFORE the manifest goes live." -ForegroundColor Red
    exit 1
}

# Build the latest.json content
$releasedAt = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
$mandatoryJson = if ($mandatory) { "true" } else { "false" }
$manifest = @"
{
  "version": "$version",
  "downloadUrl": "https://hmtech.solutions/releases/$zipName",
  "sha256": "$sha",
  "sizeBytes": $zipSize,
  "releaseNotes_en": "$notesEn",
  "releaseNotes_ar": "$notesAr",
  "mandatory": $mandatoryJson,
  "minVersion": "4.0.0",
  "releasedAt": "$releasedAt"
}
"@

# Write manifest to a temp file locally (UTF-8 with no BOM so Linux reads it cleanly)
$tempManifest = Join-Path $env:TEMP "latest.json"
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($tempManifest, $manifest, $utf8NoBom)

Write-Host "  Uploading latest.json (may prompt for VPS password if no SSH key)..."
scp $tempManifest "${VpsHost}:${VpsManifestDir}/latest.json"
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: scp upload of latest.json failed." -ForegroundColor Red
    exit 1
}
Remove-Item $tempManifest -Force
Write-Host "  OK - latest.json uploaded" -ForegroundColor Green

# ----- Step 7: Verify -----
Write-Host ""
Write-Host "Step 7/7 - Verifying it is live" -ForegroundColor Yellow
Write-Host "----------------------------------------"
Start-Sleep -Seconds 2

# (1) The API endpoint is the SOURCE OF TRUTH — it's exactly what the desktop
# UpdateCheckService polls (/api/version/latest reads wwwroot/releases/latest.json).
try {
    $resp = Invoke-RestMethod -Uri $VerifyUrl -Method Get -TimeoutSec 15
    if ($resp.version -eq $version) {
        Write-Host "  OK - $VerifyUrl is serving v$version (mandatory=$($resp.mandatory))" -ForegroundColor Green
    } else {
        Write-Host "  WARN - API returned version $($resp.version), expected $version" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  WARN - API endpoint not reachable: $($_.Exception.Message)" -ForegroundColor Yellow
}

# (2) Confirm the download the manifest points at is actually reachable — a 200
# with the full byte count. This is the exact GET each customer's updater performs,
# and it exercises the shared /releases/ nginx alias -> $VpsZipDir.
$zipUrl = "$ZipUrlBase/$zipName"
try {
    $head = Invoke-WebRequest -Uri $zipUrl -Method Head -TimeoutSec 20
    $len  = [int64]$head.Headers['Content-Length']
    if ($head.StatusCode -eq 200 -and $len -eq $zipSize) {
        Write-Host "  OK - $zipUrl is live (HTTP 200, $len bytes)" -ForegroundColor Green
    } else {
        Write-Host "  WARN - $zipUrl returned HTTP $($head.StatusCode), Content-Length $len (expected 200 / $zipSize)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ERROR - download URL not reachable: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "          Customers would see the update but fail to download it." -ForegroundColor Red
    Write-Host "          Check the /releases/ nginx alias points at $VpsZipDir." -ForegroundColor Red
}

# ----- Done -----
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Release v$version published successfully" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Verify manually any time:"
Write-Host "  API:      $VerifyUrl   (source of truth — what the desktop polls)"
Write-Host "  Download: $ZipUrlBase/$zipName"
Write-Host ""
