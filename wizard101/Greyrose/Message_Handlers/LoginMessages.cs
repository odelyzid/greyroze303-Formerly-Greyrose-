using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Greyrose.Data;

namespace Greyrose
{
    partial class Handlers
    {
        static readonly HashSet<uint> LoginNoResponseExpected = new HashSet<uint>
        {
            1, 2, 3, 5, 7, 11, 12, 14, 16, 17, 18, 19, 20, 21, 24, 25, 26
        };

        public static Tuple<byte[],int,int> _7LoginMessages(BinaryReader data, ClientSession session)
        {
            int TCPPort = session.TCPPort;
            int UDPPort = session.UDPPort;
            string Key = session.Key;
            uint msgid = DataHandler.MSGID(data);
            uint msglen = DataHandler.USHRT(data);


            if (msgid == 1)
            {
                ServerLog.WriteLine("MSG_CHARACTERINFO");
            }
            else if (msgid == 2)
            {
                ServerLog.WriteLine("MSG_CHARACTERLIST");
            }
            else if (msgid == 3)
            {
                ServerLog.WriteLine("MSG_CHARACTERSELECTED");

            }
            else if (msgid == 4)
            {
                ServerLog.WriteLine("MSG_CREATECHARACTER");
                return HandleCreateCharacter(data, session);
            }
            else if (msgid == 5)
            {
                ServerLog.WriteLine("MSG_CREATECHARACTERRESPONSE");
            }
            else if (msgid == 6)
            {
                ServerLog.WriteLine("MSG_DELETECHARACTER");
                return HandleDeleteCharacter(data, session);
            }
            else if (msgid == 7)
            {
                ServerLog.WriteLine("MSG_DELETECHARACTERRESPONSE");
            }
            else if (msgid == 8)
            {
                ServerLog.WriteLine("MSG_REQUESTCHARACTERLIST");
                ServerLog.WriteLine("PLAY FLOW: character list requested (serverListSent={0}, userAdmitted={1}).",
                    session.ServerListSent, session.UserAdmitted);
                return Tuple.Create(BuildCharacterListResponseWithPlaySetup(session), 0, 0);
            }
            else if (msgid == 9)
            {
                ServerLog.WriteLine("MSG_REQUESTSERVERLIST");
                ServerLog.WriteLine("PLAY FLOW: server list requested.");
                return HandleRequestServerList(session);
            }
            else if (msgid == 10)
            {
                ServerLog.WriteLine("MSG_SELECTCHARACTER");
                ServerLog.WriteLine("PLAY FLOW: select character requested.");

                long charGid = (long)DataHandler.GID(data);
                string serverName = DataHandler.STR(data);
                if (string.IsNullOrEmpty(serverName))
                    serverName = ServerListCodec.DefaultServerName;
                ServerLog.WriteLine("PLAY FLOW: char GID={0}, server='{1}'", charGid, serverName);

                if (!ServerListCodec.IsKnownServerName(serverName))
                    ServerLog.WriteLine("PLAY FLOW: warning — server '{0}' does not match default '{1}'.",
                        serverName, ServerListCodec.DefaultServerName);

                AccountRecord account = ResolveAccount(session);
                CharacterRecord character = null;
                if (account != null)
                {
                    foreach (var ch in DataStore.GetCharactersByAccountId(account.Id))
                    {
                        if (ch.CharGid == charGid)
                        {
                            character = ch;
                            break;
                        }
                    }
                }
                if (character == null)
                    character = DataStore.GetDefaultCharacter();
                if (character != null)
                    session.SelectedCharacterId = character.Id;

                KIPacket TempResponse = new KIPacket();
                TempResponse.Header(0x00, 0x00, 0x07, 0x03); //Create header with SVCID 7 and MSGID 3 (MSG_CHARACTERSELECTED)
                TempResponse._STR(""); //IP
                TempResponse._INT(0); //TCPPORT
                TempResponse._INT(0); //UDPPORT
                TempResponse._STR(""); //KEY
                TempResponse._GID(0); //UserID
                TempResponse._GID(0); //CharID
                TempResponse._GID(0); //ZoneID
                TempResponse._STR(""); //ZoneName
                TempResponse._STR(""); //Location
                TempResponse._INT(0); //Slot
                TempResponse._INT(1); //PrepPhase
                TempResponse._INT(0); //Error
                TempResponse._STR(DefaultGameData.DefaultLoginServer); //LoginServer
                byte[] packet1 = TempResponse.Finalise();

                ServerLog.WriteLine("PLAY FLOW: sending prep-phase CHARACTERSELECTED for char id {0}.",
                    session.SelectedCharacterId);
                return Tuple.Create(packet1,1,0);
            }
            else if (msgid == 11)
            {
                ServerLog.WriteLine("MSG_SERVERLIST");
            }
            else if (msgid == 12)
            {
                ServerLog.WriteLine("MSG_STARTCHARACTERLIST");
            }
            else if (msgid == 13)
            {
                ServerLog.WriteLine("MSG_USER_AUTHEN");
                return HandleUserAuthenRequest(data, session, hasLocale: false, hasSteamPatcher: false);
            }
            else if (msgid == 14)
            {
                ServerLog.WriteLine("MSG_USER_AUTHEN_RSP");
            }
            else if (msgid == 15)
            {
                ServerLog.WriteLine("MSG_USER_VALIDATE");
                ServerLog.WriteLine("PLAY FLOW: validate requested.");

                UInt64 UserID = DataHandler.GID(data);
                string PassKey3 = DataHandler.STR(data);
                UInt64 MachineID = DataHandler.GID(data);
                string Locale = DataHandler.STR(data);
                string PatchClientID = DataHandler.STR(data);

                ServerLog.WriteLine("PLAY FLOW: validate UserID={0}, Locale={1}", UserID, Locale);
                ServerLog.WriteLine("PassKey3: {0}", PassKey3);
                ServerLog.WriteLine("Machine ID: {0}", MachineID);
                ServerLog.WriteLine("Patch Client ID: {0}", PatchClientID);

                session.AccountUserGid = (long)UserID;
                var byValidateGid = DataStore.GetAccountByUserGid((long)UserID);
                if (byValidateGid != null)
                    ServerLog.WriteLine("PLAY FLOW: validate matched account '{0}'.", byValidateGid.Username);
                return HandleUserValidate(session);
            }
            else if (msgid == 16)
            {
                ServerLog.WriteLine("MSG_USER_VALIDATE_RSP");
            }
            else if (msgid == 17)
            {
                ServerLog.WriteLine("MSG_DISCONNECT_LOGIN_AFK");
            }
            else if (msgid == 18)
            {
                ServerLog.WriteLine("MSG_LOGIN_NOT_AFK");
                uint badgeNameId = DataHandler.UINT(data);
                ServerLog.WriteLine("PLAY FLOW: login not AFK (badge={0})", badgeNameId);
                return null;
            }
            else if (msgid == 19)
            {
                ServerLog.WriteLine("MSG_LOGINSERVERSHUTDOWN");
            }
            else if (msgid == 20)
            {
                ServerLog.WriteLine("MSG_USER_ADMIT_IND");
            }
            else if (msgid == 21)
            {
                ServerLog.WriteLine("MSG_WEBCHARACTERINFO");
            }
            else if (msgid == 22)
            {
                ServerLog.WriteLine("MSG_USER_AUTHEN_V2");
                return HandleUserAuthenRequest(data, session, hasLocale: true, hasSteamPatcher: false);
            }
            else if (msgid == 23)
            {
                ServerLog.WriteLine("MSG_SAVECHARACTER");
                return HandleSaveCharacter(data, session);
            }
            else if (msgid == 24)
            {
                ServerLog.WriteLine("MSG_WEB_AUTHEN");
            }
            else if (msgid == 25)
            {
                ServerLog.WriteLine("MSG_WEB_VALIDATE");
            }
            else if (msgid == 26)
            {
                ServerLog.WriteLine("MSG_CHANGECHARACTERNAME");
            }
            else if (msgid == 27)
            {
                ServerLog.WriteLine("MSG_USER_AUTHEN_V3");
                return HandleUserAuthenRequest(data, session, hasLocale: true, hasSteamPatcher: true);
            }
            else
            {
                ServerLog.WriteLine("UNSUPPORTED MESSAGE! Make sure you are running revision r667549.Wizard_1_390");
            }

            if (!LoginNoResponseExpected.Contains(msgid))
                ServerLog.WriteLine("LOGIN: no response for msgid {0}", msgid);
            return null;
        }

