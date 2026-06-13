# Greyrose

A quick-and-dirty login/patch/game server emulator for the **April 2019** Wizard101 client (revision `r667549.Wizard_1_390`). Built to get in-game — not for accuracy or completeness.

## Quick Start

```bash
# 1. Point DNS at localhost (edit as Administrator):
#    C:\Windows\System32\drivers\etc\hosts
#    127.0.0.1 login.us.wizard101.com
#    127.0.0.1 patch.us.wizard101.com
#    Then: ipconfig /flushdns

# 2. Build
dotnet build wizard101/WizPS.sln

# 3. Run (console mode — starts all servers immediately)
dotnet run --project wizard101/Greyrose/Greyrose.csproj -- --console

# 4. Launch the client (from Wizard101 April of 2019/Bin/):
WizardGraphicalClient.exe -L login.us.wizard101.com 12000
```

## Build & Run

| Command | Description |
|---------|-------------|
| `dotnet build wizard101/WizPS.sln` | Build the project |
| `dotnet run --project wizard101/Greyrose/Greyrose.csproj` | Run with Windows GUI |
| `dotnet run -- ... --console` | Run in console mode (Linux/Docker) |
| `dotnet run -- ... --db <path>` | Use a custom database path |

See [`AGENTS.md`](AGENTS.md) for the full command reference, CLI flags, and architecture details.

## Client Setup

The bundled client is in the `Wizard101 April of 2019/` directory. There are four ways to connect it to Greyrose:

### Method 1: Hosts file (recommended, no client changes)

Edit `C:\Windows\System32\drivers\etc\hosts` as Administrator and add:

```
127.0.0.1 login.us.wizard101.com
127.0.0.1 patch.us.wizard101.com
```

Then run `ipconfig /flushdns`. The launcher will resolve to localhost automatically. If ping shows `169.254.x.x`, see the DNS troubleshooting in [`AGENTS.md`](AGENTS.md).

### Method 2: Command-line override (direct client launch)

Open a terminal in `Wizard101 April of 2019/Bin/` and launch the game client directly, bypassing the launcher:

```
WizardGraphicalClient.exe -L 127.0.0.1 12000
```

The `-L` flag overrides the login server address and port. The working directory **must** be `Bin/` so asset paths (PatchConfig.xml, WAD files) resolve correctly.

A helper script is provided:

```powershell
# From the repo root:
powershell -File wizard101/Greyrose/Tools/LaunchGame.ps1
```

### Method 3: Edit PatchConfig.xml

In both `Bin/PatchConfig.xml` and `PatchClient/BankA/PatchConfig.xml`, change the server hostnames to `127.0.0.1`:

```xml
<PatchServerHostname host="127.0.0.1" />
<LoginHostname host="127.0.0.1" />
```

The launcher (`WizardLauncher.exe`) reads these files on startup. No hosts entries needed.

### Method 4: Hex-edit the client executable (advanced)

For a permanent patch that works without hosts or config changes, use a hex editor to replace server domains in `WizardGraphicalClient.exe`:

```
login.us.wizard101.com → 127.0.0.1.login.zz (same byte length)
patch.us.wizard101.com → 127.0.0.1.patch.zz
```

Strings are null-terminated ASCII. Search for `login.us.wizard101.com` and `patch.us.wizard101.com`. Replace with a same-length string (pad with nulls if shorter). This avoids the need for hosts entries.

## Overview

Three servers, all started in parallel:

| Server | Port(s) | Purpose |
|--------|---------|---------|
| Login  | TCP 12000 | KIP auth + character management |
| Patch  | TCP 12500, HTTP 12501 | File-list metadata + file serving |
| Game   | TCP 12170 | Movement, teleport, login blobs |

Default account: username `Greyrose`, UserGID `4295088136144`. Database auto-seeds on first run.

## Requirements

- .NET SDK 8.0 or later
- Windows (for the GUI) or any platform (console mode)
- Hosts entries pointing `login.us.wizard101.com` and `patch.us.wizard101.com` to `127.0.0.1`

## Docker

```bash
./build.sh    # builds for win-x64, linux-x64, linux-arm, linux-arm64
```

Artifacts land in `artifacts/`. Linux containers are console-only (no WinForms).

## Repository Structure

```
wizard101/
  Greyrose/        # Main C# project (Program.cs, Servers/, Message_Handlers/, Data/, UI/)
  WizPS.sln        # Solution file
Wizard101 April of 2019/   # Game client (bundled)
```

## Credits

- **Queeniee** — Original [RaGEZONE release](https://forum.ragezone.com/threads/first-official-wizard101-private-server-files-release.1169493/) (Nov 2019), provided the initial server emulator and the April 2019 game client
- **odelyzid** — Revived and extended the project with GUI, database layer, dynamic blob building, logging, PowerShell analysis tools, and ongoing development
- **Joshsora** — [libki](https://github.com/Joshsora/libki) for KIP/DML protocol documentation
- The Wizard101 reverse-engineering community

## License

Copyright © 2019–2026. Internal/educational use.
