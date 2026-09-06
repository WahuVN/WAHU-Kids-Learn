using System;

namespace WAHU.Security
{
    public sealed class ParentPinRecord
    {
        public int schema_version { get; set; }
        public string algorithm { get; set; }
        public int iterations { get; set; }
        public string salt_b64 { get; set; }
        public string hash_b64 { get; set; }
        public int failed_attempts { get; set; }
        public string locked_until_utc { get; set; }
        public string updated_at_utc { get; set; }
    }

    public sealed class ParentPinVerifyResult
    {
        public bool Success { get; set; }
        public bool Locked { get; set; }
        public int RemainingSeconds { get; set; }
        public string Reason { get; set; }
    }
}