        static Tuple<byte[], int, int> HandleRequestServerList(ClientSession session)
        {
            session.ServerListSent = true;
            byte[] packets = ServerListCodec.BuildServerListResponse();
            ServerLog.WriteLine("SERVER LIST SENT! ({0} bytes, realm={1})",
                packets.Length, ServerListCodec.DefaultServerName);
            LogServerListHexPrefix(packets);
            return Tuple.Create(packets, 0, 0);
        }

        static void LogServerListHexPrefix(byte[] packets)
        {
            if (packets == null || packets.Length == 0)
                return;
            int dumpLen = Math.Min(64, packets.Length);
            var sb = new StringBuilder();
            for (int i = 0; i < dumpLen; i++)
                sb.Append(packets[i].ToString("X2")).Append(' ');
            ServerLog.WriteLine("PLAY FLOW: server list prefix hex: {0}", sb.ToString().TrimEnd());
        }

        static byte[] BuildUserAdmitInd()
        {
            var admit = new KIPacket();
            admit.Header(0, 0, 7, 20);
            admit._INT(1);
            admit._UINT(0);
            return admit.Finalise();
        }

        static byte[] BuildServerListIfNeeded(ClientSession session)
        {
            if (session.ServerListSent)
                return Array.Empty<byte>();

            session.ServerListSent = true;
            byte[] packets = ServerListCodec.BuildServerListResponse();
            ServerLog.WriteLine("PLAY FLOW: proactive server list bundled with character list ({0} bytes).",
                packets.Length);
            LogServerListHexPrefix(packets);
            return packets;
        }

