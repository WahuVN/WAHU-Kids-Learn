using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace WAHU.Platform
{
    public sealed class UpdateManifest
    {
        public int SchemaVersion { get; set; }
        public string AppVersion { get; set; }
        public string Channel { get; set; }
        public string InstallerUrl { get; set; }
        public string InstallerSha256 { get; set; }
        public long InstallerBytes { get; set; }
        public bool ProductionSigned { get; set; }
        public string MinimumWindows { get; set; }
    }

    public sealed class StagedUpdate
    {
        public string AppVersion { get; set; }
        public string InstallerPath { get; set; }
        public string InstallerSha256 { get; set; }
        public long InstallerBytes { get; set; }
        public bool ProductionSigned { get; set; }
        public DateTime StagedAtUtc { get; set; }
    }

    public static class AppVersionComparer
    {
        private sealed class Parsed
        {
            public int[] Numbers;
            public string Suffix;
        }

        public static bool IsNewer(string candidate, string current)
        {
            return Compare(candidate, current) > 0;
        }

        public static int Compare(string left, string right)
        {
            var a = Parse(left);
            var b = Parse(right);
            var max = Math.Max(a.Numbers.Length, b.Numbers.Length);
            for (var i = 0; i < max; i++)
            {
                var av = i < a.Numbers.Length ? a.Numbers[i] : 0;
                var bv = i < b.Numbers.Length ? b.Numbers[i] : 0;
                if (av != bv) return av.CompareTo(bv);
            }
            var aStable = string.IsNullOrEmpty(a.Suffix);
            var bStable = string.IsNullOrEmpty(b.Suffix);
            if (aStable != bStable) return aStable ? 1 : -1;
            return string.Compare(a.Suffix, b.Suffix, StringComparison.OrdinalIgnoreCase);
        }

        private static Parsed Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new FormatException("App version is empty.");
            var text = value.Trim();
            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase)) text = text.Substring(1);
            var dash = text.IndexOf('-');
            var numeric = dash >= 0 ? text.Substring(0, dash) : text;
            var suffix = dash >= 0 ? text.Substring(dash + 1) : string.Empty;
            var parts = numeric.Split('.');
            if (parts.Length < 2 || parts.Length > 4) throw new FormatException("Unsupported app version: " + value);
            var numbers = new List<int>();
            foreach (var part in parts)
            {
                int n;
                if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out n) || n < 0)
                    throw new FormatException("Invalid app version component: " + value);
                numbers.Add(n);
            }
            if (suffix.Length > 48 || suffix.Any(c => !(char.IsLetterOrDigit(c) || c == '.' || c == '-')))
                throw new FormatException("Invalid app version suffix: " + value);
            return new Parsed { Numbers = numbers.ToArray(), Suffix = suffix };
        }
    }

    public static class UpdateManifestParser
    {
        private static readonly Regex Sha256Regex = new Regex("^[0-9A-Fa-f]{64}$", RegexOptions.Compiled);
        private const string ReleasePrefix = "https://github.com/WahuVN/WAHU-Kids-Learn/releases/download/";

        public static UpdateManifest ParseAndValidate(string json, RuntimeConfigBundle config)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("Update manifest empty.");
            if (json.Length * 2L > config.UpdateMaxManifestBytes) throw new InvalidDataException("Update manifest exceeds size limit.");
            Dictionary<string, object> root;
            try { root = new JavaScriptSerializer { MaxJsonLength = (int)Math.Min(int.MaxValue, config.UpdateMaxManifestBytes), RecursionLimit = 32 }.DeserializeObject(json) as Dictionary<string, object>; }
            catch (Exception ex) { throw new InvalidDataException("Update manifest JSON invalid.", ex); }
            if (root == null) throw new InvalidDataException("Update manifest root must be object.");
            var manifest = new UpdateManifest
            {
                SchemaVersion = Int(root, "schema_version"),
                AppVersion = String(root, "app_version"),
                Channel = String(root, "channel"),
                InstallerUrl = String(root, "installer_url"),
                InstallerSha256 = String(root, "installer_sha256").ToUpperInvariant(),
                InstallerBytes = Long(root, "installer_bytes"),
                ProductionSigned = Bool(root, "production_signed"),
                MinimumWindows = String(root, "min_windows")
            };
            if (manifest.SchemaVersion != 1) throw new InvalidDataException("Unsupported update manifest schema_version.");
            AppVersionComparer.Compare(manifest.AppVersion, config.AppVersion);
            if (!string.Equals(manifest.Channel, config.UpdateChannel, StringComparison.Ordinal))
                throw new InvalidDataException("Update channel mismatch.");
            if (!Sha256Regex.IsMatch(manifest.InstallerSha256)) throw new InvalidDataException("Installer SHA-256 invalid.");
            if (manifest.InstallerBytes <= 0 || manifest.InstallerBytes > config.UpdateMaxInstallerBytes)
                throw new InvalidDataException("Installer size outside update policy.");
            if (!string.Equals(manifest.MinimumWindows, "6.1sp1", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Update min_windows mismatch.");
            Uri uri;
            if (!Uri.TryCreate(manifest.InstallerUrl, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidDataException("Installer URL must be HTTPS.");
            if (!manifest.InstallerUrl.StartsWith(ReleasePrefix, StringComparison.Ordinal))
                throw new InvalidDataException("Installer URL is outside WahuVN/WAHU-Kids-Learn releases.");
            var file = Path.GetFileName(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(file) || !file.StartsWith("WAHU-Kids-Learn-Setup-win7-x86-", StringComparison.Ordinal) || !file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Unexpected installer filename.");
            if (config.UpdateChannel == "stable" && config.UpdateAuthenticodeRequiredForProduction && !manifest.ProductionSigned)
                throw new InvalidDataException("Stable update manifest must declare production_signed=true.");
            return manifest;
        }

        private static string String(Dictionary<string, object> root, string key)
        {
            object value; if (!root.TryGetValue(key, out value) || value == null) throw new InvalidDataException("Missing update manifest key: " + key);
            var text = Convert.ToString(value, CultureInfo.InvariantCulture); if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Empty update manifest key: " + key); return text;
        }
        private static int Int(Dictionary<string, object> root, string key) { return Convert.ToInt32(Value(root, key), CultureInfo.InvariantCulture); }
        private static long Long(Dictionary<string, object> root, string key) { return Convert.ToInt64(Value(root, key), CultureInfo.InvariantCulture); }
        private static bool Bool(Dictionary<string, object> root, string key) { var v = Value(root, key); if (!(v is bool)) throw new InvalidDataException("Invalid bool: " + key); return (bool)v; }
        private static object Value(Dictionary<string, object> root, string key) { object value; if (!root.TryGetValue(key, out value) || value == null) throw new InvalidDataException("Missing update manifest key: " + key); return value; }
    }
}
