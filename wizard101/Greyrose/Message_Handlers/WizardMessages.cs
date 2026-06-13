using System;
using System.IO;

namespace Greyrose
{
    partial class Handlers
    {
        public static byte[] _12WizardMessages(BinaryReader reader)
        {
            uint msgid = reader.ReadByte();
            ServerLog.WriteLine("Wizard [SVCID 12] MSGID: {0}", msgid);

            switch (msgid)
            {
                default:
                    ServerLog.WriteLine("UNHANDLED Wizard MSGID: {0}", msgid);
                    break;
            }

            return null;
        }
    }
}