        static byte[] BuildProactiveAdmitIfNeeded(ClientSession session)
        {
            if (session.UserAdmitted)
                return Array.Empty<byte>();

            session.UserAdmitted = true;
            ServerLog.WriteLine("PLAY FLOW: proactive admit sent after character list.");
            return BuildUserAdmitInd();
        }

        static byte[] BuildCharacterListResponseWithPlaySetup(ClientSession session)
        {
            var parts = new List<byte>();
            parts.AddRange(BuildServerListIfNeeded(session));
            parts.AddRange(BuildCharacterListResponse(session));
            parts.AddRange(BuildProactiveAdmitIfNeeded(session));
            return parts.ToArray();
        }

        static Tuple<byte[], int, int> HandleUserValidate(ClientSession session)
        {
            AccountRecord account = ResolveAccount(session);
            if (account == null)
            {
                ServerLog.WriteLine("PLAY FLOW: validate failed — no account.");
                return Tuple.Create(BuildUserValidateResponse(0, "", 1, "No account configured.", 0), 0, 0);
            }

            session.UserAdmitted = true;
            ServerLog.WriteLine("PLAY FLOW: validate success for user GID {0} ({1}).", account.UserGid, account.Username);
            byte[] packets = BuildUserValidateResponses(account);
            return Tuple.Create(packets, 0, 0);
        }

        static byte[] BuildUserValidateResponses(AccountRecord account)
        {
            var validate = new KIPacket();
            validate.Header(0, 0, 7, 16);
            validate._INT(0);
            validate._STR("");
            validate._GID((ulong)account.UserGid);
            validate._STR("");
            validate._INT(1);
            validate._INT(0);

            return validate.Finalise().Concat(BuildUserAdmitInd()).ToArray();
        }

