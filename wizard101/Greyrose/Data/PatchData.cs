using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Greyrose;

namespace Greyrose.Data
{
    static class PatchData
    {
        public struct FileListMetadata
        {
            public uint LatestVersion;
            public string ListFileName;
            public uint ListFileType;
            public uint ListFileTime;
            public uint ListFileSize;
            public uint ListFileCRC;
            public string ListFileURL;
            public string URLPrefix;
            public string URLSuffix;
        }

        const string EmptyFileListXml =
            "<?xml version=\"1.0\" ?>\r\n<LatestFileList>\r\n<_TableList>\r\n</_TableList>\r\n</LatestFileList>\r\n";

        static string PatchDirectory =>
            Path.Combine(AppContext.BaseDirectory, "Data", "Patch");

        static readonly object PatchBuildLock = new object();
        static FileListMetadata CachedMetadata;
        static bool MetadataCached;

        public static string GetPatchDirectory() => PatchDirectory;

        /// <summary>Force rebuild with metadata-only patch list (no package tables).</summary>
        public static void ForceRebuildMinimal()
        {
            lock (PatchBuildLock)
            {
                string binPath = Path.Combine(PatchDirectory, DefaultPatchData.ListFileName);
                byte[] built = DmlLatestFileListBuilder.BuildMetadataOnly();
                Directory.CreateDirectory(PatchDirectory);
                WriteListFileAtomically(binPath, built);
                SyncClientPatchInfo(binPath);
                MetadataCached = false;
                RefreshMetadataCache();
                ServerLog.WriteLine("Patch: rebuilt minimal LatestFileList.bin ({0} bytes)", built.Length);
            }
        }

        public static void ForceRebuildFull()
        {
            lock (PatchBuildLock)
            {
                string binPath = Path.Combine(PatchDirectory, DefaultPatchData.ListFileName);
                byte[] built = BuildLatestFileListBin(EmptyFileListXml);
                Directory.CreateDirectory(PatchDirectory);
                WriteListFileAtomically(binPath, built);
                SyncClientPatchInfo(binPath);
                MetadataCached = false;
                RefreshMetadataCache();
                ServerLog.WriteLine("Patch: rebuilt full LatestFileList.bin ({0} bytes)", built.Length);
            }
        }

        public static void EnsurePatchFiles()
        {
            lock (PatchBuildLock)
            {
                EnsurePatchFilesCore();
            }
        }

        static void EnsurePatchFilesCore()
        {
            Directory.CreateDirectory(PatchDirectory);
            EnsureClientPatchInfo();

            string xmlPath = Path.Combine(PatchDirectory, "LatestFileList.xml");
            if (!File.Exists(xmlPath))
                File.WriteAllText(xmlPath, EmptyFileListXml, Encoding.UTF8);

            string binPath = Path.Combine(PatchDirectory, DefaultPatchData.ListFileName);
            bool needsBuild = !File.Exists(binPath);
            if (!needsBuild)
            {
                byte[] existing = ReadListFileBytes(binPath);
                if (!ShouldUseFullPackageList() && existing.Length < 4096)
                    needsBuild = true;
                else if (!IsValidListFile(existing) && existing.Length < 4096)
                    needsBuild = true;
            }

            if (!needsBuild && !TryUseExistingListFile(binPath))
                needsBuild = true;

            if (needsBuild)
            {
                if (!ShouldUseFullPackageList() && TryCopyStockListFile(binPath))
                {
                    ServerLog.WriteLine("Patch: restored stock LatestFileList.bin ({0} bytes) at {1}",
                        ReadListFileBytes(binPath).Length, binPath);
                }
                else
                {
                    byte[] built = ShouldUseFullPackageList()
                        ? BuildLatestFileListBin(EmptyFileListXml)
                        : DmlLatestFileListBuilder.BuildMetadataOnly();
                    WriteListFileAtomically(binPath, built);
                    ServerLog.WriteLine(
                        "Patch: built LatestFileList.bin ({0} bytes, DML{1}) at {2}",
                        built.Length,
                        ShouldUseFullPackageList()
                            ? $", {LocalPackageCatalog.LoadPackageNames().Count} packages"
                            : ", metadata-only",
                        binPath);
                }

                SyncClientPatchInfo(binPath);
                MetadataCached = false;
            }
            else if (!ShouldUseFullPackageList())
            {
                byte[] existing = ReadListFileBytes(binPath);
                if (existing.Length < 4096 && TryCopyStockListFile(binPath))
                {
                    ServerLog.WriteLine("Patch: upgraded minimal list to stock ({0} bytes)",
                        ReadListFileBytes(binPath).Length);
                    SyncClientPatchInfo(binPath);
                    MetadataCached = false;
                }
            }

            RefreshMetadataCache();
        }

