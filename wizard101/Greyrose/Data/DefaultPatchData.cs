namespace Greyrose.Data
{
    static class DefaultPatchData
    {
        public const int HttpPort = 12501;
        public const uint LatestVersion = 1;
        public const string ListFileName = "LatestFileList.bin";
        public const uint ListFileType = 1;
        public const string URLSuffix = "";

        public static string HttpBaseUrl => "http://127.0.0.1:" + HttpPort + "/";
        public static string ListFileUrl => HttpBaseUrl + ListFileName;
    }
}
