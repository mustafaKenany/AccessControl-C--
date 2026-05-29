# VPS credentials & SSH setup

**⚠️ This file does NOT contain credentials. It tells you where YOUR credentials should live.**

## What you need

To run `publish-release.ps1` you need to authenticate to the VPS (`hmtech.solutions` / `89.116.39.155`) as `root`. There are two ways:

| Option | When asked for password | Setup effort | Recommended |
|---|---|---|---|
| **A. SSH key** | Never (after one-time setup) | 5 minutes once | ✅ Yes |
| **B. Password every time** | 2 times per release | None | Works but tedious |

## Where to store your VPS password (NEVER in this repo)

The VPS root password was given to you by Hostinger when you set up the server.

**Safe places to keep it:**
- A password manager: **1Password / Bitwarden / KeePass** — best choice
- A text file OUTSIDE this repo folder, e.g. `C:\Users\<you>\Documents\hmtech-vps.txt`
- Hostinger's control panel → Server → Reset Root Password (if you ever forget it)

**NEVER:**
- In any file under `D:\AccessControlPro\`
- In `appsettings.json` or any source file
- In a git commit message
- In a screenshot you share

## Option A — Set up an SSH key (do this once)

This means you'll never type the VPS password again when running release scripts.

### Step 1 — Generate a key on YOUR dev PC

Open PowerShell. Run:

```powershell
ssh-keygen -t ed25519 -C "mustafa-dev-pc"
```

When it asks "Enter file in which to save the key", **press Enter** to accept the default
(`C:\Users\My Laptop\.ssh\id_ed25519`).

When it asks "Enter passphrase", **press Enter twice** for no passphrase (simpler — or set one
if you want extra security; you'll be asked for it each time you SSH).

You now have **two files** in `C:\Users\My Laptop\.ssh\`:

| File | What it is | Permission |
|---|---|---|
| `id_ed25519` | **PRIVATE key** — stays on this PC only, NEVER share it | Locked |
| `id_ed25519.pub` | **PUBLIC key** — you'll paste this into the VPS | OK to share |

### Step 2 — Copy your PUBLIC key to the VPS

This is the only time you'll type the VPS password. Open PowerShell and run:

```powershell
type $env:USERPROFILE\.ssh\id_ed25519.pub | ssh root@89.116.39.155 "mkdir -p ~/.ssh; cat >> ~/.ssh/authorized_keys; chmod 600 ~/.ssh/authorized_keys"
```

Type the VPS root password when asked. After this, the VPS knows your dev PC.

### Step 3 — Test it

Run this — it should connect **without asking for a password**:

```powershell
ssh root@89.116.39.155 "echo Connected as: $(whoami)"
```

If you see `Connected as: root` immediately → SSH key is working. Done.

If it still asks for a password → check that `/root/.ssh/authorized_keys` on the VPS contains
your public key on its own line.

## Option B — Type the password each time

If you don't want to set up keys, you can skip the setup and just type the VPS password when
`publish-release.ps1` asks. The script will prompt you 2 times per release run (once for the
ZIP upload, once for the `latest.json` upload).

## Troubleshooting

**"scp: command not found"** → OpenSSH client isn't installed.
Settings → Apps → Optional Features → "OpenSSH Client" → Install.

**"Permission denied (publickey)"** after key setup** → the public key didn't make it into
`/root/.ssh/authorized_keys` on the VPS. SSH in with the password (`ssh root@89.116.39.155`)
and check `cat ~/.ssh/authorized_keys` — your `id_ed25519.pub` content should be one of the lines.

**"Host key verification failed"** → first time connecting to this VPS from this PC. Run
`ssh root@89.116.39.155` once, type `yes` when it asks to accept the fingerprint, then
exit. After that, scripts work.

## What to do if your VPS password leaks or you lose it

1. Reset it via Hostinger control panel → Server → Reset Root Password
2. Update the new password in your password manager / text file
3. If you had an SSH key set up, the key still works — no re-setup needed
