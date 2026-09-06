using System;
using System.Collections.Generic;
using System.Linq;
using WAHU.Platform;

namespace WAHU.Performance
{
    public sealed class PerformanceAutotuner
    {
        public PerformanceDecision Select(PreflightReport hardware, PerformanceMeasurements measurements = null)
        {
            if (hardware == null) throw new ArgumentNullException("hardware");
            measurements = measurements ?? new PerformanceMeasurements();
            var evidence = new List<string>();
            var renderP95 = P95(measurements.RenderFrameMs);
            var inputP95 = P95(measurements.InputDelayMs);
            var runtimeEnough = measurements.RenderFrameMs != null && measurements.RenderFrameMs.Count >= 20;

            var low = false;
            if (hardware.RamTotalMb.HasValue && hardware.RamTotalMb.Value <= 3072) { low = true; evidence.Add("ram_at_or_below_3gb"); }
            if (hardware.LogicalCores > 0 && hardware.LogicalCores <= 2) { low = true; evidence.Add("logical_cores_at_or_below_2"); }
            if (runtimeEnough && renderP95 > 40.0) { low = true; evidence.Add("render_p95_over_40ms"); }
            if (measurements.InputDelayMs != null && measurements.InputDelayMs.Count >= 20 && inputP95 > 80.0) { low = true; evidence.Add("input_p95_over_80ms"); }
            if (measurements.MaxWorkingSetMb > 220.0) { low = true; evidence.Add("working_set_over_220mb"); }
            if (measurements.GdiPressureEvents >= 3) { low = true; evidence.Add("repeated_gdi_pressure"); }
            if (!low) evidence.Add("normal_budget_not_exceeded");

            return new PerformanceDecision
            {
                Profile = low ? PerformanceProfileKind.LOW : PerformanceProfileKind.NORMAL,
                Evidence = evidence,
                RenderP95Ms = renderP95,
                InputP95Ms = inputP95,
                HasSufficientRuntimeEvidence = runtimeEnough
            };
        }

        public RuntimePerformanceSettings BuildRuntimeSettings(RuntimeConfigBundle config, PerformanceDecision decision)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (decision == null) throw new ArgumentNullException("decision");
            var low = decision.Profile == PerformanceProfileKind.LOW;
            return new RuntimePerformanceSettings
            {
                Profile = decision.Profile,
                MotionFpsCap = low ? config.LowMotionFpsCap : config.NormalMotionFpsCap,
                MaxAnimatedRegions = low ? config.LowMaxAnimatedRegions : config.NormalMaxAnimatedRegions,
                ImageCacheBytes = (long)(low ? config.LowImageCacheMb : config.NormalImageCacheMb) * 1024L * 1024L,
                AudioCacheBytes = (long)(low ? config.LowAudioCacheMb : config.NormalAudioCacheMb) * 1024L * 1024L,
                DecorativeMotionAllowedOutsideLearningFocus = !low,
                Evidence = decision.Evidence == null ? new List<string>() : new List<string>(decision.Evidence)
            };
        }

        public static double P95(IEnumerable<double> values)
        {
            if (values == null) return 0;
            var a = values.Where(x => x >= 0).OrderBy(x => x).ToArray();
            if (a.Length == 0) return 0;
            var i = (int)Math.Ceiling(a.Length * 0.95) - 1;
            return a[Math.Max(0, Math.Min(a.Length - 1, i))];
        }
    }
}
