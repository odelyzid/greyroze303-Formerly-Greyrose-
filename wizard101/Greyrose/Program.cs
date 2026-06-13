using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Greyrose.Data;

namespace Greyrose
{
    class Program
    {
        static void Main(string[] args)
        {
            DataStore.Initialize(TryGetDbPath(args));

            if (args.Contains("--build-patch-only"))
            {
                PatchData.EnsurePatchFiles();
                var meta = PatchData.GetFileListMetadata();
                Console.WriteLine("Patch list built: {0} bytes, CRC {1:X8}",
                    meta.ListFileSize, meta.ListFileCRC);
                return;
            }

            if (args.Contains("--validate-patch-bin"))
            {
                string path = Path.Combine(PatchData.GetPatchDirectory(), DefaultPatchData.ListFileName);
                Environment.Exit(PatchListAudit.RunValidate(path));
                return;
            }

            if (args.Contains("--validate-login-blob"))
            {
                Environment.Exit(RunValidateLoginBlob(args));
                return;
            }

            if (args.Contains("--resanitize-player-blobs"))
            {
                Environment.Exit(ResanitizePlayerBlobs());
                return;
            }

            if (args.Contains("--import-zone-login-blob"))
            {
                Environment.Exit(RunImportZoneLoginBlob(args));
                return;
            }

            if (args.Contains("--dump-zone-login-blob"))
            {
                Environment.Exit(RunDumpZoneLoginBlob(args));
                return;
            }

            if (args.Contains("--inspect-login-blob"))
            {
                Environment.Exit(RunInspectLoginBlob(args));
                return;
            }

            if (args.Contains("--build-patch-minimal"))
            {
                PatchData.ForceRebuildMinimal();
                var meta = PatchData.GetFileListMetadata();
                Console.WriteLine("Minimal patch list: {0} bytes, CRC {1:X8}",
                    meta.ListFileSize, meta.ListFileCRC);
                return;
            }

#if GREYROSE_WINFORMS
            if (args.Contains("--create-ico"))
            {
                string png = Path.Combine(AppContext.BaseDirectory, "Assets", "greyrose303.png");
                if (!File.Exists(png))
                    png = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "greyrose303.png"));
                string ico = Path.ChangeExtension(png, ".ico");
                Branding.IconFactory.CreateIcoFromPng(png, ico);
                Console.WriteLine("Wrote {0}", ico);
                return;
            }

            if (args.Contains("--apply-branding"))
            {
                Environment.Exit(Branding.BrandingCommand.Run(args));
                return;
            }
#endif

#if GREYROSE_WINFORMS
            if (OperatingSystem.IsWindows() && !args.Contains("--console"))
            {
                UI.GuiEntry.Run();
                return;
            }
#endif