        static byte[] BuildUserValidateResponse(long userGid, string rec1, int error, string reason, int payingUser)
        {
            var response = new KIPacket();
            response.Header(0, 0, 7, 16);
            response._INT(error);
            response._STR(reason ?? "");
            response._GID((ulong)userGid);
            response._STR(rec1 ?? "");
            response._INT(payingUser);
            response._INT(0);
            return response.Finalise();
        }

        static Tuple<byte[], int, int> HandleDeleteCharacter(BinaryReader data, ClientSession session)
        {
            long charGid = (long)DataHandler.GID(data);
            ServerLog.WriteLine("DELETECHARACTER: GID={0}", charGid);

            AccountRecord account = ResolveAccount(session);
            if (account != null)
            {
                foreach (var ch in DataStore.GetCharactersByAccountId(account.Id))
                {
                    if (ch.CharGid == charGid)
                    {
                        DataStore.DeletePlayerState(ch.Id);
                        DataStore.DeleteCharacter(ch.Id);
                        ServerLog.WriteLine("DELETECHARACTER: removed '{0}' from database.", ch.Name);
                        break;
                    }
                }
            }

            var response = new KIPacket();
            response.Header(0, 0, 7, 7);
            response._INT(0);
            return Tuple.Create(response.Finalise(), 0, 0);
        }

        static Tuple<byte[], int, int> HandleSaveCharacter(BinaryReader data, ClientSession session)
        {
            string characterInfo = DataHandler.STR(data);
            byte[] blob = CharacterInfoCodec.Latin1Bytes(characterInfo);
            ServerLog.WriteLine("SAVECHARACTER: {0} bytes", blob.Length);

            if (blob.Length >= 24 && session.SelectedCharacterId.HasValue)
            {
                var character = DataStore.GetCharacter(session.SelectedCharacterId.Value);
                if (character != null)
                {
                    character.CharacterInfoHex = CharacterInfoCodec.BytesToHex(blob);
                    string name = CharacterInfoCodec.TryExtractName(blob);
                    if (!string.IsNullOrEmpty(name))
                        character.Name = name;
                    DataStore.UpdateCharacter(character);
                    ServerLog.WriteLine("SAVECHARACTER: updated character id {0}.", character.Id);
                }
            }

            return null;
        }

        static Tuple<byte[], int, int> HandleUserAuthenRequest(BinaryReader data, ClientSession session, bool hasLocale, bool hasSteamPatcher)
        {
            DataStore.EnsureSeeded();

            string rec1 = DataHandler.STR(data);
            string version = DataHandler.STR(data);
            string revision = DataHandler.STR(data);
            string dataRevision = DataHandler.STR(data);
            string crc = DataHandler.STR(data);
            ulong machineId = DataHandler.GID(data);
            string locale = hasLocale ? DataHandler.STR(data) : "";
            string patchClientId = DataHandler.STR(data);
            uint isSteamPatcher = hasSteamPatcher ? DataHandler.UINT(data) : 0;

            byte[] rec1Bytes = Encoding.Latin1.GetBytes(rec1);
            string rec1Hex = rec1Bytes.Length > 16
                ? BitConverter.ToString(rec1Bytes, 0, 16) + "..."
                : BitConverter.ToString(rec1Bytes);
            ServerLog.WriteLine("Rec1: {0} bytes ({1})", rec1Bytes.Length, rec1Hex);
            ServerLog.WriteLine("Version: {0}", version);
            ServerLog.WriteLine("Revision: {0}", revision);
            ServerLog.WriteLine("Machine ID: {0}", machineId);
            if (hasLocale)
                ServerLog.WriteLine("Locale: {0}", locale);
            ServerLog.WriteLine("Patch Client ID: {0}", patchClientId);
            if (hasSteamPatcher)
                ServerLog.WriteLine("Is Steam Patcher: {0}", isSteamPatcher);

            AccountRecord account = DataStore.ResolveAuthenAccount(session);
            if (account == null)
            {
                ServerLog.WriteLine("AUTHEN failed: no account in database.");
                return Tuple.Create(BuildUserAuthenResponse(0, "", 1, "No account configured."), 0, 0);
            }

            session.AccountUserGid = account.UserGid;
            ServerLog.WriteLine("AUTHEN success for user GID {0} ({1}).", account.UserGid, account.Username);
            ServerLog.WriteLine("PLAY FLOW: after auth — serverListSent={0}, userAdmitted={1}.",
                session.ServerListSent, session.UserAdmitted);
            return Tuple.Create(BuildUserAuthenResponse(account.UserGid, account.Username), 0, 0);
        }

