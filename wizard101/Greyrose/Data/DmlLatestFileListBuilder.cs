using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Greyrose.Data
{
    /// <summary>
    /// Builds Wizard101 LatestFileList.bin (DML table stream, not KingsIsle BINd).
    /// FLT columns store uint32 bit patterns (see w101-client-go/dml.DecodeTable).
    /// </summary>
    static class DmlLatestFileListBuilder
    {
        public const string MetaAbout = "About";
        public const string MetaPatchClient = "PatchClient";
        public const string MetaBase = "Base";

        public struct PackageFileInfo
        {
            public string PackageName;
            public string SrcFileName;
            public string TarFileName;
            public uint FileType;
            public uint Size;
            public uint HeaderSize;
            public uint CompressedHeaderSize;
            public uint Crc;
            public uint HeaderCrc;
        }

        public static byte[] Build(IReadOnlyList<string> packageNames, IReadOnlyList<PackageFileInfo> packages)
        {
            var tableList = new List<string> { MetaAbout, MetaPatchClient, MetaBase };
            tableList.AddRange(packageNames);

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

            WriteTableListTable(w, tableList);
            WriteAboutTable(w);
            WritePatchClientTable(w);
            WriteBaseTable(w);
            foreach (var pkg in packages)
                WritePackageTable(w, pkg);

            return ms.ToArray();
        }

        /// <summary>Metadata tables only (_TableList, About, PatchClient, Base) — for client parse debugging.</summary>
        public static byte[] BuildMetadataOnly()
        {
            return Build(Array.Empty<string>(), Array.Empty<PackageFileInfo>());
        }

        public static bool IsDmlFile(byte[] data)
        {
            if (data == null || data.Length < 6)
                return false;
            if (BitConverter.ToUInt32(data, 0) == KiBinaryXml.BindSignature)
                return false;
            return data[4] == 0x02 && data[5] == DmlTableWriter.TypeRecordTemplate;
        }

        public static bool IsValidListFile(byte[] data)
        {
            if (!IsDmlFile(data))
                return false;

            if (!ContainsUtf16(data, MetaAbout) || !ContainsUtf16(data, MetaPatchClient)
                || !ContainsUtf16(data, MetaBase))
                return false;

            var streamCheck = DmlTableStreamValidator.Validate(data);
            if (!streamCheck.Ok)
                return false;

            // Metadata-only private-server list (_TableList + About + PatchClient + Base)
            if (streamCheck.TablesParsed == 4)
                return true;

            // Full list with package tables
            if (streamCheck.TablesParsed < 5)
                return false;

            return HasValidPackageFileTypeEncoding(data);
        }

        static bool HasValidPackageFileTypeEncoding(byte[] data)
        {
            // FileType 5 as uint32 FLT: 05 00 00 00
            byte[] marker = { 0x05, 0x00, 0x00, 0x00 };
            int hits = 0;
            for (int i = 0; i <= data.Length - marker.Length; i++)
            {
                if (!MatchesAt(data, marker, i))
                    continue;
                hits++;
                if (hits >= 3)
                    return true;
            }
            return false;
        }

        static void WriteAboutTable(BinaryWriter w)
        {
            DmlTableWriter.WriteTableHeader(w, 1);
            DmlTableWriter.WriteRecordTemplate(w, new[]
            {
                new DmlTableWriter.TemplateField("LatestVersion", DmlTableWriter.TypeUint),
                new DmlTableWriter.TemplateField("Locale", DmlTableWriter.TypeWstr)
            }, MetaAbout);

            ushort recordSize = (ushort)(4 + DmlTableWriter.WstrPayloadBytes("English"));
            DmlTableWriter.WriteRecordHeader(w, recordSize);
            DmlTableWriter.WriteUInt32Field(w, 1);
            DmlTableWriter.WriteWstrField(w, "English");
        }

        static void WritePatchClientTable(BinaryWriter w)
        {
            DmlTableWriter.WriteTableHeader(w, 1);
            DmlTableWriter.WriteRecordTemplate(w, new[]
            {
                new DmlTableWriter.TemplateField("Version", DmlTableWriter.TypeUint)
            }, MetaPatchClient);

            DmlTableWriter.WriteRecordHeader(w, 4);
            DmlTableWriter.WriteUInt32Field(w, 1);
        }

        static void WriteBaseTable(BinaryWriter w)
        {
            DmlTableWriter.WriteTableHeader(w, 1);
            DmlTableWriter.WriteRecordTemplate(w, new[]
            {
                new DmlTableWriter.TemplateField("URLPrefix", DmlTableWriter.TypeWstr)
            }, MetaBase);

            string urlPrefix = DefaultPatchData.HttpBaseUrl;
            DmlTableWriter.WriteRecordHeader(w, (ushort)DmlTableWriter.WstrPayloadBytes(urlPrefix));
            DmlTableWriter.WriteWstrField(w, urlPrefix);
        }

        static void WriteTableListTable(BinaryWriter w, IReadOnlyList<string> names)
        {
            DmlTableWriter.WriteTableHeader(w, (uint)names.Count);
            DmlTableWriter.WriteRecordTemplate(w, new[]
            {
                new DmlTableWriter.TemplateField("Name", DmlTableWriter.TypeWstr)
            }, "_TableList");

            foreach (string name in names)
            {
                DmlTableWriter.WriteRecordHeader(w, (ushort)DmlTableWriter.WstrPayloadBytes(name));
                DmlTableWriter.WriteWstrField(w, name);
            }
        }

        static void WritePackageTable(BinaryWriter w, PackageFileInfo pkg)
        {
            string tar = string.IsNullOrEmpty(pkg.TarFileName) ? pkg.SrcFileName : pkg.TarFileName;

            DmlTableWriter.WriteTableHeader(w, 1);
            DmlTableWriter.WriteRecordTemplate(w, new[]
            {
                new DmlTableWriter.TemplateField("SrcFileName", DmlTableWriter.TypeWstr),
                new DmlTableWriter.TemplateField("TarFileName", DmlTableWriter.TypeWstr),
                new DmlTableWriter.TemplateField("FileType", DmlTableWriter.TypeFlt),
                new DmlTableWriter.TemplateField("Size", DmlTableWriter.TypeFlt),
                new DmlTableWriter.TemplateField("HeaderSize", DmlTableWriter.TypeFlt),
                new DmlTableWriter.TemplateField("CompressedHeaderSize", DmlTableWriter.TypeFlt),
                new DmlTableWriter.TemplateField("CRC", DmlTableWriter.TypeFlt),
                new DmlTableWriter.TemplateField("HeaderCRC", DmlTableWriter.TypeFlt)
            }, pkg.PackageName);

            ushort recordSize = (ushort)(DmlTableWriter.WstrPayloadBytes(pkg.SrcFileName)
                + DmlTableWriter.WstrPayloadBytes(tar)
                + 4 * 6);
            DmlTableWriter.WriteRecordHeader(w, recordSize);
            DmlTableWriter.WriteWstrField(w, pkg.SrcFileName);
            DmlTableWriter.WriteWstrField(w, tar);
            DmlTableWriter.WriteUInt32Field(w, pkg.FileType);
            DmlTableWriter.WriteUInt32Field(w, pkg.Size);
            DmlTableWriter.WriteUInt32Field(w, pkg.HeaderSize);
            DmlTableWriter.WriteUInt32Field(w, pkg.CompressedHeaderSize);
            DmlTableWriter.WriteUInt32Field(w, pkg.Crc);
            DmlTableWriter.WriteUInt32Field(w, pkg.HeaderCrc);
        }

        static bool ContainsUtf16(byte[] data, string value)
        {
            byte[] pattern = Encoding.Unicode.GetBytes(value);
            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                if (MatchesAt(data, pattern, i))
                    return true;
            }
            return false;
        }

        static bool MatchesAt(byte[] data, byte[] pattern, int offset)
        {
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[offset + j] != pattern[j])
                    return false;
            }
            return true;
        }
    }
}
