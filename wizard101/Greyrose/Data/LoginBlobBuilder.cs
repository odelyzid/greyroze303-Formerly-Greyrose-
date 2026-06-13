using System;

namespace Greyrose.Data
{
    static class LoginBlobBuilder
    {
        public const string SourceCreation = "creation";
        public const string SourceCreatedZoneCapture = "created-zone-capture";
        public const string SourceDefault = "default";
        public const string SourceStored = "stored";

        static readonly byte[] InventoryMarker = { 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0xB7, 0xF3, 0x63, 0x6D };
        static readonly byte[] EquipmentMarker = { 0x73, 0x09 };
        static readonly byte[] BadTemplateMarker = { 0x6A, 0x69, 0x6E, 0x6F }; // uint template 0x6F6E696A
        static int? _statsOnlyPrefixLength;

        public static byte[] BuildLoginBlob(CharacterRecord character, PlayerStateRecord state, byte[] defaultLogin)
        {
            return BuildLoginBlobWithInfo(character, state, defaultLogin).Blob;
        }

        public static LoginBlobBuildInfo BuildLoginBlobWithInfo(
            CharacterRecord character,
            PlayerStateRecord state,
            byte[] defaultLogin)
        {
            bool isCreatedCharacter = CharacterInfoCodec.UsesZoneLoginCapture(character);

            ulong charGid = (ulong)(character?.CharGid ?? DefaultGameData.DefaultCharGid);
            ulong zoneGid = (ulong)(character?.ZoneGid ?? DefaultGameData.DefaultZoneGid);

            if (state != null && !string.IsNullOrWhiteSpace(state.LoginBlobHex))
            {
                byte[] stored = CharacterInfoCodec.HexToBytes(state.LoginBlobHex);
                if (ShouldUseStoredLoginBlob(stored, isCreatedCharacter))
                {
                    return new LoginBlobBuildInfo
                    {
                        Blob = SanitizeForZoneLogin(stored, isCreatedCharacter),
                        Source = SourceStored,
                        IsCreatedCharacter = isCreatedCharacter
                    };
                }
            }

            if (isCreatedCharacter)
            {
                byte[] creation = CharacterInfoCodec.HexToBytes(character.CharacterInfoHex);

                byte[] zoneCapture = CreatedZoneLoginBlob.GetBytes();
                if (zoneCapture != null && zoneCapture.Length >= ZoneLoginBlobImporter.MinPlayerBlobBytes)
                {
                    byte[] output = (byte[])zoneCapture.Clone();
                    if (creation != null && creation.Length > 0)
                        MergeIdentityFromCreation(output, creation);
                    output = PrepareForCharacter(output, charGid, zoneGid);
                    return new LoginBlobBuildInfo
                    {
                        Blob = SanitizeForZoneLogin(output, true),
                        Source = SourceCreatedZoneCapture,
                        IsCreatedCharacter = true
                    };
                }

                if (creation == null || creation.Length == 0)
                {
                    return new LoginBlobBuildInfo
                    {
                        Blob = Array.Empty<byte>(),
                        Source = SourceCreation,
                        IsCreatedCharacter = true
                    };
                }

                if (creation.Length >= ZoneLoginBlobImporter.MinPlayerBlobBytes)
                {
                    byte[] output = PrepareForCharacter(creation, charGid, zoneGid);
                    return new LoginBlobBuildInfo
                    {
                        Blob = SanitizeForZoneLogin(output, true),
                        Source = SourceCreation,
                        IsCreatedCharacter = true
                    };
                }

                byte[] fallback = PrepareForCharacter(creation, charGid, zoneGid);
                return new LoginBlobBuildInfo
                {
                    Blob = SanitizeForZoneLogin(fallback, true),
                    Source = SourceCreation,
                    IsCreatedCharacter = true
                };
            }

            if (defaultLogin == null || defaultLogin.Length == 0)
                defaultLogin = DefaultLoginBlob.GetBytes();

            if (defaultLogin == null || defaultLogin.Length == 0)
            {
                return new LoginBlobBuildInfo
                {
                    Blob = Array.Empty<byte>(),
                    Source = SourceDefault,
                    IsCreatedCharacter = false
                };
            }

            byte[] fromDefault = PrepareForCharacter(defaultLogin, charGid, zoneGid);
            MergeCharacterProfile(fromDefault, character);
            return new LoginBlobBuildInfo
            {
                Blob = SanitizeForZoneLogin(fromDefault, false),
                Source = SourceDefault,
                IsCreatedCharacter = false
            };
        }

