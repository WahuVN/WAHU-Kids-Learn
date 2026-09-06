using System;
using System.IO;
using System.Net;
using System.Diagnostics;

namespace WAHU.Platform
{
    public sealed class GitHubUpdateClient
    {
        private static readonly object CheckGate = new object();
        public bool TryCheckAndStage(RuntimeConfigBundle config, string currentVersion, out string result)
        {
            return TryCheckAndStage(config, currentVersion, false, out result);
        }

        public bool TryCheckAndStage(RuntimeConfigBundle config, string currentVersion, bool force, out string result)
        {
            lock (CheckGate)
            {
            result = "skipped";
            if (config == null) { result = "disabled:no_config"; return false; }
            if (config.PortableMode) { result = "portable_manual_only"; return false; }
            if (!config.UpdateEnabled || !config.UpdateCheckOnStartup || !config.UpdateAutoDownload) { result = "disabled"; return false; }

            StagedUpdate existing;
            if (UpdateStagingService.TryGetStagedUpdate(config, currentVersion, out existing))
            {
                result = "already_staged:" + existing.AppVersion;
                UpdateStatusService.WriteResult(config, result, string.Empty);
                return true;
            }

            var now = DateTime.UtcNow;
            if (!force && !UpdateStagingService.IsCheckDue(config, now)) { result = "not_due"; return false; }
            UpdateStagingService.MarkCheckAttempt(config, now);
            UpdateStagingService.Cleanup(config, currentVersion, now);
            Directory.CreateDirectory(config.UpdatesDirectory);
            try
            {
                EnableTls12();
                var manifestText = DownloadText(config.UpdateManifestUrl, config.UpdateConnectTimeoutMs, config.UpdateReadTimeoutMs, config.UpdateMaxManifestBytes);
                var manifest = UpdateManifestParser.ParseAndValidate(manifestText, config);
                if (!AppVersionComparer.IsNewer(manifest.AppVersion, currentVersion))
                {
                    result = "current:" + manifest.AppVersion;
                    UpdateStatusService.WriteResult(config, result, string.Empty);
                    return false;
                }
                var tempDir = Path.Combine(config.UpdatesDirectory, "download");
                Directory.CreateDirectory(tempDir);
                var temp = Path.Combine(tempDir, "installer-" + Guid.NewGuid().ToString("N") + ".exe");
                try
                {
                    DownloadFile(manifest.InstallerUrl, temp, config.UpdateConnectTimeoutMs, config.UpdateDownloadTimeoutMs, config.UpdateMaxInstallerBytes);
                    UpdateStagingService.StageVerifiedInstaller(config, manifest, temp);
                    result = "staged:" + manifest.AppVersion;
                    UpdateStatusService.WriteResult(config, result, string.Empty);
                    return true;
                }
                finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
            }
            catch (Exception ex)
            {
                result = "update_check_failed:" + ex.GetType().Name;
                UpdateStatusService.WriteResult(config, result, ex.Message);
                return false;
            }
            }
        }

        private static string DownloadText(string url, int connectTimeoutMs, int readTimeoutMs, long maxBytes)
        {
            var request = Create(url, connectTimeoutMs, readTimeoutMs);
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                ValidateFinalResponse(response);
                if (response.ContentLength > maxBytes) throw new InvalidDataException("Manifest Content-Length exceeds limit.");
                CopyBounded(stream, ms, maxBytes, readTimeoutMs);
                return System.Text.Encoding.UTF8.GetString(ms.ToArray()).TrimStart('\uFEFF');
            }
        }

        private static void DownloadFile(string url, string destination, int connectTimeoutMs, int readTimeoutMs, long maxBytes)
        {
            var request = Create(url, connectTimeoutMs, readTimeoutMs);
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ValidateFinalResponse(response);
                if (response.ContentLength > maxBytes) throw new InvalidDataException("Installer Content-Length exceeds limit.");
                CopyBounded(stream, file, maxBytes, readTimeoutMs);
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

        private static void CopyBounded(Stream source, Stream destination, long maxBytes, int totalTimeoutMs)
        {
            var buffer = new byte[64 * 1024];
            long total = 0;
            int read;
            var elapsed = Stopwatch.StartNew();
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (elapsed.ElapsedMilliseconds > totalTimeoutMs) throw new TimeoutException("Download exceeded configured total timeout.");
                total += read;
                if (total > maxBytes) throw new InvalidDataException("Download exceeded configured size limit.");
                destination.Write(buffer, 0, read);
            }
            if (elapsed.ElapsedMilliseconds > totalTimeoutMs) throw new TimeoutException("Download exceeded configured total timeout.");
        }

        private static void ValidateFinalResponse(HttpWebResponse response)
        {
            if (response == null || response.ResponseUri == null || response.ResponseUri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidDataException("Update response must remain HTTPS.");
            var host = response.ResponseUri.Host;
            if (!string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(host, "release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(host, "objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Unexpected final update host: " + host);
        }

        private static void EnableTls12()
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
        }

    }
}
