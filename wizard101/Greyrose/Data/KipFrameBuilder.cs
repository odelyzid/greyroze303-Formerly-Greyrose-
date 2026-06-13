using System;
using System.IO;

namespace Greyrose.Data
{
    /// <summary>
    /// Builds KingsIsle KINP application messages (0xF00D framing) with exact payload sizes.
    /// </summary>
    static class KipFrameBuilder
    {
        public static byte[] BuildApplicationMessage(byte svcid, byte msgid, byte[] payload)
        {
            if (payload == null)
                payload = Array.Empty<byte>();

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);

            // Start signal + placeholder outer length
            w.Write((ushort)0xF00D);
            w.Write((ushort)0);

            w.Write((byte)0); // control
            w.Write((byte)0); // opcode
            w.Write((ushort)0); // unknown

            w.Write(svcid);
            w.Write(msgid);
            w.Write((ushort)0); // inner length placeholder

            w.Write(payload);
            w.Write((byte)0); // padding

            byte[] packet = ms.ToArray();

            ushort innerLen = (ushort)(packet.Length - 12);
            ushort outerLen = (ushort)(packet.Length - 4);

            packet[2] = (byte)(outerLen & 0xFF);
            packet[3] = (byte)(outerLen >> 8);
            packet[10] = (byte)(innerLen & 0xFF);
            packet[11] = (byte)(innerLen >> 8);

            return packet;
        }
    }
}
