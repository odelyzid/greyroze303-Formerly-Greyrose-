using System;
using System.IO;
using System.Text;

namespace Greyrose.Data
{
    static class PatchListAudit
    {
        public static int RunValidate(string binPath)
        {
            if (!File.Exists(binPath))
            {
                Console.WriteLine("Patch audit: file not found: {0}", binPath);
                return 1;
            }

            byte[] bytes = File.ReadAllBytes(binPath);
            var vr = DmlTableStreamValidator.Validate(bytes);
            uint crc = KiCrc32.Compute(bytes);

            Console.WriteLine("Patch audit: {0}", binPath);
            Console.WriteLine("  Size: {0} bytes, CRC: {1:X8}", bytes.Length, crc);
            Console.WriteLine("  DmlLatestFileListBuilder.IsValidListFile: {0}",
                DmlLatestFileListBuilder.IsValidListFile(bytes));
            Console.WriteLine("  Stream validator: {0} (tables={1}, offset={2})",
                vr.Ok ? "OK" : vr.Error, vr.TablesParsed, vr.Offset);

            string refPath = binPath + ".reference";
            if (File.Exists(refPath))
                CompareToReference(bytes, File.ReadAllBytes(refPath));

            bool valid = vr.Ok && DmlLatestFileListBuilder.IsValidListFile(bytes);
            return valid ? 0 : 1;
        }

        static void CompareToReference(byte[] built, byte[] reference)
        {
            Console.WriteLine("  Reference: {0} bytes", reference.Length);
            int min = Math.Min(built.Length, reference.Length);
            for (int i = 0; i < min; i++)
            {
                if (built[i] == reference[i])
                    continue;
                Console.WriteLine("  First byte mismatch at offset {0}: built={1:X2} ref={2:X2}",
                    i, built[i], reference[i]);
                DumpContext(built, reference, i);
                return;
            }
            if (built.Length != reference.Length)
                Console.WriteLine("  Same prefix; length differs (built={0}, ref={1})",
                    built.Length, reference.Length);
            else
                Console.WriteLine("  Reference: identical bytes");
        }

        static void DumpContext(byte[] built, byte[] reference, int offset)
        {
            int start = Math.Max(0, offset - 16);
            int len = Math.Min(48, Math.Min(built.Length, reference.Length) - start);
            Console.WriteLine("  Built : {0}", BitConverter.ToString(built, start, len));
            Console.WriteLine("  Ref   : {0}", BitConverter.ToString(reference, start, len));
            string ascii = Encoding.ASCII.GetString(built, start, len);
            Console.WriteLine("  ASCII : {0}", ascii.Replace('\0', '.'));
        }
    }
}
