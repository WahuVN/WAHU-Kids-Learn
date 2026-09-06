using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace WAHU.Platform
{
    public static class UpdateStagingService
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024, RecursionLimit = 32 };

        public static bool IsCheckDue(RuntimeConfigBundle config, DateTime utcNow)
        {
            if (config == null || !config.UpdateEnabled || config.PortableMode) return false;
            var path = Path.Combine(config.UpdatesDirectory, "last-check-utc.txt");
            if (!File.Exists(path)) return true;
            DateTime last;
            if (!DateTime.TryParse(File.ReadAllText(path).Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out last)) return true;
            return utcNow.ToUniversalTime() - last.ToUniversalTime() >= TimeSpan.FromHours(config.UpdateCheckIntervalHours);
        }

        public static void MarkCheckAttempt(RuntimeConfigBundle config, DateTime utcNow)
        {
            Directory.CreateDirectory(config.UpdatesDirectory);
            WriteTextAtomic(Path.Combine(config.UpdatesDirectory, "last-check-utc.txt"), utcNow.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
        }

        public static StagedUpdate StageVerifiedInstaller(RuntimeConfigBundle config, UpdateManifest manifest, string downloadedPath)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (manifest == null) throw new ArgumentNullException("manifest");
            if (config.PortableMode) throw new InvalidOperationException("Portable mode does not auto-stage binary updates.");
            if (!File.Exists(downloadedPath)) throw new FileNotFoundException("Downloaded installer missing.", downloadedPath);
            var info = new FileInfo(downloadedPath);
            if (info.Length != manifest.InstallerBytes || info.Length > config.UpdateMaxInstallerBytes) throw new InvalidDataException("Downloaded installer size mismatch.");
            var hash = Hashing.Sha256File(downloadedPath);
            if (!string.Equals(hash, manifest.InstallerSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Downloaded installer SHA-256 mismatch.");
            VerifySignaturePolicy(config, manifest, downloadedPath);

            var versionSegment = SafeVersionSegment(manifest.AppVersion);
            var versionDir = Path.Combine(config.UpdatesDirectory, "staged", versionSegment);
            Directory.CreateDirectory(versionDir);
            var finalInstaller = Path.Combine(versionDir, Path.GetFileName(new Uri(manifest.InstallerUrl).AbsolutePath));
            var temp = finalInstaller + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                File.Copy(downloadedPath, temp, true);
                if (!string.Equals(Hashing.Sha256File(temp), hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Staged installer copy hash mismatch.");
                if (File.Exists(finalInstaller)) File.Delete(finalInstaller);
                File.Move(temp, finalInstaller);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }

            var staged = new StagedUpdate
            {
                AppVersion = manifest.AppVersion,
                InstallerPath = finalInstaller,
                InstallerSha256 = hash,
                InstallerBytes = info.Length,
                ProductionSigned = manifest.ProductionSigned,
                StagedAtUtc = DateTime.UtcNow
            };
            var state = new Dictionary<string, object>
            {
                { "schema_version", 1 }, { "app_version", staged.AppVersion },
                { "installer_relative_path", RelativeUnder(config.UpdatesDirectory, finalInstaller) },
                { "installer_sha256", staged.InstallerSha256 }, { "installer_bytes", staged.InstallerBytes },
                { "production_signed", staged.ProductionSigned }, { "staged_at_utc", staged.StagedAtUtc.ToString("o", CultureInfo.InvariantCulture) }
            };
            WriteTextAtomic(Path.Combine(config.UpdatesDirectory, "staged-update.json"), Json.Serialize(state));
            return staged;
        }

        public static bool TryGetStagedUpdate(RuntimeConfigBundle config, string currentVersion, out StagedUpdate staged)
        {
            staged = null;
            if (config == null || config.PortableMode || !config.UpdateEnabled) return false;
            var statePath = Path.Combine(config.UpdatesDirectory, "staged-update.json");
            if (!File.Exists(statePath)) return false;
            try
            {
                var root = Json.DeserializeObject(File.ReadAllText(statePath)) as Dictionary<string, object>;
                if (root == null || Convert.ToInt32(root["schema_version"], CultureInfo.InvariantCulture) != 1) return false;
                var version = Convert.ToString(root["app_version"], CultureInfo.InvariantCulture);
                if (!AppVersionComparer.IsNewer(version, currentVersion)) { ClearStaged(config); return false; }
                var relative = Convert.ToString(root["installer_relative_path"], CultureInfo.InvariantCulture);
                var path = ResolveUnder(config.UpdatesDirectory, relative);
                var sha = Convert.ToString(root["installer_sha256"], CultureInfo.InvariantCulture).ToUpperInvariant();
                var bytes = Convert.ToInt64(root["installer_bytes"], CultureInfo.InvariantCulture);
                var signed = Convert.ToBoolean(root["production_signed"], CultureInfo.InvariantCulture);
                DateTime at; DateTime.TryParse(Convert.ToString(root["staged_at_utc"], CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out at);
                if (!File.Exists(path) || new FileInfo(path).Length != bytes || bytes <= 0 || bytes > config.UpdateMaxInstallerBytes) { ClearStaged(config); return false; }
                if (!string.Equals(Hashing.Sha256File(path), sha, StringComparison.OrdinalIgnoreCase)) { ClearStaged(config); return false; }
                if (config.UpdateChannel == "stable" && config.UpdateAuthenticodeRequiredForProduction)
                {
                    if (!signed) { ClearStaged(config); return false; }
                    var sig = SignatureVerifier.CheckCacheOnly(path);
                    if (sig.SignatureDigestValid != true || sig.PublisherChainTrusted != true) { ClearStaged(config); return false; }
                }
                staged = new StagedUpdate { AppVersion = version, InstallerPath = path, InstallerSha256 = sha, InstallerBytes = bytes, ProductionSigned = signed, StagedAtUtc = at };
                return true;
            }
            catch { ClearStaged(config); return false; }
        }

        public static void ClearStaged(RuntimeConfigBundle config)
        {
            try { var path = Path.Combine(config.UpdatesDirectory, "staged-update.json"); if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void VerifySignaturePolicy(RuntimeConfigBundle config, UpdateManifest manifest, string path)
        {
            if (config.UpdateChannel == "stable" && config.UpdateAuthenticodeRequiredForProduction)
            {
                if (!manifest.ProductionSigned) throw new InvalidDataException("Production update is not declared signed.");
                var sig = SignatureVerifier.CheckCacheOnly(path);
                if (sig.SignatureDigestValid != true || sig.PublisherChainTrusted != true) throw new InvalidDataException("Production update Authenticode verification failed: " + sig.Note);
            }
            else if (!config.UpdateAllowUnsignedDevBuilds)
            {
                var sig = SignatureVerifier.CheckCacheOnly(path);
                if (sig.SignatureDigestValid != true) throw new InvalidDataException("Dev update signature required by policy.");
            }
        }

        private static string SafeVersionSegment(string version)
        {
            foreach (var c in version) if (!(char.IsLetterOrDigit(c) || c == '.' || c == '-')) throw new InvalidDataException("Unsafe staged update version.");
            return version;
        }
        private static string RelativeUnder(string root, string path)
        {
            var r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var p = Path.GetFullPath(path); if (!p.StartsWith(r, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Staged path outside updates root.");
            return p.Substring(r.Length);
        }
        private static string ResolveUnder(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("Invalid staged relative path.");
            var r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var p = Path.GetFullPath(Path.Combine(root, relative)); if (!p.StartsWith(r, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Staged path escapes updates root.");
            return p;
        }
        private static void WriteTextAtomic(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp = path + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temp, text);
                if (File.Exists(path)) { try { File.Replace(temp, path, null); return; } catch { File.Delete(path); } }
                File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
