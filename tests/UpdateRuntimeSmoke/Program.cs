using System;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using WAHU.Platform;

namespace WAHU.UpdateRuntimeSmoke
{
    internal static class Program
    {
        private static int _passed;

        private static int Main(string[] args)
        {
            if (HasArg(args, "--stage-local")) return RunStageLocal(args);
            if (HasArg(args, "--live")) return RunLive(args);
            var temp = Path.Combine(Path.GetTempPath(), "wahu-update-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                var config = RuntimeConfigBundle.Load(FindConfig());
                Set(config, "UpdatesDirectory", temp);
                Set(config, "PortableMode", false);
                Assert(config.UpdateEnabled && config.UpdateCheckOnStartup && config.UpdateAutoDownload && config.UpdateAutoInstallOnNextStart, "policy_enabled");
                Assert(config.UpdateManifestUrl == "https://github.com/WahuVN/WAHU-Kids-Learn/releases/download/update-dev/update-manifest.json", "manifest_url_locked");
                Assert(config.UpdateFailureRetryHours == 1 && config.UpdateCheckIntervalHours == 6, "retry_policy");
                Assert(config.UpdateStagedRetentionDays == 7 && config.UpdateDownloadTempRetentionHours == 24, "retention_policy");
                Assert(config.LaunchWithWindowsDefault, "startup_default_enabled");
                Assert(StartupRegistrationService.BuildCommand(@"C:\Program Files\WAHU Kids Learn\WAHUKidsLearn.exe") == "\"C:\\Program Files\\WAHU Kids Learn\\WAHUKidsLearn.exe\" --startup", "startup_command_quoted");
                Assert(AppVersionComparer.IsNewer("0.1.20-dev", "0.1.19-dev"), "version_patch_newer");
                Assert(!AppVersionComparer.IsNewer("0.1.19-dev", "0.1.19-dev"), "version_equal_not_newer");
                Assert(AppVersionComparer.IsNewer("0.1.19", "0.1.19-dev"), "stable_newer_than_prerelease");
                Assert(AppVersionComparer.IsNewer("1.0.0", "0.99.99"), "version_major_newer");

                var fake = Path.Combine(temp, "download.exe");
                File.WriteAllBytes(fake, new byte[] { 1,2,3,4,5,6,7,8,9 });
                var hash = Hashing.Sha256File(fake);
                var manifestJson = Manifest("0.1.20-dev", hash, new FileInfo(fake).Length,
                    "https://github.com/WahuVN/WAHU-Kids-Learn/releases/download/v0.1.20-dev/WAHU-Kids-Learn-Setup-win7-x86-0.1.20-dev.exe");
                var manifest = UpdateManifestParser.ParseAndValidate(manifestJson, config);
                Assert(manifest.AppVersion == "0.1.20-dev", "manifest_valid");
                Assert(UpdateManifestParser.ParseAndValidate("\uFEFF" + manifestJson, config).AppVersion == "0.1.20-dev", "manifest_utf8_bom_valid");
                AssertRejected(() => UpdateManifestParser.ParseAndValidate(manifestJson.Replace("https://github.com/WahuVN/WAHU-Kids-Learn/releases/download/", "https://example.com/"), config), "manifest_wrong_origin_rejected");
                AssertRejected(() => UpdateManifestParser.ParseAndValidate(manifestJson.Replace("https://github.com/", "http://github.com/"), config), "manifest_http_rejected");
                AssertRejected(() => UpdateManifestParser.ParseAndValidate(manifestJson.Replace(hash, "BAD"), config), "manifest_bad_hash_rejected");

                Set(config, "UpdateChannel", "stable");
                var stableManifestJson = manifestJson.Replace("\"channel\":\"dev\"", "\"channel\":\"stable\"");
                AssertRejected(() => UpdateManifestParser.ParseAndValidate(stableManifestJson, config), "stable_unsigned_manifest_rejected");
                Set(config, "UpdateChannel", "dev");

                var staged = UpdateStagingService.StageVerifiedInstaller(config, manifest, fake);
                Assert(File.Exists(staged.InstallerPath), "installer_staged");
                StagedUpdate loaded;
                Assert(UpdateStagingService.TryGetStagedUpdate(config, "0.1.19-dev", out loaded), "staged_reload_valid");
                Assert(loaded.AppVersion == "0.1.20-dev" && loaded.InstallerSha256 == hash, "staged_metadata_valid");
                var status = UpdateStatusService.Read(config, @"C:\WAHU\WAHUKidsLearn.exe");
                Assert(status.StagedVersion == "0.1.20-dev", "status_reads_staged_version");
                File.AppendAllText(loaded.InstallerPath, "tamper");
                Assert(!UpdateStagingService.TryGetStagedUpdate(config, "0.1.19-dev", out loaded), "staged_tamper_rejected");

                var now = DateTime.UtcNow;
                Assert(UpdateStagingService.IsCheckDue(config, now), "first_check_due");
                UpdateStagingService.MarkCheckAttempt(config, now);
                UpdateStatusService.WriteResult(config, "current:0.1.20-dev", string.Empty);
                Assert(!UpdateStagingService.IsCheckDue(config, now.AddHours(1)), "success_check_throttled");
                Assert(UpdateStagingService.IsCheckDue(config, now.AddHours(7)), "success_check_due_after_interval");
                UpdateStagingService.MarkCheckAttempt(config, now);
                UpdateStatusService.WriteResult(config, "update_check_failed:WebException", "offline");
                Assert(!UpdateStagingService.IsCheckDue(config, now.AddMinutes(30)), "failure_retry_not_too_soon");
                Assert(UpdateStagingService.IsCheckDue(config, now.AddHours(2)), "failure_retry_after_one_hour");
                status = UpdateStatusService.Read(config, @"C:\WAHU\WAHUKidsLearn.exe");
                Assert(status.LastResult == "update_check_failed:WebException", "status_reads_last_result");
                Assert(status.LastCheckUtc.HasValue, "status_reads_last_check");

                TestCleanup(config, now);

                Set(config, "PortableMode", true);
                string portableResult;
                Assert(!new GitHubUpdateClient().TryCheckAndStage(config, "0.1.19-dev", true, out portableResult) && portableResult == "portable_manual_only", "portable_never_auto_updates_binary");

                Console.WriteLine("UPDATE_RUNTIME_SMOKE_PASS assertions=" + _passed);
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine("UPDATE_RUNTIME_SMOKE_FAIL " + ex); return 1; }
            finally { try { Directory.Delete(temp, true); } catch { } }
        }

