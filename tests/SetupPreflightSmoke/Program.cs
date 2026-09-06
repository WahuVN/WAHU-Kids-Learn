using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WAHU.Platform;

namespace WAHU.SetupPreflight.Smoke
{
    internal static class Program
    {
        private static int _passed;

        [STAThread]
        private static int Main()
        {
            try
            {
                Assert(!CompatibilityClassifier.IsWindows7Sp1OrLater(new Version(6, 1, 7600), string.Empty), "win7_rtm_rejected");
                Assert(CompatibilityClassifier.IsWindows7Sp1OrLater(new Version(6, 1, 7601), "Service Pack 1"), "win7_sp1_accepted");
                Assert(CompatibilityClassifier.IsNet48OrLater(528040), "net48_boundary");
                Assert(CompatibilityClassifier.Classify(new Version(6, 1, 7601), "Service Pack 1", 528040, "PASS") == "WIN7_SP1_RUNTIME_COMPATIBLE", "win7_ready_classification");
                Assert(CompatibilityClassifier.Classify(new Version(6, 1, 7601), "Service Pack 1", 528040, "WARN") == "WIN7_SP1_SHA2_READINESS_UNKNOWN", "win7_sha2_warn_classification");
                Assert(Hashing.Sha256Text("abc") == "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", "sha256_known_vector");

                var report = PreflightProbe.Collect(typeof(Program).Assembly.Location);
                Assert(!string.IsNullOrWhiteSpace(report.OsVersion), "probe_os_version");
                Assert(report.ProcessArch == "x86", "probe_process_x86");
                Assert(report.SchemaVersion == 1, "report_schema_v1");
                Assert(report.Sha2Evidence != null && report.Warnings != null, "report_collections_initialized");

                var temp = Path.Combine(Path.GetTempPath(), "wahu-preflight-smoke-" + Guid.NewGuid().ToString("N") + ".json");
                JsonReportWriter.Write(report, temp);
                var json = File.ReadAllText(temp);
                Assert(json.Contains("\"schema_version\":1"), "json_schema_written");
                File.Delete(temp);

                var unsigned = SignatureVerifier.CheckCacheOnly(typeof(Program).Assembly.Location);
                Assert(!unsigned.SignaturePresent, "dev_binary_unsigned_expected");

                TestSingleInstanceGuard();
                TestRuntimeSessionMarker();
                TestRuntimeConfigBundle();

                Console.WriteLine("SETUP_PREFLIGHT_SMOKE_PASS assertions=" + _passed);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SETUP_PREFLIGHT_SMOKE_FAIL " + ex);
                return 1;
            }
        }

        private static void TestSingleInstanceGuard()
        {
            var name = "WAHU-Smoke-" + Guid.NewGuid().ToString("N");
            using (var primary = SingleInstanceGuard.TryAcquire(name))
            {
                Assert(primary.IsPrimaryInstance, "single_instance_primary_acquired");
                bool secondAcquired = true;
                var task = Task.Run(() =>
                {
                    using (var second = SingleInstanceGuard.TryAcquire(name))
                        secondAcquired = second.IsPrimaryInstance;
                });
                task.Wait();
                Assert(!secondAcquired, "single_instance_secondary_rejected");
            }

            using (var afterRelease = SingleInstanceGuard.TryAcquire(name))
                Assert(afterRelease.IsPrimaryInstance, "single_instance_reacquire_after_release");
        }

