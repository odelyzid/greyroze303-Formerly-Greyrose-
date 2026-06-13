using System;
using System.IO;

namespace Greyrose.Data
{
    static class DefaultLoginBlob
    {
        static byte[] _cached;

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

        static string ResolvePath()
        {
            string[] candidates =
            {
                Path.Combine(AppContext.BaseDirectory, "Data", "DefaultLoginBlob.bin"),
                Path.Combine(AppContext.BaseDirectory, "..", "Data", "DefaultLoginBlob.bin")
            };

            foreach (string candidate in candidates)
            {
                string full = Path.GetFullPath(candidate);
                if (File.Exists(full))
                    return full;
            }

            return null;
        }
    }
}