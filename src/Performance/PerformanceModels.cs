using System.Collections.Generic;

namespace WAHU.Performance
{
    public enum PerformanceProfileKind { LOW, NORMAL }
    public enum DegradationStage { None, DecorativeOff, ReducedIdleFps, OffscreenPaused, SkipTweenIntermediate, CachePressureReduced, TemporaryLowMotion }

    public sealed class PerformanceMeasurements
    {
        public IList<double> RenderFrameMs { get; set; } = new List<double>();
        public IList<double> InputDelayMs { get; set; } = new List<double>();
        public double MaxWorkingSetMb { get; set; }
        public int GdiPressureEvents { get; set; }
    }

    public sealed class RuntimePerformanceSettings
    {
        public PerformanceProfileKind Profile { get; set; }
        public int MotionFpsCap { get; set; }
        public int MaxAnimatedRegions { get; set; }
        public long ImageCacheBytes { get; set; }
        public long AudioCacheBytes { get; set; }
        public bool DecorativeMotionAllowedOutsideLearningFocus { get; set; }
        public IList<string> Evidence { get; set; }
    }

    public sealed class PerformanceDecision
    {
        public PerformanceProfileKind Profile { get; set; }
        public IList<string> Evidence { get; set; }
        public double RenderP95Ms { get; set; }
        public double InputP95Ms { get; set; }
        public bool HasSufficientRuntimeEvidence { get; set; }
    }
}
