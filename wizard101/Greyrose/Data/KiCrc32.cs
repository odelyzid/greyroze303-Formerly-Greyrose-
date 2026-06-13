namespace Greyrose.Data
{
    /// <summary>KingsIsle patch CRC-32/IEEE (init 0, no final XOR).</summary>
    static class KiCrc32
    {
        public static uint Compute(byte[] data)
        {
            uint crc = 0;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
            return crc;
        }
    }
}
