using System;
using System.Collections.Generic;
using System.Text;

namespace Greyrose.Data
{
    static class KiBinaryXml
    {
        public const uint BindSignature = 1682852162; // ASCII "BINd" as LE u32

        public static bool IsBindFile(byte[] data)
        {
            return data != null
                && data.Length >= 8
                && BitConverter.ToUInt32(data, 0) == BindSignature;
        }

        public static uint WizHashString(string value)
        {
            byte[] input = Encoding.ASCII.GetBytes(value);
            uint hash = 0;
            if (input.Length == 0)
                return hash;

            int iVar3 = 0;
            int iVar4 = 0;
            for (int i = 0; i < input.Length; i++)
            {
                int cVar2 = input[i] - 32;
                hash ^= (uint)(cVar2 << (iVar3 & 0x1f));
                if (iVar3 > 0x18)
                {
                    hash ^= (uint)(cVar2 >> (iVar4 & 0x1f));
                    if (iVar3 > 0x1a)
                    {
                        iVar3 -= 32;
                        iVar4 += 32;
                    }
                }
                iVar3 += 5;
                iVar4 -= 5;
            }

            if ((int)hash < 0)
                hash = (uint)-((int)hash);
            return hash;
        }

        /// <summary>
        /// Builds a minimal KingsIsle BINd file for an empty LatestFileList (_TableList with zero rows).
        /// Uses DEFAULT serialization options (0).
        /// </summary>
        public static byte[] BuildEmptyLatestFileListBin()
        {
            var body = new KiBitWriter();
            body.WriteUInt32(96); // field size in bits + 64
            body.WriteUInt32(WizHashString("_TableList"));
            body.WriteUInt32(0); // empty vector count

            int bodyBits = body.BitLength;
            int classSizeStored = bodyBits + 32;

            var output = new KiBitWriter();
            output.WriteUInt32(BindSignature);
            output.WriteUInt32(0); // SerializationOptions.DEFAULT
            output.WriteUInt32(WizHashString("LatestFileList"));
            output.WriteUInt32((uint)classSizeStored);
            output.WriteBytes(body.ToByteArray());
            return output.ToByteArray();
        }

        sealed class KiBitWriter
        {
            readonly List<byte> _bytes = new List<byte>();
            int _bitBuffer;
            int _bitCount;

            public int BitLength => _bytes.Count * 8 + _bitCount;

            public void WriteUInt32(uint value)
            {
                WriteBits(value, 32);
            }

            public void WriteBytes(byte[] data)
            {
                FlushBits();
                _bytes.AddRange(data);
            }

            void WriteBits(uint value, int bitCount)
            {
                for (int i = 0; i < bitCount; i++)
                {
                    _bitBuffer |= (int)(((value >> i) & 1) << _bitCount);
                    _bitCount++;
                    if (_bitCount == 8)
                    {
                        _bytes.Add((byte)_bitBuffer);
                        _bitBuffer = 0;
                        _bitCount = 0;
                    }
                }
            }

            void FlushBits()
            {
                if (_bitCount > 0)
                {
                    _bytes.Add((byte)_bitBuffer);
                    _bitBuffer = 0;
                    _bitCount = 0;
                }
            }

            public byte[] ToByteArray()
            {
                FlushBits();
                return _bytes.ToArray();
            }
        }
    }
}
