using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Greyrose;

namespace Greyrose.Data
{
    public static class DataStore
    {
        const int DefaultPurchasedSlots = 5;

        public static void Initialize(string dbPath = null)
        {
            Database.Initialize(dbPath);
            if (Database.IsEmpty())
                SeedData.Apply();
            else
                EnsureDefaultAccountSlots();
        }

        public static void EnsureSeeded()
        {
            if (Database.IsEmpty())
                SeedData.Apply();
            else
                EnsureDefaultAccountSlots();
        }

        /// <summary>
        /// Bumps purchased_slots on the default account when an older DB was seeded with 0.
        /// </summary>
        public static void EnsureDefaultAccountSlots()
        {
            var account = GetAccountByUserGid(DefaultGameData.DefaultUserGid) ?? GetDefaultAccount();
            if (account == null || account.PurchasedSlots >= DefaultPurchasedSlots)
                return;

            int previous = account.PurchasedSlots;
            account.PurchasedSlots = DefaultPurchasedSlots;
            UpdateAccount(account);
            ServerLog.WriteLine("Migrated default account PurchasedSlots {0} → {1}.",
                previous, DefaultPurchasedSlots);
        }

        public static AccountRecord GetAccountById(long id)
        {
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, user_gid, username, pass_key, purchased_slots FROM accounts WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return ReadAccount(r);
        }

        public static AccountRecord GetDefaultAccount()
        {
            var byClientGid = GetAccountByUserGid(DefaultGameData.DefaultUserGid);
            if (byClientGid != null)
                return byClientGid;

            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, user_gid, username, pass_key, purchased_slots FROM accounts ORDER BY id LIMIT 1";
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return ReadAccount(r);
        }

        /// <summary>
        /// Picks the account for MSG_USER_AUTHEN when Rec1 is an opaque credential blob.
        /// </summary>
        public static AccountRecord ResolveAuthenAccount(ClientSession session, string usernameHint = null)
        {
            if (!string.IsNullOrWhiteSpace(usernameHint))
            {
                var byName = GetAccountByUsername(usernameHint);
                if (byName != null)
                    return byName;
            }

            if (session?.AccountUserGid is long gid)
            {
                var byGid = GetAccountByUserGid(gid);
                if (byGid != null)
                    return byGid;
            }

            var accounts = GetAllAccounts();
            if (accounts.Count == 0)
                return null;
            if (accounts.Count == 1)
                return accounts[0];

            var nonSeed = accounts.Find(a => a.UserGid != DefaultGameData.DefaultUserGid);
            if (nonSeed != null)
                return nonSeed;

            return GetDefaultAccount();
        }

        public static AccountRecord GetAccountByUserGid(long userGid)
        {
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, user_gid, username, pass_key, purchased_slots FROM accounts WHERE user_gid = $gid";
            cmd.Parameters.AddWithValue("$gid", userGid);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return ReadAccount(r);
        }

        public static AccountRecord GetAccountByUsername(string username)
        {
            if (string.IsNullOrEmpty(username))
                return null;

            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, user_gid, username, pass_key, purchased_slots FROM accounts WHERE username = $user COLLATE NOCASE LIMIT 1";
            cmd.Parameters.AddWithValue("$user", username);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return ReadAccount(r);
        }

        public static List<AccountRecord> GetAllAccounts()
        {
            var list = new List<AccountRecord>();
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, user_gid, username, pass_key, purchased_slots FROM accounts ORDER BY id";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(ReadAccount(r));
            return list;
        }

