using System.IO;

namespace Greyrose.Data
{
    /// <summary>
    /// Builds _ServerList DML table stream for MSG_SERVERLIST (svc 7, msg 11).
    /// Fields match WizardGraphicalClient: ServerSelectState (INT), ZoneName (STR).
    /// </summary>
    static class DmlServerListBuilder
    {
        public const string TableName = "_ServerList";

        /// <summary>Full table stream for one selectable realm row.</summary>
        public static byte[] BuildRowTable(string zoneName, int serverSelectState = 1)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);

            DmlTableWriter.WriteTableHeader(w, 1);
            DmlTableWriter.WriteRecordTemplate(w, new[]
            {
                new DmlTableWriter.TemplateField("ServerSelectState", DmlTableWriter.TypeInt),
                new DmlTableWriter.TemplateField("ZoneName", DmlTableWriter.TypeStr)
            }, TableName);

            ushort recordSize = (ushort)(4 + DmlTableWriter.StrPayloadBytes(zoneName));
            DmlTableWriter.WriteRecordHeader(w, recordSize);
            DmlTableWriter.WriteInt32Field(w, serverSelectState);
            DmlTableWriter.WriteStringField(w, zoneName);

            return ms.ToArray();
        }

        /// <summary>4-byte table terminator (record count 0).</summary>
        public static byte[] BuildEndMarker() => new byte[] { 0, 0, 0, 0 };
    }
}
