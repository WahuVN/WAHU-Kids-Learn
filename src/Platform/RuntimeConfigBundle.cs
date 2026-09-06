using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace WAHU.Platform
{
    public sealed class RuntimeConfigException : Exception
    {
        public RuntimeConfigException(string message) : base(message) { }
        public RuntimeConfigException(string message, Exception inner) : base(message, inner) { }
    }

    public sealed class RuntimeConfigBundle
    {
        private readonly Dictionary<string, string> _hashes;

        private RuntimeConfigBundle()
        {
            _hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string ConfigDirectory { get; private set; }
        public string DatabasePath { get; private set; }
        public string UserRoot { get; private set; }
        public string RecoveryDirectory { get; private set; }
        public string BackupsDirectory { get; private set; }
        public string TempDirectory { get; private set; }
        public bool PortableMode { get; private set; }
        public string StorageMode { get { return PortableMode ? "PORTABLE" : "INSTALLED"; } }
        public string DatabaseJournalMode { get; private set; }
        public string AppVersion { get; private set; }
        public int DesignWidth { get; private set; }
        public int DesignHeight { get; private set; }
        public int PrimaryChildTargetPx { get; private set; }
        public int LowMotionFpsCap { get; private set; }
        public int NormalMotionFpsCap { get; private set; }
        public int LowMaxAnimatedRegions { get; private set; }
        public int NormalMaxAnimatedRegions { get; private set; }
        public int LowImageCacheMb { get; private set; }
        public int NormalImageCacheMb { get; private set; }
        public int LowAudioCacheMb { get; private set; }
        public int NormalAudioCacheMb { get; private set; }
        public IReadOnlyDictionary<string, string> FileSha256 { get { return _hashes; } }

        public static RuntimeConfigBundle Load(string configDirectory)
        {
            return Load(configDirectory, AppDomain.CurrentDomain.BaseDirectory, false);
        }

        public static RuntimeConfigBundle Load(string configDirectory, string applicationBaseDirectory, bool portableMode)
        {
            if (string.IsNullOrWhiteSpace(configDirectory)) throw new ArgumentException("configDirectory");
            if (string.IsNullOrWhiteSpace(applicationBaseDirectory)) throw new ArgumentException("applicationBaseDirectory");
            if (!Directory.Exists(configDirectory)) throw new RuntimeConfigException("Thiếu thư mục config runtime: " + configDirectory);

            var serializer = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024, RecursionLimit = 64 };
            var docs = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            var required = new[]
            {
                "runtime_defaults_v1.json", "database_runtime_v1.json", "paths_v1.json", "feature_flags_v1.json",
                "accessibility_policy_v1.json", "performance_profiles_v1.json", "logging_policy_v1.json", "content_limits_v1.json",
                "install_manifest_v1.json"
            };

            var bundle = new RuntimeConfigBundle
            {
                ConfigDirectory = Path.GetFullPath(configDirectory),
                PortableMode = portableMode
            };
            foreach (var name in required)
            {
                var path = Path.Combine(configDirectory, name);
                if (!File.Exists(path)) throw new RuntimeConfigException("Thiếu config bắt buộc: " + name);
                try
                {
                    var root = serializer.DeserializeObject(File.ReadAllText(path)) as Dictionary<string, object>;
                    if (root == null) throw new RuntimeConfigException("JSON root phải là object: " + name);
                    docs[name] = root;
                    bundle._hashes[name] = Hashing.Sha256File(path);
                }
                catch (RuntimeConfigException) { throw; }
                catch (Exception ex) { throw new RuntimeConfigException("Config JSON không hợp lệ: " + name, ex); }
            }

            ValidateSchema(docs["runtime_defaults_v1.json"], 2, "runtime_defaults_v1.json");
            ValidateSchema(docs["database_runtime_v1.json"], 3, "database_runtime_v1.json");
            ValidateSchema(docs["paths_v1.json"], 1, "paths_v1.json");
            ValidateSchema(docs["feature_flags_v1.json"], 1, "feature_flags_v1.json");
            ValidateSchema(docs["accessibility_policy_v1.json"], 1, "accessibility_policy_v1.json");
            ValidateSchema(docs["performance_profiles_v1.json"], 2, "performance_profiles_v1.json");
            ValidateSchema(docs["logging_policy_v1.json"], 1, "logging_policy_v1.json");
            ValidateSchema(docs["content_limits_v1.json"], 1, "content_limits_v1.json");
            ValidateSchema(docs["install_manifest_v1.json"], 2, "install_manifest_v1.json");

            ValidateRuntimeDefaults(bundle, docs["runtime_defaults_v1.json"]);
            ValidateDatabase(bundle, docs["database_runtime_v1.json"]);
            ValidatePaths(bundle, docs["paths_v1.json"], Path.GetFullPath(applicationBaseDirectory), portableMode);
            ValidateFeatureFlags(docs["feature_flags_v1.json"]);
            ValidateAccessibility(bundle, docs["accessibility_policy_v1.json"]);
            ValidatePerformance(bundle, docs["performance_profiles_v1.json"]);
            ValidateLogging(docs["logging_policy_v1.json"]);
            ValidateContentLimits(docs["content_limits_v1.json"]);
            ValidateInstallManifest(bundle, docs["install_manifest_v1.json"]);
            return bundle;
        }

        private static void ValidateRuntimeDefaults(RuntimeConfigBundle bundle, Dictionary<string, object> root)
        {
            var app = Obj(root, "app");
            RequireString(app, "target_framework", "net48"); RequireString(app, "primary_platform", "x86");
            RequireString(app, "min_windows", "6.1sp1"); RequireBool(app, "offline_first", true);
            var display = Obj(root, "display");
            bundle.DesignWidth = Int(display, "design_width"); bundle.DesignHeight = Int(display, "design_height");
            if (bundle.DesignWidth != 1024 || bundle.DesignHeight != 768) throw new RuntimeConfigException("V1 design canvas phải là 1024x768.");
            RequireString(display, "dpi_awareness", "system"); RequireBool(display, "win7_manifest_dpiAware", true);
            var privacy = Obj(root, "privacy");
            RequireBool(privacy, "network_enabled", false); RequireBool(privacy, "analytics_enabled", false);
            RequireBool(privacy, "ads_enabled", false); RequireBool(privacy, "cloud_required", false);
            RequireBool(privacy, "collect_only_required_data", true);
        }

        private static void ValidateDatabase(RuntimeConfigBundle bundle, Dictionary<string, object> root)
        {
            var provider = Obj(root, "provider");
            RequireString(provider, "name", "System.Data.SQLite"); RequireString(provider, "version", "2.0.4");
            RequireString(provider, "target_framework", "net48"); RequireString(provider, "platform", "x86");
            var native = Obj(root, "native_sqlite");
            RequireString(native, "package", "SourceGear.sqlite3"); RequireString(native, "package_version", "3.53.4");
            RequireString(native, "engine_version", "3.53.4"); RequireString(native, "binary", "e_sqlite3.dll"); RequireString(native, "platform", "x86");
            var connection = Obj(root, "connection");
            RequireBool(connection, "foreign_keys", true); RequireBool(connection, "parameterized_sql_required", true); RequireBool(connection, "pooling", false);
            if (Int(connection, "busy_timeout_ms") < 500) throw new RuntimeConfigException("busy_timeout_ms quá thấp.");
            var journal = Obj(root, "journal");
            bundle.DatabaseJournalMode = String(journal, "default").ToUpperInvariant();
            if (bundle.DatabaseJournalMode != "DELETE") throw new RuntimeConfigException("V1 safe default journal phải là DELETE cho tới khi Win7 target gate cho phép thay đổi.");
            RequireBool(journal, "wal_allowed_only_after_target_verification", true);
        }

        private static void ValidatePaths(RuntimeConfigBundle bundle, Dictionary<string, object> root, string applicationBaseDirectory, bool portableMode)
        {
            if (portableMode)
            {
                var portable = Obj(root, "portable");
                RequireString(portable, "user_root", @".\UserData");
                RequireString(portable, "database", @".\UserData\data\learning.db");
                bundle.UserRoot = ResolvePortablePath(applicationBaseDirectory, String(portable, "user_root"));
                bundle.DatabasePath = ResolvePortablePath(applicationBaseDirectory, String(portable, "database"));
                bundle.RecoveryDirectory = Path.Combine(bundle.UserRoot, "recovery");
                bundle.BackupsDirectory = Path.Combine(bundle.UserRoot, "backups");
                bundle.TempDirectory = Path.Combine(bundle.UserRoot, "temp");
                if (!IsPathUnder(bundle.UserRoot, bundle.DatabasePath))
                    throw new RuntimeConfigException("Portable database phải nằm dưới portable UserData.");
                return;
            }

            var installed = Obj(root, "installed");
            var expectedRoot = @"%LOCALAPPDATA%\WAHU Kids Learn";
            RequireString(installed, "user_root", expectedRoot);
            RequireString(installed, "database", expectedRoot + @"\data\learning.db");
            RequireString(installed, "recovery", expectedRoot + @"\recovery");
            RequireString(installed, "backups", expectedRoot + @"\backups");
            RequireString(installed, "temp", expectedRoot + @"\temp");
            bundle.UserRoot = ExpandAllowedPath(String(installed, "user_root"));
            bundle.DatabasePath = ExpandAllowedPath(String(installed, "database"));
            bundle.RecoveryDirectory = ExpandAllowedPath(String(installed, "recovery"));
            bundle.BackupsDirectory = ExpandAllowedPath(String(installed, "backups"));
            bundle.TempDirectory = ExpandAllowedPath(String(installed, "temp"));
        }

        private static void ValidateFeatureFlags(Dictionary<string, object> root)
        {
            foreach (var key in new[] { "network", "auto_update_internet", "remote_analytics", "ads", "public_leaderboard", "loss_based_streak", "lootbox", "live_ai_child_mode", "camera_emotion_detection", "biometric_detection", "background_video", "lottie_runtime", "webview_runtime", "ai_pronunciation_score" }) RequireBool(root, key, false);
            RequireBool(root, "parent_mode", true); RequireBool(root, "offline_content_import", true); RequireBool(root, "backup_restore", true); RequireBool(root, "reduced_motion", true);
        }

        private static void ValidateAccessibility(RuntimeConfigBundle bundle, Dictionary<string, object> root)
        {
            var dpi = Obj(root, "dpi"); RequireString(dpi, "awareness", "system"); RequireBool(dpi, "win7_manifest_dpiAware", true);
            var pointer = Obj(root, "pointer"); bundle.PrimaryChildTargetPx = Int(pointer, "child_primary_target_px_at_96dpi");
            if (bundle.PrimaryChildTargetPx < 56) throw new RuntimeConfigException("Primary child target quá nhỏ.");
            RequireBool(pointer, "precision_drag_required", false); RequireBool(pointer, "nonessential_drag_requires_single_pointer_alternative", true);
            RequireBool(Obj(root, "visual"), "color_only_state_forbidden", true);
        }

        private static void ValidatePerformance(RuntimeConfigBundle bundle, Dictionary<string, object> root)
        {
            var timing = Obj(root, "timing"); RequireBool(timing, "fps_values_are_render_caps", true);
            RequireString(timing, "elapsed_time_source", "System.Diagnostics.Stopwatch"); RequireBool(timing, "coalesce_ui_updates", true);
            if (Int(timing, "max_pending_ui_updates") != 1) throw new RuntimeConfigException("max_pending_ui_updates phải là 1.");
            var profiles = Obj(root, "profiles");
            var low = Obj(profiles, "LOW");
            var normal = Obj(profiles, "NORMAL");
            bundle.LowMotionFpsCap = Int(low, "motion_fps_cap");
            bundle.NormalMotionFpsCap = Int(normal, "motion_fps_cap");
            bundle.LowMaxAnimatedRegions = Int(low, "max_animated_regions");
            bundle.NormalMaxAnimatedRegions = Int(normal, "max_animated_regions");
            bundle.LowImageCacheMb = Int(low, "image_cache_mb");
            bundle.NormalImageCacheMb = Int(normal, "image_cache_mb");
            bundle.LowAudioCacheMb = Int(low, "audio_cache_mb");
            bundle.NormalAudioCacheMb = Int(normal, "audio_cache_mb");
            if (bundle.LowMotionFpsCap != 18 || bundle.NormalMotionFpsCap != 30) throw new RuntimeConfigException("V1 motion caps phải là LOW=18, NORMAL=30 cho tới khi target benchmark version hóa thay đổi.");
            if (bundle.LowMaxAnimatedRegions != 1 || bundle.NormalMaxAnimatedRegions != 2) throw new RuntimeConfigException("V1 animated-region budget phải LOW=1, NORMAL=2.");
            if (bundle.LowImageCacheMb <= 0 || bundle.NormalImageCacheMb < bundle.LowImageCacheMb) throw new RuntimeConfigException("Image cache profile không hợp lệ.");
            if (bundle.LowAudioCacheMb <= 0 || bundle.NormalAudioCacheMb < bundle.LowAudioCacheMb) throw new RuntimeConfigException("Audio cache profile không hợp lệ.");
        }

        private static void ValidateLogging(Dictionary<string, object> root)
        {
            RequireBool(root, "remote_upload", false); var performance = Obj(root, "performance_sampling");
            RequireBool(performance, "per_frame_disk_log", false); RequireBool(performance, "aggregate_only", true);
        }

        private static void ValidateInstallManifest(RuntimeConfigBundle bundle, Dictionary<string, object> root)
        {
            bundle.AppVersion = String(root, "app_version");
            var runtime = Obj(root, "runtime");
            RequireString(runtime, "framework", ".NET Framework 4.8");
            RequireString(runtime, "platform", "x86");
            if (Int(runtime, "framework_min_release_key") != 528040)
                throw new RuntimeConfigException("install_manifest net48 Release floor mismatch.");
            var installer = Obj(root, "installer");
            RequireString(installer, "tool", "Inno Setup");
            RequireString(installer, "setup_architecture", "x86");
            RequireString(installer, "min_windows", "6.1sp1");
        }

        private static void ValidateContentLimits(Dictionary<string, object> root)
        {
            var pack = Obj(root, "pack");
            if (Int(pack, "max_compressed_mb") <= 0 || Int(pack, "max_uncompressed_mb") <= 0 || Int(pack, "max_files") <= 0) throw new RuntimeConfigException("Content pack limits phải dương.");
            if (Int(pack, "max_uncompressed_mb") < Int(pack, "max_compressed_mb")) throw new RuntimeConfigException("max_uncompressed_mb không thể nhỏ hơn max_compressed_mb.");
            RequireBool(pack, "executable_entries_allowed", false);
        }

        private static void ValidateSchema(Dictionary<string, object> root, int expected, string file)
        { var actual = Int(root, "schema_version"); if (actual != expected) throw new RuntimeConfigException(file + " schema_version=" + actual + ", expected=" + expected); }

        private static Dictionary<string, object> Obj(Dictionary<string, object> parent, string key)
        { object value; if (!parent.TryGetValue(key, out value)) throw new RuntimeConfigException("Thiếu object config: " + key); var obj = value as Dictionary<string, object>; if (obj == null) throw new RuntimeConfigException("Config key không phải object: " + key); return obj; }
        private static string String(Dictionary<string, object> obj, string key)
        { object value; if (!obj.TryGetValue(key, out value) || value == null) throw new RuntimeConfigException("Thiếu string config: " + key); var text = Convert.ToString(value, CultureInfo.InvariantCulture); if (string.IsNullOrWhiteSpace(text)) throw new RuntimeConfigException("String config rỗng: " + key); return text; }
        private static int Int(Dictionary<string, object> obj, string key)
        { object value; if (!obj.TryGetValue(key, out value) || value == null) throw new RuntimeConfigException("Thiếu integer config: " + key); try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); } catch (Exception ex) { throw new RuntimeConfigException("Integer config không hợp lệ: " + key, ex); } }
        private static bool Bool(Dictionary<string, object> obj, string key)
        { object value; if (!obj.TryGetValue(key, out value) || value == null) throw new RuntimeConfigException("Thiếu boolean config: " + key); if (value is bool) return (bool)value; throw new RuntimeConfigException("Boolean config không hợp lệ: " + key); }
        private static void RequireString(Dictionary<string, object> obj, string key, string expected)
        { var actual = String(obj, key); if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) throw new RuntimeConfigException(key + "=" + actual + ", expected=" + expected); }
        private static void RequireBool(Dictionary<string, object> obj, string key, bool expected)
        { var actual = Bool(obj, key); if (actual != expected) throw new RuntimeConfigException(key + "=" + actual + ", expected=" + expected); }

        private static string ExpandAllowedPath(string configured)
        {
            if (configured.IndexOf('%') >= 0 && !configured.StartsWith("%LOCALAPPDATA%", StringComparison.OrdinalIgnoreCase) && !configured.StartsWith("%USERPROFILE%", StringComparison.OrdinalIgnoreCase)) throw new RuntimeConfigException("Path chứa environment token không được phép: " + configured);
            var expanded = configured.Replace("%LOCALAPPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)).Replace("%USERPROFILE%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            return Path.GetFullPath(expanded);
        }

        private static string ResolvePortablePath(string applicationBaseDirectory, string configured)
        {
            if (Path.IsPathRooted(configured) || configured.IndexOf('%') >= 0)
                throw new RuntimeConfigException("Portable path phải là relative path và không chứa environment token: " + configured);
            var basePath = Path.GetFullPath(applicationBaseDirectory);
            var candidate = Path.GetFullPath(Path.Combine(basePath, configured));
            if (!IsPathUnder(basePath, candidate))
                throw new RuntimeConfigException("Portable path thoát khỏi thư mục ứng dụng: " + configured);
            return candidate;
        }

        private static bool IsPathUnder(string root, string candidate)
        {
            var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var normalizedCandidate = Path.GetFullPath(candidate);
            return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }
    }
}
