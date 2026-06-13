namespace Greyrose.Data
{
    static class SeedData
    {
        public static void Apply()
        {
            var accountId = DataStore.InsertAccount(new AccountRecord
            {
                UserGid = DefaultGameData.DefaultUserGid,
                Username = DefaultGameData.DefaultUsername,
                PassKey = "",
                PurchasedSlots = 5
            });

            var charId = DataStore.InsertCharacter(new CharacterRecord
            {
                AccountId = accountId,
                CharGid = DefaultGameData.DefaultCharGid,
                Name = DefaultGameData.DefaultCharacterName,
                Slot = 0,
                ZoneName = DefaultGameData.DefaultZoneName,
                ZoneGid = DefaultGameData.DefaultZoneGid,
                Location = DefaultGameData.DefaultLocation,
                CharacterInfoHex = DefaultGameData.DefaultCharacterInfoHex
            });

            DataStore.SavePlayerState(new PlayerStateRecord
            {
                CharacterId = charId,
                X = 2572,
                Y = 4376,
                Z = -28,
                Rot = 5.55f,
                LoginBlobHex = "",
                ZoneBlobHex = DefaultZoneBlob.GetHex()
            });

            ServerLog.WriteLine("Database seeded with default account and character.");
        }
    }
}
