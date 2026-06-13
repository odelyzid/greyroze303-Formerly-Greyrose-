using System;
using System.IO;
using System.IO.Compression;

namespace Greyrose.Data
{
    /// <summary>
    /// Builds the ZoneServerData BINSTR payload for MSG_LOGINCOMPLETE.
    /// </summary>
    static class ZoneLoginPayloadBuilder
    {
        public static byte[] CompressPlayerBlob(byte[] loginBlob)
        {
            if (loginBlob == null || loginBlob.Length == 0)
                loginBlob = Array.Empty<byte>();

            using var outstream = new MemoryStream();
            using (var compressor = new ZLibStream(outstream, CompressionMode.Compress, leaveOpen: true))
            {
                compressor.Write(loginBlob, 0, loginBlob.Length);
                compressor.Flush();
            }

            return outstream.ToArray();
        }

        public static byte[] BuildZoneDataValue(byte[] zoneStatePrefix, byte[] loginBlob)
        {
            zoneStatePrefix ??= Array.Empty<byte>();
            if (zoneStatePrefix.Length < 2)
                throw new ArgumentException("Zone prefix must include a 2-byte length header.", nameof(zoneStatePrefix));

            byte[] compressedPlayerData = CompressPlayerBlob(loginBlob);
            int zoneBodyLength = zoneStatePrefix.Length - 2;
            byte[] zoneDataValue = new byte[zoneBodyLength + 2 + 4 + compressedPlayerData.Length];

            Buffer.BlockCopy(zoneStatePrefix, 2, zoneDataValue, 0, zoneBodyLength);
            Buffer.BlockCopy(
                BitConverter.GetBytes((short)(compressedPlayerData.Length + 6)),
                0,
                zoneDataValue,
                zoneBodyLength,
                2);
            Buffer.BlockCopy(
                BitConverter.GetBytes(loginBlob.Length),
                0,
                zoneDataValue,
                zoneBodyLength + 2,
                4);
            Buffer.BlockCopy(
                compressedPlayerData,
                0,
                zoneDataValue,
                zoneBodyLength + 2 + 4,
                compressedPlayerData.Length);

            return zoneDataValue;
        }
    }
}
