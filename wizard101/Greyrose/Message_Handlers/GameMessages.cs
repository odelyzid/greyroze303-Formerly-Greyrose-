using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Greyrose.Data;

namespace Greyrose
{
    partial class Handlers
    {
        static long GetCharacterId(ClientSession session) => PlayerData.ResolveCharacterId(session);

        static PlayerData.PlayerStruct LoadPlayer(ClientSession session) => PlayerData.Load(GetCharacterId(session));

        static void SavePlayer(ClientSession session, PlayerData.PlayerStruct player) => PlayerData.Save(GetCharacterId(session), player);

        public static Tuple<byte[], int, int> _5GameMessages(BinaryReader data, ClientSession session)
        {
            uint msgid = DataHandler.MSGID(data);
            uint msglen = DataHandler.USHRT(data);


            if (msgid == 7)
            {
                Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                ServerLog.ColorTitle("MSG_ATTACH");
                GameHandoff.TryApply(session);

                long charId = GetCharacterId(session);
                var character = DataStore.GetCharacter(charId) ?? DataStore.GetDefaultCharacter();
                var state = DataStore.GetPlayerState(charId);
                string zoneName = character?.ZoneName ?? DefaultGameData.DefaultZoneName;
                ulong zoneGid = (ulong)(character?.ZoneGid ?? DefaultGameData.DefaultZoneGid);
                byte[] manualdat = DefaultLoginBlob.GetBytes();
                if (manualdat.Length == 0)
                {
                    ServerLog.WriteLine("GS: DefaultLoginBlob.bin missing - MSG_LOGINCOMPLETE may fail.");
                    manualdat = Array.Empty<byte>();
                }

                var build = LoginBlobBuilder.BuildLoginBlobWithInfo(character, null, manualdat);
                manualdat = build.Blob;
                bool createdCharacter = build.IsCreatedCharacter;
                var blobCheck = LoginBlobBuilder.Validate(manualdat, createdCharacter);
                if (!blobCheck.Ok)
                    manualdat = LoginBlobBuilder.SanitizeForZoneLogin(manualdat, createdCharacter);

                if (state == null && charId > 0)
                    state = new PlayerStateRecord { CharacterId = charId };
                DefaultZoneBlob.EnsurePlayerStateZoneBlob(state);

                byte[] hardcodedZoneBlob = DefaultZoneBlob.GetBytes();
                if (state != null && !string.IsNullOrWhiteSpace(state.ZoneBlobHex))
                    hardcodedZoneBlob = CharacterInfoCodec.HexToBytes(state.ZoneBlobHex);

                int zoneBodyBytes = hardcodedZoneBlob.Length >= 2 ? hardcodedZoneBlob.Length - 2 : 0;
                byte[] compressedPlayer = ZoneLoginPayloadBuilder.CompressPlayerBlob(manualdat);
                ServerLog.WriteLine(
                    "GS: login source={0}, created={1}, player={2}B ? {3} (equip@{4}, inventory@{5}, bad@{6})",
                    build.Source, createdCharacter, manualdat.Length, blobCheck.Message,
                    blobCheck.EquipmentMarkerOffset, blobCheck.InventoryMarkerOffset,
                    blobCheck.BadTemplateOffset);
                ServerLog.WriteLine(
                    "GS: zone prefix={0}B, compressed player={1}B",
                    zoneBodyBytes, compressedPlayer.Length);

                if (charId > 0)
                {
                    string freshHex = CharacterInfoCodec.BytesToHex(manualdat);
                    if (state == null)
                        state = new PlayerStateRecord { CharacterId = charId };
                    bool dirty = false;
                    if (!string.Equals(state.LoginBlobHex, freshHex, StringComparison.OrdinalIgnoreCase))
                    {
                        state.LoginBlobHex = freshHex;
                        dirty = true;
                    }
                    string zoneHex = DefaultZoneBlob.GetHex();
                    if (string.IsNullOrWhiteSpace(state.ZoneBlobHex))
                    {
                        state.ZoneBlobHex = zoneHex;
                        dirty = true;
                    }
                    if (dirty)
                    {
                        state.CharacterId = charId;
                        DataStore.SavePlayerState(state);
                    }
                }

                //Raw bytes:
                //
                //68 02 01 00 00 00 1F 00 00 00 00 00 CA 48 49 71 04 00 00 00 73 09 84 12 00 00 02 00 00 00 00 00 00 00 00 00 00 00 1C 0E EB 52 02 00 00 00 00 00 5D A5 DE 39 EE 48 99 3F 00 00 00 00 03 00 00 00 00 00 5D A5 DE 39 10 E9 75 04 00 00 00 00 03 00 00 00 00 00 04 A2 75 06 00 00 1A 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F 84 12 00 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 03 00 00 00 73 09
                //C6 EF 01 00 (WAND TEMPLATE ID 0001efc6, ObjectData/Tier1/Wands/Wand-T1-034.xml:126918)
                //01 00 00 00 00 00 00 00 00 00 06 A2 75 06 00 00 1A 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F
                //C6 EF 01 00 (WAND TEMPLATE ID 0001efc6, ObjectData/Tier1/Wands/Wand-T1-034.xml:126918)
                //00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 73 09 F1 2E 01 00 01 00 00 00 00 00 00 00 00 00 64 00 32 06 00 00 57 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F F1 2E 01 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 00 00 00 00 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 73 09 CC 12 00 00 01 00 00 00 00 00 00 00 00 00 24 F4 31 06 00 00 6F 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F CC 12 00 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 00 00 00 00 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 04 00 00 00 00 00 B7 F3 63 6D 04 A2 75 06 00 00 1A 02 84 84 24 00 00 00 B7 F3 63 6D 06 A2 75 06 00 00 1A 02 69 F3 26 67 00 00 B7 F3 63 6D 64 00 32 06 00 00 57 02 08 58 01 00 00 00 B7 F3 63 6D 24 F4 31 06 00 00 6F 02 33 B5 13 05 04 00 00 00 00 00 44 64 73 43 84 12 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 44 64 73 43
                //C6 EF 01 00 (WAND TEMPLATE ID 0001efc6, ObjectData/Tier1/Wands/Wand-T1-034.xml:126918)
                //00 00 00 00 00 00 00 00 00 00 00 00 00 00 44 64 73 43 F1 2E 01 00 61 00 00 00 00 00 00 00 00 00 00 00 00 00 44 64 73 43 CC 12 00 00 61 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 03 9B 97 4C 0D 00 00 00 73 09 04 2F 01 00 01 00 00 00 00 00 00 00 00 00 08 00 32 06 00 00 57 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F 04 2F 01 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 73 09 15 81 02 00 04 00 00 00 00 00 00 00 00 00 00 00 13 5A E5 70 00 00 1C E8 EA 52 4C 4E 5A 08 00 00 A0 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 4A 4E 5A 08 00 00 A0 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F 15 81 02 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 73 09 04 2F 01 00 01 00 00 00 00 00 00 00 00 00 54 79 4A 0A 00 00 08 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F 04 2F 01 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 73 09 17 2B 07 00 05 00 00 00 00 00 00 00 00 00 00 00 13 5A E5 70 00 00 82 91 1A 61 00 00 00 00 00 00 00 00 F8 7E 6F 35 00 00 00 00 00 00 00 00 0C A8 4A 0A 00 00 08 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F 17 2B 07 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 18 00 00 00 73 09 0A 6F 05 00 04 00 00 00 00 00 00 00 00 00 00 00 13 5A E5 70 00 00 82 91 1A 61 00 00 E1 17 15 6D 0E A8 4A 0A 00 00 08 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F 0A 6F 05 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 73 09 7F 72 02 00 03 00 00 00 00 00 00 00 00 00 00 00 13 5A E5 70 00 00 82 91 1A 61 12 A8 4A 0A 00 00 08 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F 7F 72 02 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 73 09 92 74 02 00 03 00 00 00 00 00 00 00 00 00 00 00 13 5A E5 70 00 00 82 91 1A 61 71 A8 4A 0A 00 00 08 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F 92 74 02 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 73 09 46 74 02 00 05 00 00 00 00 00 00 00 00 00 00 00 13 5A E5 70 00 00 82 91 1A 61 00 00 FA C2 8E 61 00 00 00 00 00 00 00 00 00 00 00 00 CA AB 4A 0A 00 00 08 02 00 80 22 44 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F 46 74 02 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 73 09 84 38 05 00 03 00 00 00 00 00 00 00 00 00 00 00 13 5A E5 70 00 00 82 91 1A 61 62 AC 4A 0A 00 00 08 02 00 00 16 43 00 00 7A 43 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F 84 38 05 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 73 09 D5 74 02 00 03 00 00 00 00 00 00 00 00 00 00 00 13 5A E5 70 00 00 82 91 1A 61 82 AC 4A 0A 00 00 08 02 00 00 AF 43 00 00 AF C3 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F D5 74 02 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 73 09 20 72 02 00 04 00 00 00 00 00 00 00 00 00 00 00 13 5A E5 70 00 00 82 91 1A 61 00 00 00 00 00 00 70 B6 4A 0A 00 00 08 02 00 80 22 44 00 00 7A 43 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F 20 72 02 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 73 09 ED 87 01 00 04 00 00 00 00 00 E1 AF 4E 58 01 00 00 00 00 05 00 00 00 00 00 E2 BB CE 64 17 39 27 00 01 00 00 00 00 00 E2 BB CE 64 29 D3 48 13 01 00 00 00 00 00 E2 BB CE 64 20 70 1C 23 01 00 00 00 00 00 E2 BB CE 64 D0 D5 03 05 01 00 00 00 00 00 E2 BB CE 64 1A 3F 3C 75 01 00 00 00 05 00 00 00 00 00 E2 BB CE 64 17 39 27 00 5A 00 00 00 00 00 E2 BB CE 64 29 D3 48 13 64 00 00 00 00 00 E2 BB CE 64 20 70 1C 23 5A 00 00 00 00 00 E2 BB CE 64 D0 D5 03 05 32 00 00 00 00 00 E2 BB CE 64 1A 3F 3C 75 5A 00 00 00 32 AA 2D 5D 00 00 00 00 00 00 00 00 0A 00 00 00 12 00 54 61 6C 65 6E 74 52 61 6E 6B 55 6E 63 6F 6D 6D 6F 6E 12 00 54 61 6C 65 6E 74 52 61 6E 6B 55 6E 63 6F 6D 6D 6F 6E 0E 00 54 61 6C 65 6E 74 52 61 6E 6B 52 61 72 65 12 00 54 61 6C 65 6E 74 52 61 6E 6B 55 6E 63 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 12 00 54 61 6C 65 6E 74 52 61 6E 6B 55 6E 63 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 0A 00 00 00 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 12 00 54 61 6C 65 6E 74 52 61 6E 6B 55 6E 63 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 0E 00 54 61 6C 65 6E 74 52 61 6E 6B 52 61 72 65 1D 00 00 00 00 00 00 00 E0 06 00 00 0A 00 00 00 7D 00 00 00 00 00 00 00 00 00 1F B0 E5 23 00 00 77 00 31 00 01 02 00 00 00 E6 AD D0 20 00 00 00 00 00 00 00 00 00 00 00 00 00 00 1D 00 00 00 00 00 00 00 01 00 00 00 00 00 00 82 91 1A 61 00 00 96 AA DC 2D 00 00 39 8A DE 05 00 00 00 00 01 35 3A B6 07 00 00 A6 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F ED 87 01 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 88 00 00 00 73 09 5F 88 01 00 04 00 00 00 00 00 E1 AF 4E 58 01 00 00 00 00 05 00 00 00 00 00 E2 BB CE 64 17 39 27 00 01 00 00 00 00 00 E2 BB CE 64 29 D3 48 13 01 00 00 00 00 00 E2 BB CE 64 20 70 1C 23 01 00 00 00 00 00 E2 BB CE 64 D0 D5 03 05 01 00 00 00 00 00 E2 BB CE 64 1A 3F 3C 75 01 00 00 00 05 00 00 00 00 00 E2 BB CE 64 17 39 27 00 4B 00 00 00 00 00 E2 BB CE 64 29 D3 48 13 73 00 00 00 00 00 E2 BB CE 64 20 70 1C 23 4B 00 00 00 00 00 E2 BB CE 64 D0 D5 03 05 32 00 00 00 00 00 E2 BB CE 64 1A 3F 3C 75 64 00 00 00 3B AA 2D 5D 00 00 00 00 00 00 00 00 0A 00 00 00 12 00 54 61 6C 65 6E 74 52 61 6E 6B 55 6E 63 6F 6D 6D 6F 6E 12 00 54 61 6C 65 6E 74 52 61 6E 6B 55 6E 63 6F 6D 6D 6F 6E 12 00 54 61 6C 65 6E 74 52 61 6E 6B 55 6E 63 6F 6D 6D 6F 6E 12 00 54 61 6C 65 6E 74 52 61 6E 6B 55 6E 63 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 12 00 54 61 6C 65 6E 74 52 61 6E 6B 55 6E 63 6F 6D 6D 6F 6E 0A 00 00 00 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 10 00 54 61 6C 65 6E 74 52 61 6E 6B 43 6F 6D 6D 6F 6E 12 00 54 61 6C 65 6E 74 52 61 6E 6B 55 6E 63 6F 6D 6D 6F 6E 1A 00 00 00 00 00 00 00 D2 05 00 00 0A 00 00 00 7D 00 00 00 00 00 00 00 00 00 1F B0 E5 23 00 00 30 00 08 00 01 02 00 00 00 02 58 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 1A 00 00 00 00 00 00 00 01 00 00 00 00 00 00 82 91 1A 61 00 00 96 AA DC 2D 00 00 39 8A DE 05 00 00 00 00 01 38 3A B6 07 00 00 A6 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F 5F 88 01 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 88 00 00 00 50 00 00 00 64 00 00 00 00 00 CD 85 87 2C 00 00 49 3B 81 18 00 00 00 00 00 00 10 F7 D5 73 00 00 00 00 00 00 00 00 FA C2 8E 61 00 00 00 00 00 00 00 00 A1 6B F9 2D 0D 5B 25 00 77 01 00 00 04 00 00 00 01 00 00 00 00 00 00 00 00 00 15 13 19 5E 00 00 AF 52 71 18 02 00 00 00 EE 48 99 3F 10 E9 75 04 00 00 A9 19 EB 6C 03 00 00 00 00 00 5D A5 DE 39 17 82 40 44 00 00 00 00 01 00 00 00 00 00 5D A5 DE 39 CD 1D DE 6A 00 00 00 00 01 00 00 00 00 00 5D A5 DE 39 B6 43 A9 06 00 00 00 00 01 00 00 00 00 00 07 8D D0 72 38 00 80 2D 80 00 40 00 01 00 00 00 88 BE C1 04 00 00 00 02 00 00 00 00 78 9B 11 3D 00 00 20 3F 3F 00 01 01 00 00 00 88 BE C1 04 17 00 57 69 7A 61 72 64 42 61 64 67 65 73 5F 45 4D 50 54 59 54 49 54 4C 45 B0 43 00 00 00 00 00 00 09 EA 48 5D 00 00 80 DB CD 69 96 00 00 00 64 00 00 00 00 00 00 00 00 00 00 00 D6 D3 69 1F 00 00 13 5A E5 70 00 00 EE 44 D4 5C 01 00 00 00 00 00 00 00 00 2A 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 BE 5B B6 44 D0 AB 4A 0A 00 00 08 02 04 00 00 00 00 00 BC 98 41 3B 00 00 59 02 2E 79 00 00 00 00 01 00 00 00 00 00 68 8B E3 31 00 00 00 00 01 00 00 00 00 00 68 8B E3 31 09 00 00 00 84 09 B7 A1 01 00 03 00 00 00 00 00 00 00 00 00 00 00 36 DD 6A 50 01 00 00 00 00 00 7E E9 78 4E A1 4E 5A 08 00 00 A0 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F B7 A1 01 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 0B 00 00 00 84 09 B9 A1 01 00 03 00 00 00 00 00 00 00 00 00 00 00 36 DD 6A 50 01 00 00 00 00 00 7E E9 78 4E CA 80 45 06 00 00 45 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F B9 A1 01 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 02 00 00 00 84 09 D9 A1 01 00 03 00 00 00 00 00 00 00 00 00 00 00 36 DD 6A 50 01 00 00 00 00 00 7E E9 78 4E 0B A8 4A 0A 00 00 08 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F D9 A1 01 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 84 09 D7 A1 01 00 03 00 00 00 00 00 00 00 00 00 00 00 36 DD 6A 50 01 00 00 00 00 00 7E E9 78 4E 0F A8 4A 0A 00 00 08 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F D7 A1 01 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 84 09 C6 A1 01 00 02 00 00 00 00 00 00 00 00 00 00 00 36 DD 6A 50 01 00 00 00 72 A8 4A 0A 00 00 08 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F C6 A1 01 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 84 09 B3 A1 01 00 03 00 00 00 00 00 00 00 00 00 00 00 36 DD 6A 50 01 00 00 00 00 00 7E E9 78 4E 10 7E 12 07 00 00 D7 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F B3 A1 01 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 05 00 00 00 84 09 BB A1 01 00 03 00 00 00 00 00 00 00 00 00 00 00 36 DD 6A 50 01 00 00 00 00 00 7E E9 78 4E 6C C6 52 07 00 00 D1 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F BB A1 01 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 03 00 00 00 84 09 B8 A1 01 00 02 00 00 00 00 00 00 00 00 00 00 00 36 DD 6A 50 01 00 00 00 A8 98 AD 06 00 00 DC 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F B8 A1 01 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 02 00 00 00 84 09 B4 A1 01 00 02 00 00 00 00 00 00 00 00 00 00 00 36 DD 6A 50 01 00 00 00 10 99 AD 06 00 00 DC 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 3F B4 A1 01 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 E7 03 00 00 00 00 00 00 00 00 80 3F 00 00 00 00 00 00 00 00 00 00 D8 33 3D 76 02 00 00 00 E6 25 D6 2A 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 5F 30 73 1D 00 00 68 8B E3 31 00 00 00 00 E7 03 00 00 00 00 60 51 70 38 00 00 35 0E 99 04 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 98 8D 90 75 00 00 84 88 B6 7D 00 00 07 8F B4 4B 00 00 CF A0 B6 70 00 00 1F F0 DE 23 00 00 56 19 07 0F 00 00 00 00 00 00 00 00 AB 81 22 01 00 00 AA 02 00 C0 20 45 00 C0 88 45 00 00 E0 C1 00 00 00 00 00 00 00 00 9A 99 B1 40 00 00 80 3F 01 00 00 00 00 02 00 00 00 00 00 00 00 00 00 00 00 00 0F 00 A9 81 22 01 00 00 AA 02 00 00 1C D7 51 47 EB 01 00 00 15 00 00 00 E0 93 04 00 2A 00 00 00 EB 01 00 00 CA 23 00 00 15 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 04 00 00 00 04 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 03 00 00 00 00 00 00 00 00 04 64 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 00 03 00 00 00 00 07 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
                //
                //
                KIPacket holygrail = new KIPacket();
                holygrail.Header(0, 0, 5, 108); //Header (SVCID: 5, MSGID: 108, MSG_LOGINCOMPLETE)
                holygrail._STR(zoneName); //ZoneName

                byte[] zoneDataValue = ZoneLoginPayloadBuilder.BuildZoneDataValue(hardcodedZoneBlob, manualdat);
                holygrail._BINSTR(zoneDataValue);
                holygrail._UINT((uint)unixTimestamp); //ServerTime
                holygrail._GID(zoneGid); //ZoneID
                holygrail._UINT(4288020480); //DynamicZoneID
                holygrail._UINT(57781); //DynamicServerProcID
                holygrail._UINT(17328); //Permissions (not sure what the possible permissions are)
                holygrail._UINT(1); //IsCSR 0 for not. 1 for yes? Maybe this allows csr funtimes :P
                holygrail._STR("Greyrose.Game"); //ZoneServer
                holygrail._UBYT(0); //TestServer
                holygrail._UINT(0); //AltMusicFile
                holygrail._UBYT(0); //ShowSubscriberIcon (0 will hide membership button, 1 will show it (top-left corner))
                holygrail._INT(100); //Crown price percentage (usually 100, meaning full price)
                holygrail._INT(1); //UseFriendFinder
                holygrail._STR(ServerListCodec.DefaultServerName); //RealmName
                holygrail._UBYT(0); //IsBossMarkZone
                holygrail._BINSTR(new byte[0]); //CriticalObjects (empty = no loading screen blockers, loads everything from WADs)

                byte[] loginComplete = holygrail.Finalise();
                ServerLog.WriteLine(
                    "GS: MSG_LOGINCOMPLETE sent ? zone '{0}', char id {1}, player blob {2} bytes, packet {3} bytes.",
                    zoneName, charId, manualdat.Length, loginComplete.Length);
                return Tuple.Create(loginComplete, 0, 0);
            }
            else if (msgid == 2)
            {
                ServerLog.ColorTitle("MSG_CHARACTERLIST"); //Should only be sent by the server, not the client. But you never know :/
                return null; //Don't return any data (not important right now)
            }
            else if (msgid == 36)
            {
                ServerLog.ColorTitle("MSG_CLIENTMOVE");
                float x = DataHandler.SHRT(data) * 4;
                float y = DataHandler.SHRT(data) * 4;
                float z = DataHandler.SHRT(data) * 4;
                float rot = (float)(DataHandler.UBYT(data) * Math.PI * 2 / 250);
                ServerLog.WriteLine("X: {0}", x);
                ServerLog.WriteLine("Y: {0}", y);
                ServerLog.WriteLine("Z: {0}", z);
                ServerLog.WriteLine("Rot: {0}", rot); //Not completely accurate (inaccuracy of +-0.02), but close enough
                ServerLog.WriteLine("Zone: {0}", DataHandler.UBYT(data));

                var player = LoadPlayer(session);
                player.X = x;
                player.Y = y;
                player.Z = z;
                player.Rot = rot;
                SavePlayer(session, player);

                //Uncomment to make marker trails behind your character as you walk
                /*
                KIPacket temp = new KIPacket();
                temp.Header(0, 0, 5, 111); //MSG_MARK_LOCATION_RESPONSE (SVCID:5, MSGID:111)
                temp._BYT(1); //Result 1 for success?
                temp._STR("WizardCity/WC_Ravenwood"); //Zone Name
                temp._STR("Ravenwood"); //Zone Display Name Id (Not sure what to put, so I just put ravenwood?)
                temp._BYT(0); //ZoneType 0? as in free? idk
                temp._GID(0); //Instance ID. I have no idea, maybe the current session id?
                temp._FLT(PlayerData.player1.X); //X
                temp._FLT(PlayerData.player1.Y); //Y
                temp._FLT(PlayerData.player1.Z); //Z
                temp._FLT((float)(PlayerData.player1.Rot)); //Direction (rotation)
                temp._STR("0"); //Commons Zone ID (What does that mean?)
                temp._STR("1"); //Mark type? What does that mean?

                //Save player location to marker variable
                PlayerData.player1.Marker_X = (ushort)(PlayerData.player1.X / 4);
                PlayerData.player1.Marker_Y = (ushort)(PlayerData.player1.Y / 4);
                PlayerData.player1.Marker_Z = (ushort)(PlayerData.player1.Z / 4);
                PlayerData.player1.Marker_Rot = (byte)(PlayerData.player1.Rot / Math.PI / 2 * 250);

                return Tuple.Create(temp.Finalise(), 0, 0);
                */

                return null; //Don't return any data (not important right now)
            }
            else if (msgid == 37)
            {
                ServerLog.ColorTitle("MSG_CLIENTMOVESTATE");
                ServerLog.WriteLine("State: {0}", DataHandler.BYT(data));

                return null;
            }
            else if (msgid == 40)
            {
                ServerLog.ColorTitle("MSG_CLIENT_DISCONNECT");
                return null;
            }
            else if (msgid == 100)
            {
                ServerLog.ColorTitle("MGS_JUMP"); //The player jumped

                /*
                //I like hooking the jump packet, because it's very easy to send from the client.
                KIPacket temp = new KIPacket();
                temp.Header(0, 0, 12, 203); //MSG_UPDATEMANA (SVCID:12, MSGID:203)
                temp._INT(100); //Set mana to 100
                temp._INT(120); //Set max mana to 120
                byte[] packet1 = temp.Finalise();

                temp = new KIPacket(); //Reset the packet
                temp.Header(0, 0, 12, 202);//MSG_UPDATEHEALTH (SVCID:12, MSGID:202)
                temp._GID(191965934135706027); //Player id to update health for (Galen SparkleGlen)
                //temp._HEXSTRING("A9 81 22 01 00 00 AA 02");
                //temp._HEXSTRING("02 aa 00 00 01 22 81 a9");
                temp._INT(10000); //Set health to 10,000
                temp._INT(12000); //Set max health to 12,000
                byte[] packet2 = temp.Finalise();

                byte[] response = packet1.Concat(packet2).ToArray(); //Combine the two packets into one

                return Tuple.Create(response, 0, 0); //Return the UPDATEMANA message
                */
                return null; //We don't need to do anything because there's no other players to send the information to
            }
            else if (msgid == 110)
            {
                ServerLog.ColorTitle("MSG_MARK_LOCATION");

                var player = LoadPlayer(session);
                var character = DataStore.GetCharacter(GetCharacterId(session)) ?? DataStore.GetDefaultCharacter();
                string zoneName = character?.ZoneName ?? DefaultGameData.DefaultZoneName;

                KIPacket temp = new KIPacket();
                temp.Header(0, 0, 5, 111); //MSG_MARK_LOCATION_RESPONSE (SVCID:5, MSGID:111)
                temp._BYT(1); //Result 1 for success?
                temp._STR(zoneName); //Zone Name
                temp._STR("Ravenwood"); //Zone Display Name Id (Not sure what to put, so I just put ravenwood?)
                temp._BYT(0); //ZoneType 0? as in free? idk
                temp._GID(0); //Instance ID. I have no idea, maybe the current session id?
                temp._FLT(player.X); //X
                temp._FLT(player.Y); //Y
                temp._FLT(player.Z); //Z
                temp._FLT((float)(player.Rot)); //Direction (rotation)
                temp._STR("0"); //Commons Zone ID (What does that mean?)
                temp._STR("1"); //Mark type? What does that mean?
                
                //Save player location to marker variable
                player.Marker_X = (ushort)(player.X/4);
                player.Marker_Y = (ushort)(player.Y/4);
                player.Marker_Z = (ushort)(player.Z/4);
                player.Marker_Rot = (byte)(player.Rot/Math.PI/2*250);
                SavePlayer(session, player);

                return Tuple.Create(temp.Finalise(), 0, 0);

            }
            else if (msgid == 171)
            {
                ServerLog.ColorTitle("MSG_RECALL"); //Player requested to go to their marker

                var player = LoadPlayer(session);

                KIPacket temp = new KIPacket();
                temp.Header(0, 0, 5, 220); //MSG_SERVERTELEPORT (SVCID:5, MSGID:221)
                temp._USHRT(player.Marker_X); //X
                temp._USHRT(player.Marker_Y); //Y
                temp._USHRT(player.Marker_Z); //Z
                temp._UBYT(player.Marker_Rot); //Rot
                temp._USHRT(15); //MobileID (It says GID, but ushrt is only two bytes ? So I'm going to assume it's a local id assigned to players on the current server. But how is it determined? Anyway, for galen's case, it's always 15)
                return Tuple.Create(temp.Finalise(), 0, 0);

                /*
                <_MsgName TYPE="STR" NOXFER="TRUE">MSG_SERVERTELEPORT</_MsgName>
                <_MsgDescription TYPE="STR" NOXFER="TRUE">A forced teleport message, transferred from server to client (contains GID).</_MsgDescription>
                <_MsgHandler TYPE="STR" NOXFER="TRUE">MSG_ServerTeleport</_MsgHandler>
                <LocationX TYPE="USHRT"></LocationX>
                <LocationY TYPE="USHRT"></LocationY>
                <LocationZ TYPE="USHRT"></LocationZ>
                <Direction TYPE="UBYT"></Direction>
                <MobileID TYPE="USHRT"></MobileID>
                */
            }
            else if (msgid <= 253) //GameMessages has 253 different messages
            {
                Console.ForegroundColor = ConsoleColor.Red;
                ServerLog.WriteLine("Unhandled message!");
                ServerLog.WriteLine("SERVICE: 5 (GameMessages)");
                ServerLog.WriteLine("MESSAGE ID: " + msgid);
                Console.ResetColor();
                return null; //We don't know how to handle the message, so there's nothing to send back to the client
            }

            //If the messageid is greater than 253, it must be a new message.
            ServerLog.WriteLine("UNKNOWN MESSAGE ID! Make sure you are running revision r667549.Wizard_1_390");
            return null;
        }


    }
}
