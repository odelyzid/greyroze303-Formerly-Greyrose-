using System;
using System.Text;

namespace Greyrose.Networking
{
    public sealed class KinPacketFrame
    {
        public bool Valid;
        public int TotalLength;
        public ushort OuterLength;
        public byte IsControl;
        public byte OpCode;
        public byte SvcId;
        public byte MsgId;
        public ushort InnerLength;
        public bool IsApplicationMessage;

        public static KinPacketFrame TryParse(byte[] buffer, int length)
        {
            var frame = new KinPacketFrame();
            if (buffer == null || length < 8)
                return frame;

            if (buffer[0] != 0x0D || buffer[1] != 0xF0)
                return frame;

            frame.Valid = true;
            frame.TotalLength = length;
            frame.OuterLength = BitConverter.ToUInt16(buffer, 2);
            frame.IsControl = buffer[4];
            frame.OpCode = buffer[5];

            if (length >= 10)
                frame.IsApplicationMessage = frame.IsControl == 0 && frame.OpCode == 0;

            if (frame.IsApplicationMessage && length >= 12)
            {
                frame.SvcId = buffer[8];
                frame.MsgId = buffer[9];
                frame.InnerLength = BitConverter.ToUInt16(buffer, 10);
            }

            return frame;
        }

        public static string FormatControlOpCode(byte opCode)
        {
            switch (opCode)
            {
                case 0: return "SESSION_OFFER";
                case 3: return "KEEP_ALIVE";
                case 4: return "KEEP_ALIVE_RSP";
                case 5: return "SESSION_ACCEPT";
                default: return "CTRL_" + opCode;
            }
        }

        public static string FormatHex(byte[] data, int length, int maxBytes = 512)
        {
            if (data == null || length <= 0)
                return "";

            int take = Math.Min(length, maxBytes);
            var sb = new StringBuilder(take * 3);
            for (int i = 0; i < take; i++)
            {
                if (i > 0)
                    sb.Append(' ');
                sb.Append(data[i].ToString("X2"));
            }
            if (length > maxBytes)
                sb.Append(" ...");
            return sb.ToString();
        }
    }
}