        public static long InsertAccount(AccountRecord a)
        {
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO accounts (user_gid, username, pass_key, purchased_slots)
                VALUES ($gid, $user, $pass, $slots); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$gid", a.UserGid);
            cmd.Parameters.AddWithValue("$user", a.Username ?? "");
            cmd.Parameters.AddWithValue("$pass", a.PassKey ?? "");
            cmd.Parameters.AddWithValue("$slots", a.PurchasedSlots);
            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        public static void UpdateAccount(AccountRecord a)
        {
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE accounts SET user_gid=$gid, username=$user, pass_key=$pass, purchased_slots=$slots WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", a.Id);
            cmd.Parameters.AddWithValue("$gid", a.UserGid);
            cmd.Parameters.AddWithValue("$user", a.Username ?? "");
            cmd.Parameters.AddWithValue("$pass", a.PassKey ?? "");
            cmd.Parameters.AddWithValue("$slots", a.PurchasedSlots);
            cmd.ExecuteNonQuery();
        }

        public static void DeleteAccount(long id)
        {
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM accounts WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public static List<CharacterRecord> GetCharactersByAccountId(long accountId)
        {
            var list = new List<CharacterRecord>();
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT id, account_id, char_gid, name, slot, zone_name, zone_gid, location, character_info_hex
                FROM characters WHERE account_id=$aid ORDER BY slot, id";
            cmd.Parameters.AddWithValue("$aid", accountId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(ReadCharacter(r));
            return list;
        }

        public static CharacterRecord GetCharacter(long id)
        {
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT id, account_id, char_gid, name, slot, zone_name, zone_gid, location, character_info_hex
                FROM characters WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return ReadCharacter(r);
        }

        public static CharacterRecord GetCharacterBySlot(long accountId, int slot)
        {
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT id, account_id, char_gid, name, slot, zone_name, zone_gid, location, character_info_hex
                FROM characters WHERE account_id=$aid AND slot=$slot LIMIT 1";
            cmd.Parameters.AddWithValue("$aid", accountId);
            cmd.Parameters.AddWithValue("$slot", slot);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return ReadCharacter(r);
        }

        public static CharacterRecord GetDefaultCharacter()
        {
            var account = GetDefaultAccount();
            if (account == null) return null;
            var chars = GetCharactersByAccountId(account.Id);
            return chars.Count > 0 ? chars[0] : null;
        }

        public static List<CharacterRecord> GetAllCharacters()
        {
            var list = new List<CharacterRecord>();
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT id, account_id, char_gid, name, slot, zone_name, zone_gid, location, character_info_hex
                FROM characters ORDER BY id";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(ReadCharacter(r));
            return list;
        }

        public static CharacterRecord GetCharacterByCharGid(long charGid)
        {
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT id, account_id, char_gid, name, slot, zone_name, zone_gid, location, character_info_hex
                FROM characters WHERE char_gid=$gid LIMIT 1";
            cmd.Parameters.AddWithValue("$gid", charGid);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return ReadCharacter(r);
        }

        public static long InsertCharacter(CharacterRecord c)
        {
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO characters (account_id, char_gid, name, slot, zone_name, zone_gid, location, character_info_hex)
                VALUES ($aid, $gid, $name, $slot, $zone, $zgid, $loc, $hex); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$aid", c.AccountId);
            cmd.Parameters.AddWithValue("$gid", c.CharGid);
            cmd.Parameters.AddWithValue("$name", c.Name ?? "");
            cmd.Parameters.AddWithValue("$slot", c.Slot);
            cmd.Parameters.AddWithValue("$zone", c.ZoneName ?? "");
            cmd.Parameters.AddWithValue("$zgid", c.ZoneGid);
            cmd.Parameters.AddWithValue("$loc", c.Location ?? "");
            cmd.Parameters.AddWithValue("$hex", c.CharacterInfoHex ?? "");
            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        public static void UpdateCharacter(CharacterRecord c)
        {
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE characters SET account_id=$aid, char_gid=$gid, name=$name, slot=$slot,
                zone_name=$zone, zone_gid=$zgid, location=$loc, character_info_hex=$hex WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", c.Id);
            cmd.Parameters.AddWithValue("$aid", c.AccountId);
            cmd.Parameters.AddWithValue("$gid", c.CharGid);
            cmd.Parameters.AddWithValue("$name", c.Name ?? "");
            cmd.Parameters.AddWithValue("$slot", c.Slot);
            cmd.Parameters.AddWithValue("$zone", c.ZoneName ?? "");
            cmd.Parameters.AddWithValue("$zgid", c.ZoneGid);
            cmd.Parameters.AddWithValue("$loc", c.Location ?? "");
            cmd.Parameters.AddWithValue("$hex", c.CharacterInfoHex ?? "");
            cmd.ExecuteNonQuery();
        }

        public static void DeleteCharacter(long id)
        {
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM characters WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public static PlayerStateRecord GetPlayerState(long characterId)
        {
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT character_id, x, y, z, rot, marker_x, marker_y, marker_z, marker_rot, login_blob_hex, zone_blob_hex
                FROM player_state WHERE character_id=$id";
            cmd.Parameters.AddWithValue("$id", characterId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return ReadPlayerState(r);
        }

        public static void SavePlayerState(PlayerStateRecord s)
        {
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO player_state (character_id, x, y, z, rot, marker_x, marker_y, marker_z, marker_rot, login_blob_hex, zone_blob_hex)
                VALUES ($cid, $x, $y, $z, $rot, $mx, $my, $mz, $mr, $login, $zone)
                ON CONFLICT(character_id) DO UPDATE SET
                x=$x, y=$y, z=$z, rot=$rot, marker_x=$mx, marker_y=$my, marker_z=$mz, marker_rot=$mr,
                login_blob_hex=$login, zone_blob_hex=$zone";
            cmd.Parameters.AddWithValue("$cid", s.CharacterId);
            cmd.Parameters.AddWithValue("$x", s.X);
            cmd.Parameters.AddWithValue("$y", s.Y);
            cmd.Parameters.AddWithValue("$z", s.Z);
            cmd.Parameters.AddWithValue("$rot", s.Rot);
            cmd.Parameters.AddWithValue("$mx", s.MarkerX);
            cmd.Parameters.AddWithValue("$my", s.MarkerY);
            cmd.Parameters.AddWithValue("$mz", s.MarkerZ);
            cmd.Parameters.AddWithValue("$mr", s.MarkerRot);
            cmd.Parameters.AddWithValue("$login", s.LoginBlobHex ?? "");
            cmd.Parameters.AddWithValue("$zone", (object)s.ZoneBlobHex ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public static void DeletePlayerState(long characterId)
        {
            using var conn = Database.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM player_state WHERE character_id=$id";
            cmd.Parameters.AddWithValue("$id", characterId);
            cmd.ExecuteNonQuery();
        }

        static AccountRecord ReadAccount(SqliteDataReader r)
        {
            return new AccountRecord
            {
                Id = r.GetInt64(0),
                UserGid = r.GetInt64(1),
                Username = r.GetString(2),
                PassKey = r.GetString(3),
                PurchasedSlots = r.GetInt32(4)
            };
        }

        static CharacterRecord ReadCharacter(SqliteDataReader r)
        {
            return new CharacterRecord
            {
                Id = r.GetInt64(0),
                AccountId = r.GetInt64(1),
                CharGid = r.GetInt64(2),
                Name = r.GetString(3),
                Slot = r.GetInt32(4),
                ZoneName = r.GetString(5),
                ZoneGid = r.GetInt64(6),
                Location = r.GetString(7),
                CharacterInfoHex = r.GetString(8)
            };
        }

        static PlayerStateRecord ReadPlayerState(SqliteDataReader r)
        {
            return new PlayerStateRecord
            {
                CharacterId = r.GetInt64(0),
                X = (float)r.GetDouble(1),
                Y = (float)r.GetDouble(2),
                Z = (float)r.GetDouble(3),
                Rot = (float)r.GetDouble(4),
                MarkerX = (ushort)r.GetInt64(5),
                MarkerY = (ushort)r.GetInt64(6),
                MarkerZ = (ushort)r.GetInt64(7),
                MarkerRot = (byte)r.GetInt64(8),
                LoginBlobHex = r.GetString(9),
                ZoneBlobHex = r.IsDBNull(10) ? null : r.GetString(10)
            };
        }
    }
}
