using System;

namespace WAHU.Platform
{
    public static class CompatibilityClassifier
    {
        public const int Net48ReleaseMin = 528040;

        public static bool IsWindows7(Version version)
        {
            return version != null && version.Major == 6 && version.Minor == 1;
        }

        public static bool IsWindows7Sp1OrLater(Version version, string servicePack)
        {
            if (version == null) return false;
            if (version.Major > 6 || (version.Major == 6 && version.Minor > 1)) return true;
            if (!IsWindows7(version)) return false;
            return !string.IsNullOrWhiteSpace(servicePack) && servicePack.IndexOf("Service Pack 1", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsNet48OrLater(int release)
        {
            return release >= Net48ReleaseMin;
        }

        public static string Classify(Version version, string servicePack, int netRelease, string legacySha2Readiness)
        {
            if (!IsWindows7Sp1OrLater(version, servicePack)) return "UNSUPPORTED_OS";

            if (IsWindows7(version))
            {
                if (!string.Equals(legacySha2Readiness, "PASS", StringComparison.OrdinalIgnoreCase))
                    return "WIN7_SP1_SHA2_READINESS_UNKNOWN";
                return IsNet48OrLater(netRelease) ? "WIN7_SP1_RUNTIME_COMPATIBLE" : "WIN7_SP1_BASE";
            }

            return IsNet48OrLater(netRelease) ? "NEWER_WINDOWS_DEV_COMPATIBLE" : "FRAMEWORK_PREREQUISITE_REQUIRED";
        }
    }
}
