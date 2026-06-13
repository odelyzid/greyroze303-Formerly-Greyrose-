using System;
using System.Collections.Generic;
using Greyrose.Data;

namespace Greyrose.Networking
{
    public static class GamePacketTrace
    {
        const int MaxLines = 2000;

        static readonly object Sync = new object();
        static readonly Queue<string> Lines = new Queue<string>(MaxLines + 1);

        public static bool Enabled { get; set; } = true;
        public static bool IncludeHexDump { get; set; } = true;
        public static int MaxHexBytes { get; set; } = 512;

        public static event Action<string> OnLine;

        public static IReadOnlyList<string> Snapshot()
        {
            lock (Sync)
                return Lines.ToArray();
        }

        public static void Clear()
        {
            lock (Sync)
                Lines.Clear();
        }

        public static void LogInbound(ClientSession session, byte[] buffer, int length)
        {
            if (!Enabled || buffer == null || length <= 0)
                return;
            Append(FormatLine("RX", session, buffer, length));
        }

        public static void LogOutbound(ClientSession session, byte[] data)
        {
            if (!Enabled || data == null || data.Length == 0)
                return;
            Append(FormatLine("TX", session, data, data.Length));
        }

        static string FormatLine(string direction, ClientSession session, byte[] buffer, int length)
        {
            ushort sid = session?.SessionID ?? 0;
            var frame = KinPacketFrame.TryParse(buffer, length);
            string summary;
            if (!frame.Valid)
                summary = "invalid frame";
            else if (frame.IsApplicationMessage)
            {
                string msgName = frame.SvcId == 5
                    ? GameMessageNames.GetName(frame.MsgId)
                    : "SVCID=" + frame.SvcId;
                summary = string.Format(
                    "SVCID={0} msgid={1} {2} outer={3} inner={4}",
                    frame.SvcId, frame.MsgId, msgName, frame.OuterLength, frame.InnerLength);
            }
            else
                summary = string.Format(
                    "CTRL opCode={0} ({1}) outer={2}",
                    frame.OpCode, KinPacketFrame.FormatControlOpCode(frame.OpCode), frame.OuterLength);

            string line = string.Format(
                "{0:HH:mm:ss.fff} {1} sess={2} len={3} {4}",
                DateTime.Now, direction, sid, length, summary);

            if (IncludeHexDump)
                line += Environment.NewLine + "  " + KinPacketFrame.FormatHex(buffer, length, MaxHexBytes);

            return line;
        }

        static void Append(string line)
        {
            lock (Sync)
            {
                while (Lines.Count >= MaxLines)
                    Lines.Dequeue();
                Lines.Enqueue(line);
            }
            OnLine?.Invoke(line);
        }
    }
}