        static byte[] ReadListFileBytes(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        static void WriteListFileAtomically(string path, byte[] bytes)
        {
            string tempPath = path + ".tmp";
            File.WriteAllBytes(tempPath, bytes);
            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
            {
                File.Move(tempPath, path);
            }
        }

        static void SyncClientPatchInfo(string serverBinPath)
        {
            string clientPatchInfo = ResolveClientPatchInfoDirectory();
            if (clientPatchInfo == null || !File.Exists(serverBinPath))
                return;

            try
            {
                Directory.CreateDirectory(clientPatchInfo);
                string clientBin = Path.Combine(clientPatchInfo, DefaultPatchData.ListFileName);
                byte[] bytes = ReadListFileBytes(serverBinPath);
                uint crc = KiCrc32.Compute(bytes);

                if (File.Exists(clientBin))
                {
                    byte[] existing = ReadListFileBytes(clientBin);
                    if (KiCrc32.Compute(existing) == crc)
                        return;
                }

                string tempPath = clientBin + ".tmp";
                File.WriteAllBytes(tempPath, bytes);
                if (File.Exists(clientBin))
                    File.Replace(tempPath, clientBin, null);
                else
                    File.Move(tempPath, clientBin);

                ServerLog.WriteLine(
                    "Patch: synced LatestFileList.bin ({0} bytes, DML) to client PatchInfo",
                    bytes.Length);
            }
            catch (IOException ex)
            {
                ServerLog.WriteLine(
                    "Patch: skipped PatchInfo sync (file in use): {0}",
                    ex.Message);
            }
        }

        static bool TryUseExistingListFile(string binPath)
        {
            foreach (string candidate in GetListFileCandidates(binPath))
            {
                if (!File.Exists(candidate))
                    continue;

                byte[] bytes = ReadListFileBytes(candidate);
                bool trusted = IsTrustedStockListSource(candidate);
                if (!trusted && !IsValidListFile(bytes))
                    continue;
                if (!ShouldUseFullPackageList() && bytes.Length < 4096)
                    continue;

                if (!string.Equals(candidate, binPath, StringComparison.OrdinalIgnoreCase))
                    File.Copy(candidate, binPath, overwrite: true);

                ServerLog.WriteLine("Patch: using LatestFileList.bin ({0} bytes, DML) from {1}",
                    bytes.Length, candidate);
                return true;
            }

            if (File.Exists(binPath))
            {
                byte[] existing = ReadListFileBytes(binPath);
                if (IsValidListFile(existing)
                    && (ShouldUseFullPackageList() || existing.Length >= 4096))
                    return true;
            }

            return false;
        }

        static bool TryCopyStockListFile(string binPath)
        {
            foreach (string candidate in GetStockListFileCandidates())
            {
                if (!File.Exists(candidate))
                    continue;

                byte[] bytes = ReadListFileBytes(candidate);
                if (bytes.Length < 4096)
                    continue;

                bool trusted = IsTrustedStockListSource(candidate);
                if (!trusted && !IsValidListFile(bytes))
                    continue;

                WriteListFileAtomically(binPath, bytes);
                ServerLog.WriteLine("Patch: copied stock LatestFileList.bin ({0} bytes) from {1}",
                    bytes.Length, candidate);
                return true;
            }

            return false;
        }

        static bool IsTrustedStockListSource(string path)
        {
            if (path.EndsWith(".reference", StringComparison.OrdinalIgnoreCase))
                return true;

            string projectSource = ResolveProjectPatchDataFile();
            return projectSource != null
                && string.Equals(path, projectSource, StringComparison.OrdinalIgnoreCase);
        }

        static IEnumerable<string> GetStockListFileCandidates()
        {
            yield return Path.Combine(PatchDirectory, "LatestFileList.bin.reference");
            string projectSource = ResolveProjectPatchDataFile();
            if (projectSource != null)
                yield return projectSource;
        }

        static string ResolveProjectPatchDataFile()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int depth = 0; depth < 10 && dir != null; depth++)
            {
                string wizardPath = Path.Combine(
                    dir.FullName,
                    "wizard101",
                    "Greyrose",
                    "Data",
                    "Patch",
                    DefaultPatchData.ListFileName);
                if (File.Exists(wizardPath))
                    return wizardPath;

                string dataPatch = Path.Combine(dir.FullName, "Data", "Patch", DefaultPatchData.ListFileName);
                if (File.Exists(dataPatch) && new FileInfo(dataPatch).Length >= 4096)
                    return dataPatch;

                dir = dir.Parent;
            }

