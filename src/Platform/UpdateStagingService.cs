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
            var hours = LastResultWasFailure(config) ? config.UpdateFailureRetryHours : config.UpdateCheckIntervalHours;
            return utcNow.ToUniversalTime() - last.ToUniversalTime() >= TimeSpan.FromHours(hours);
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

        public static string PeekStagedVersion(RuntimeConfigBundle config, string currentVersion)
        {
            if (config == null || config.PortableMode || !config.UpdateEnabled) return null;
            try
            {
                var statePath = Path.Combine(config.UpdatesDirectory, "staged-update.json");
                if (!File.Exists(statePath)) return null;
                var root = Json.DeserializeObject(File.ReadAllText(statePath)) as Dictionary<string, object>;
                if (root == null || Convert.ToInt32(root["schema_version"], CultureInfo.InvariantCulture) != 1) return null;
                var version = Convert.ToString(root["app_version"], CultureInfo.InvariantCulture);
                if (!AppVersionComparer.IsNewer(version, currentVersion)) return null;
                var relative = Convert.ToString(root["installer_relative_path"], CultureInfo.InvariantCulture);
                var path = ResolveUnder(config.UpdatesDirectory, relative);
                var bytes = Convert.ToInt64(root["installer_bytes"], CultureInfo.InvariantCulture);
                if (!File.Exists(path) || bytes <= 0 || bytes > config.UpdateMaxInstallerBytes || new FileInfo(path).Length != bytes) return null;
                return version;
            }
            catch { return null; }
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

        public static void Cleanup(RuntimeConfigBundle config, string currentVersion, DateTime utcNow)
        {
            if (config == null || config.PortableMode || string.IsNullOrWhiteSpace(config.UpdatesDirectory)) return;
            try
            {
                var root = Path.GetFullPath(config.UpdatesDirectory);
                Directory.CreateDirectory(root);
                DeleteOldFiles(Path.Combine(root, "download"), utcNow.ToUniversalTime().AddHours(-config.UpdateDownloadTempRetentionHours));
                DeleteOldDirectories(Path.Combine(root, "helper-run"), utcNow.ToUniversalTime().AddHours(-config.UpdateDownloadTempRetentionHours), null);

                var activeVersion = ReadStagedVersion(root);
                var stagedRoot = Path.Combine(root, "staged");
                if (Directory.Exists(stagedRoot))
                {
                    var cutoff = utcNow.ToUniversalTime().AddDays(-config.UpdateStagedRetentionDays);
                    foreach (var directory in new DirectoryInfo(stagedRoot).GetDirectories())
                    {
                        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                        var keepActive = !string.IsNullOrWhiteSpace(activeVersion) && string.Equals(directory.Name, activeVersion, StringComparison.OrdinalIgnoreCase);
                        var isCurrent = string.Equals(directory.Name, currentVersion, StringComparison.OrdinalIgnoreCase);
                        if (!keepActive && (isCurrent || directory.LastWriteTimeUtc < cutoff)) SafeDeleteDirectory(stagedRoot, directory.FullName);
                    }
                }
            }
            catch { }
        }

        private static bool LastResultWasFailure(RuntimeConfigBundle config)
        {
            try
            {
                var path = Path.Combine(config.UpdatesDirectory, "last-result.txt");
                if (!File.Exists(path)) return false;
                foreach (var line in File.ReadAllLines(path))
                    if (line.StartsWith("result=update_check_failed:", StringComparison.Ordinal)) return true;
            }
            catch { }
            return false;
        }

        private static string ReadStagedVersion(string updatesRoot)
        {
            try
            {
                var path = Path.Combine(updatesRoot, "staged-update.json");
                if (!File.Exists(path)) return null;
                var root = Json.DeserializeObject(File.ReadAllText(path)) as Dictionary<string, object>;
                object value;
                if (root != null && root.TryGetValue("app_version", out value)) return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            catch { }
            return null;
        }

        private static void DeleteOldFiles(string directory, DateTime cutoffUtc)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var file in new DirectoryInfo(directory).GetFiles())
            {
                try
                {
                    if ((file.Attributes & FileAttributes.ReparsePoint) == 0 && file.LastWriteTimeUtc < cutoffUtc) file.Delete();
                }
                catch { }
            }
        }

        private static void DeleteOldDirectories(string root, DateTime cutoffUtc, string keepName)
        {
            if (!Directory.Exists(root)) return;
            foreach (var directory in new DirectoryInfo(root).GetDirectories())
            {
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                if (!string.IsNullOrWhiteSpace(keepName) && string.Equals(directory.Name, keepName, StringComparison.OrdinalIgnoreCase)) continue;
                if (directory.LastWriteTimeUtc < cutoffUtc) SafeDeleteDirectory(root, directory.FullName);
            }
        }

        private static void SafeDeleteDirectory(string root, string path)
        {
            try
            {
                var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!normalized.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) return;
                var info = new DirectoryInfo(path);
                if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0) return;
                info.Delete(true);
            }
            catch { }
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
