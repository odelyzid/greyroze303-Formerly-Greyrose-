using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Greyrose.Branding
{
    static class ExeIconResource
    {
        const int RtGroupIcon = 14;
        const int RtIcon = 3;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr BeginUpdateResource(string fileName, bool deleteAll);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UpdateResource(IntPtr hUpdate, IntPtr type, IntPtr name, ushort language, byte[] data, uint dataSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool EndUpdateResource(IntPtr hUpdate, bool discard);

        public static void ApplyIcon(string exePath, string icoPath)
        {
            if (!File.Exists(exePath))
                throw new FileNotFoundException("Executable not found.", exePath);
            if (!File.Exists(icoPath))
                throw new FileNotFoundException("Icon file not found.", icoPath);

            byte[] iconDir = BuildGroupIconResource(icoPath);
            var icons = ExtractIconImages(icoPath);

            IntPtr h = BeginUpdateResource(exePath, false);
            if (h == IntPtr.Zero)
                throw new InvalidOperationException("BeginUpdateResource failed: " + Marshal.GetLastWin32Error());

            try
            {
                if (!UpdateResource(h, (IntPtr)RtGroupIcon, (IntPtr)1, 0, iconDir, (uint)iconDir.Length))
                    throw new InvalidOperationException("UpdateResource group failed: " + Marshal.GetLastWin32Error());

                for (int i = 0; i < icons.Length; i++)
                {
                    if (!UpdateResource(h, (IntPtr)RtIcon, (IntPtr)(i + 1), 0, icons[i], (uint)icons[i].Length))
                        throw new InvalidOperationException("UpdateResource icon failed: " + Marshal.GetLastWin32Error());
                }

                if (!EndUpdateResource(h, false))
                    throw new InvalidOperationException("EndUpdateResource failed: " + Marshal.GetLastWin32Error());
            }
            catch
            {
                EndUpdateResource(h, true);
                throw;
            }
        }

        /// <summary>ICONDIR for RT_GROUP_ICON — nId entries are 1-based RT_ICON resource ids.</summary>
        static byte[] BuildGroupIconResource(string icoPath)
        {
            byte[] ico = File.ReadAllBytes(icoPath);
            if (ico.Length < 6)
                throw new InvalidDataException("Invalid .ico file.");
            int count = ico[4] | (ico[5] << 8);
            if (count <= 0)
                throw new InvalidDataException("No icons in .ico file.");

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((ushort)0);
            w.Write((ushort)1);
            w.Write((ushort)count);

            for (int i = 0; i < count; i++)
            {
                int entry = 6 + i * 16;
                w.Write(ico[entry]);
                w.Write(ico[entry + 1]);
                w.Write(ico[entry + 2]);
                w.Write(ico[entry + 3]);
                w.Write((ushort)(ico[entry + 4] | (ico[entry + 5] << 8)));
                w.Write((ushort)(ico[entry + 6] | (ico[entry + 7] << 8)));
                w.Write(BitConverter.ToUInt32(ico, entry + 8));
                w.Write((ushort)(i + 1));
            }

            return ms.ToArray();
        }

        static byte[][] ExtractIconImages(string icoPath)
        {
            byte[] ico = File.ReadAllBytes(icoPath);
            int count = ico[4] | (ico[5] << 8);
            var result = new byte[count][];
            for (int i = 0; i < count; i++)
            {
                int entry = 6 + i * 16;
                uint size = BitConverter.ToUInt32(ico, entry + 8);
                uint offset = BitConverter.ToUInt32(ico, entry + 12);
                result[i] = new byte[size];
                Buffer.BlockCopy(ico, (int)offset, result[i], 0, (int)size);
            }
            return result;
        }
    }
}
