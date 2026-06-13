using System;
using System.IO;

namespace Greyrose
{
    partial class Handlers
    {
        public static byte[] _52QuestMessages(BinaryReader reader)
        {
            uint msgid = reader.ReadByte();
            ServerLog.WriteLine("Quest [SVCID 52] MSGID: {0}", msgid);
            ServerLog.ColorTitle("MSG_QUEST");

            return null;
        }
    }
}
