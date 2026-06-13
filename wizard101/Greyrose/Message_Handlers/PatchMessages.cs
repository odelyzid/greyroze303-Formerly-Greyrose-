using System;
using System.IO;
using Greyrose.Data;

namespace Greyrose
{
    partial class Handlers
    {
        public static byte[] _8PatchMessages(BinaryReader data)
        {
            uint msgid = DataHandler.MSGID(data);
            uint msglen = DataHandler.USHRT(data);

            if (msgid == 1)
            {
                ServerLog.WriteLine("MSG: Latest File List");
                uint latestVersion = DataHandler.UINT(data);
                string listFileName = DataHandler.STR(data);
                uint listFileType = DataHandler.UINT(data);
                uint listFileTime = DataHandler.UINT(data);
                uint listFileSize = DataHandler.UINT(data);
                uint listFileCrc = DataHandler.UINT(data);
                string listFileUrl = DataHandler.STR(data);
                string urlPrefix = DataHandler.STR(data);
                string urlSuffix = DataHandler.STR(data);

                ServerLog.WriteLine("Latest Version: {0}", latestVersion);
                ServerLog.WriteLine("File Name: {0}", listFileName);
                ServerLog.WriteLine("Locale request (V1): ignored");

                return BuildLatestFileListResponse(1, "English");
            }
            else if (msgid == 2)
            {
                ServerLog.WriteLine("MSG: Latest File List V2");

                uint latestVersion = DataHandler.UINT(data);
                string listFileName = DataHandler.STR(data);
                uint listFileType = DataHandler.UINT(data);
                uint listFileTime = DataHandler.UINT(data);
                uint listFileSize = DataHandler.UINT(data);
                uint listFileCrc = DataHandler.UINT(data);
                string listFileUrl = DataHandler.STR(data);
                string urlPrefix = DataHandler.STR(data);
                string urlSuffix = DataHandler.STR(data);
                string locale = DataHandler.STR(data);

                ServerLog.WriteLine("Client request: Ver={0}, File={1}, Locale={2}",
                    latestVersion, listFileName, locale);

                byte[] response = BuildLatestFileListResponse(2, locale);
                var meta = PatchData.GetFileListMetadata();
                ServerLog.WriteLine("PATCH FILE LIST SENT: Ver={0}, Size={1}, CRC={2:X8}, URL={3}",
                    meta.LatestVersion, meta.ListFileSize, meta.ListFileCRC, meta.ListFileURL);
                return response;
            }
            else if (msgid == 3)
            {
                ServerLog.WriteLine("MSG: Next Version");

                string pkgName = DataHandler.STR(data);
                int version = DataHandler.INT(data);
                string urlPrefix = DataHandler.STR(data);
                string fileName = DataHandler.STR(data);
                int fileType = DataHandler.INT(data);

                string normalized = LocalPackageCatalog.NormalizePackageName(pkgName);
                bool known = LocalPackageCatalog.IsKnownPackage(normalized);
                int responseVersion = known ? version : 0;

                ServerLog.WriteLine("PkgName: {0} (normalized: {1}), Version: {2}, known: {3}, response: {4}",
                    pkgName, normalized, version, known, responseVersion);

                var p = new KIPacket();
                p.Header(0, 0, 8, 3);
                p._STR(pkgName ?? "");
                p._INT(responseVersion);
                p._STR(urlPrefix ?? "");
                p._STR(fileName ?? "");
                p._INT(fileType);
                return p.Finalise();
            }

            return null;
        }

        static byte[] BuildLatestFileListResponse(uint msgid, string locale)
        {
            if (string.IsNullOrEmpty(locale))
                locale = "English";

            var meta = PatchData.GetFileListMetadata();
            var p = new KIPacket();
            p.Header(0, 0, 8, msgid);
            p._UINT(meta.LatestVersion);
            p._STR(meta.ListFileName);
            p._UINT(meta.ListFileType);
            p._UINT(meta.ListFileTime);
            p._UINT(meta.ListFileSize);
            p._UINT(meta.ListFileCRC);
            p._STR(meta.ListFileURL);
            p._STR(meta.URLPrefix);
            p._STR(meta.URLSuffix);
            if (msgid == 2)
                p._STR(locale);
            return p.Finalise();
        }
    }
}
