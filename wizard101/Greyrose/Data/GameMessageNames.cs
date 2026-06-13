using System.Collections.Generic;

namespace Greyrose.Data
{
    static class GameMessageNames
    {
        static readonly Dictionary<uint, string> Known = new Dictionary<uint, string>
        {
            { 2, "MSG_CHARACTERLIST" },
            { 7, "MSG_ATTACH" },
            { 36, "MSG_CLIENTMOVE" },
            { 37, "MSG_CLIENTMOVESTATE" },
            { 40, "MSG_CLIENT_DISCONNECT" },
            { 100, "MSG_JUMP" },
            { 108, "MSG_LOGINCOMPLETE" },
            { 110, "MSG_MARK_LOCATION" },
            { 111, "MSG_MARK_LOCATION_RESPONSE" },
            { 171, "MSG_RECALL" },
            { 220, "MSG_SERVERTELEPORT" },
        };

        public static string GetName(uint msgid)
        {
            if (Known.TryGetValue(msgid, out string name))
                return name;
            return "msgid=" + msgid;
        }
    }
}
