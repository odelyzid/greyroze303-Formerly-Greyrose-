using System;
using System.IO;
using System.IO.Compression;

namespace Greyrose.Data
{
    /// <summary>
    /// Imports or extracts the uncompressed player blob used in MSG_LOGINCOMPLETE zone data.
    /// </summary>
    static class ZoneLoginBlobImporter
    {
        public const int MinPlayerBlobBytes = 24;
        public const int MaxPlayerBlobBytes = 16384;

        public static bool TryReadPlayerBlobFile(string path, out byte[] playerBlob, out string error)
        {
            playerBlob = null;
            error = null;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = "file not found";
                return false;
            }

            byte[] raw = File.ReadAllBytes(path);
            if (TryParseZoneDataValue(raw, out playerBlob))
                return ValidateImported(playerBlob, out error);

            if (TryDecompressZlib(raw, out playerBlob))
                return ValidateImported(playerBlob, out error);

            playerBlob = raw;
            return ValidateImported(playerBlob, out error);
        }

        public static bool TryParseZoneDataValue(byte[] zoneDataValue, out byte[] playerBlob)
        {
            playerBlob = null;
            if (zoneDataValue == null || zoneDataValue.Length < 8)
                return false;

            for (int zoneEnd = zoneDataValue.Length - 6; zoneEnd >= 0; zoneEnd--)
            {
                int payloadLen = BitConverter.ToInt32(zoneDataValue, zoneEnd + 2);
                if (payloadLen <= 0 || payloadLen > zoneDataValue.Length)
                    continue;

                int compressedStart = zoneEnd + 6;
                if (compressedStart + payloadLen > zoneDataValue.Length)
                    continue;

                int headerLen = BitConverter.ToUInt16(zoneDataValue, zoneEnd);
                if (headerLen != payloadLen + 6)
                    continue;

                byte[] compressed = new byte[payloadLen];
                Buffer.BlockCopy(zoneDataValue, compressedStart, compressed, 0, payloadLen);
                if (!TryDecompressZlib(compressed, out playerBlob))
                    continue;

                if (playerBlob != null && playerBlob.Length >= MinPlayerBlobBytes)
                    return true;
            }

            return false;
        }

        public static bool ValidateImported(byte[] playerBlob, out string error)
        {
            error = null;
            if (playerBlob == null || playerBlob.Length < MinPlayerBlobBytes)
            {
                error = $"player blob too short (min {MinPlayerBlobBytes} bytes)";
                return false;
            }

            if (playerBlob.Length > MaxPlayerBlobBytes)
            {
                error = $"player blob too long (max {MaxPlayerBlobBytes} bytes)";
                return false;
            }

            var check = LoginBlobBuilder.Validate(playerBlob, createdCharacter: true);
            if (!check.Ok)
            {
                error = check.Message;
                return false;
            }

            return true;
        }

        public static string SaveToDataDirectory(byte[] playerBlob)
        {
            string dir = CreatedZoneLoginBlob.GetDataDirectory();
            Directory.CreateDirectory(dir);
            string dest = Path.Combine(dir, CreatedZoneLoginBlob.FileName);
            File.WriteAllBytes(dest, playerBlob);
            CreatedZoneLoginBlob.ClearCache();
            return dest;
        }

        public static bool TryDecompressZlib(byte[] compressed, out byte[] decompressed)
        {
            decompressed = null;
            if (compressed == null || compressed.Length == 0)
                return false;

            try
            {
                using var input = new MemoryStream(compressed);
                using var zlib = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                zlib.CopyTo(output);
                decompressed = output.ToArray();
                return decompressed.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
