using Microsoft.Data.Sqlite;
foreach (var db in new[] {
  @"d:\Wizard101_client_04_2019\wizard101\Greyrose\bin\Debug\net8.0-windows\win-x64\greyrose.db",
  @"C:\Users\Gamma\AppData\Roaming\Greyrose\greyrose.db" })
{
  Console.WriteLine("=== " + db + " ===");
  using var c = new SqliteConnection("Data Source=" + db);
  c.Open();
  using var cmd = c.CreateCommand();
  cmd.CommandText = "SELECT id, name, char_gid, length(character_info_hex), substr(character_info_hex,1,24) FROM characters WHERE id=6";
  using var r = cmd.ExecuteReader();
  if (!r.Read()) { Console.WriteLine("char 6 not found"); continue; }
  Console.WriteLine($"id={r.GetInt64(0)} name='{r.GetString(1)}' gid={r.GetInt64(2)} info_hex_len={r.GetInt64(3)} prefix={r.GetString(4)}");
  cmd.CommandText = "SELECT length(login_blob_hex) FROM player_state WHERE character_id=6";
  var o = cmd.ExecuteScalar();
  Console.WriteLine("login_blob_hex chars: " + (o ?? "(no row)"));
  cmd.CommandText = "SELECT id, name FROM characters ORDER BY id";
  using var r2 = cmd.ExecuteReader();
  while (r2.Read()) Console.WriteLine($"  char {r2.GetInt64(0)}: {r2.GetString(1)}");
}
