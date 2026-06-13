# Greyrose — Wizard101 Private Server Emulator

> Quick-and-dirty login/patch/game server emulator for Wizard101 client revision **r667549.Wizard_1_390**. Built to get in-game, not for accuracy.

## Build

- **Solution:** `wizard101/WizPS.sln` — single project `Greyrose` (C#, **.NET 8** SDK-style)
- **Dependencies:** `Microsoft.Data.Sqlite` (SQLite database); BCL `System.IO.Compression.ZLibStream` for game blobs
- **Requires:** .NET SDK 8.0 or later
- **csproj quirks:** `ImplicitUsings` and `Nullable` are **disabled**; target framework is `net8.0-windows` on Windows, `net8.0` on Linux; UI/Branding code is conditionally compiled out on non-Windows
- **Build:** `dotnet build wizard101/WizPS.sln`
- **Run (dev, Windows GUI):** `dotnet run --project wizard101/Greyrose/Greyrose.csproj`
- **Run (console only):** `dotnet run --project wizard101/Greyrose/Greyrose.csproj -- --console`
- **Custom DB path:** `dotnet run --project wizard101/Greyrose/Greyrose.csproj -- --db <path>`
- **Rebuild patch list only:** `dotnet run --project wizard101/Greyrose/Greyrose.csproj -- --build-patch-only`
- **Minimal patch list (for patch error 16):** `dotnet run --project wizard101/Greyrose/Greyrose.csproj -- --build-patch-minimal`
- **Full package patch list:** set `GREYROSE_FULL_PATCH=1` env var before `--build-patch-only`
- **Validate LatestFileList.bin:** `dotnet run --project wizard101/Greyrose/Greyrose.csproj -- --validate-patch-bin`
- **Validate/login-blob commands:**
  - `--validate-login-blob [--char-id <id>]` — build + validate a character's login blob
  - `--inspect-login-blob --char-id <id>` — detailed blob structure dump
  - `--dump-zone-login-blob --char-id <id>` — hex dump of built blob
  - `--import-zone-login-blob <file>` — import zone capture blob to Data/
  - `--resanitize-player-blobs` — rebuild stored player login blobs after blob fixes
- **Branding (WinForms only):**
  - `--create-ico` — generate .ico from Assets/greyrose303.png
  - `--apply-branding` — apply Greyrose launcher/server icons
  - `powershell -File wizard101/Greyrose/Tools/ApplyGreyroseBranding.ps1` — patches executables
- **Publish (single-file, self-contained):**
  ```bash
  dotnet publish wizard101/Greyrose/Greyrose.csproj -c Release -r win-x64 --self-contained true
  ```
- **Docker (multi-platform):** `./build.sh` — builds via `wizard101/Greyrose/Dockerfile`, extracts to `artifacts/{win-x64,linux-x64,linux-arm,linux-arm64}` (console-only; no WinForms on Linux)
- **No tests, no CI, no linter/typecheck**

## Run

1. Add hosts entries (edit `C:\Windows\System32\drivers\etc\hosts` as Administrator):
   ```
   127.0.0.1 login.us.wizard101.com
   127.0.0.1 patch.us.wizard101.com
   ```
   Login → port 12000 (launcher auth); patch → port 12500 (file-list/patching). Run `ipconfig /flushdns` after editing. Verify with `ping login.us.wizard101.com` — must reply from `127.0.0.1`, not `169.254.x.x`.

   **Troubleshooting DNS:** If ping shows `169.254.x.x`, remove duplicate/conflicting hosts entries, disable unused virtual adapters (Hyper-V, WSL, VPN), flush DNS, ensure only one `127.0.0.1` line per hostname.
2. Point the game client at `127.0.0.1` (hosts or patched client).
3. **Windows GUI:** launch Greyrose without args. Click **Start** after editing the database (servers do not auto-start in GUI mode).
4. **Console / Linux / Docker:** pass `--console` — all three servers start immediately.

## Server Ports

| Server | TCP   | UDP   | Notes |
|--------|-------|-------|-------|
| Login  | 12000 | —     | KIP protocol |
| Patch  | 12500 | —     | KIP protocol (metadata/file-list requests) |
| Patch  | 12501 | —     | HTTP (PatchFileServer — serves actual file bytes) |
| Game   | 12170 | 12171 | UDP 12171 is advertised but **no UDP listener is implemented** |

All TCP bind to `0.0.0.0`. Encryption key (same for all): `"but most of all, 11a10318 is my hero"`

## Database

- **File:** `greyrose.db` beside the executable, or `%AppData%\Greyrose\greyrose.db` if install folder is not writable
- **Schema:** `wizard101/Greyrose/Data/Schema.sql` — tables `accounts`, `characters`, `player_state`
- **Default account:** UserGID `4295088136144`, username `Greyrose`
- **Seed:** on first run (empty DB), a default Ravenwood character is inserted from hardcoded packet data
- **GUI tabs:** Accounts, Characters, Player State — full CRUD; toolbar **Initialize database** re-runs seed if empty

## Architecture

- **Entrypoint:** `Program.cs` — GUI on Windows by default (`UI/MainForm`); `--console` runs `Server.LS`, `Server.PS`, `Server.GS` in parallel
- **`ServerLog`** — console output + `OnLine` event (GUI log tab subscribes)
- **`Server`** — `partial class` split across `Servers/LoginServer.cs`, `Servers/PatchServer.cs`, `Servers/GameServer.cs`, `Servers/ServerLifecycle.cs`, `Servers/PatchFileServer.cs`
- **`PatchFileServer`** — separate `HttpListener` on port 12501 (started first via `Server.StartPatchFileServer()`), serves file bytes from `Data/Patch/`
- **`ClientSession`** — per-connection TCP state; `SelectedCharacterId`, `AccountUserGid` set during login flow
- **`GameHandoff`** — bridges login server's character-select to the game server's TCP session (120-second timeout, single-session lock)
- **`MessageParser`** reads raw bytes, validates `0xF00D` header, dispatches by SVCID:
  - Control opcodes → `ControlMessages` (session offer, keepalive, session accept)
  - `SVCID 1` → `BaseMessages` (ping/pong)
  - `SVCID 2` → `ExtendedBaseMessages` (log-only stubs: raw text, force disconnect)
  - `SVCID 5` → `GameMessages` (movement, attach/logincomplete, mark/recall — uses DB player state)
  - `SVCID 7` → `LoginMessages` (auth, char list from DB, create/delete/select character)
  - `SVCID 8` → `PatchMessages` (file list metadata requests — served as DML table-streams)
  - `SVCID 12` → `WizardMessages` (log-only stub)
  - `SVCID 52` → `QuestMessages` (log-only stub)
- **`KIPacket` (`MessageCrafter.cs`)** — builder class for DML packets; has a **4096-byte backing buffer** and is **not thread-safe** (static backing fields)
- **`DataHandler`** — reader helpers for DML field types
- **`KinPacketFrame` / `GamePacketTrace`** — frame parser + live packet tracing (powers GUI GamePacketLogPanel)
- **Player data pipeline:**
  - `PlayerData` — load/save in-memory `PlayerStruct` through `DataStore` keyed by `SelectedCharacterId`
  - `LoginBlobBuilder` constructs MSG_LOGINCOMPLETE blobs (detects creation vs. zone-capture vs. default sources)
  - `CharacterInfoCodec` — hex/bytes, name extraction, dispatch-slot management
  - `ZoneLoginPayloadBuilder` — compresses player blob via ZLibStream and wraps in zone-state prefix
  - `DefaultZoneBlob` / `CreatedZoneBlob` — static zone-state prefixes bundled in the project
  - `LoginBlobInspector` / `ZoneLoginBlobImporter` — blob validation and import tools
- **DML table-stream tooling:** `DmlTableWriter`, `KipFrameBuilder`, `DmlServerListBuilder`, `DmlLatestFileListBuilder`, `DmlTableStreamValidator`

## Key Conventions

- All packets use **KIP (KingsIsle Networking Protocol)** framing: `0xF00D` + length(ushort) + payload
- DML fields: `BYT/UBYT/SHRT/USHRT/INT/UINT/FLT/DBL/GID/STR/WSTR` — little-endian
- Character list and login blobs come from the database (editable hex fields in GUI)
- Comment-out patterns: unused handlers remain as commented code blocks (marker trails, jump-triggered stat updates)

## Protocol References

- `wizard101/Greyrose/Documentation/Packets.txt` — KIP/DML format docs (sourced from Joshsora's libki wiki)
- `wizard101/Greyrose/Documentation/Messages.txt` — how to extract message XML from `root.wad` via QuickBMS
- `wizard101/Greyrose/Documentation/LoginServerList.md` — MSG_SERVERLIST encoding notes
- External: `https://github.com/Joshsora/libki` — more accurate/complete implementation

## Known Limitations

- Session key is hardcoded; no real crypto
- Auth debug-admits; DB defaults to first account
- Patch TCP server (12500) sends metadata only; PatchFileServer HTTP (12501) serves file bytes
- No zone transfer logic beyond the initial teleport to Ravenwood
- `KIPacket` has a 4096-byte backing buffer — packets exceeding this will silently corrupt
- `KIPacket` uses static backing fields — not thread-safe for concurrent clients
- `MessageParser.Parse` passes the full read buffer without slicing to bytes actually read
- UDP port 12171 is referenced in char-select handoff but not bound
- Many login/game message IDs are log-only stubs (no response packets)
- Docker/Linux builds exclude WinForms (`net8.0` only); GUI requires `net8.0-windows`
