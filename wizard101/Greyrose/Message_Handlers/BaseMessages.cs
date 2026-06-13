using System.IO;

namespace Greyrose
{
    partial class Handlers
    {
        public static byte[] _1BaseMessages(BinaryReader data, bool SessionOpen, ushort SessionID)
        {
            uint msgid = DataHandler.MSGID(data);
            uint msglen = DataHandler.USHRT(data);

            if (msgid == 1)
            {
                ServerLog.WriteLine("MSG: Ping");

                if (!SessionOpen)
                    return BuildSessionOffer(SessionID);

                KIPacket resp = new KIPacket();
                resp._UBYT(0x0D);
                resp._UBYT(0xF0);
                resp._USHRT(09);
                resp._UBYT(0);
                resp._UBYT(0);
                resp._USHRT(0);
                resp._UBYT(1);
                resp._UBYT(2);
                resp._USHRT(4);

                return resp.RawFinalise();
            }
            else if (msgid == 2)
            {
                ServerLog.WriteLine("MSG: Ping response");
            }

            return null;
        }
    }
}
