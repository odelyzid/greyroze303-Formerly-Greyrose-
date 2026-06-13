using System;
using System.Collections.Generic;
using System.Text;

namespace Greyrose.Data
{
    public sealed class LoginBlobInspection
    {
        public byte[] Blob;
        public int StatsPrefixLength;
        public int InventoryOffset = -1;
        public List<EquipmentSlotEntry> EquipmentSlots = new List<EquipmentSlotEntry>();
    }

    public sealed class EquipmentSlotEntry
    {
        public int Index;
        public int Offset;
        public uint TemplateId;
        public string HexSnippet;
    }

    public static class LoginBlobInspector
    {
        static readonly byte[] InventoryMarker = { 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0xB7, 0xF3, 0x63, 0x6D };
        static readonly byte[] EquipmentMarker = { 0x73, 0x09 };

        public static LoginBlobInspection Parse(byte[] blob)
        {
            var result = new LoginBlobInspection { Blob = blob ?? Array.Empty<byte>() };
            if (result.Blob.Length == 0)
                return result;

            result.InventoryOffset = FindOffset(result.Blob, InventoryMarker);
            int firstEquip = FindOffset(result.Blob, EquipmentMarker);
            result.StatsPrefixLength = firstEquip > 0 ? firstEquip : result.Blob.Length;

            if (firstEquip < 0)
                return result;

            int slot = 0;
            for (int i = firstEquip; i <= result.Blob.Length - EquipmentMarker.Length; i++)
            {
                if (!MatchesAt(result.Blob, EquipmentMarker, i))
                    continue;

                if (result.InventoryOffset > 0 && i >= result.InventoryOffset)
                    break;

                uint templateId = 0;
                int templateOffset = i + EquipmentMarker.Length;
                if (templateOffset + 4 <= result.Blob.Length)
                    templateId = BitConverter.ToUInt32(result.Blob, templateOffset);

                int snippetEnd = Math.Min(result.Blob.Length, i + 32);
                if (result.InventoryOffset > 0)
                    snippetEnd = Math.Min(snippetEnd, result.InventoryOffset);

                result.EquipmentSlots.Add(new EquipmentSlotEntry
                {
                    Index = slot++,
                    Offset = i,
                    TemplateId = templateId,
                    HexSnippet = BytesToHex(result.Blob, i, snippetEnd - i)
                });
            }

            return result;
        }

        public static byte[] ParseHex(string hex)
        {
            return CharacterInfoCodec.HexToBytes(hex);
        }

        public static string BytesToHex(byte[] data, int offset, int length)
        {
            if (data == null || length <= 0)
                return "";
            var sb = new StringBuilder(length * 3);
            int end = Math.Min(data.Length, offset + length);
            for (int i = offset; i < end; i++)
            {
                if (i > offset)
                    sb.Append(' ');
                sb.Append(data[i].ToString("X2"));
            }
            return sb.ToString();
        }

        public static byte[] StripInventory(byte[] blob)
        {
            if (blob == null || blob.Length == 0)
                return blob ?? Array.Empty<byte>();

            int inv = FindOffset(blob, InventoryMarker);
            if (inv <= 0)
                return (byte[])blob.Clone();

            byte[] trimmed = new byte[inv];
            Array.Copy(blob, 0, trimmed, 0, inv);
            return trimmed;
        }

        public static bool TrySetEquipmentTemplate(byte[] blob, int slotIndex, uint templateId, out byte[] output)
        {
            output = null;
            var inspection = Parse(blob);
            if (slotIndex < 0 || slotIndex >= inspection.EquipmentSlots.Count)
                return false;

            output = (byte[])blob.Clone();
            var slot = inspection.EquipmentSlots[slotIndex];
            int templateOffset = slot.Offset + EquipmentMarker.Length;
            if (templateOffset + 4 > output.Length)
                return false;

            BitConverter.GetBytes(templateId).CopyTo(output, templateOffset);
            return true;
        }

        public static string FormatInspectionReport(LoginBlobInspection inspection, bool createdCharacter)
        {
            if (inspection?.Blob == null || inspection.Blob.Length == 0)
                return "empty blob";

            var v = LoginBlobBuilder.Validate(inspection.Blob, createdCharacter);
            var sb = new StringBuilder();
            sb.AppendLine("Login blob: " + inspection.Blob.Length + " bytes — " + v.Message);
            sb.AppendLine("  Stats prefix: " + inspection.StatsPrefixLength + " bytes");
            sb.AppendLine("  Equipment slots: " + inspection.EquipmentSlots.Count);
            foreach (var slot in inspection.EquipmentSlots)
            {
                sb.AppendLine(string.Format(
                    "    [{0}] offset={1} template=0x{2:X8} ({3})",
                    slot.Index, slot.Offset, slot.TemplateId, slot.HexSnippet));
            }
            if (inspection.InventoryOffset >= 0)
                sb.AppendLine("  Inventory section at offset: " + inspection.InventoryOffset);
            return sb.ToString();
        }

        static int FindOffset(byte[] data, byte[] pattern)
        {
            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                if (MatchesAt(data, pattern, i))
                    return i;
            }
            return -1;
        }

        static bool MatchesAt(byte[] data, byte[] pattern, int offset)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                if (data[offset + i] != pattern[i])
                    return false;
            }
            return true;
        }
    }
}
