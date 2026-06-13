using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Threading;

using Greyrose.Data;

namespace Greyrose
{
    partial class Server
    {
        private static class LS_Settings
        {
            public static int TCPPort = 12000;
            public static int UDPPort = 0;
            public static string Key = "but most of all, 11a10318 is my hero";
            public static IPAddress IP = IPAddress.Parse("0.0.0.0");
            public static ushort SessionID = 4513;
            public static List<ClientSession> Sessions = new List<ClientSession>();
        }

        public static async Task LS()
        {
            TcpListener server = null;
            try
            {
                server = new TcpListener(LS_Settings.IP, LS_Settings.TCPPort);
                server.Start();
                RegisterListener("LS", server);
                ServerLog.Write("Login: Waiting for a connection...\n");

                while (true)
                {
                    TcpClient client = await server.AcceptTcpClientAsync();
                    var session = new ClientSession(client,
                        LS_Settings.TCPPort, LS_Settings.UDPPort,
                        LS_Settings.Key, "Greyrose.Login", LS_Settings.SessionID);
                    lock (LS_Settings.Sessions)
                        LS_Settings.Sessions.Add(session);
                    ServerLog.WriteLine("Login: Connected! ({0} active)", LS_Settings.Sessions.Count);

                    _ = Task.Run(() => HandleLSClient(session));
                }
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.OperationAborted
                                         || e.SocketErrorCode == SocketError.Interrupted)
            {
            }
            catch (SocketException e)
            {
                ServerLog.WriteLine("Login: SocketException: {0}", e.Message);
            }
            finally
            {
                ServerLog.WriteLine("\n\n\n====================\nLogin: SERVER STOP\n====================\n\n\n");
                server?.Stop();
            }

            return;
        }

        static void HandleLSClient(ClientSession session)
        {
            try
            {
                session.Send(Handlers.BuildSessionOffer(session.SessionID));
                ServerLog.WriteLine("Login: SESSION OFFER sent (session {0})", session.SessionID);

                byte[] bytes = new byte[4096];
                int i;

                session.Stream.ReadTimeout = System.Threading.Timeout.Infinite;

                while ((i = session.Read(bytes)) != 0)
                {
                    try
                    {
                        Tuple<byte[], int, int> result = MessageParser.Parse(bytes, i, session);
                        if (result == null)
                            continue;

                        if (result.Item1 != null)
                            session.Send(result.Item1);

                        if (result.Item2 == 1)
                        {
                            ServerLog.WriteLine("PLAY FLOW: sending full CHARACTERSELECTED handoff to game server.");
                            SendCharacterSelected(session, DefaultGameData.DefaultLoginServer);
                        }

                        if (result.Item3 == 1)
                            session.State = ConnectionState.SESSION_ESTABLISHED;
                    }
                    catch (Exception ex)
                    {
                        var dump = new StringBuilder();
                        for (int q = 0; q < i; q++)
                            dump.Append(bytes[q].ToString("X2"));
                        ServerLog.WriteLine("Login: Parse error ({0}): {1}", ex.GetType().Name, ex.Message);
                        ServerLog.WriteLine("Login: Packet ({0} bytes): {1}", i, dump);
                    }
                }
            }
            catch (System.IO.IOException)
            {
                ServerLog.WriteLine("Login: Client disconnected\n");
            }
            catch (System.Exception ex)
            {
                ServerLog.WriteLine("Login: Client error: {0}\n", ex.Message);
            }
            finally
            {
                session.Disconnect();
                lock (LS_Settings.Sessions)
                    LS_Settings.Sessions.Remove(session);
                ServerLog.WriteLine("Login: Client removed ({0} active)", LS_Settings.Sessions.Count);
            }
        }

        static void SendCharacterSelected(ClientSession session, string loginServer)
        {
            CharacterRecord character = null;
            AccountRecord account = null;

            if (session.SelectedCharacterId.HasValue)
                character = DataStore.GetCharacter(session.SelectedCharacterId.Value);
            if (character == null)
                character = DataStore.GetDefaultCharacter();
            if (character != null)
                account = DataStore.GetAccountById(character.AccountId);
            if (account == null)
                account = DataStore.GetDefaultAccount();

            if (character == null || account == null)
            {
                ServerLog.WriteLine("SendCharacterSelected: no character in database.");
                return;
            }

            KIPacket p = new KIPacket();
            p.Header(0x00, 0x00, 0x07, 0x03);
            p._STR("127.0.0.1");
            p._INT(GS_Settings.TCPPort);
            p._INT(GS_Settings.UDPPort);
            p._STR(GS_Settings.Key);
            p._GID((ulong)account.UserGid);
            p._GID((ulong)character.CharGid);
            p._GID((ulong)character.ZoneGid);
            p._STR(character.ZoneName);
            p._STR(character.Location);
            p._INT(character.Slot);
            p._INT(0);
            p._INT(0);
            p._STR(loginServer);
            GameHandoff.Register(character.CharGid, character.Id, account.UserGid);
            session.Send(p.Finalise());
            ServerLog.WriteLine("PLAY FLOW: CHARACTERSELECTED handoff sent — {0}:{1}, char GID {2}.",
                "127.0.0.1", GS_Settings.TCPPort, character.CharGid);
        }
    }
}