        private static void TestRuntimeSessionMarker()
        {
            var dir = Path.Combine(Path.GetTempPath(), "wahu-runtime-marker-" + Guid.NewGuid().ToString("N"));
            var markerPath = Path.Combine(dir, "runtime.running");
            try
            {
                using (var first = RuntimeSessionMarker.Begin(markerPath))
                {
                    Assert(!first.PreviousRunUnclean, "runtime_marker_first_run_clean");
                    Assert(File.Exists(markerPath), "runtime_marker_created");
                }
                Assert(!File.Exists(markerPath), "runtime_marker_removed_on_clean_dispose");

                Directory.CreateDirectory(dir);
                File.WriteAllText(markerPath, "simulated stale marker");
                using (var recovered = RuntimeSessionMarker.Begin(markerPath))
                    Assert(recovered.PreviousRunUnclean, "runtime_marker_detects_unclean_previous_run");
                Assert(!File.Exists(markerPath), "runtime_marker_recovery_cleanup");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }


        private static void TestRuntimeConfigBundle()
        {
            var source = FindConfigDirectory();
            var bundle = RuntimeConfigBundle.Load(source);
            Assert(bundle.FileSha256.Count == 10, "runtime_config_ten_required_files");
            Assert(bundle.AppVersion == "0.1.0-dev", "runtime_config_source_app_version");
            Assert(bundle.DatabaseJournalMode == "DELETE", "runtime_config_delete_journal");
            Assert(bundle.DatabasePath.EndsWith(@"WAHU Kids Learn\data\learning.db", StringComparison.OrdinalIgnoreCase), "runtime_config_database_path");
            Assert(bundle.BackupsDirectory.EndsWith(@"WAHU Kids Learn\backups", StringComparison.OrdinalIgnoreCase), "runtime_config_installed_backup_path");
            Assert(bundle.PrimaryChildTargetPx == 64, "runtime_config_child_target_64");
            Assert(bundle.LowMotionFpsCap == 18 && bundle.NormalMotionFpsCap == 30, "runtime_config_motion_caps");
            Assert(bundle.UpdateEnabled && bundle.UpdateCheckOnStartup && bundle.UpdateAutoDownload, "runtime_config_update_enabled");
            Assert(bundle.LaunchWithWindowsDefault, "runtime_config_startup_default");
            Assert(bundle.UpdateManifestUrl == "https://github.com/WahuVN/WAHU-Kids-Learn/releases/latest/download/update-manifest.json", "runtime_config_update_feed_locked");

            var portableBase = Path.Combine(Path.GetTempPath(), "wahu-portable-config-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(portableBase);
            var portable = RuntimeConfigBundle.Load(source, portableBase, true);
            Assert(portable.PortableMode && portable.StorageMode == "PORTABLE", "runtime_config_portable_mode");
            Assert(portable.UserRoot == Path.GetFullPath(Path.Combine(portableBase, "UserData")), "runtime_config_portable_user_root");
            Assert(portable.DatabasePath == Path.GetFullPath(Path.Combine(portableBase, "UserData", "data", "learning.db")), "runtime_config_portable_database_path");
            Assert(portable.BackupsDirectory == Path.GetFullPath(Path.Combine(portableBase, "UserData", "backups")), "runtime_config_portable_backup_path");
            Directory.Delete(portableBase, true);

            var temp = Path.Combine(Path.GetTempPath(), "wahu-config-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                foreach (var file in Directory.GetFiles(source, "*.json"))
                    File.Copy(file, Path.Combine(temp, Path.GetFileName(file)), true);

                var flagsPath = Path.Combine(temp, "feature_flags_v1.json");
                var flags = File.ReadAllText(flagsPath);
                File.WriteAllText(flagsPath, flags.Replace("\"network\": false", "\"network\": true"));
                AssertConfigRejected(temp, "runtime_config_network_tamper_rejected");

                File.Copy(Path.Combine(source, "feature_flags_v1.json"), flagsPath, true);
                var runtimePath = Path.Combine(temp, "runtime_defaults_v1.json");
                var runtime = File.ReadAllText(runtimePath);
                File.WriteAllText(runtimePath, runtime.Replace("\"schema_version\": 2", "\"schema_version\": 999"));
                AssertConfigRejected(temp, "runtime_config_schema_tamper_rejected");

                File.Copy(Path.Combine(source, "runtime_defaults_v1.json"), runtimePath, true);
                File.Delete(Path.Combine(temp, "logging_policy_v1.json"));
                AssertConfigRejected(temp, "runtime_config_missing_required_file_rejected");

                File.Copy(Path.Combine(source, "logging_policy_v1.json"), Path.Combine(temp, "logging_policy_v1.json"), true);
                var updatePath = Path.Combine(temp, "update_policy_v1.json");
                var updateText = File.ReadAllText(updatePath);
                File.WriteAllText(updatePath, updateText.Replace("WahuVN/WAHU-Kids-Learn/releases/latest", "evil/example/releases/latest"));
                AssertConfigRejected(temp, "runtime_config_update_feed_tamper_rejected");
                File.Copy(Path.Combine(source, "update_policy_v1.json"), updatePath, true);
                var pathsPath = Path.Combine(temp, "paths_v1.json");
                var paths = File.ReadAllText(pathsPath);
                File.WriteAllText(pathsPath, paths.Replace("\"user_root\": \".\\\\UserData\"", "\"user_root\": \"..\\\\escape\""));
                AssertPortableConfigRejected(temp, Path.Combine(temp, "portable-app"), "runtime_config_portable_path_tamper_rejected");
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        private static void AssertConfigRejected(string directory, string assertionName)
        {
            bool rejected = false;
            try { RuntimeConfigBundle.Load(directory); }
            catch (RuntimeConfigException) { rejected = true; }
            Assert(rejected, assertionName);
        }

        private static void AssertPortableConfigRejected(string directory, string applicationBase, string assertionName)
        {
            bool rejected = false;
            try { RuntimeConfigBundle.Load(directory, applicationBase, true); }
            catch (RuntimeConfigException) { rejected = true; }
            Assert(rejected, assertionName);
        }

        private static string FindConfigDirectory()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "setup", "config");
                if (File.Exists(Path.Combine(candidate, "runtime_defaults_v1.json"))) return candidate;
                dir = dir.Parent;
            }
            var cwd = Path.Combine(Environment.CurrentDirectory, "setup", "config");
            if (File.Exists(Path.Combine(cwd, "runtime_defaults_v1.json"))) return cwd;
            throw new DirectoryNotFoundException("setup/config not found for smoke test");
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            _passed++;
        }
    }
}
