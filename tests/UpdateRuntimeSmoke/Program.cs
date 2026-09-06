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
        private static int Main()
        {
            var temp = Path.Combine(Path.GetTempPath(), "wahu-update-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                var config = RuntimeConfigBundle.Load(FindConfig());
                Set(config, "UpdatesDirectory", temp);
                Set(config, "PortableMode", false);
                Assert(config.UpdateEnabled && config.UpdateCheckOnStartup && config.UpdateAutoDownload && config.UpdateAutoInstallOnNextStart, "policy_enabled");
                Assert(config.UpdateManifestUrl == "https://github.com/WahuVN/WAHU-Kids-Learn/releases/latest/download/update-manifest.json", "manifest_url_locked");
                Assert(config.LaunchWithWindowsDefault, "startup_default_enabled");
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
                AssertRejected(() => UpdateManifestParser.ParseAndValidate(manifestJson.Replace("https://github.com/WahuVN/WAHU-Kids-Learn/releases/download/", "https://example.com/"), config), "manifest_wrong_origin_rejected");
                AssertRejected(() => UpdateManifestParser.ParseAndValidate(manifestJson.Replace("https://github.com/", "http://github.com/"), config), "manifest_http_rejected");
                AssertRejected(() => UpdateManifestParser.ParseAndValidate(manifestJson.Replace(hash, "BAD"), config), "manifest_bad_hash_rejected");

                var staged = UpdateStagingService.StageVerifiedInstaller(config, manifest, fake);
                Assert(File.Exists(staged.InstallerPath), "installer_staged");
                StagedUpdate loaded;
                Assert(UpdateStagingService.TryGetStagedUpdate(config, "0.1.19-dev", out loaded), "staged_reload_valid");
                Assert(loaded.AppVersion == "0.1.20-dev" && loaded.InstallerSha256 == hash, "staged_metadata_valid");
                File.AppendAllText(loaded.InstallerPath, "tamper");
                Assert(!UpdateStagingService.TryGetStagedUpdate(config, "0.1.19-dev", out loaded), "staged_tamper_rejected");

                Assert(UpdateStagingService.IsCheckDue(config, DateTime.UtcNow), "first_check_due");
                UpdateStagingService.MarkCheckAttempt(config, DateTime.UtcNow);
                Assert(!UpdateStagingService.IsCheckDue(config, DateTime.UtcNow.AddHours(1)), "check_throttled");
                Assert(UpdateStagingService.IsCheckDue(config, DateTime.UtcNow.AddHours(7)), "check_due_after_interval");

                Console.WriteLine("UPDATE_RUNTIME_SMOKE_PASS assertions=" + _passed);
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine("UPDATE_RUNTIME_SMOKE_FAIL " + ex); return 1; }
            finally { try { Directory.Delete(temp, true); } catch { } }
        }

        private static string Manifest(string version, string hash, long bytes, string url)
        {
            return new JavaScriptSerializer().Serialize(new { schema_version = 1, app_version = version, channel = "dev", installer_url = url, installer_sha256 = hash, installer_bytes = bytes, production_signed = false, min_windows = "6.1sp1" });
        }
        private static void Set(object instance, string property, object value) { var p = instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public); p.GetSetMethod(true).Invoke(instance, new[] { value }); }
        private static void AssertRejected(Action action, string name) { var rejected = false; try { action(); } catch { rejected = true; } Assert(rejected, name); }
        private static void Assert(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); _passed++; }
        private static string FindConfig()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null) { var c = Path.Combine(dir.FullName, "setup", "config"); if (File.Exists(Path.Combine(c, "update_policy_v1.json"))) return c; dir = dir.Parent; }
            throw new DirectoryNotFoundException("setup/config not found");
        }
    }
}
