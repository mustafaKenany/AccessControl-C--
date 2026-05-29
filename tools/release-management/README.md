# Release Management

How to ship a new AccessControlPro build to all customers.

## The auto-update flow (high-level)

1. WPF apps poll `https://hmtech.solutions/api/version/latest` on every launch
2. If the manifest version is newer than the running version → show a "تحديث جديد متاح / New update available" badge
3. Customer clicks → app downloads the ZIP, verifies SHA-256, launches `Updater.exe`, swaps files, relaunches
4. `appsettings.json`, `License.dat`, `Logs/`, `Backups/`, and the SQL Server database are **never touched**

## Publishing a new release — step by step

### 1. Build and zip the new version locally

```cmd
cd D:\AccessControlPro
dotnet publish src/AccessControlPro.WPF/AccessControlPro.WPF.csproj ^
    -c Release -r win-x64 --self-contained false ^
    -o publish/v4.5.0
```

Make a ZIP of `publish/v4.5.0/`. PowerShell:
```powershell
Compress-Archive -Path publish\v4.5.0\* -DestinationPath publish\AccessControlPro-v4.5.0.zip
```

Compute the SHA-256 of the ZIP (you'll need it for the manifest):
```powershell
Get-FileHash publish\AccessControlPro-v4.5.0.zip -Algorithm SHA256 | Select-Object Hash
```

### 2. Upload the ZIP to the VPS

```bash
scp publish/AccessControlPro-v4.5.0.zip root@89.116.39.155:/var/www/gymapp/AccessControlPro.Web/wwwroot/releases/
```

### 3. Update the manifest

SSH into the VPS:
```bash
ssh root@89.116.39.155
cd /var/www/gymapp/AccessControlPro.Web/wwwroot/releases/
nano latest.json
```

Contents of `latest.json`:
```json
{
  "version": "4.5.0",
  "downloadUrl": "https://hmtech.solutions/releases/AccessControlPro-v4.5.0.zip",
  "sha256": "PUT_THE_HASH_FROM_STEP_1_HERE",
  "sizeBytes": 84123456,
  "releaseNotes_en": "• What's new in English (bullets, separated by \\n)\n• Second bullet",
  "releaseNotes_ar": "• ما الجديد بالعربية\n• البند الثاني",
  "mandatory": false,
  "minVersion": "4.0.0",
  "releasedAt": "2026-05-28T18:00:00Z"
}
```

Field meanings:
- **version**: semver — `MAJOR.MINOR.PATCH`. WPF compares this against its own Assembly version.
- **downloadUrl**: full HTTPS URL to the ZIP. Must be HTTPS — WPF rejects HTTP for security.
- **sha256**: hex string from `Get-FileHash`. WPF aborts the install if the downloaded ZIP doesn't match.
- **sizeBytes**: approximate file size (for progress UI; doesn't have to be exact)
- **releaseNotes_en / releaseNotes_ar**: shown in the "Update available" dialog. Use `\n` for line breaks. WPF picks the right language based on `LanguageManager.IsArabic`.
- **mandatory**: if `true`, the dialog has no "Later" button — customer must update before they can use the app. Use sparingly (security patches only).
- **minVersion**: the oldest WPF version allowed to skip this update. Anything older is treated as `mandatory: true` for that customer. Use this to force-upgrade customers stuck on very old builds.
- **releasedAt**: ISO 8601 UTC timestamp. Shown in "What's new" dialog and used for telemetry only.

### 4. Verify it's live

From any browser:
```
https://hmtech.solutions/api/version/latest
```
Should return your JSON. Cache-Control header is set to 5 minutes so customers will pick up the new version on their next app launch within ~5 minutes of you publishing.

### 5. (Optional) Roll back a bad release

Just edit `latest.json` back to the previous version. Customers who already updated will stay on the new version; customers who hadn't updated yet will not be prompted.

If a release has a critical bug and you need EVERYONE to roll back, you can:
1. Build the previous version as a new "higher" version (e.g. `4.5.1` containing the v4.4 code)
2. Set `mandatory: true` and `minVersion: 4.5.0`
3. Everyone on 4.5.0 will be forced to "upgrade" to the rollback build

## How customers see it

| Step | What they see |
|---|---|
| App launches | Normal startup, backup splash (if stale), then login |
| Update detected | Small toast/badge in main window: "تحديث جديد متاح" / "New update available" |
| They click | Dialog with version, release notes in their language, file size, ~time estimate |
| They click "Update Now" | Download progress bar (10%, 50%, 100%) |
| Download complete | "Verifying..." → "Backing up..." → "Restarting to apply update..." |
| App restarts | `Updater.exe` runs (~30 sec), then the new app launches → backup check → login |
| First login on new version | One-time "What's new in v4.5.0" dialog with the release notes |

## What's NOT touched by an update

- `appsettings.json` — gym name, language, API key, SQL connection string, backup path — all preserved
- `License.dat` — license stays activated
- `Logs/` folder — all log history preserved
- `Backups/` folder — all `.bak` files preserved
- SQL Server database — never touched (lives at `C:\Program Files\Microsoft SQL Server\...\DATA\`)
- The new version runs `DatabaseMigrator` on first launch, so any new tables/columns are added automatically and idempotently

## What IS replaced

Everything in the install folder EXCEPT the items above:
- `AccessControlPro.WPF.exe` and all `.dll` files
- `Resources/` (fonts, images, language files)
- `runtimes/` (native libs)
- The XAML resource files
