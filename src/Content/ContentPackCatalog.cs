using System;
using System.Collections.Generic;
using System.IO;

namespace WAHU.Content
{
    public sealed class ContentPackCatalog
    {
        private readonly ContentPackValidator _validator = new ContentPackValidator();

        public IList<ContentPackValidationResult> ScanChildReady(string contentStoreRoot)
        {
            var result = new List<ContentPackValidationResult>();
            if (string.IsNullOrWhiteSpace(contentStoreRoot) || !Directory.Exists(contentStoreRoot)) return result;
            foreach (var packIdDir in Directory.GetDirectories(contentStoreRoot))
            {
                if (Path.GetFileName(packIdDir).StartsWith(".import_tmp_", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var versionDir in Directory.GetDirectories(packIdDir))
                {
                    var validation = _validator.ValidateDirectory(versionDir, true);
                    if (validation.IsValid && validation.ChildRuntimeAllowed) result.Add(validation);
                }
            }
            return result;
        }

        public string ResolveRequiredFile(string installedPackPath, string relativePath)
        {
            string normalized;
            if (!ContentPackValidator.TryNormalizeRelativePath(relativePath, out normalized))
                throw new InvalidDataException("Invalid content-relative path.");
            var full = ContentPackValidator.SafeCombine(installedPackPath, normalized);
            if (!File.Exists(full)) throw new FileNotFoundException("Required content asset missing.", full);
            return full;
        }

        public string ResolveOptionalFileOrFallback(string installedPackPath, string relativePath, string fallbackPath)
        {
            string normalized;
            if (!ContentPackValidator.TryNormalizeRelativePath(relativePath, out normalized)) return fallbackPath;
            var full = ContentPackValidator.SafeCombine(installedPackPath, normalized);
            return File.Exists(full) ? full : fallbackPath;
        }
    }
}