        static byte[] BuildUserAuthenResponse(long userGid, string rec1, int error = 0, string reason = "")
        {
            var response = new KIPacket();
            response.Header(0, 0, 7, 14);
            response._INT(error);
            response._GID((ulong)userGid);
            response._STR(rec1 ?? "");
            response._STR(reason ?? "");
            response._STR("");
            response._INT(error == 0 ? 1 : 0);
            response._INT(0);
            return response.Finalise();
        }

        static AccountRecord ResolveAccount(ClientSession session)
        {
            if (session.AccountUserGid.HasValue)
            {
                var byGid = DataStore.GetAccountByUserGid(session.AccountUserGid.Value);
                if (byGid != null)
                    return byGid;
            }
            return DataStore.GetDefaultAccount();
        }

        static byte[] BuildCharacterListResponse(ClientSession session)
        {
            DataStore.EnsureSeeded();

            AccountRecord account = ResolveAccount(session);
            if (account == null)
            {
                ServerLog.WriteLine("CHARACTER LIST: no account in database.");
                return BuildCharacterListPackets(DefaultGameData.DefaultLoginServer, 0,
                    CharacterInfoCodec.HexToBytes(DefaultGameData.DefaultCharacterInfoHex));
            }

            var characterBlobs = new List<byte[]>();
            foreach (var ch in DataStore.GetCharactersByAccountId(account.Id))
            {
                if (string.IsNullOrWhiteSpace(ch.CharacterInfoHex) && string.IsNullOrWhiteSpace(ch.Name))
                    continue;

                byte[] clientBlob = CharacterInfoCodec.PrepareForClient(ch);
                characterBlobs.Add(clientBlob);

                ServerLog.WriteLine("CHARACTER LIST: slot {0} name='{1}' blob={2} bytes",
                    ch.Slot, ch.Name, clientBlob.Length);
            }

            if (characterBlobs.Count == 0)
            {
                ServerLog.WriteLine("CHARACTER LIST: account '{0}' has no character hex; using default Ravenwood.", account.Username);
                characterBlobs.Add(CharacterInfoCodec.PrepareForClient(new CharacterRecord
                {
                    CharGid = DefaultGameData.DefaultCharGid,
                    Name = DefaultGameData.DefaultCharacterName,
                    CharacterInfoHex = DefaultGameData.DefaultCharacterInfoHex
                }));
            }

            ServerLog.WriteLine("CHARACTER LIST SENT! ({0} characters)", characterBlobs.Count);
            ServerLog.WriteLine("PLAY FLOW: after character list — serverListSent={0}, userAdmitted={1}.",
                session.ServerListSent, session.UserAdmitted);
            return BuildCharacterListPackets(DefaultGameData.DefaultLoginServer, account.PurchasedSlots, characterBlobs.ToArray());
        }

