using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace Greyrose
{
    partial class Server
    {
        private static class PS_Settings
        {
            public static int TCPPort = 12500;
            public static int UDPPort = 0;
            public static string Key = "but most of all, 11a10318 is my hero";
            public static IPAddress IP = IPAddress.Parse("0.0.0.0");
            public static ushort SessionID = 4513;
            public static List<ClientSession> Sessions = new List<ClientSession>();
        }

        public static async Task PS()
        {
            TcpListener server = null;
            try
            {
                server = new TcpListener(PS_Settings.IP, PS_Settings.TCPPort);
                server.Start();
                RegisterListener("PS", server);
                ServerLog.Write("Patch: Waiting for a connection...\n");

                while (true)
                {
                    TcpClient client = await server.AcceptTcpClientAsync();
                    var session = new ClientSession(client,
                        PS_Settings.TCPPort, PS_Settings.UDPPort,
                        PS_Settings.Key, "Greyrose.Patch", PS_Settings.SessionID);
                    lock (PS_Settings.Sessions)
                        PS_Settings.Sessions.Add(session);
                    ServerLog.WriteLine("Patch: Connected! ({0} active)", PS_Settings.Sessions.Count);

                    _ = Task.Run(() => HandlePSClient(session));
                }
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.OperationAborted
                                         || e.SocketErrorCode == SocketError.Interrupted)
            {
            }
            catch (SocketException e)
            {
                ServerLog.WriteLine("Patch: SocketException: {0}", e.Message);
            }
            finally
            {
                ServerLog.WriteLine("Patch: SERVER STOP\n");
                server?.Stop();
            }

            return;
        }

        static void HandlePSClient(ClientSession session)
        {
            try
            {
                session.Send(Handlers.BuildSessionOffer(session.SessionID));
                ServerLog.WriteLine("Patch: SESSION OFFER sent (session {0})", session.SessionID);

                byte[] bytes = new byte[4096];
                int i;

                while ((i = session.Read(bytes)) != 0)
                {
                    try
                    {
                        Tuple<byte[], int, int> result = MessageParser.Parse(bytes, i, session);
                        if (result == null)
                            continue;

                        if (result.Item1 != null)
                        {
                            try
                            {
                                session.Send(result.Item1);
                            }
                            catch (Exception sendEx)
                            {
                                ServerLog.WriteLine("Patch: Send error ({0}): {1}",
                                    sendEx.GetType().Name, sendEx.Message);
                            }
                        }

                        if (result.Item3 == 1)
                            session.State = ConnectionState.SESSION_ESTABLISHED;
                    }
                    catch (Exception ex)
                    {
                        var dump = new System.Text.StringBuilder();
                        for (int q = 0; q < i; q++)
                            dump.Append(bytes[q].ToString("X2"));
                        ServerLog.WriteLine("Patch: Parse error ({0}): {1}", ex.GetType().Name, ex.Message);
                        ServerLog.WriteLine("Patch: Packet ({0} bytes): {1}", i, dump);
                    }
                }
            }
            catch
            {
                ServerLog.WriteLine("Patch: Client disconnected\n");
            }
            finally
            {
                session.Disconnect();
                lock (PS_Settings.Sessions)
                    PS_Settings.Sessions.Remove(session);
                ServerLog.WriteLine("Patch: Client removed ({0} active)", PS_Settings.Sessions.Count);
            }
        }


    }
}
