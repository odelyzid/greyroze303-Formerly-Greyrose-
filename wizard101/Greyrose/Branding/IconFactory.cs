using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Greyrose.Branding
{
    static class IconFactory
    {
        public static void CreateIcoFromPng(string pngPath, string icoPath)
        {
            using var source = Image.FromFile(pngPath);
            int[] sizes = { 16, 32, 48, 256 };
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)sizes.Length);

            var images = new Bitmap[sizes.Length];
            var pngData = new byte[sizes.Length][];
            int offset = 6 + 16 * sizes.Length;

            for (int i = 0; i < sizes.Length; i++)
            {
                images[i] = ResizeSquare(source, sizes[i]);
                using var pngMs = new MemoryStream();
                images[i].Save(pngMs, ImageFormat.Png);
                pngData[i] = pngMs.ToArray();

                writer.Write((byte)(sizes[i] == 256 ? 0 : (byte)sizes[i]));
                writer.Write((byte)(sizes[i] == 256 ? 0 : (byte)sizes[i]));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)pngData[i].Length);
                writer.Write((uint)offset);
                offset += pngData[i].Length;
            }

            for (int i = 0; i < sizes.Length; i++)
                writer.Write(pngData[i]);

            Directory.CreateDirectory(Path.GetDirectoryName(icoPath) ?? ".");
            File.WriteAllBytes(icoPath, ms.ToArray());

            foreach (var img in images)
                img.Dispose();
        }

        public static Icon LoadIcon(string icoPath) => new Icon(icoPath);

        static Bitmap ResizeSquare(Image source, int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(source, 0, 0, size, size);
            return bmp;
        }

        public static byte[] RenderPng(Image source, int width, int height)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(source, 0, 0, width, height);
            }
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            bmp.Dispose();
            return ms.ToArray();
        }
    }
}