            return null;
        }

        static bool IsValidListFile(byte[] bytes) => DmlLatestFileListBuilder.IsValidListFile(bytes);

        static IEnumerable<string> GetListFileCandidates(string binPath)
        {
            yield return Path.Combine(PatchDirectory, "LatestFileList.bin.reference");
            string projectSource = ResolveProjectPatchDataFile();
            if (projectSource != null)
                yield return projectSource;
            yield return binPath;
        }

        static void EnsureClientPatchInfo()
        {
            string clientPatchInfo = ResolveClientPatchInfoDirectory();
            if (clientPatchInfo == null)
                return;

            Directory.CreateDirectory(clientPatchInfo);
            string xmlPath = Path.Combine(clientPatchInfo, "LatestFileList.xml");
            if (!File.Exists(xmlPath))
                File.WriteAllText(xmlPath, EmptyFileListXml, Encoding.UTF8);


        }

        static string ResolveClientPatchInfoDirectory()
        {
            string[] relativePaths =
            {
                Path.Combine("Wizard101 April of 2019", "PatchInfo"),
                Path.Combine("..", "Wizard101 April of 2019", "PatchInfo")
            };

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int depth = 0; depth < 8 && dir != null; depth++)
            {
                foreach (var relative in relativePaths)
                {
                    string candidate = Path.GetFullPath(Path.Combine(dir.FullName, relative));
                    if (Directory.Exists(Path.GetDirectoryName(candidate)))
                        return candidate;
                }
                dir = dir.Parent;
            }

            return null;
        }

        static bool ShouldUseFullPackageList()
        {
            string flag = Environment.GetEnvironmentVariable("GREYROSE_FULL_PATCH");
            return string.Equals(flag, "1", StringComparison.Ordinal)
                || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
        }

        static byte[] BuildLatestFileListBin(string xml)
        {
            var names = LocalPackageCatalog.LoadPackageNamesForPatch();
            if (names.Count == 0)
            {
                names = new System.Collections.Generic.List<string>
                {
                    "WizardCity-WorldData",
                    "WizardCity-WC_Ravenwood"
                };
            }

            var packages = LocalPackageCatalog.BuildPackageEntries(names);
            return DmlLatestFileListBuilder.Build(names, packages);
        }

        public static FileListMetadata GetFileListMetadata()
        {
            if (MetadataCached)
                return CachedMetadata;

            lock (PatchBuildLock)
            {
                if (MetadataCached)
                    return CachedMetadata;

                EnsurePatchFilesCore();
                return CachedMetadata;
            }
        }

        static FileListMetadata RefreshMetadataCache()
        {
            string binPath = Path.Combine(PatchDirectory, DefaultPatchData.ListFileName);
            byte[] bytes = File.Exists(binPath) ? ReadListFileBytes(binPath) : Array.Empty<byte>();
                uint crc = bytes.Length > 0 ? KiCrc32.Compute(bytes) : 0;

            uint listFileTime = 0;
            if (File.Exists(binPath))
            {
                listFileTime = (uint)new DateTimeOffset(File.GetLastWriteTimeUtc(binPath)).ToUnixTimeSeconds();
            }

            var meta = new FileListMetadata
            {
                LatestVersion = DefaultPatchData.LatestVersion,
                ListFileName = DefaultPatchData.ListFileName,
                ListFileType = DefaultPatchData.ListFileType,
                ListFileTime = listFileTime,
                ListFileSize = (uint)bytes.Length,
                ListFileCRC = crc,
                ListFileURL = DefaultPatchData.ListFileUrl,
                URLPrefix = DefaultPatchData.HttpBaseUrl,
                URLSuffix = DefaultPatchData.URLSuffix
            };

            CachedMetadata = meta;
            MetadataCached = true;
            return meta;
        }

    }
}
