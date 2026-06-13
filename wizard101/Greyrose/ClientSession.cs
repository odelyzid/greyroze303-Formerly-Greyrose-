using System;
using System.Net.Sockets;

namespace Greyrose
{
    public enum ConnectionState
    {
        DISCONNECTED,
        CONNECTED,
        SESSION_ESTABLISHED,
        IN_CHAR_SELECT,
        IN_GAME
    }

    public class ClientSession
    {
        public TcpClient Client { get; private set; }
        public NetworkStream Stream { get; private set; }
        public ConnectionState State { get; set; }
        public ushort SessionID { get; set; }
        public int TCPPort { get; private set; }
        public int UDPPort { get; private set; }
        public string Key { get; private set; }
        public string ServerName { get; set; }
        public DateTime ConnectedAt { get; set; }
        public bool Transfer { get; set; }
        public long? SelectedCharacterId { get; set; }
        public long? AccountUserGid { get; set; }
        public bool ServerListSent { get; set; }
        public bool UserAdmitted { get; set; }

        public ClientSession(TcpClient client, int tcpPort, int udpPort, string key, string serverName, ushort sessionID)
        {
            Client = client;
            Stream = client.GetStream();
            State = ConnectionState.CONNECTED;
            TCPPort = tcpPort;
            UDPPort = udpPort;
            Key = key;
            ServerName = serverName;
            SessionID = sessionID;
            ConnectedAt = DateTime.UtcNow;
            Transfer = false;
        }

        public int Read(byte[] buffer)
        {
            return Stream.Read(buffer, 0, buffer.Length);
        }

        public void Send(byte[] data)
        {
            if (data != null)
                Stream.Write(data, 0, data.Length);
        }

        public void Disconnect()
        {
            State = ConnectionState.DISCONNECTED;
            try { Stream?.Close(); } catch { }
            try { Client?.Close(); } catch { }
        }
    }
}
