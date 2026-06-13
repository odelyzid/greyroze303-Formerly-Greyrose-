using System.Net.Sockets;

namespace Greyrose
{
    partial class Server
    {
        static TcpListener _lsListener;
        static TcpListener _psListener;
        static TcpListener _gsListener;

        internal static void RegisterListener(string server, TcpListener listener)
        {
            if (server == "LS") _lsListener = listener;
            else if (server == "PS") _psListener = listener;
            else if (server == "GS") _gsListener = listener;
        }

        public static void StopAll()
        {
            StopPatchFileServer();
            try { _lsListener?.Stop(); } catch { }
            try { _psListener?.Stop(); } catch { }
            try { _gsListener?.Stop(); } catch { }
            _lsListener = null;
            _psListener = null;
            _gsListener = null;
        }
    }
}
