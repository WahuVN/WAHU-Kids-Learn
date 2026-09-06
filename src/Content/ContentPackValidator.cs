using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace WAHU.Content
{
    public sealed class ContentPackValidator
    {
        private static readonly Regex SafeId = new Regex("^[a-z0-9][a-z0-9_-]{2,63}$", RegexOptions.Compiled);
        private static readonly Regex SafeVersion = new Regex("^[0-9A-Za-z][0-9A-Za-z._-]{0,31}$", RegexOptions.Compiled);

        public ContentPackValidationResult ValidateDirectory(string packRoot, bool requireVerifiedForChildRuntime)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(packRoot) || !Directory.Exists(packRoot))
                return Invalid("pack_directory_missing");

            var manifestPath = Path.Combine(packRoot, "manifest.json");
            if (!File.Exists(manifestPath)) return Invalid("manifest_missing");

            ContentPackManifest manifest;
            try
            {
                var json = File.ReadAllText(manifestPath);
                manifest = new JavaScriptSerializer().Deserialize<ContentPackManifest>(json);
            }
            catch (Exception ex)
            {
                return Invalid("manifest_parse_error:" + ex.GetType().Name);
            }

            if (manifest == null) return Invalid("manifest_null");
            if (manifest.schema_version != 1) errors.Add("manifest_schema_version_unsupported");
            if (!SafeId.IsMatch(manifest.pack_id ?? string.Empty)) errors.Add("pack_id_invalid");
            if (!SafeVersion.IsMatch(manifest.version ?? string.Empty)) errors.Add("pack_version_invalid");
            if (manifest.subject != "math" && manifest.subject != "english") errors.Add("subject_invalid");
            if (manifest.grade != 2) errors.Add("grade_invalid");
            if (manifest.files == null || manifest.files.Count == 0) errors.Add("manifest_files_empty");

            var childAllowed = string.Equals(manifest.status, "VERIFIED", StringComparison.Ordinal);
            if (requireVerifiedForChildRuntime && !childAllowed) errors.Add("child_runtime_requires_verified_status");

            var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (manifest.files != null)
            {
                foreach (var file in manifest.files)
                {
                    string normalized;
                    if (!TryNormalizeRelativePath(file == null ? null : file.path, out normalized))
                    {
                        errors.Add("manifest_file_path_invalid");
                        continue;
                    }
                    if (!listed.Add(normalized)) errors.Add("manifest_file_duplicate:" + normalized);
                    if (file == null || !IsSha256(file.sha256)) errors.Add("manifest_file_sha256_invalid:" + normalized);
                    var full = SafeCombine(packRoot, normalized);
                    if (!File.Exists(full))
                    {
                        errors.Add("manifest_file_missing:" + normalized);
                        continue;
                    }
                    if (file != null && IsSha256(file.sha256))
                    {
                        var actual = Sha256(full);
                        if (!string.Equals(actual, file.sha256, StringComparison.OrdinalIgnoreCase))
                            errors.Add("manifest_file_hash_mismatch:" + normalized);
                    }
                }
            }

            var actualFiles = Directory.GetFiles(packRoot, "*", SearchOption.AllDirectories)
                .Select(x => NormalizeRelative(packRoot, x))
                .Where(x => !string.Equals(x, "manifest.json", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var actual in actualFiles)
                if (!listed.Contains(actual)) errors.Add("unlisted_file_present:" + actual);

            return new ContentPackValidationResult
            {
                IsValid = errors.Count == 0,
                ChildRuntimeAllowed = childAllowed && errors.Count == 0,
                Manifest = manifest,
                ManifestSha256 = Sha256(manifestPath),
                Errors = errors
            };
        }

        public static bool TryNormalizeRelativePath(string input, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(input)) return false;
            var path = input.Replace('\\', '/').Trim();
            if (path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("\\", StringComparison.Ordinal)) return false;
            if (path.IndexOf(':') >= 0) return false;
            var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts.Any(x => x == "." || x == "..")) return false;
            normalized = string.Join("/", parts);
            return normalized.Length > 0;
        }

        public static string SafeCombine(string root, string normalizedRelativePath)
        {
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var combined = Path.GetFullPath(Path.Combine(rootFull, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Path escapes pack root.");
            return combined;
        }

        public static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            for (var i = 0; i < value.Length; i++)
                if (!Uri.IsHexDigit(value[i])) return false;
            return true;
        }

        private static string NormalizeRelative(string root, string fullPath)
        {
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(fullPath);
            return full.Substring(rootFull.Length).Replace(Path.DirectorySeparatorChar, '/');
        }

        private static ContentPackValidationResult Invalid(string error)
        {
            return new ContentPackValidationResult { IsValid = false, ChildRuntimeAllowed = false, Errors = new[] { error } };
        }
    }
}
