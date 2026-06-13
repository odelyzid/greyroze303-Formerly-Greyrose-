using System;
using System.IO;
using System.Text;

namespace Greyrose.Data
{
    /// <summary>
    /// Validates DML table-stream binaries by walking record boundaries.
    /// </summary>
    static class DmlTableStreamValidator
    {
        public sealed class ValidationResult
        {
            public bool Ok { get; set; }
            public string Error { get; set; }
            public int TablesParsed { get; set; }
            public int Offset { get; set; }
        }

        public static ValidationResult Validate(byte[] data)
        {
            var result = new ValidationResult();
            if (data == null || data.Length < 6)
            {
                result.Error = "too short";
                return result;
            }

            if (BitConverter.ToUInt32(data, 0) == KiBinaryXml.BindSignature)
            {
                result.Error = "BINd file, not DML table stream";
                return result;
            }

            int offset = 0;
            try
            {
                while (offset < data.Length)
                {
                    if (offset + 6 > data.Length)
                    {
                        result.Error = $"truncated table header at {offset}";
                        result.Offset = offset;
                        return result;
                    }

                    uint recordCount = BitConverter.ToUInt32(data, offset);
                    offset += 4;

                    if (recordCount == 0)
                    {
                        result.TablesParsed++;
                        continue;
                    }

                    if (data[offset] != 0x02 || data[offset + 1] != DmlTableWriter.TypeRecordTemplate)
                    {
                        result.Error = $"bad template marker at {offset - 4} (count={recordCount})";
                        result.Offset = offset;
                        return result;
                    }
                    offset += 2;

                    if (offset + 2 > data.Length)
                    {
                        result.Error = $"truncated template length at {offset}";
                        result.Offset = offset;
                        return result;
                    }

                    ushort templateLen = BitConverter.ToUInt16(data, offset);
                    offset += 2;
                    if (offset + templateLen > data.Length)
                    {
                        result.Error = $"template overruns at {offset} (len={templateLen})";
                        result.Offset = offset;
                        return result;
                    }
                    offset += templateLen;

                    for (uint r = 0; r < recordCount; r++)
                    {
                        if (offset + 4 > data.Length)
                        {
                            result.Error = $"truncated record header at {offset} (row {r}/{recordCount})";
                            result.Offset = offset;
                            return result;
                        }

                        if (data[offset] != 0x02 || data[offset + 1] != DmlTableWriter.TypeRecord)
                        {
                            result.Error = $"bad record marker at {offset} (row {r}/{recordCount})";
                            result.Offset = offset;
                            return result;
                        }
                        offset += 2;

                        ushort recordSize = BitConverter.ToUInt16(data, offset);
                        offset += 2;
                        if (offset + recordSize > data.Length)
                        {
                            result.Error = $"record overruns at {offset} (size={recordSize}, row {r}/{recordCount})";
                            result.Offset = offset;
                            return result;
                        }
                        offset += recordSize;
                    }

                    result.TablesParsed++;
                }

                result.Ok = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.Offset = offset;
                return result;
            }
        }

        public static string DescribeTables(byte[] data, int maxTables = 8)
        {
            var sb = new StringBuilder();
            var vr = Validate(data);
            sb.Append(vr.Ok ? "OK" : "FAIL").Append(": ").Append(vr.Error ?? "valid");
            sb.Append(", tables=").Append(vr.TablesParsed);
            return sb.ToString();
        }
    }
}
