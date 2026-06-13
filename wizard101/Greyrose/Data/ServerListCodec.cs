using System.Collections.Generic;

namespace Greyrose.Data
{
    static class ServerListCodec
    {
        public const string DefaultServerName = "Greyrose";

        /// <summary>
        /// MSG_SERVERLIST (svc 7, msg 11): two KIP frames.
        /// 1) Full _ServerList DML table stream (one selectable realm)
        /// 2) 4-byte end marker (record count 0)
        /// </summary>
        public static byte[] BuildServerListResponse()
        {
            var packets = new List<byte>();

            packets.AddRange(KipFrameBuilder.BuildApplicationMessage(
                7, 11, DmlServerListBuilder.BuildRowTable(DefaultServerName)));

            packets.AddRange(KipFrameBuilder.BuildApplicationMessage(
                7, 11, DmlServerListBuilder.BuildEndMarker()));

            return packets.ToArray();
        }

        public static bool IsKnownServerName(string serverName)
        {
            if (string.IsNullOrEmpty(serverName))
                return true;
            return string.Equals(serverName, DefaultServerName, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(serverName, "Greyrose.Login", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