        static bool ShouldUseStoredLoginBlob(byte[] stored, bool isCreatedCharacter)
        {
            if (stored == null || stored.Length == 0)
                return false;

            int statsOnly = GetStatsOnlyPrefixLength();
            if (FindFirstOffset(stored, EquipmentMarker) >= 0)
                return false;
            if (FindFirstOffset(stored, InventoryMarker) >= 0)
                return false;

            if (isCreatedCharacter)
            {
                if (CreatedZoneLoginBlob.IsAvailable())
                {
                    int captureLen = CreatedZoneLoginBlob.GetBytes().Length;
                    if (stored.Length >= captureLen - 32 && stored.Length <= captureLen + 32)
                        return FindFirstOffset(stored, InventoryMarker) < 0;
                    if (stored.Length <= CharacterInfoCodec.DisplayBlobSize)
                        return false;
                }
                return stored.Length <= Math.Max(statsOnly, CharacterInfoCodec.DisplayBlobSize);
            }

            return stored.Length <= statsOnly;
        }

        /// <summary>
        /// Strips equipment/inventory sections before MSG_LOGINCOMPLETE zlib payload.
        /// </summary>
        public static byte[] SanitizeForZoneLogin(byte[] loginBlob, bool createdCharacter = false)
        {
            if (loginBlob == null || loginBlob.Length == 0)
                return loginBlob ?? Array.Empty<byte>();

            if (createdCharacter)
            {
                int inv = FindFirstOffset(loginBlob, InventoryMarker);
                if (inv > 0)
                    loginBlob = TruncateAt(loginBlob, inv);
                // Galen capture gear uses template IDs the April 2019 client does not ship;
                // equipment here triggers ClientWizInventoryBehavior / template 0x6F6E696A failures.
                int firstEquip = FindFirstOffset(loginBlob, EquipmentMarker);
                if (firstEquip > 0)
                    loginBlob = TruncateAt(loginBlob, firstEquip);
            }
            else
            {
                int firstEquip = FindFirstOffset(loginBlob, EquipmentMarker);
                if (firstEquip > 0)
                    loginBlob = TruncateAt(loginBlob, firstEquip);
                else
                    loginBlob = TruncateBeforeInventory(loginBlob);
            }

            int bad = FindFirstOffset(loginBlob, BadTemplateMarker);
            if (bad > 0)
            {
                byte[] trimmed = new byte[bad];
                Array.Copy(loginBlob, 0, trimmed, 0, bad);
                loginBlob = trimmed;
            }

            if (!createdCharacter)
            {
                int maxSafe = GetStatsOnlyPrefixLength();
                if (maxSafe > 16 && loginBlob.Length > maxSafe)
                {
                    byte[] trimmed = new byte[maxSafe];
                    Array.Copy(loginBlob, 0, trimmed, 0, maxSafe);
                    loginBlob = trimmed;
                }
            }

            return loginBlob;
        }

        public static LoginBlobValidation Validate(byte[] loginBlob, bool createdCharacter = false)
        {
            var result = new LoginBlobValidation { Length = loginBlob?.Length ?? 0 };
            if (loginBlob == null || loginBlob.Length == 0)
            {
                result.Ok = false;
                result.Message = "empty blob";
                return result;
            }

            result.EquipmentMarkerOffset = FindFirstOffset(loginBlob, EquipmentMarker);
            result.InventoryMarkerOffset = FindFirstOffset(loginBlob, InventoryMarker);
            result.BadTemplateOffset = FindFirstOffset(loginBlob, BadTemplateMarker);

            int maxSafe = createdCharacter ? 0 : GetStatsOnlyPrefixLength();
            if (!createdCharacter && maxSafe > 0 && loginBlob.Length > maxSafe)
            {
                result.Ok = false;
                result.Message = $"blob too long ({loginBlob.Length} > {maxSafe}, equipment/inventory still present)";
                return result;
            }

            if (createdCharacter
                && loginBlob.Length > CharacterInfoCodec.DisplayBlobSize
                && loginBlob.Length > ZoneLoginBlobImporter.MaxPlayerBlobBytes)
            {
                result.Ok = false;
                result.Message = $"created blob too long ({loginBlob.Length} > {ZoneLoginBlobImporter.MaxPlayerBlobBytes})";
                return result;
            }

            result.Ok = result.BadTemplateOffset < 0
                && result.InventoryMarkerOffset < 0
                && result.EquipmentMarkerOffset < 0;
            result.Message = result.Ok
                ? "OK"
                : result.BadTemplateOffset >= 0
                    ? $"bad template 0x6F6E696A at offset {result.BadTemplateOffset}"
                    : result.InventoryMarkerOffset >= 0
                        ? $"inventory marker at offset {result.InventoryMarkerOffset}"
                        : $"equipment marker at offset {result.EquipmentMarkerOffset}";
            return result;
        }

        public static byte[] PrepareForCharacter(byte[] loginBlob, ulong charGid, ulong zoneGid)
        {
            if (loginBlob == null || loginBlob.Length == 0)
                return loginBlob;

            byte[] output = (byte[])loginBlob.Clone();
            ReplaceGid(output, (ulong)DefaultGameData.DefaultCharGid, charGid);
            ReplaceGid(output, (ulong)DefaultGameData.DefaultZoneGid, zoneGid);
            return output;
        }

