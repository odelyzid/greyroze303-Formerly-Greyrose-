using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Greyrose.Data
{
    static class LocalPackageCatalog
    {
        const uint DefaultFileType = 5;
        const long MaxCrcScanBytes = 50 * 1024 * 1024;

        public static IReadOnlyList<string> LoadPackageNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string listPath = ResolveLocalPackagesListPath();
            if (listPath != null && File.Exists(listPath))
            {
                foreach (string line in File.ReadAllLines(listPath))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length > 0)
                        names.Add(trimmed);
                }
            }

            AddClassicModeAliases(names);
            return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Package names that have a local .wad file (for LatestFileList.bin).</summary>
        public static IReadOnlyList<string> LoadPackageNamesForPatch()
        {
            var result = new List<string>();
            foreach (string name in LoadPackageNames())
            {
                if (ResolveWadPath(name) != null)
                    result.Add(name);
            }
            return result;
        }

        public static bool IsKnownPackage(string pkgName)
        {
            if (string.IsNullOrWhiteSpace(pkgName))
                return false;

            string normalized = NormalizePackageName(pkgName);
            return LoadPackageNames().Contains(normalized, StringComparer.OrdinalIgnoreCase)
                || ResolveWadPath(normalized) != null;
        }

        public static string NormalizePackageName(string pkgName)
        {
            if (string.IsNullOrEmpty(pkgName))
                return pkgName;

            if (pkgName.StartsWith("ClassicMode-", StringComparison.OrdinalIgnoreCase))
                return "WizardCity-" + pkgName.Substring("ClassicMode-".Length);

            return pkgName;
        }

        public static IReadOnlyList<DmlLatestFileListBuilder.PackageFileInfo> BuildPackageEntries(
            IReadOnlyList<string> packageNames)
        {
            var entries = new List<DmlLatestFileListBuilder.PackageFileInfo>();
            foreach (string name in packageNames)
                entries.Add(BuildPackageEntry(name));
            return entries;
        }

        static DmlLatestFileListBuilder.PackageFileInfo BuildPackageEntry(string name)
        {
            string src = "Data/GameData/" + name + ".wad";
            string wadPath = ResolveWadPath(name);
            uint size = 1;
            uint crc = 0;
            uint headerCrc = 0;

            if (wadPath != null && File.Exists(wadPath))
            {
                var info = new FileInfo(wadPath);
                size = info.Length > uint.MaxValue ? uint.MaxValue : (uint)info.Length;
                if (info.Length > 0 && info.Length <= MaxCrcScanBytes)
                    crc = ComputeFileCrc(wadPath);
            }

            return new DmlLatestFileListBuilder.PackageFileInfo
            {
                PackageName = name,
                SrcFileName = src,
                TarFileName = src,
                FileType = DefaultFileType,
                Size = size,
                HeaderSize = 0,
                CompressedHeaderSize = 0,
                Crc = crc,
                HeaderCrc = headerCrc
            };
        }

        static uint ComputeFileCrc(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return KiCrc32.Compute(ms.ToArray());
        }

        static string ResolveWadPath(string packageName)
        {
            string[] roots =
            {
                Path.Combine("Wizard101 April of 2019", "Data", "GameData"),
                Path.Combine("..", "Wizard101 April of 2019", "Data", "GameData"),
                Path.Combine(AppContext.BaseDirectory, "Data", "GameData")
            };

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int depth = 0; depth < 8 && dir != null; depth++)
            {
                foreach (string root in roots)
                {
                    string candidate = Path.GetFullPath(Path.Combine(dir.FullName, root, packageName + ".wad"));
                    if (File.Exists(candidate))
                        return candidate;
                }
                dir = dir.Parent;
            }

            return null;
        }

        static void AddClassicModeAliases(HashSet<string> names)
        {
            var aliases = new List<string>();
            foreach (string name in names.ToList())
            {
                if (!name.StartsWith("WizardCity-", StringComparison.OrdinalIgnoreCase))
                    continue;
                string alias = "ClassicMode-" + name.Substring("WizardCity-".Length);
                if (names.Contains(alias))
                    continue;
                if (ResolveWadPath(alias) != null)
                    aliases.Add(alias);
            }
            foreach (string alias in aliases)
                names.Add(alias);
        }

        static string ResolveLocalPackagesListPath()
        {
            string[] relativePaths =
            {
                Path.Combine("Wizard101 April of 2019", "LocalPackagesList.txt"),
                Path.Combine("..", "Wizard101 April of 2019", "LocalPackagesList.txt")
            };

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int depth = 0; depth < 8 && dir != null; depth++)
            {
                foreach (var relative in relativePaths)
                {
                    string candidate = Path.GetFullPath(Path.Combine(dir.FullName, relative));
                    if (File.Exists(candidate))
                        return candidate;
                }
                dir = dir.Parent;
            }

            return null;
        }
    }
}
