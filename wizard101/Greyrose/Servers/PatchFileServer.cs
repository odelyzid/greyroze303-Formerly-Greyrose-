using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Greyrose.Data;

namespace Greyrose
{
    partial class Server
    {
        static HttpListener _patchHttp;
        static CancellationTokenSource _patchHttpCts;

        public static void StartPatchFileServer()
        {
            if (_patchHttp != null)
                return;

            PatchData.EnsurePatchFiles();
            string prefix = DefaultPatchData.HttpBaseUrl;
            _patchHttp = new HttpListener();
            _patchHttp.Prefixes.Add(prefix);
            _patchHttpCts = new CancellationTokenSource();

            try
            {
                _patchHttp.Start();
            }
            catch (HttpListenerException ex)
            {
                ServerLog.WriteLine("Patch HTTP: failed to start on {0} ({1})", prefix, ex.Message);
                _patchHttp = null;
                _patchHttpCts = null;
                return;
            }

            ServerLog.WriteLine("Patch HTTP: serving {0}", PatchData.GetPatchDirectory());
            _ = Task.Run(() => RunPatchFileServer(_patchHttpCts.Token));
        }

        public static void StopPatchFileServer()
        {
            try { _patchHttpCts?.Cancel(); } catch { }
            try { _patchHttp?.Stop(); } catch { }
            try { _patchHttp?.Close(); } catch { }
            _patchHttp = null;
            _patchHttpCts = null;
        }

        static byte[] ReadPatchFileBytes(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        static async Task RunPatchFileServer(CancellationToken cancel)
        {
            string root = PatchData.GetPatchDirectory();

            while (!cancel.IsCancellationRequested)
            {
                HttpListenerContext ctx = null;
                try
                {
                    ctx = await _patchHttp.GetContextAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    if (cancel.IsCancellationRequested)
                        break;
                    continue;
                }

                if (ctx == null)
                    continue;

                try
                {
                    string name = Path.GetFileName(ctx.Request.Url.LocalPath);
                    if (string.IsNullOrEmpty(name))
                        name = DefaultPatchData.ListFileName;

                    string path = Path.Combine(root, name);
                    if (!File.Exists(path))
                    {
                        ctx.Response.StatusCode = 404;
                        ctx.Response.Close();
                        continue;
                    }

                    byte[] body = ReadPatchFileBytes(path);
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/octet-stream";
                    ctx.Response.ContentLength64 = body.Length;
                    ctx.Response.OutputStream.Write(body, 0, body.Length);
                    ctx.Response.Close();
                    ServerLog.WriteLine("Patch HTTP: served {0} ({1} bytes)", name, body.Length);
                }
                catch (Exception ex)
                {
                    ServerLog.WriteLine("Patch HTTP: error: {0}", ex.Message);
                    try { ctx.Response.Abort(); } catch { }
                }
            }
        }
    }
}
