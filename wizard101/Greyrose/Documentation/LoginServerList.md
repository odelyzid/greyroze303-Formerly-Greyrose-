# MSG_SERVERLIST / _ServerList (April 2019 client)

`root.wad` is not bundled in this repo. Definitions were inferred from:

- `WizardGraphicalClient.exe` strings: `_ServerList`, `ServerSelectState`, `ZoneName`
- [`PatchClient/BankA/LoginMessages.xml`](../../../Wizard101%20April%20of%202019/PatchClient/BankA/LoginMessages.xml): `MSG_SERVERLIST` has an empty `<RECORD>` (no xfer fields in patch XML)
- Client log: `DMLRecord::FromBinary: Expected 4 bytes, Received N` on svc 7 / msg 11

## Greyrose encoding (current)

Two KIP frames on login (svc 7, msg 11):

1. **Row** — full DML table stream for `_ServerList` with one record (`ServerSelectState` INT, `ZoneName` STR)
2. **End** — exactly `00 00 00 00` (table record count 0)

Built by [`DmlServerListBuilder.cs`](../Data/DmlServerListBuilder.cs) and framed with [`KipFrameBuilder.cs`](../Data/KipFrameBuilder.cs).

To extract authoritative defs from the game, use QuickBMS + `wizard101_kiwad.bms` on `Data/GameData/root.wad` and open `LoginMessages.xml` (see [`Messages.txt`](Messages.txt)).
