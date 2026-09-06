using System;
using System.Collections.Generic;

namespace WAHU.Content
{
    public sealed class ContentPackFileEntry
    {
        public string path { get; set; }
        public string sha256 { get; set; }
    }

    public sealed class ContentPackManifest
    {
        public int schema_version { get; set; }
        public string pack_id { get; set; }
        public string version { get; set; }
        public string subject { get; set; }
        public int grade { get; set; }
        public string status { get; set; }
        public string content_kind { get; set; }
        public List<ContentPackFileEntry> files { get; set; }
    }

    public sealed class ContentPackValidationResult
    {
        public bool IsValid { get; set; }
        public bool ChildRuntimeAllowed { get; set; }
        public ContentPackManifest Manifest { get; set; }
        public string ManifestSha256 { get; set; }
        public IList<string> Errors { get; set; }
    }

    public sealed class ContentImportLimits
    {
        public long MaxCompressedBytes { get; set; } = 300L * 1024 * 1024;
        public long MaxUncompressedBytes { get; set; } = 650L * 1024 * 1024;
        public int MaxFiles { get; set; } = 12000;
        public int MaxInternalPathChars { get; set; } = 180;
    }

    public sealed class ContentImportResult
    {
        public string PackId { get; set; }
        public string Version { get; set; }
        public string InstalledPath { get; set; }
        public string ManifestSha256 { get; set; }
        public bool AlreadyInstalled { get; set; }
    }
}
