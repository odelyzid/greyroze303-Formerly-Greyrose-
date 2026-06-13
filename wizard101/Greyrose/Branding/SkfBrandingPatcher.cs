using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Greyrose.Branding
{
    /// <summary>
    /// Patches embedded PNGs in SkinCrafter wizard101.skf (SKF3.0).
    /// Replaces launcher banner (160x37) and 42x42 tiles with the Greyrose logo.
    /// </summary>
    static class SkfBrandingPatcher
    {
        const ushort AssetTag = 0x000A;

        public static void Patch(string skfPath, string pngPath)
        {
            EnsureRestorableBackup(skfPath);

            byte[] data = File.ReadAllBytes(skfPath);
            using var source = Image.FromFile(pngPath);

            var assets = ParseAssets(data);
            if (assets.Count == 0)
            {
                string backup = skfPath + ".orig";
                if (File.Exists(backup))
                {
                    File.Copy(backup, skfPath, true);
                    data = File.ReadAllBytes(skfPath);
                    assets = ParseAssets(data);
                }
            }

            if (assets.Count == 0)
                throw new InvalidDataException("No SKF assets found in " + skfPath);

            int headerEnd = assets[0].SizeFieldOffset;
            var output = new List<byte>(data.Length + 4096);
            output.AddRange(new ArraySegment<byte>(data, 0, headerEnd));

            byte[] icon42 = IconFactory.RenderPng(source, 42, 42);
            byte[] banner160 = IconFactory.RenderPng(source, 160, 37);

            foreach (var asset in assets)
            {
                byte[] png = asset.Png;
                if (asset.Width == 42 && asset.Height == 42)
                    png = icon42;
                else if (asset.Width == 160 && asset.Height == 37)
                    png = banner160;

                WriteUInt32(output, (uint)png.Length);
                WriteUInt16(output, AssetTag);
                output.AddRange(png);
            }

            File.WriteAllBytes(skfPath, output.ToArray());
        }

        static void WriteUInt32(List<byte> buf, uint v)
        {
            buf.Add((byte)(v & 0xFF));
            buf.Add((byte)((v >> 8) & 0xFF));
            buf.Add((byte)((v >> 16) & 0xFF));
            buf.Add((byte)((v >> 24) & 0xFF));
        }

        static void WriteUInt16(List<byte> buf, ushort v)
        {
            buf.Add((byte)(v & 0xFF));
            buf.Add((byte)((v >> 8) & 0xFF));
        }

        static List<SkfAsset> ParseAssets(byte[] data)
        {
            var list = new List<SkfAsset>();
            for (int i = 0; i < data.Length - 16; i++)
            {
                if (!IsPngSignature(data, i))
                    continue;
                if (i < 6 || data[i - 2] != 0x0A || data[i - 1] != 0x00)
                    continue;

                int sizeFieldOffset = i - 4;
                int pngLen = GetPngLength(data, i);
                if (pngLen <= 0 || i + pngLen > data.Length)
                    continue;

                GetPngSize(data, i, out int w, out int h);
                list.Add(new SkfAsset
                {
                    SizeFieldOffset = sizeFieldOffset,
                    PngOffset = i,
                    Png = new byte[pngLen],
                    Width = w,
                    Height = h
                });
                Buffer.BlockCopy(data, i, list[list.Count - 1].Png, 0, pngLen);
                i += pngLen - 1;
            }
            return list;
        }

        static bool IsPngSignature(byte[] data, int i) =>
            data[i] == 0x89 && data[i + 1] == 0x50 && data[i + 2] == 0x4E && data[i + 3] == 0x47
            && data[i + 4] == 0x0D && data[i + 5] == 0x0A && data[i + 6] == 0x1A && data[i + 7] == 0x0A;

        static int GetPngLength(byte[] data, int start)
        {
            int pos = start + 8;
            while (pos < data.Length - 12)
            {
                int chunkLen = ReadBe32(data, pos);
                string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
                if (chunkLen < 0 || chunkLen > 5_000_000)
                    return -1;
                pos += 12 + chunkLen;
                if (type == "IEND")
                    return pos - start;
            }
            return -1;
        }

        static void GetPngSize(byte[] data, int i, out int w, out int h)
        {
            w = ReadBe32(data, i + 16);
            h = ReadBe32(data, i + 20);
        }

        static void EnsureRestorableBackup(string skfPath)
        {
            string backup = skfPath + ".orig";
            if (!File.Exists(backup))
                return;

            var current = new FileInfo(skfPath);
            var original = new FileInfo(backup);
            if (!current.Exists || current.Length < original.Length / 2)
            {
                File.Copy(backup, skfPath, true);
                Console.WriteLine("Restored {0} from .orig ({1} bytes).",
                    Path.GetFileName(skfPath), original.Length);
            }
        }

        static int ReadBe32(byte[] data, int i) =>
            (int)((uint)data[i] << 24 | (uint)data[i + 1] << 16 | (uint)data[i + 2] << 8 | data[i + 3]);

        sealed class SkfAsset
        {
            public int SizeFieldOffset;
            public int PngOffset;
            public byte[] Png;
            public int Width;
            public int Height;
        }
    }
}
