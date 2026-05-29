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
# Blazor app is deployed directly into /var/www/gymapp/ — wwwroot is a sibling
# of the AccessControlPro.Web executable (NOT inside it as the name suggests).
$VpsReleasesDir = "/var/www/gymapp/wwwroot/releases"
$VerifyUrl      = "https://hmtech.solutions/api/version/latest"
$StaticUrl      = "https://hmtech.solutions/releases/latest.json"
$RepoRoot       = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$WpfCsproj      = Join-Path $RepoRoot "src\AccessControlPro.WPF\AccessControlPro.WPF.csproj"

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
Write-Host "  Target folder: $VpsReleasesDir"
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

# Bake the version into the build via MSBuild parameter so the WPF's
# Assembly.GetName().Version returns the same number the manifest advertises.
# Without this, the app reports 1.0.0.0 and the update prompt loops forever.
dotnet publish $WpfCsproj -c Release -o $publishDir --nologo `
    -p:Version=$version `
    -p:AssemblyVersion="$version.0" `
    -p:FileVersion="$version.0"
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: dotnet publish failed." -ForegroundColor Red
    exit 1
}
Write-Host "  OK - published to $publishDir (Version=$version)" -ForegroundColor Green

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

# Ensure the releases folder exists on the VPS before scp (idempotent — safe to re-run)
Write-Host "  Ensuring releases folder exists on VPS..."
ssh $VpsHost "mkdir -p $VpsReleasesDir"
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: could not create $VpsReleasesDir on VPS." -ForegroundColor Red
    Write-Host "Check that the path exists. Run: ssh $VpsHost `"ls -la /var/www/gymapp/AccessControlPro.Web/wwwroot/`""
    exit 1
}

Write-Host "  Uploading ZIP (may prompt for VPS password if no SSH key)..."
scp $zipPath "${VpsHost}:${VpsReleasesDir}/"
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: scp upload failed." -ForegroundColor Red
    exit 1
}
Write-Host "  OK - ZIP uploaded" -ForegroundColor Green

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
scp $tempManifest "${VpsHost}:${VpsReleasesDir}/latest.json"
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

# Try the static URL first (works as long as Blazor's UseStaticFiles is enabled —
# no need for the /api/version/latest endpoint to be deployed yet).
$staticOk = $false
try {
    $resp = Invoke-RestMethod -Uri $StaticUrl -Method Get -TimeoutSec 10
    if ($resp.version -eq $version) {
        Write-Host "  OK - $StaticUrl is serving v$version" -ForegroundColor Green
        $staticOk = $true
    } else {
        Write-Host "  WARN - static URL returned version $($resp.version), expected $version" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  WARN - Static URL not reachable: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Try the API endpoint (only works after the new Blazor build is deployed to VPS)
try {
    $resp2 = Invoke-RestMethod -Uri $VerifyUrl -Method Get -TimeoutSec 10
    if ($resp2.version -eq $version) {
        Write-Host "  OK - $VerifyUrl is serving v$version" -ForegroundColor Green
    }
} catch {
    if ($staticOk) {
        Write-Host "  INFO - API endpoint not yet active (deploy new Blazor build to enable it)" -ForegroundColor Cyan
    } else {
        Write-Host "  WARN - API endpoint also unreachable" -ForegroundColor Yellow
    }
}

# ----- Done -----
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Release v$version published successfully" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Verify manually any time:"
Write-Host "  Static:  $StaticUrl"
Write-Host "  API:     $VerifyUrl"
Write-Host ""
