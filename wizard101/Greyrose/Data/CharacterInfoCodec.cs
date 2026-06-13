using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Greyrose.Data
{
    static class CharacterInfoCodec
    {
        // Char GID lives inside the KingsIsle object graph, not at a fixed offset.
        public const int DisplayBlobSize = 168;

        public static byte[] Latin1Bytes(string value)
        {
            if (string.IsNullOrEmpty(value))
                return Array.Empty<byte>();
            return Encoding.Latin1.GetBytes(value);
        }

        public static string BytesToHex(byte[] data)
        {
            if (data == null || data.Length == 0)
                return "";
            return BitConverter.ToString(data).Replace("-", " ");
        }

        public static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return Array.Empty<byte>();

            string compact = NormalizeHex(hex);
            if (compact.Length % 2 != 0)
                compact += "0";

            var data = new byte[compact.Length / 2];
            for (int i = 0; i < data.Length; i++)
                data[i] = byte.Parse(compact.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return data;
        }

        public static long? TryExtractCharGid(byte[] blob)
        {
            if (blob == null || blob.Length < 8)
                return null;

            // Scan for a KingsIsle-style character GID embedded in the serialized blob.
            for (int i = 0; i <= blob.Length - 8; i++)
            {
                long value = BitConverter.ToInt64(blob, i);
                if (value > 100_000_000_000_000L && value < 10_000_000_000_000_000L)
                    return value;
            }

            return null;
        }

        public static byte[] PatchCharGid(byte[] blob, long charGid)
        {
            // Do not patch — the client-owned creation blob must be stored and echoed verbatim.
            return blob;
        }

        public static byte[] PatchNameHash(byte[] blob, string name)
        {
            // Name is encoded inside the KingsIsle object graph, not at a fixed offset.
            // Patching a guessed offset corrupts the blob and breaks character select.
            return blob;
        }

        public static string NormalizeHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return "";
            return string.Concat(hex.Where(c => !char.IsWhiteSpace(c)));
        }

        public static bool IsDefaultTemplate(string hex)
        {
            return string.Equals(
                NormalizeHex(hex),
                NormalizeHex(DefaultGameData.DefaultCharacterInfoHex),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True when MSG_ATTACH should use the zone-login player capture (equipment, no inventory),
        /// not the stock seed stats-only prefix.
        /// </summary>
        public static bool UsesZoneLoginCapture(CharacterRecord character)
        {
            if (character == null)
                return false;
            if (!IsDefaultTemplate(character.CharacterInfoHex))
                return true;
            return character.CharGid != DefaultGameData.DefaultCharGid;
        }

        /// <summary>
        /// Expands short CREATECHARACTER payloads (typically ~72 bytes) into the full
        /// ~168-byte display blob the client expects on the character select screen.
        /// </summary>
        public static byte[] NormalizeDisplayBlob(byte[] blob)
        {
            byte[] template = HexToBytes(DefaultGameData.DefaultCharacterInfoHex);
            if (blob == null || blob.Length == 0)
                return (byte[])template.Clone();

            if (blob.Length >= 2)
            {
                ushort declaredSize = BitConverter.ToUInt16(blob, 0);
                if (declaredSize == blob.Length && blob.Length >= DisplayBlobSize)
                    return blob;
            }

            var merged = (byte[])template.Clone();
            int srcOffset = 0;
            if (blob.Length >= 2 && blob[0] == template[0] && blob[1] == template[1])
                srcOffset = 2;

            int copyLen = Math.Min(blob.Length - srcOffset, merged.Length - 2);
            if (copyLen > 0)
                Array.Copy(blob, srcOffset, merged, 2, copyLen);

            BitConverter.GetBytes((ushort)merged.Length).CopyTo(merged, 0);
            return merged;
        }

        /// <summary>
        /// Returns the stored character blob unchanged. The client validates this
        /// KingsIsle serialized payload; do not merge templates or patch offsets.
        /// </summary>
        public static byte[] PrepareForClient(CharacterRecord character)
        {
            if (character == null)
                return Array.Empty<byte>();

            byte[] blob = HexToBytes(character.CharacterInfoHex);
            if (blob.Length == 0)
                blob = HexToBytes(DefaultGameData.DefaultCharacterInfoHex);
            return blob ?? Array.Empty<byte>();
        }

        public static string PrepareForClientHex(CharacterRecord character)
        {
            return BytesToHex(PrepareForClient(character));
        }

        public static string TryExtractName(byte[] blob)
        {
            if (blob == null || blob.Length < 3)
                return null;

            string best = null;
            for (int i = 0; i + 2 < blob.Length; i++)
            {
                int length = BitConverter.ToUInt16(blob, i);
                if (length < 3 || length > 32 || i + 2 + length > blob.Length)
                    continue;

                bool printable = true;
                for (int j = 0; j < length; j++)
                {
                    byte b = blob[i + 2 + j];
                    if (b < 0x20 || b > 0x7E)
                    {
                        printable = false;
                        break;
                    }
                }
                if (!printable)
                    continue;

                string candidate = Encoding.ASCII.GetString(blob, i + 2, length);
                if (candidate.Contains('/') || candidate.StartsWith("WizardZone", StringComparison.OrdinalIgnoreCase))
                    continue;

                best = candidate;
            }

            return best;
        }

        public static long AllocateCharGid(long accountId)
        {
            long maxGid = DefaultGameData.DefaultCharGid;
            foreach (var account in DataStore.GetAllAccounts())
            {
                foreach (var ch in DataStore.GetCharactersByAccountId(account.Id))
                    if (ch.CharGid > maxGid)
                        maxGid = ch.CharGid;
            }

            long next = maxGid + 1;
            if (next <= 0)
                next = DefaultGameData.DefaultCharGid + accountId + DateTime.UtcNow.Ticks % 1000;
            return next;
        }

        public static int FindNextSlot(IEnumerable<CharacterRecord> existing)
        {
            var used = new HashSet<int>(existing.Select(c => c.Slot));
            for (int slot = 0; slot < 128; slot++)
            {
                if (!used.Contains(slot))
                    return slot;
            }
            return existing.Count();
        }
    }
}