        public static string PrepareForCharacterHex(byte[] loginBlob, ulong charGid, ulong zoneGid)
        {
            return CharacterInfoCodec.BytesToHex(PrepareForCharacter(loginBlob, charGid, zoneGid));
        }

        static int GetStatsOnlyPrefixLength()
        {
            if (_statsOnlyPrefixLength.HasValue)
                return _statsOnlyPrefixLength.Value;

            byte[] def = DefaultLoginBlob.GetBytes();
            int cut = def.Length > 0 ? FindFirstOffset(def, EquipmentMarker) : -1;
            if (cut < 0)
                cut = def.Length > 0 ? FindFirstOffset(def, InventoryMarker) : -1;
            _statsOnlyPrefixLength = cut > 0 ? cut : 20;
            return _statsOnlyPrefixLength.Value;
        }

        static byte[] TruncateAt(byte[] loginBlob, int cut)
        {
            if (cut <= 0 || cut >= loginBlob.Length)
                return loginBlob;

            byte[] trimmed = new byte[cut];
            Array.Copy(loginBlob, 0, trimmed, 0, cut);
            return trimmed;
        }

        static void MergeCharacterProfile(byte[] loginBlob, CharacterRecord character)
        {
            if (character == null)
                return;

            byte[] template = CharacterInfoCodec.HexToBytes(DefaultGameData.DefaultCharacterInfoHex);
            byte[] profile = CharacterInfoCodec.NormalizeDisplayBlob(
                CharacterInfoCodec.HexToBytes(character.CharacterInfoHex));

            if (template.Length < 4 || profile.Length < 4)
                return;

            int anchorLen = Math.Min(24, template.Length - 2);
            var anchor = new byte[anchorLen];
            Array.Copy(template, 2, anchor, 0, anchorLen);

            for (int i = 0; i <= loginBlob.Length - anchorLen; i++)
            {
                if (!MatchesAt(loginBlob, anchor, i))
                    continue;

                int copyLen = Math.Min(profile.Length - 2, loginBlob.Length - i);
                if (copyLen > 0)
                    Array.Copy(profile, 2, loginBlob, i, copyLen);
            }
        }

        /// <summary>
        /// Copies identity fields from the client creation blob into a zone-login capture
        /// without merging the full Galen display template.
        /// </summary>
        static void MergeIdentityFromCreation(byte[] loginBlob, byte[] creationBlob)
        {
            if (loginBlob == null || creationBlob == null || loginBlob.Length < 8 || creationBlob.Length < 8)
                return;

            int srcOffset = 0;
            int dstOffset = 0;
            if (loginBlob.Length >= 2 && creationBlob.Length >= 2)
            {
                srcOffset = 2;
                dstOffset = 2;
            }

            int copyLen = Math.Min(creationBlob.Length - srcOffset, loginBlob.Length - dstOffset);
            copyLen = Math.Min(copyLen, 22);
            if (copyLen > 0)
                Array.Copy(creationBlob, srcOffset, loginBlob, dstOffset, copyLen);
        }

        static byte[] TruncateBeforeInventory(byte[] loginBlob)
        {
            int cut = FindFirstOffset(loginBlob, InventoryMarker);
            if (cut < 0)
                cut = FindFirstOffset(loginBlob, BadTemplateMarker);
            if (cut < 0)
                return loginBlob;

            if (cut <= 0 || cut >= loginBlob.Length)
                return loginBlob;

            byte[] trimmed = new byte[cut];
            Array.Copy(loginBlob, 0, trimmed, 0, cut);
            return trimmed;
        }

        static int FindFirstOffset(byte[] data, byte[] pattern)
        {
            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                if (MatchesAt(data, pattern, i))
                    return i;
            }
            return -1;
        }

        static void ReplaceGid(byte[] data, ulong oldGid, ulong newGid)
        {
            if (oldGid == newGid)
                return;

            byte[] oldBytes = BitConverter.GetBytes(oldGid);
            byte[] newBytes = BitConverter.GetBytes(newGid);

            for (int i = 0; i <= data.Length - 8; i++)
            {
                if (MatchesAt(data, oldBytes, i))
                    Buffer.BlockCopy(newBytes, 0, data, i, 8);
            }
        }

        static bool MatchesAt(byte[] data, byte[] pattern, int offset = 0)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                if (data[offset + i] != pattern[i])
                    return false;
            }
            return true;
        }
    }

    sealed class LoginBlobBuildInfo
    {
        public byte[] Blob;
        public string Source;
        public bool IsCreatedCharacter;
    }

    sealed class LoginBlobValidation
    {
        public bool Ok;
        public string Message;
        public int Length;
        public int EquipmentMarkerOffset = -1;
        public int InventoryMarkerOffset = -1;
        public int BadTemplateOffset = -1;
    }
}