        private static int RunStageLocal(string[] args)
        {
            try
            {
                var configDir = RequiredArg(args, "--config-dir");
                var appBase = RequiredArg(args, "--app-base");
                var installer = Path.GetFullPath(RequiredArg(args, "--installer"));
                var version = RequiredArg(args, "--version");
                var config = RuntimeConfigBundle.Load(configDir, appBase, false);
                if (!File.Exists(installer)) throw new FileNotFoundException("Local installer missing.", installer);
                var manifest = new UpdateManifest
                {
                    SchemaVersion = 1,
                    AppVersion = version,
                    Channel = config.UpdateChannel,
                    InstallerUrl = "https://github.com/WahuVN/WAHU-Kids-Learn/releases/download/v" + version + "/WAHU-Kids-Learn-Setup-win7-x86-" + version + ".exe",
                    InstallerSha256 = Hashing.Sha256File(installer),
                    InstallerBytes = new FileInfo(installer).Length,
                    ProductionSigned = false,
                    MinimumWindows = "6.1sp1"
                };
                var staged = UpdateStagingService.StageVerifiedInstaller(config, manifest, installer);
                Console.WriteLine("UPDATE_STAGE_LOCAL_PASS version=" + staged.AppVersion + " path=" + staged.InstallerPath + " sha256=" + staged.InstallerSha256);
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine("UPDATE_STAGE_LOCAL_FAIL " + ex); return 1; }
        }

