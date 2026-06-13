using System;
using System.IO;

namespace Greyrose
{
    partial class Handlers
    {
        public static byte[] BuildSessionOffer(ushort sessionId)
        {
            uint unixTimestamp = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            KIPacket packet = new KIPacket();
            packet._UBYT(0x0d);
            packet._UBYT(0xf0);
            packet._UBYT(0x13);
            packet._UBYT(0x00);
            packet._UBYT(0x01);
            packet._UBYT(0x00);
            packet._UBYT(0x00);
            packet._UBYT(0x00);
            packet._USHRT(sessionId);
            packet._INT(0);
            packet._UINT(unixTimestamp);
            packet._INT(800);
            return packet.RawFinalise();
        }

        public static Tuple<byte[], int, int> _0ControlMessages(BinaryReader data, int opCode, int packetLength, byte[] bytes, int readCount)
        {
            if (opCode == 0)
            {
                ServerLog.WriteLine("OPCODE: SESSION OFFER (client)");
                UInt16 sessionid = DataHandler.USHRT(data);
                UInt32 undefined = DataHandler.UINT(data);
                Int32 timestamp = DataHandler.INT(data);
                UInt32 milliseconds = DataHandler.UINT(data);
                ServerLog.WriteLine("Session ID: {0}", sessionid);
                ServerLog.WriteLine("undefined: {0}", undefined);
                ServerLog.WriteLine("Timestamp: {0}", timestamp);
                ServerLog.WriteLine("Milliseconds: {0}", milliseconds);
            }
            else if (opCode == 3)
            {
                ServerLog.WriteLine("OPCODE: KEEP ALIVE");
                UInt16 sessionid = DataHandler.USHRT(data);
                UInt16 milliseconds = DataHandler.USHRT(data);
                UInt16 minutes = DataHandler.USHRT(data);
                ServerLog.WriteLine("Sessiond ID: {0}", sessionid);
                ServerLog.WriteLine("Milliseconds: {0}", milliseconds);
                ServerLog.WriteLine("Minutes: {0}", minutes);

                int frameLen = Math.Min(readCount, packetLength + 4);
                byte[] b = new byte[frameLen];
                Array.Copy(bytes, 0, b, 0, frameLen);
                b[5] = 0x04;
                return Tuple.Create(b, 0, 0);
            }
            else if (opCode == 4)
            {
                ServerLog.WriteLine("OPCODE: KEEP ALIVE RESPONSE");
                uint undefined = DataHandler.USHRT(data);
                uint timestamp = DataHandler.UINT(data);
                ServerLog.WriteLine("Unknown: {0}", undefined);
                ServerLog.WriteLine("Timestamp: {0}", timestamp);
            }
            else if (opCode == 5)
            {
                ServerLog.WriteLine("OPCODE: SESSION ACCEPT");
                uint undefined = DataHandler.USHRT(data);
                uint undefined2 = DataHandler.UINT(data);
                uint timestamp = DataHandler.UINT(data);
                uint milliseconds = DataHandler.UINT(data);
                uint sessionId = DataHandler.USHRT(data);
                ServerLog.WriteLine("Unknown1: {0}", undefined);
                ServerLog.WriteLine("Unknown2: {0}", undefined2);
                ServerLog.WriteLine("Timestamp: {0}", timestamp);
                ServerLog.WriteLine("Milliseconds: {0}", milliseconds);
                ServerLog.WriteLine("Session ID: {0}", sessionId);
                ServerLog.WriteLine("Session established.");
                byte[] bytarr = null;
                return Tuple.Create(bytarr, 0, 1);
            }
            else
            {
                ServerLog.WriteLine("UNSUPPORTED OPCODE!");
            }

            return null;
        }
    }
}