        static Tuple<byte[], int, int> HandleCreateCharacter(BinaryReader data, ClientSession session)
        {
            DataStore.EnsureSeeded();

            string creationInfo = DataHandler.STR(data);
            byte[] blob = CharacterInfoCodec.Latin1Bytes(creationInfo);
            if (blob.Length < 24)
            {
                ServerLog.WriteLine("CREATECHARACTER rejected: CreationInfo too short ({0} bytes).", blob.Length);
                return Tuple.Create(BuildCreateCharacterResponse(1), 0, 0);
            }

            AccountRecord account = ResolveAccount(session);
            if (account == null)
            {
                ServerLog.WriteLine("CREATECHARACTER rejected: no account.");
                return Tuple.Create(BuildCreateCharacterResponse(1), 0, 0);
            }

            var existing = DataStore.GetCharactersByAccountId(account.Id);
            int maxCharacters = Math.Max(1, 1 + account.PurchasedSlots);
            if (existing.Count >= maxCharacters)
            {
                ServerLog.WriteLine("CREATECHARACTER rejected: slot limit ({0}/{1}).", existing.Count, maxCharacters);
                return Tuple.Create(BuildCreateCharacterResponse(2), 0, 0);
            }

            long charGid = CharacterInfoCodec.TryExtractCharGid(blob) ?? 0;
            if (charGid == 0)
            {
                ServerLog.WriteLine("CREATECHARACTER rejected: no GID found in creation blob ({0} bytes).", blob.Length);
                return Tuple.Create(BuildCreateCharacterResponse(1), 0, 0);
            }
            if (existing.Any(c => c.CharGid == charGid))
            {
                ServerLog.WriteLine("CREATECHARACTER rejected: duplicate GID {0}.", charGid);
                return Tuple.Create(BuildCreateCharacterResponse(1), 0, 0);
            }

            string name = CharacterInfoCodec.TryExtractName(blob) ?? DefaultGameData.DefaultCharacterName;
            int slot = CharacterInfoCodec.FindNextSlot(existing);
            string hex = CharacterInfoCodec.BytesToHex(blob);

            ServerLog.WriteLine("CREATECHARACTER blob prefix: {0}",
                hex.Length > 48 ? hex.Substring(0, 48) + "..." : hex);

            long charId = DataStore.InsertCharacter(new CharacterRecord
            {
                AccountId = account.Id,
                CharGid = charGid,
                Name = name,
                Slot = slot,
                ZoneName = DefaultGameData.DefaultZoneName,
                ZoneGid = DefaultGameData.DefaultZoneGid,
                Location = DefaultGameData.DefaultLocation,
                CharacterInfoHex = hex
            });

            var newCharacter = new CharacterRecord
            {
                CharGid = charGid,
                ZoneGid = DefaultGameData.DefaultZoneGid,
                CharacterInfoHex = hex
            };
            byte[] loginBlob = LoginBlobBuilder.BuildLoginBlob(newCharacter, null, null);
            string loginBlobHex = CharacterInfoCodec.BytesToHex(loginBlob);

            DataStore.SavePlayerState(new PlayerStateRecord
            {
                CharacterId = charId,
                X = 2572,
                Y = 4376,
                Z = -28,
                Rot = 5.55f,
                LoginBlobHex = loginBlobHex,
                ZoneBlobHex = DefaultZoneBlob.GetHex()
            });

            ServerLog.WriteLine("CREATECHARACTER success: name='{0}', slot={1}, GID={2}, blob={3} bytes",
                name, slot, charGid, blob.Length);

            var packets = BuildCreateCharacterResponse(0)
                .Concat(BuildCharacterListResponseWithPlaySetup(session))
                .ToArray();
            return Tuple.Create(packets, 0, 0);
        }

        static byte[] BuildCreateCharacterResponse(int errorCode)
        {
            var response = new KIPacket();
            response.Header(0, 0, 7, 5);
            response._INT(errorCode);
            return response.Finalise();
        }

        static byte[] BuildCharacterListPackets(string loginServer, int purchasedSlots, params byte[][] characterBlobs)
        {
            var packets = new List<byte>();

            var start = new KIPacket();
            start.Header(0, 0, 7, 12);
            start._STR(loginServer);
            start._INT(purchasedSlots);
            packets.AddRange(start.Finalise());

            foreach (byte[] blob in characterBlobs)
            {
                var info = new KIPacket();
                info.Header(0, 0, 7, 1);
                // CharacterInfo is TYPE="STR" in LoginMessages.xml — length-prefixed Latin1 bytes.
                info._BINSTR(blob);
                packets.AddRange(info.Finalise());
            }

            var end = new KIPacket();
            end.Header(0, 0, 7, 2);
            end._UINT(0);
            packets.AddRange(end.Finalise());

            return packets.ToArray();
        }
    }
}
