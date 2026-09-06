using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using WAHU.Content;

namespace WAHU.ContentRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        private static void Main()
        {
            var root = Path.Combine(Path.GetTempPath(), "wahu-content-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                TestBundledPackValidation();
                TestMathCurriculumBaselineSync();
                TestSecureImport(root);
                TestAdversarialArchives(root);
                Console.WriteLine("CONTENT_RUNTIME_SMOKE_PASS assertions=" + _assertions);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void TestBundledPackValidation()
        {
            var validator = new ContentPackValidator();
            var projectRoot = FindProjectRoot();
            var math = validator.ValidateDirectory(Path.Combine(projectRoot, "content_packs", "math_grade2_v1"), true);
            var english = validator.ValidateDirectory(Path.Combine(projectRoot, "content_packs", "english_grade2_v1"), true);
            Assert(math.IsValid && math.ChildRuntimeAllowed, "bundled_math_verified");
            Assert(english.IsValid && english.ChildRuntimeAllowed, "bundled_english_verified");
            Assert(math.Manifest.version == "1.4.0" && english.Manifest.version == "1.0.0", "bundled_versions_explicit");
        }

        private static void TestMathCurriculumBaselineSync()
        {
            var projectRoot = FindProjectRoot();
            var docsText = File.ReadAllText(Path.Combine(projectRoot, "docs", "05_MATH_GRADE2_CURRICULUM.md"));
            var baselineText = File.ReadAllText(Path.Combine(projectRoot, "curriculum", "math_grade2", "moet_baseline_v1.json"));
            var ignored = new HashSet<string>(StringComparer.Ordinal) { "BOOK_MAPPED", "SUPPLEMENTARY", "VERIFIED_A" };
            var missing = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(docsText, "`([A-Z][A-Z0-9_]+)`"))
            {
                var id = match.Groups[1].Value;
                if (!ignored.Contains(id) && !baselineText.Contains("\"" + id + "\"")) missing.Add(id);
            }
            Assert(missing.Count == 0, "math_machine_baseline_covers_documented_curriculum_ids");
        }
        private static void TestSecureImport(string root)
        {
            var store = Path.Combine(root, "store");
            var goodZip = Path.Combine(root, "good.zip");
            WritePackZip(goodZip, "pack_good", "1.0.0", "VERIFIED", "content.json", "{\"hello\":1}");
            var importer = new SecureContentImporter();
            var imported = importer.ImportZip(goodZip, store, true, TinyLimits());
            Assert(!imported.AlreadyInstalled && Directory.Exists(imported.InstalledPath), "verified_pack_installed");
            var importedAgain = importer.ImportZip(goodZip, store, true, TinyLimits());
            Assert(importedAgain.AlreadyInstalled, "same_immutable_version_idempotent");

            var catalog = new ContentPackCatalog();
            var ready = catalog.ScanChildReady(store);
            Assert(ready.Count == 1 && ready[0].Manifest.pack_id == "pack_good", "catalog_only_child_ready_verified");
            var fallback = Path.Combine(root, "fallback.png");
            File.WriteAllText(fallback, "fallback");
            Assert(catalog.ResolveOptionalFileOrFallback(imported.InstalledPath, "missing.png", fallback) == fallback, "missing_optional_asset_fallback");

            bool approvalRequired = false;
            try { importer.ImportZip(goodZip, Path.Combine(root, "no-approval"), false, TinyLimits()); }
            catch (UnauthorizedAccessException) { approvalRequired = true; }
            Assert(approvalRequired, "parent_explicit_approval_required");

            var holdZip = Path.Combine(root, "hold.zip");
            WritePackZip(holdZip, "pack_hold", "1.0.0", "HOLD", "content.json", "{}");
            bool holdRejected = false;
            try { importer.ImportZip(holdZip, store, true, TinyLimits()); }
            catch (InvalidDataException) { holdRejected = true; }
            Assert(holdRejected, "hold_pack_rejected_for_child_runtime");
        }

        private static void TestAdversarialArchives(string root)
        {
            var importer = new SecureContentImporter();
            var store = Path.Combine(root, "adversarial-store");

            AssertRejected(importer, store, MakeZip(root, "zipslip.zip", z => Add(z, "../escape.txt", "x")), "zip_slip_rejected");
            AssertRejected(importer, store, MakeZip(root, "absolute.zip", z => Add(z, "C:/evil.txt", "x")), "absolute_path_rejected");
            AssertRejected(importer, store, MakeZip(root, "exe.zip", z => Add(z, "payload.exe", "MZ")), "executable_rejected");
            AssertRejected(importer, store, MakeZip(root, "nested.zip", z => Add(z, "nested.zip", "fake")), "nested_archive_rejected");
            AssertRejected(importer, store, MakeZip(root, "duplicate.zip", z => { Add(z, "A.json", "1"); Add(z, "a.json", "2"); }), "casefold_duplicate_rejected");
            AssertRejected(importer, store, MakeZip(root, "too-many.zip", z => { Add(z, "a.txt", "1"); Add(z, "b.txt", "2"); Add(z, "c.txt", "3"); }), "file_count_limit_rejected", new ContentImportLimits { MaxCompressedBytes = 1024 * 1024, MaxUncompressedBytes = 1024 * 1024, MaxFiles = 2, MaxInternalPathChars = 180 });
            AssertRejected(importer, store, MakeZip(root, "bomb.zip", z => Add(z, "huge.txt", new string('A', 4096))), "uncompressed_limit_rejected", new ContentImportLimits { MaxCompressedBytes = 1024 * 1024, MaxUncompressedBytes = 1024, MaxFiles = 10, MaxInternalPathChars = 180 });

            var mismatch = Path.Combine(root, "hash-mismatch.zip");
            using (var fs = new FileStream(mismatch, FileMode.Create))
            using (var z = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                Add(z, "content.json", "{}");
                Add(z, "manifest.json", ManifestJson("pack_hash_bad", "1.0.0", "VERIFIED", "content.json", new string('0', 64)));
            }
            AssertRejected(importer, store, mismatch, "manifest_hash_mismatch_rejected");

            var longName = new string('a', 181) + ".txt";
            AssertRejected(importer, store, MakeZip(root, "longpath.zip", z => Add(z, longName, "x")), "internal_path_length_rejected");

            var malformed = Path.Combine(root, "malformed.zip");
            File.WriteAllBytes(malformed, Encoding.ASCII.GetBytes("not-a-zip"));
            AssertRejected(importer, store, malformed, "malformed_archive_rejected");

            var compressedTooLarge = MakeZip(root, "compressed-too-large.zip", z => Add(z, "a.txt", new string('Z', 2048)));
            AssertRejected(importer, store, compressedTooLarge, "compressed_size_limit_rejected", new ContentImportLimits { MaxCompressedBytes = 1, MaxUncompressedBytes = 1024 * 1024, MaxFiles = 10, MaxInternalPathChars = 180 });
        }

        private static void AssertRejected(SecureContentImporter importer, string store, string zip, string name, ContentImportLimits limits = null)
        {
            var rejected = false;
            try { importer.ImportZip(zip, store, true, limits ?? TinyLimits()); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected, name);
        }

        private static ContentImportLimits TinyLimits()
        {
            return new ContentImportLimits { MaxCompressedBytes = 1024 * 1024, MaxUncompressedBytes = 128 * 1024, MaxFiles = 20, MaxInternalPathChars = 180 };
        }

        private static string MakeZip(string root, string name, Action<ZipArchive> write)
        {
            var path = Path.Combine(root, name);
            using (var fs = new FileStream(path, FileMode.Create))
            using (var z = new ZipArchive(fs, ZipArchiveMode.Create)) write(z);
            return path;
        }

        private static void WritePackZip(string path, string packId, string version, string status, string contentPath, string content)
        {
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var hash = Sha256(contentBytes);
            using (var fs = new FileStream(path, FileMode.Create))
            using (var z = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                Add(z, contentPath, content);
                Add(z, "manifest.json", ManifestJson(packId, version, status, contentPath, hash));
            }
        }

        private static string ManifestJson(string packId, string version, string status, string file, string hash)
        {
            return "{\"schema_version\":1,\"pack_id\":\"" + packId + "\",\"version\":\"" + version + "\",\"subject\":\"math\",\"grade\":2,\"status\":\"" + status + "\",\"content_kind\":\"smoke\",\"files\":[{\"path\":\"" + file.Replace("\\", "/") + "\",\"sha256\":\"" + hash + "\"}]}";
        }

        private static void Add(ZipArchive z, string name, string text)
        {
            var e = z.CreateEntry(name, CompressionLevel.Optimal);
            using (var writer = new StreamWriter(e.Open(), new UTF8Encoding(false))) writer.Write(text);
        }

        private static string Sha256(byte[] data)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty);
        }

        private static string FindProjectRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "WAHUKidsLearn.sln"))) return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Project root not found.");
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }
    }
}
