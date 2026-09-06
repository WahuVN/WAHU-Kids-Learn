using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace WAHU.Platform
{
    [DataContract]
    public sealed class PreflightReport
    {
        [DataMember(Name="schema_version", Order=1)] public int SchemaVersion { get; set; }
        [DataMember(Name="captured_at_utc", Order=2)] public string CapturedAtUtc { get; set; }
        [DataMember(Name="os_version", Order=3)] public string OsVersion { get; set; }
        [DataMember(Name="service_pack", Order=4)] public string ServicePack { get; set; }
        [DataMember(Name="is_target_windows7", Order=5)] public bool IsTargetWindows7 { get; set; }
        [DataMember(Name="os_supported", Order=6)] public bool OsSupported { get; set; }
        [DataMember(Name="process_arch", Order=7)] public string ProcessArch { get; set; }
        [DataMember(Name="os_arch", Order=8)] public string OsArch { get; set; }
        [DataMember(Name="logical_cores", Order=9)] public int LogicalCores { get; set; }
        [DataMember(Name="ram_total_mb", Order=10, EmitDefaultValue=false)] public long? RamTotalMb { get; set; }
        [DataMember(Name="free_disk_mb", Order=11, EmitDefaultValue=false)] public long? FreeDiskMb { get; set; }
        [DataMember(Name="system_dpi", Order=12, EmitDefaultValue=false)] public float? SystemDpi { get; set; }
        [DataMember(Name="high_contrast_enabled", Order=13)] public bool HighContrastEnabled { get; set; }
        [DataMember(Name="stopwatch_high_resolution", Order=14)] public bool StopwatchHighResolution { get; set; }
        [DataMember(Name="net_framework_release", Order=15)] public int NetFrameworkRelease { get; set; }
        [DataMember(Name="net48_or_later", Order=16)] public bool Net48OrLater { get; set; }
        [DataMember(Name="legacy_sha2_readiness", Order=17)] public string LegacySha2Readiness { get; set; }
        [DataMember(Name="sha2_evidence", Order=18)] public List<string> Sha2Evidence { get; set; }
        [DataMember(Name="audio_output_available", Order=19)] public bool AudioOutputAvailable { get; set; }
        [DataMember(Name="microphone_available", Order=20)] public bool MicrophoneAvailable { get; set; }
        [DataMember(Name="signature_target", Order=21, EmitDefaultValue=false)] public string SignatureTarget { get; set; }
        [DataMember(Name="signature_present", Order=22, EmitDefaultValue=false)] public bool? SignaturePresent { get; set; }
        [DataMember(Name="signature_digest_valid", Order=23, EmitDefaultValue=false)] public bool? SignatureDigestValid { get; set; }
        [DataMember(Name="publisher_chain_trusted", Order=24, EmitDefaultValue=false)] public bool? PublisherChainTrusted { get; set; }
        [DataMember(Name="signature_validation_elapsed_ms", Order=25, EmitDefaultValue=false)] public long? SignatureValidationElapsedMs { get; set; }
        [DataMember(Name="winverifytrust_code", Order=26, EmitDefaultValue=false)] public string WinVerifyTrustCode { get; set; }
        [DataMember(Name="signature_note", Order=27, EmitDefaultValue=false)] public string SignatureNote { get; set; }
        [DataMember(Name="compatibility_level", Order=28)] public string CompatibilityLevel { get; set; }
        [DataMember(Name="warnings", Order=29)] public List<string> Warnings { get; set; }

        public PreflightReport()
        {
            SchemaVersion = 1;
            Sha2Evidence = new List<string>();
            Warnings = new List<string>();
        }
    }
}
