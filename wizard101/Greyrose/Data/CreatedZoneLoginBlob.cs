using System;
using System.IO;

namespace Greyrose.Data
{
    /// <summary>
    /// Player section for MSG_LOGINCOMPLETE (zone login), captured from a working new-wizard login.
    /// Replace via --import-zone-login-blob when you have an April 2019 stock-server capture.
    /// </summary>
    static class CreatedZoneLoginBlob
    {
        public const string FileName = "CreatedZoneLoginBlob.bin";
        static byte[] _cached;

        public static bool IsAvailable()
        {
            byte[] bytes = GetBytes();
            return bytes != null && bytes.Length > 0;
        }

        public static byte[] GetBytes()
        {
            if (_cached != null)
                return (byte[])_cached.Clone();

            string path = ResolvePath();
            if (path != null && File.Exists(path))
            {
                _cached = File.ReadAllBytes(path);
                return (byte[])_cached.Clone();
            }

            return Array.Empty<byte>();
        }

        public static string ResolvePath()
        {
            string[] candidates =
            {
                Path.Combine(AppContext.BaseDirectory, "Data", FileName),
                Path.Combine(AppContext.BaseDirectory, "..", "Data", FileName),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", FileName)
            };

            foreach (string candidate in candidates)
            {
                string full = Path.GetFullPath(candidate);
                if (File.Exists(full))
                    return full;
            }

            return null;
        }

        public static string GetDataDirectory()
        {
            string resolved = ResolvePath();
            if (!string.IsNullOrEmpty(resolved))
                return Path.GetDirectoryName(resolved);

            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Data"));
        }

        public static void ClearCache()
        {
            _cached = null;
        }
    }
}