            if (OperatingSystem.IsWindows())
                Console.Title = "GreyrOze303";
            RunConsoleServer();
        }

        static string TryGetDbPath(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--db")
                    return args[i + 1];
            }
            return null;
        }

        static void WriteDatabasePath()
        {
            Console.WriteLine("Database: {0}", Database.Path);
        }

        static int RunValidateLoginBlob(string[] args)
        {
            WriteDatabasePath();
            byte[] def = DefaultLoginBlob.GetBytes();
            CharacterRecord ch = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--char-id" && long.TryParse(args[i + 1], out long charId))
                {
                    ch = DataStore.GetCharacter(charId);
                    Console.WriteLine("Character id {0} name '{1}' gid={2} defaultTemplate={3} zoneCapture={4}",
                        charId, ch?.Name, ch?.CharGid,
                        ch != null && CharacterInfoCodec.IsDefaultTemplate(ch.CharacterInfoHex),
                        ch != null && CharacterInfoCodec.UsesZoneLoginCapture(ch));
                    break;
                }
            }

            var build = LoginBlobBuilder.BuildLoginBlobWithInfo(ch, null, def);
            byte[] blob = build.Blob;

            if (blob == null || blob.Length == 0)
            {
                Console.WriteLine("No login blob available.");
                return 1;
            }

            var v = LoginBlobBuilder.Validate(blob, build.IsCreatedCharacter);
            Console.WriteLine("Login blob: {0} bytes (source={1}, created={2}, zoneCapture={3})",
                v.Length, build.Source, build.IsCreatedCharacter, CreatedZoneLoginBlob.IsAvailable());
            Console.WriteLine("  Equipment marker offset: {0}", v.EquipmentMarkerOffset);
            Console.WriteLine("  Inventory marker offset: {0}", v.InventoryMarkerOffset);
            Console.WriteLine("  Bad template offset:     {0}", v.BadTemplateOffset);
            Console.WriteLine("  Result: {0}", v.Message);
            return v.Ok ? 0 : 1;
        }

        static int RunImportZoneLoginBlob(string[] args)
        {
            string path = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--import-zone-login-blob")
                {
                    path = args[i + 1];
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                Console.WriteLine("Usage: --import-zone-login-blob <file.bin|zone-data.bin>");
                return 1;
            }

            if (!ZoneLoginBlobImporter.TryReadPlayerBlobFile(path, out byte[] playerBlob, out string error))
            {
                Console.WriteLine("Import failed: {0}", error);
                return 1;
            }

            string dest = ZoneLoginBlobImporter.SaveToDataDirectory(playerBlob);
            Console.WriteLine("Imported {0} bytes -> {1}", playerBlob.Length, dest);
            return 0;
        }

        static int RunInspectLoginBlob(string[] args)
        {
            WriteDatabasePath();
            CharacterRecord ch = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--char-id" && long.TryParse(args[i + 1], out long charId))
                {
                    ch = DataStore.GetCharacter(charId);
                    Console.WriteLine("Character id {0} name '{1}' gid={2} defaultTemplate={3} zoneCapture={4}",
                        charId, ch?.Name, ch?.CharGid,
                        ch != null && CharacterInfoCodec.IsDefaultTemplate(ch.CharacterInfoHex),
                        ch != null && CharacterInfoCodec.UsesZoneLoginCapture(ch));
                    break;
                }
            }

            if (ch == null)
            {
                Console.WriteLine("Character not found in this database.");
                return 1;
            }

            byte[] def = DefaultLoginBlob.GetBytes();
            var build = LoginBlobBuilder.BuildLoginBlobWithInfo(ch, null, def);
            byte[] blob = build.Blob;

            if (blob == null || blob.Length == 0)
            {
                Console.WriteLine("No login blob to inspect.");
                return 1;
            }

            bool created = build.IsCreatedCharacter;
            Console.WriteLine("Zone-login build: {0} bytes (source={1}, created={2})",
                blob.Length, build.Source, created);

            var state = ch != null ? DataStore.GetPlayerState(ch.Id) : null;
            if (state != null && !string.IsNullOrWhiteSpace(state.LoginBlobHex))
            {
                byte[] stored = CharacterInfoCodec.HexToBytes(state.LoginBlobHex);
                bool same = stored.Length == blob.Length;
                if (same)
                {
                    for (int i = 0; i < stored.Length; i++)
                    {
                        if (stored[i] != blob[i])
                        {
                            same = false;
                            break;
                        }
                    }
                }
                if (!same)
                {
                    Console.WriteLine("Stored DB blob: {0} bytes (differs — run --resanitize-player-blobs or Save in Inventory tab)",
                        stored.Length);
                    Console.Write(LoginBlobInspector.FormatInspectionReport(
                        LoginBlobInspector.Parse(stored), created));
                    Console.WriteLine();
                }
            }

            Console.WriteLine("--- MSG_ATTACH payload (fresh build) ---");
            Console.Write(LoginBlobInspector.FormatInspectionReport(LoginBlobInspector.Parse(blob), created));
            return 0;
        }

        static int RunDumpZoneLoginBlob(string[] args)
        {
            byte[] def = DefaultLoginBlob.GetBytes();
            CharacterRecord ch = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--char-id" && long.TryParse(args[i + 1], out long charId))
                    ch = DataStore.GetCharacter(charId);
            }

            var build = LoginBlobBuilder.BuildLoginBlobWithInfo(ch, null, def);
            if (build.Blob == null || build.Blob.Length == 0)
            {
                Console.WriteLine("No login blob to dump.");
                return 1;
            }

            Console.WriteLine("source={0} created={1} bytes={2}",
                build.Source, build.IsCreatedCharacter, build.Blob.Length);
            Console.WriteLine(CharacterInfoCodec.BytesToHex(build.Blob));
            return 0;
        }

        static int ResanitizePlayerBlobs()
        {
            WriteDatabasePath();
            int updated = 0;
            byte[] def = DefaultLoginBlob.GetBytes();
            foreach (var ch in DataStore.GetAllCharacters())
            {
                var state = DataStore.GetPlayerState(ch.Id);
                if (state == null)
                    continue;

                byte[] fresh = LoginBlobBuilder.BuildLoginBlob(ch, null, def);
                string hex = CharacterInfoCodec.BytesToHex(fresh);
                string zoneHex = DefaultZoneBlob.GetHex();
                bool loginSame = string.Equals(state.LoginBlobHex, hex, StringComparison.OrdinalIgnoreCase);
                bool zoneSame = string.Equals(state.ZoneBlobHex, zoneHex, StringComparison.OrdinalIgnoreCase);
                if (loginSame && zoneSame)
                    continue;

                state.LoginBlobHex = hex;
                if (string.IsNullOrWhiteSpace(state.ZoneBlobHex))
                    state.ZoneBlobHex = zoneHex;
                DataStore.SavePlayerState(state);
                updated++;
                Console.WriteLine("Updated char {0} ({1}): login blob {2} bytes, zone blob {3} bytes",
                    ch.Id, ch.Name, fresh.Length,
                    CharacterInfoCodec.HexToBytes(state.ZoneBlobHex).Length);
            }

            Console.WriteLine("Resanitized {0} character(s).", updated);
            return 0;
        }

        static void RunConsoleServer()
        {
            Server.StartPatchFileServer();
            var task1 = Task.Run(Server.LS);
            var task2 = Task.Run(Server.PS);
            var task3 = Task.Run(Server.GS);
            Task.WhenAll(task1, task2, task3).GetAwaiter().GetResult();
        }
    }
}
