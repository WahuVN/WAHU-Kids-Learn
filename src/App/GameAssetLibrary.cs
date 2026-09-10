using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.RegularExpressions;

namespace WAHUKidsLearn
{
    internal static class GameAssetLibrary
    {
        private sealed class CacheEntry
        {
            public Image Image;
            public long EstimatedBytes;
            public LinkedListNode<string> LruNode;
        }

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, CacheEntry> Cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly LinkedList<string> Lru = new LinkedList<string>();
        private const long DefaultCacheBudgetBytes = 96L * 1024L * 1024L;
        private const long MinimumCacheBudgetBytes = 8L * 1024L * 1024L;
        private static long _cacheBudgetBytes = DefaultCacheBudgetBytes;
        private static long _cachedBytes;

        private static readonly Regex ProductionAssetCountRegex = new Regex(
            "\"totalSelected\"\\s*:\\s*(\\d+)",
            RegexOptions.CultureInvariant);

        public static int ExpectedProductionPngCount
        {
            get
            {
                try
                {
                    var manifestPath = Path.Combine(RootPath, "ASSET_SELECTION_MANIFEST.json");
                    if (!File.Exists(manifestPath)) return 0;
                    var matches = ProductionAssetCountRegex.Matches(File.ReadAllText(manifestPath));
                    if (matches.Count != 1) return 0;
                    int value;
                    return int.TryParse(matches[0].Groups[1].Value, out value) && value > 0 ? value : 0;
                }
                catch { return 0; }
            }
        }

        public static string RootPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Generated", "Ready"); }
        }

