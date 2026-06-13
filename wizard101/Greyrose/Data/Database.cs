using System;
using System.IO;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Greyrose.Data
{
    static class Database
    {
        static string _path;

        public static string Path => _path;

        public static void Initialize(string dbPath = null)
        {
            if (string.IsNullOrEmpty(dbPath))
            {
                var exeDir = AppContext.BaseDirectory;
                var candidate = System.IO.Path.Combine(exeDir, "greyrose.db");
                if (CanWrite(exeDir))
                    dbPath = candidate;
                else
                    dbPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Greyrose", "greyrose.db");
            }

            var dir = System.IO.Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            _path = dbPath;
            using var conn = OpenConnection();
            ApplySchema(conn);
        }

        static bool CanWrite(string dir)
        {
            try
            {
                var test = System.IO.Path.Combine(dir, ".write_test");
                File.WriteAllText(test, "x");
                File.Delete(test);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static SqliteConnection OpenConnection()
        {
            var conn = new SqliteConnection($"Data Source={_path}");
            conn.Open();
            return conn;
        }

        static void ApplySchema(SqliteConnection conn)
        {
            var sql = ReadSchema();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        static string ReadSchema()
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "Schema.sql");
            if (File.Exists(path))
                return File.ReadAllText(path);

            path = System.IO.Path.Combine(
                Directory.GetCurrentDirectory(), "wizard101", "Greyrose", "Data", "Schema.sql");
            if (File.Exists(path))
                return File.ReadAllText(path);

            return @"CREATE TABLE IF NOT EXISTS accounts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_gid INTEGER NOT NULL UNIQUE,
    username TEXT NOT NULL DEFAULT '',
    pass_key TEXT NOT NULL DEFAULT '',
    purchased_slots INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS characters (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    account_id INTEGER NOT NULL,
    char_gid INTEGER NOT NULL,
    name TEXT NOT NULL DEFAULT '',
    slot INTEGER NOT NULL DEFAULT 0,
    zone_name TEXT NOT NULL DEFAULT 'WizardCity/WC_Ravenwood',
    zone_gid INTEGER NOT NULL DEFAULT 0,
    location TEXT NOT NULL DEFAULT '2572,4376,-28,5.55',
    character_info_hex TEXT NOT NULL DEFAULT '',
    FOREIGN KEY (account_id) REFERENCES accounts(id) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS player_state (
    character_id INTEGER PRIMARY KEY,
    x REAL NOT NULL DEFAULT 2572,
    y REAL NOT NULL DEFAULT 4376,
    z REAL NOT NULL DEFAULT -28,
    rot REAL NOT NULL DEFAULT 5.55,
    marker_x INTEGER NOT NULL DEFAULT 0,
    marker_y INTEGER NOT NULL DEFAULT 0,
    marker_z INTEGER NOT NULL DEFAULT 0,
    marker_rot INTEGER NOT NULL DEFAULT 0,
    login_blob_hex TEXT NOT NULL DEFAULT '',
    zone_blob_hex TEXT,
    FOREIGN KEY (character_id) REFERENCES characters(id) ON DELETE CASCADE
);";
        }

        public static bool IsEmpty()
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM accounts";
            return Convert.ToInt64(cmd.ExecuteScalar()) == 0;
        }
    }
}
