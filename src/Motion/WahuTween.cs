using System;

namespace WAHU.Motion
{
    public sealed class WahuTween : IMotionTrack
    {
        private readonly double _from;
        private readonly double _to;
        private readonly double _durationMs;
        private readonly TweenEasing _easing;
        private readonly Action<double> _apply;
        private double _startMs;
        private bool _started;

        public WahuTween(double from, double to, double durationMs, TweenEasing easing, Action<double> apply)
        {
            if (durationMs <= 0) throw new ArgumentOutOfRangeException("durationMs");
            _from = from;
            _to = to;
            _durationMs = durationMs;
            _easing = easing;
            _apply = apply ?? throw new ArgumentNullException("apply");
            State = TweenState.Pending;
        }

        public TweenState State { get; private set; }
        public bool IsFinished { get { return State == TweenState.Completed || State == TweenState.Cancelled; } }
        public bool IsVisible { get; set; } = true;
        public double CurrentValue { get; private set; }

        public void StartAt(double nowMs)
        {
            if (_started || IsFinished) return;
            _started = true;
            _startMs = nowMs;
            State = TweenState.Active;
            CurrentValue = _from;
            _apply(_from);
        }

        public void Tick(double nowMs)
        {
            if (IsFinished) return;
            if (!_started) StartAt(nowMs);
            var raw = (nowMs - _startMs) / _durationMs;
            var t = Math.Max(0, Math.Min(1, raw));
            var eased = Ease(t, _easing);
            CurrentValue = _from + ((_to - _from) * eased);
            if (t >= 1)
            {
                CurrentValue = _to;
                _apply(_to);
                State = TweenState.Completed;
                return;
            }
            _apply(CurrentValue);
        }

        public void Cancel(bool snapToEnd)
        {
            if (IsFinished) return;
            if (snapToEnd)
            {
                CurrentValue = _to;
                _apply(_to);
            }
            State = TweenState.Cancelled;
        }

        public static double Ease(double t, TweenEasing easing)
        {
            t = Math.Max(0, Math.Min(1, t));
            switch (easing)
            {
                case TweenEasing.EaseOutCubic:
                    var x = 1 - t;
                    return 1 - x * x * x;
                case TweenEasing.EaseInOutCubic:
                    return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
                default:
                    return t;
            }
        }
    }
}
