using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Greyrose.Networking;

namespace Greyrose
{
    partial class Server
    {
        public static class GS_Settings
        {
            public static int TCPPort = 12170;
            public static int UDPPort = 12171;
            public static IPAddress IP = IPAddress.Parse("0.0.0.0");
            public static string Key = "but most of all, 11a10318 is my hero";
            public static ushort SessionID = 792;
            public static List<ClientSession> Sessions = new List<ClientSession>();
        }

        public static async Task GS()
        {
            ServerLog.WriteLine("GAME SERVER STARTED!");
            TcpListener server = null;
            try
            {
                server = new TcpListener(GS_Settings.IP, GS_Settings.TCPPort);
                server.Start();
                RegisterListener("GS", server);
                ServerLog.Write("GS: Waiting for a connection...\n");

                while (true)
                {
                    TcpClient client = await server.AcceptTcpClientAsync();
                    var session = new ClientSession(client,
                        GS_Settings.TCPPort, GS_Settings.UDPPort,
                        GS_Settings.Key, "Greyrose.Game", GS_Settings.SessionID);
                    lock (GS_Settings.Sessions)
                        GS_Settings.Sessions.Add(session);
                    ServerLog.WriteLine("GS: Connected! ({0} active)", GS_Settings.Sessions.Count);

                    _ = Task.Run(() => HandleGSClient(session));
                }
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.OperationAborted
                                         || e.SocketErrorCode == SocketError.Interrupted)
            {
            }
            catch (SocketException e)
            {
                ServerLog.WriteLine("GS: SocketException: {0}", e.Message);
            }
            finally
            {
                ServerLog.WriteLine("\n\n\n====================\nGS: SERVER STOP\n====================\n\n\n");
                server?.Stop();
            }

            return;
        }

        static void HandleGSClient(ClientSession session)
        {
            try
            {
                byte[] sessionOffer = Handlers.BuildSessionOffer(session.SessionID);
                GsSend(session, sessionOffer);
                ServerLog.WriteLine("GS: SESSION OFFER sent (session {0})", session.SessionID);

                byte[] bytes = new byte[4096];
                int i;

                session.Stream.ReadTimeout = System.Threading.Timeout.Infinite;

                while ((i = session.Read(bytes)) != 0)
                {
                    try
                    {
                        GamePacketTrace.LogInbound(session, bytes, i);
                        Tuple<byte[], int, int> result = MessageParser.Parse(bytes, i, session);
                        if (result == null)
                            continue;

                        if (result.Item1 != null)
                            GsSend(session, result.Item1);

                        if (result.Item3 == 1)
                            session.State = ConnectionState.SESSION_ESTABLISHED;
                    }
                    catch (Exception ex)
                    {
                        string dump = null;
                        for (int q = 0; q < i; q++)
                            dump = dump + bytes[q].ToString("X2");
                        ServerLog.WriteLine("GS: Parse error: {0} — {1}\n", ex.Message, dump);
                    }
                }
            }
            catch
            {
                ServerLog.WriteLine("GS: Client disconnected\n");
            }
            finally
            {
                session.Disconnect();
                lock (GS_Settings.Sessions)
                    GS_Settings.Sessions.Remove(session);
                ServerLog.WriteLine("GS: Client removed ({0} active)", GS_Settings.Sessions.Count);
            }
        }

        static void GsSend(ClientSession session, byte[] data)
        {
            GamePacketTrace.LogOutbound(session, data);
            session.Send(data);
        }
    }
}
