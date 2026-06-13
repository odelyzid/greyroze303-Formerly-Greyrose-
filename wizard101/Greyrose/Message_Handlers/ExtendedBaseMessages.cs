using System;
using System.IO;

namespace Greyrose
{
    partial class Handlers
    {
        public static byte[] _2ExtendedBaseMessages(BinaryReader reader)
        {
            uint msgid = reader.ReadByte();
            ServerLog.WriteLine("ExtendedBase [SVCID 2] MSGID: {0}", msgid);

            switch (msgid)
            {
                case 1: // MSG_RAW_TEXT
                    ServerLog.ColorTitle("MSG_RAW_TEXT");
                    break;
                case 2: // MSG_CUSTOMDICT
                    ServerLog.ColorTitle("MSG_CUSTOMDICT");
                    break;
                case 3: // MSG_CUSTOMRECORD
                    ServerLog.ColorTitle("MSG_CUSTOMRECORD");
                    break;
                case 4: // MSG_RAWRECORD
                    ServerLog.ColorTitle("MSG_RAWRECORD");
                    break;
                case 5: // MSG_SERVERMESSAGE
                    ServerLog.ColorTitle("MSG_SERVERMESSAGE");
                    break;
                case 6: // MSG_FORCE_DISCONNECT
                    ServerLog.ColorTitle("MSG_FORCE_DISCONNECT");
                    break;
                default:
                    ServerLog.WriteLine("UNHANDLED ExtendedBase MSGID: {0}", msgid);
                    break;
            }

            return null;
        }
    }
}
