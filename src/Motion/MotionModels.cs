using System;

namespace WAHU.Motion
{
    public enum MotionClass { INSTRUCTIONAL, SIGNAL, FEEDBACK, NAVIGATION, DECORATIVE }
    public enum MotionMode { Normal, Reduced, Minimal }
    public enum PerformanceProfile { LOW, NORMAL }
    public enum TweenEasing { Linear, EaseOutCubic, EaseInOutCubic }
    public enum TweenState { Pending, Active, Completed, Cancelled }

    public sealed class MotionRequest
    {
        public MotionClass MotionClass { get; set; }
        public MotionMode Mode { get; set; }
        public PerformanceProfile PerformanceProfile { get; set; }
        public string BehaviorState { get; set; }
        public bool QuestionActive { get; set; }
        public bool ReadingOrListening { get; set; }
        public bool Visible { get; set; } = true;
        public int ActiveAnimatedRegions { get; set; }
    }

    public sealed class MotionDecision
    {
        public bool Allowed { get; set; }
        public string Reason { get; set; }
        public int FpsCap { get; set; }
        public int MaxAnimatedRegions { get; set; }
        public bool PreferStaticSignal { get; set; }
    }

    public interface IMotionClock
    {
        double ElapsedMilliseconds { get; }
    }

    public interface IMotionTrack
    {
        bool IsFinished { get; }
        bool IsVisible { get; }
        void StartAt(double nowMs);
        void Tick(double nowMs);
    }
}
