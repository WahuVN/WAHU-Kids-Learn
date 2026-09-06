using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace WAHU.Content
{
    public sealed class SecureContentImporter
    {
        private static readonly HashSet<string> ForbiddenExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".hta", ".msi", ".scr", ".lnk", ".reg", ".cpl", ".sys", ".pif", ".jar"
        };

        private static readonly HashSet<string> NestedArchiveExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz"
        };

        private readonly ContentPackValidator _validator = new ContentPackValidator();

        public ContentImportResult ImportZip(string zipPath, string contentStoreRoot, bool parentExplicitlyApproved, ContentImportLimits limits = null)
        {
            if (!parentExplicitlyApproved) throw new UnauthorizedAccessException("Parent explicit approval is required for external content import.");
            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath)) throw new FileNotFoundException("Content pack archive not found.", zipPath);
            if (string.IsNullOrWhiteSpace(contentStoreRoot)) throw new ArgumentException("contentStoreRoot");
            limits = limits ?? new ContentImportLimits();
            if (new FileInfo(zipPath).Length > limits.MaxCompressedBytes) throw new InvalidDataException("Compressed pack size exceeds limit.");

            Directory.CreateDirectory(contentStoreRoot);
            var tempRoot = Path.Combine(contentStoreRoot, ".import_tmp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                InspectAndExtract(zipPath, tempRoot, limits);
                var validation = _validator.ValidateDirectory(tempRoot, true);
                if (!validation.IsValid || !validation.ChildRuntimeAllowed)
                    throw new InvalidDataException("Content pack validation failed: " + string.Join(";", validation.Errors ?? new string[0]));

                var manifest = validation.Manifest;
                var packParent = Path.Combine(contentStoreRoot, manifest.pack_id);
                var destination = Path.Combine(packParent, manifest.version);
                Directory.CreateDirectory(packParent);

                if (Directory.Exists(destination))
                {
                    var existing = _validator.ValidateDirectory(destination, true);
                    if (existing.IsValid && string.Equals(existing.ManifestSha256, validation.ManifestSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        return new ContentImportResult
                        {
                            PackId = manifest.pack_id,
                            Version = manifest.version,
                            InstalledPath = destination,
                            ManifestSha256 = validation.ManifestSha256,
                            AlreadyInstalled = true
                        };
                    }
                    throw new InvalidDataException("Immutable content version already exists with different or invalid content.");
                }

                Directory.Move(tempRoot, destination);
                tempRoot = null;
                return new ContentImportResult
                {
                    PackId = manifest.pack_id,
                    Version = manifest.version,
                    InstalledPath = destination,
                    ManifestSha256 = validation.ManifestSha256,
                    AlreadyInstalled = false
                };
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(tempRoot) && Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
        }

        private static void InspectAndExtract(string zipPath, string tempRoot, ContentImportLimits limits)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long declaredUncompressed = 0;
            var fileCount = 0;

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    fileCount++;
                    if (fileCount > limits.MaxFiles) throw new InvalidDataException("Content pack file count exceeds limit.");

                    string normalized;
                    if (!ContentPackValidator.TryNormalizeRelativePath(entry.FullName, out normalized))
                        throw new InvalidDataException("Unsafe archive path: " + entry.FullName);
                    if (normalized.Length > limits.MaxInternalPathChars)
                        throw new InvalidDataException("Archive path exceeds internal path limit: " + normalized);
                    if (!seen.Add(normalized))
                        throw new InvalidDataException("Duplicate normalized archive path: " + normalized);

                    var extension = Path.GetExtension(normalized);
                    if (ForbiddenExtensions.Contains(extension)) throw new InvalidDataException("Executable/script entry forbidden: " + normalized);
                    if (NestedArchiveExtensions.Contains(extension)) throw new InvalidDataException("Nested archive entry forbidden: " + normalized);
                    if (IsUnixSymlink(entry.ExternalAttributes)) throw new InvalidDataException("Symlink entry forbidden: " + normalized);

                    declaredUncompressed += entry.Length;
                    if (declaredUncompressed > limits.MaxUncompressedBytes) throw new InvalidDataException("Declared uncompressed size exceeds limit.");
                }

                long actualWritten = 0;
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    string normalized;
                    if (!ContentPackValidator.TryNormalizeRelativePath(entry.FullName, out normalized))
                        throw new InvalidDataException("Unsafe archive path.");
                    var destination = ContentPackValidator.SafeCombine(tempRoot, normalized);
                    var parent = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                    using (var source = entry.Open())
                    using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[32 * 1024];
                        int read;
                        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            actualWritten += read;
                            if (actualWritten > limits.MaxUncompressedBytes)
                                throw new InvalidDataException("Actual uncompressed size exceeds limit.");
                            target.Write(buffer, 0, read);
                        }
                    }
                }
            }
        }

        private static bool IsUnixSymlink(int externalAttributes)
        {
            var mode = (externalAttributes >> 16) & 0xF000;
            return mode == 0xA000;
        }
    }
}
