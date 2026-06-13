using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Greyrose.Data
{
    /// <summary>
    /// Shared KingsIsle DML table-stream writer (LatestFileList.bin, MSG_SERVERLIST tables, etc.).
    /// </summary>
    static class DmlTableWriter
    {
        public const byte TypeRecordTemplate = 1;
        public const byte TypeRecord = 2;

        public const byte TypeInt = 1;
        public const byte TypeUint = 2;
        public const byte TypeFlt = 3;
        public const byte TypeStr = 8;
        public const byte TypeWstr = 9;

        public readonly struct TemplateField
        {
            public readonly string Name;
            public readonly byte Type;

            public TemplateField(string name, byte type)
            {
                Name = name;
                Type = type;
            }
        }

        public static void WriteTableHeader(BinaryWriter w, uint recordCount)
        {
            w.Write(recordCount);
            w.Write((byte)0x02);
            w.Write(TypeRecordTemplate);
        }

        public static void WriteRecordTemplate(BinaryWriter w, TemplateField[] fields, string targetTable)
        {
            using var body = new MemoryStream();
            using var bw = new BinaryWriter(body, Encoding.ASCII, leaveOpen: true);

            foreach (var field in fields)
                WriteTemplateFieldHeader(bw, field.Name, field.Type);

            WriteTemplateFieldHeader(bw, "_TargetTable", TypeWstr);
            WriteWstrField(bw, targetTable);

            byte[] bodyBytes = body.ToArray();
            w.Write((ushort)bodyBytes.Length);
            w.Write(bodyBytes);
        }

        public static void WriteTemplateFieldHeader(BinaryWriter w, string name, byte type)
        {
            w.Write((ushort)name.Length);
            w.Write(Encoding.ASCII.GetBytes(name));
            w.Write(type);
            w.Write((byte)0x28);
        }

        public static void WriteRecordHeader(BinaryWriter w, ushort recordSize)
        {
            w.Write((byte)0x02);
            w.Write(TypeRecord);
            w.Write(recordSize);
        }

        public static void WriteStringField(BinaryWriter w, string value)
        {
            if (value == null)
                value = "";
            w.Write((ushort)value.Length);
            if (value.Length > 0)
                w.Write(Encoding.ASCII.GetBytes(value));
        }

        public static void WriteWstrField(BinaryWriter w, string value)
        {
            if (value == null)
                value = "";
            w.Write((ushort)value.Length);
            if (value.Length > 0)
                w.Write(Encoding.Unicode.GetBytes(value));
        }

        public static void WriteUInt32Field(BinaryWriter w, uint value)
        {
            w.Write(value);
        }

        public static void WriteInt32Field(BinaryWriter w, int value)
        {
            w.Write(value);
        }

        public static int StrPayloadBytes(string value) => 2 + (value?.Length ?? 0);

        public static int WstrPayloadBytes(string value) => 2 + (value?.Length ?? 0) * 2;
    }
}
