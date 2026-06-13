using System;
using Greyrose.Data;

namespace Greyrose
{
    class PlayerData
    {
        public struct PlayerStruct
        {
            public float X;
            public float Y;
            public float Z;
            public float Rot;
            public Int64 GID;
            public string Zone_Name;
            public Int64 Zone_ID;

            public ushort Marker_X;
            public ushort Marker_Y;
            public ushort Marker_Z;
            public byte Marker_Rot;
        }

        public static PlayerStruct Load(long characterId)
        {
            var state = DataStore.GetPlayerState(characterId);
            var character = DataStore.GetCharacter(characterId);
            if (state == null && character == null)
                return default;

            if (state == null)
            {
                return new PlayerStruct
                {
                    GID = character.CharGid,
                    Zone_Name = character.ZoneName,
                    Zone_ID = character.ZoneGid
                };
            }

            return new PlayerStruct
            {
                X = state.X,
                Y = state.Y,
                Z = state.Z,
                Rot = state.Rot,
                GID = character != null ? character.CharGid : 0,
                Zone_Name = character != null ? character.ZoneName : "",
                Zone_ID = character != null ? character.ZoneGid : 0,
                Marker_X = state.MarkerX,
                Marker_Y = state.MarkerY,
                Marker_Z = state.MarkerZ,
                Marker_Rot = state.MarkerRot
            };
        }

        public static void Save(long characterId, PlayerStruct player)
        {
            var existing = DataStore.GetPlayerState(characterId) ?? new PlayerStateRecord { CharacterId = characterId };
            existing.X = player.X;
            existing.Y = player.Y;
            existing.Z = player.Z;
            existing.Rot = player.Rot;
            existing.MarkerX = player.Marker_X;
            existing.MarkerY = player.Marker_Y;
            existing.MarkerZ = player.Marker_Z;
            existing.MarkerRot = player.Marker_Rot;
            DataStore.SavePlayerState(existing);
        }

        public static long ResolveCharacterId(ClientSession session)
        {
            if (session.SelectedCharacterId.HasValue)
                return session.SelectedCharacterId.Value;
            if (session.AccountUserGid.HasValue)
            {
                var account = DataStore.GetAccountByUserGid(session.AccountUserGid.Value);
                if (account != null)
                {
                    var chars = DataStore.GetCharactersByAccountId(account.Id);
                    if (chars.Count > 0)
                        return chars[0].Id;
                }
            }
            var ch = DataStore.GetDefaultCharacter();
            return ch?.Id ?? 0;
        }
    }
}