        private static int RunLive(string[] args)
        {
            var temp = Path.Combine(Path.GetTempPath(), "wahu-update-live-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                var config = RuntimeConfigBundle.Load(FindConfig());
                Set(config, "UpdatesDirectory", temp);
                Set(config, "PortableMode", false);
                var feedUrl = GetArgValue(args, "--feed-url");
                var channel = GetArgValue(args, "--channel");
                var expectedVersion = GetArgValue(args, "--expect-version");
                if (!string.IsNullOrWhiteSpace(feedUrl)) Set(config, "UpdateManifestUrl", feedUrl);
                if (!string.IsNullOrWhiteSpace(channel)) Set(config, "UpdateChannel", channel);
                string result;
                var staged = new GitHubUpdateClient().TryCheckAndStage(config, "0.0.0-dev", true, out result);
                Assert(staged, "live_update_staged");
                Assert(result.StartsWith("staged:", StringComparison.Ordinal), "live_result_staged");
                StagedUpdate loaded;
                Assert(UpdateStagingService.TryGetStagedUpdate(config, "0.0.0-dev", out loaded), "live_staged_reload");
                if (!string.IsNullOrWhiteSpace(expectedVersion)) Assert(loaded.AppVersion == expectedVersion, "live_expected_version");
                Assert(File.Exists(loaded.InstallerPath) && loaded.InstallerBytes > 1024 * 1024, "live_installer_downloaded");
                Assert(string.Equals(Hashing.Sha256File(loaded.InstallerPath), loaded.InstallerSha256, StringComparison.OrdinalIgnoreCase), "live_installer_hash_verified");
                Console.WriteLine("UPDATE_LIVE_SMOKE_PASS assertions=" + _passed + " result=" + result + " bytes=" + loaded.InstallerBytes);
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine("UPDATE_LIVE_SMOKE_FAIL " + ex); return 1; }
            finally { try { Directory.Delete(temp, true); } catch { } }
        }

        private static void TestCleanup(RuntimeConfigBundle config, DateTime now)
        {
            var download = Path.Combine(config.UpdatesDirectory, "download");
            Directory.CreateDirectory(download);
            var oldFile = Path.Combine(download, "old.exe");
            var freshFile = Path.Combine(download, "fresh.exe");
            File.WriteAllText(oldFile, "old"); File.SetLastWriteTimeUtc(oldFile, now.AddHours(-30));
            File.WriteAllText(freshFile, "fresh"); File.SetLastWriteTimeUtc(freshFile, now.AddHours(-1));

            var helperOld = Path.Combine(config.UpdatesDirectory, "helper-run", "old-helper");
            Directory.CreateDirectory(helperOld); Directory.SetLastWriteTimeUtc(helperOld, now.AddHours(-30));
            var stagedRoot = Path.Combine(config.UpdatesDirectory, "staged");
            var currentDir = Path.Combine(stagedRoot, "0.1.19-dev");
            var expiredDir = Path.Combine(stagedRoot, "0.1.10-dev");
            var freshDir = Path.Combine(stagedRoot, "0.1.18-dev");
            Directory.CreateDirectory(currentDir); Directory.CreateDirectory(expiredDir); Directory.CreateDirectory(freshDir);
            Directory.SetLastWriteTimeUtc(expiredDir, now.AddDays(-9)); Directory.SetLastWriteTimeUtc(freshDir, now.AddDays(-1));

            UpdateStagingService.Cleanup(config, "0.1.19-dev", now);
            Assert(!File.Exists(oldFile) && File.Exists(freshFile), "cleanup_download_retention");
            Assert(!Directory.Exists(helperOld), "cleanup_helper_retention");
            Assert(!Directory.Exists(currentDir), "cleanup_current_staged_dir");
            Assert(!Directory.Exists(expiredDir) && Directory.Exists(freshDir), "cleanup_staged_retention");
        }

        private static string Manifest(string version, string hash, long bytes, string url)
        {
            return new JavaScriptSerializer().Serialize(new { schema_version = 1, app_version = version, channel = "dev", installer_url = url, installer_sha256 = hash, installer_bytes = bytes, production_signed = false, min_windows = "6.1sp1" });
        }
        private static void Set(object instance, string property, object value) { var p = instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public); p.GetSetMethod(true).Invoke(instance, new[] { value }); }
        private static void AssertRejected(Action action, string name) { var rejected = false; try { action(); } catch { rejected = true; } Assert(rejected, name); }
        private static void Assert(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); _passed++; }
        private static bool HasArg(string[] args, string name) { foreach (var arg in args ?? new string[0]) if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true; return false; }
        private static string GetArgValue(string[] args, string name) { for (var i = 0; args != null && i + 1 < args.Length; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1]; return null; }
        private static string RequiredArg(string[] args, string name) { var value = GetArgValue(args, name); if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Missing argument " + name); return value; }
        private static string FindConfig()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null) { var c = Path.Combine(dir.FullName, "setup", "config"); if (File.Exists(Path.Combine(c, "update_policy_v1.json"))) return c; dir = dir.Parent; }
            throw new DirectoryNotFoundException("setup/config not found");
        }
    }
}