        public static string GameV2RootPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Generated", "GameV2"); }
        }

        public static long CacheBudgetBytes
        {
            get { lock (Sync) return _cacheBudgetBytes; }
        }

        public static long CachedBytes
        {
            get { lock (Sync) return _cachedBytes; }
        }

        public static int CachedImageCount
        {
            get { lock (Sync) return Cache.Count; }
        }

        public static void ConfigureCache(long maxBytes)
        {
            lock (Sync)
            {
                _cacheBudgetBytes = Math.Max(MinimumCacheBudgetBytes, maxBytes > 0 ? maxBytes : DefaultCacheBudgetBytes);
                TrimCache(null);
            }
        }

        public static void ClearCache()
        {
            lock (Sync)
            {
                foreach (var entry in Cache.Values)
                {
                    try { if (entry.Image != null) entry.Image.Dispose(); }
                    catch { }
                }
                Cache.Clear();
                Lru.Clear();
                _cachedBytes = 0;
            }
        }

        public static bool HasAsset(string relativePath)
        {
            string fullPath;
            return TryResolve(relativePath, out fullPath) && File.Exists(fullPath);
        }

        public static bool HasProductionManifest()
        {
            try { return File.Exists(Path.Combine(RootPath, "ASSET_SELECTION_MANIFEST.json")); }
            catch { return false; }
        }

        public static int CountProductionPngAssets()
        {
            try
            {
                if (!Directory.Exists(RootPath)) return 0;
                return Directory.GetFiles(RootPath, "*.png", SearchOption.AllDirectories).Length;
            }
            catch { return 0; }
        }

        public static bool HasCompleteProductionPayload()
        {
            var expected = ExpectedProductionPngCount;
            return expected > 0 && HasProductionManifest() && CountProductionPngAssets() == expected;
        }

        public static Image Get(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;
            lock (Sync)
            {
                return GetLocked(relativePath);
            }
        }

        public static bool DrawContain(Graphics g, string relativePath, RectangleF bounds)
        {
            if (g == null || bounds.Width <= 1 || bounds.Height <= 1) return false;
            lock (Sync)
            {
                var image = GetLocked(relativePath);
                if (image == null) return false;
                var target = Fit(image.Size, bounds);
                var state = g.Save();
                try
                {
                    // Even contain targets can bleed a sub-pixel past their logical host when
                    // bicubic interpolation samples edge pixels. Clip every decorative asset
                    // to the host so artwork can never paint over adjacent text.
                    g.SetClip(bounds);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(image, target);
                }
                finally
                {
                    g.Restore(state);
                }
                return true;
            }
        }

        public static bool DrawCover(Graphics g, string relativePath, RectangleF bounds)
        {
            if (g == null || bounds.Width <= 1 || bounds.Height <= 1) return false;
            lock (Sync)
            {
                var image = GetLocked(relativePath);
                if (image == null) return false;

                var scale = Math.Max(bounds.Width / image.Width, bounds.Height / image.Height);
                var width = image.Width * scale;
                var height = image.Height * scale;
                var target = new RectangleF(bounds.Left + (bounds.Width - width) / 2f, bounds.Top + (bounds.Height - height) / 2f, width, height);
                var state = g.Save();
                try
                {
                    g.SetClip(bounds);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(image, target);
                }
                finally
                {
                    g.Restore(state);
                }
                return true;
            }
        }

        public static RectangleF Fit(Size imageSize, RectangleF bounds)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0) return RectangleF.Empty;
            var scale = Math.Min(bounds.Width / imageSize.Width, bounds.Height / imageSize.Height);
            var width = imageSize.Width * scale;
            var height = imageSize.Height * scale;
            return new RectangleF(bounds.Left + (bounds.Width - width) / 2f, bounds.Top + (bounds.Height - height) / 2f, width, height);
        }

        private static Image GetLocked(string relativePath)
        {
            var key = NormalizeCacheKey(relativePath);
            if (string.IsNullOrWhiteSpace(key)) return null;

            CacheEntry cached;
            if (Cache.TryGetValue(key, out cached))
            {
                Touch(cached);
                return cached.Image;
            }

            string fullPath;
            if (!TryResolve(key, out fullPath) || !File.Exists(fullPath)) return null;

            try
            {
                using (var source = Image.FromFile(fullPath))
                {
                    var clone = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    if (source.HorizontalResolution > 0f && source.VerticalResolution > 0f)
                        clone.SetResolution(source.HorizontalResolution, source.VerticalResolution);
                    using (var g = Graphics.FromImage(clone))
                    {
                        g.Clear(Color.Transparent);
                        g.DrawImage(source, new Rectangle(0, 0, clone.Width, clone.Height));
                    }

                    var node = Lru.AddFirst(key);
                    var entry = new CacheEntry
                    {
                        Image = clone,
                        EstimatedBytes = EstimateDecodedBytes(clone),
                        LruNode = node
                    };
                    Cache[key] = entry;
                    _cachedBytes += entry.EstimatedBytes;
                    TrimCache(key);
                    return clone;
                }
            }
            catch
            {
                return null;
            }
        }

        private static void Touch(CacheEntry entry)
        {
            if (entry == null || entry.LruNode == null || entry.LruNode.List == null || entry.LruNode == Lru.First) return;
            Lru.Remove(entry.LruNode);
            Lru.AddFirst(entry.LruNode);
        }

        private static void TrimCache(string preserveKey)
        {
            while (_cachedBytes > _cacheBudgetBytes && Cache.Count > 0)
            {
                LinkedListNode<string> victimNode = Lru.Last;
                if (victimNode == null) break;
                if (!string.IsNullOrWhiteSpace(preserveKey) && string.Equals(victimNode.Value, preserveKey, StringComparison.OrdinalIgnoreCase))
                {
                    victimNode = victimNode.Previous;
                    if (victimNode == null) break;
                }

                CacheEntry victim;
                if (!Cache.TryGetValue(victimNode.Value, out victim))
                {
                    Lru.Remove(victimNode);
                    continue;
                }

                Cache.Remove(victimNode.Value);
                Lru.Remove(victimNode);
                _cachedBytes = Math.Max(0, _cachedBytes - victim.EstimatedBytes);
                try { if (victim.Image != null) victim.Image.Dispose(); }
                catch { }
            }
        }

        private static long EstimateDecodedBytes(Image image)
        {
            if (image == null) return 0;
            try { return checked((long)image.Width * image.Height * 4L); }
            catch { return long.MaxValue / 4L; }
        }

        private static string NormalizeCacheKey(string relativePath)
        {
            return (relativePath ?? string.Empty).Replace('\\', '/').TrimStart('/');
        }

        private static bool TryResolve(string relativePath, out string fullPath)
        {
            fullPath = null;
            try
            {
                var normalized = NormalizeCacheKey(relativePath).Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                var root = Path.GetFullPath(RootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var candidate = Path.GetFullPath(Path.Combine(root, normalized));
                if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;
                if (File.Exists(candidate))
                {
                    fullPath = candidate;
                    return true;
                }

                var gameRoot = Path.GetFullPath(GameV2RootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var gameCandidate = Path.GetFullPath(Path.Combine(gameRoot, normalized));
                if (!gameCandidate.StartsWith(gameRoot, StringComparison.OrdinalIgnoreCase)) return false;
                fullPath = gameCandidate;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
