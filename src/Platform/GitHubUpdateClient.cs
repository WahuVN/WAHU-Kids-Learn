using System;
using System.IO;
using System.Net;

namespace WAHU.Platform
{
    public sealed class GitHubUpdateClient
    {
        public bool TryCheckAndStage(RuntimeConfigBundle config, string currentVersion, out string result)
        {
            result = "skipped";
            if (config == null || config.PortableMode || !config.UpdateEnabled || !config.UpdateCheckOnStartup || !config.UpdateAutoDownload) return false;
            var now = DateTime.UtcNow;
            if (!UpdateStagingService.IsCheckDue(config, now)) { result = "not_due"; return false; }
            UpdateStagingService.MarkCheckAttempt(config, now);
            Directory.CreateDirectory(config.UpdatesDirectory);
            try
            {
                EnableTls12();
                var manifestText = DownloadText(config.UpdateManifestUrl, config.UpdateConnectTimeoutMs, config.UpdateReadTimeoutMs, config.UpdateMaxManifestBytes);
                var manifest = UpdateManifestParser.ParseAndValidate(manifestText, config);
                if (!AppVersionComparer.IsNewer(manifest.AppVersion, currentVersion)) { result = "current"; return false; }
                var tempDir = Path.Combine(config.UpdatesDirectory, "download");
                Directory.CreateDirectory(tempDir);
                var temp = Path.Combine(tempDir, "installer-" + Guid.NewGuid().ToString("N") + ".exe");
                try
                {
                    DownloadFile(manifest.InstallerUrl, temp, config.UpdateConnectTimeoutMs, config.UpdateDownloadTimeoutMs, config.UpdateMaxInstallerBytes);
                    UpdateStagingService.StageVerifiedInstaller(config, manifest, temp);
                    result = "staged:" + manifest.AppVersion;
                    return true;
                }
                finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
            }
            catch (Exception ex)
            {
                result = "update_check_failed:" + ex.GetType().Name;
                WriteResult(config, result, ex.Message);
                return false;
            }
        }

        private static string DownloadText(string url, int connectTimeoutMs, int readTimeoutMs, long maxBytes)
        {
            var request = Create(url, connectTimeoutMs, readTimeoutMs);
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                if (response.ContentLength > maxBytes) throw new InvalidDataException("Manifest Content-Length exceeds limit.");
                CopyBounded(stream, ms, maxBytes);
                return System.Text.Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private static void DownloadFile(string url, string destination, int connectTimeoutMs, int readTimeoutMs, long maxBytes)
        {
            var request = Create(url, connectTimeoutMs, readTimeoutMs);
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (response.ContentLength > maxBytes) throw new InvalidDataException("Installer Content-Length exceeds limit.");
                CopyBounded(stream, file, maxBytes);
                file.Flush(true);
            }
        }

        private static HttpWebRequest Create(string url, int connectTimeoutMs, int readTimeoutMs)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.AllowAutoRedirect = true;
            request.MaximumAutomaticRedirections = 5;
            request.Timeout = connectTimeoutMs;
            request.ReadWriteTimeout = readTimeoutMs;
            request.UserAgent = "WAHU-Kids-Learn-Updater/1";
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Proxy = WebRequest.DefaultWebProxy;
            return request;
        }

        private static void CopyBounded(Stream source, Stream destination, long maxBytes)
        {
            var buffer = new byte[64 * 1024];
            long total = 0;
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                if (total > maxBytes) throw new InvalidDataException("Download exceeded configured size limit.");
                destination.Write(buffer, 0, read);
            }
        }

        private static void EnableTls12()
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
        }

        private static void WriteResult(RuntimeConfigBundle config, string result, string detail)
        {
            try
            {
                Directory.CreateDirectory(config.UpdatesDirectory);
                File.WriteAllLines(Path.Combine(config.UpdatesDirectory, "last-result.txt"), new[]
                {
                    "captured_at_utc=" + DateTime.UtcNow.ToString("o"), "result=" + result, "detail=" + (detail ?? string.Empty)
                });
            }
            catch { }
        }
    }
}
