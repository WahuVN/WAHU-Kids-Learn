using System;
using System.Collections.Generic;
using System.Linq;

namespace WAHU.Motion
{
    public sealed class RenderBudgetMonitor
    {
        private readonly Queue<double> _samples = new Queue<double>();
        private readonly int _capacity;

        public RenderBudgetMonitor(int capacity = 60)
        {
            if (capacity < 5) throw new ArgumentOutOfRangeException("capacity");
            _capacity = capacity;
        }

        public int DroppedFrames { get; private set; }
        public int SampleCount { get { return _samples.Count; } }

        public void RecordFrame(double frameWorkMs, double targetFrameIntervalMs)
        {
            if (frameWorkMs < 0 || targetFrameIntervalMs <= 0) throw new ArgumentOutOfRangeException();
            _samples.Enqueue(frameWorkMs);
            while (_samples.Count > _capacity) _samples.Dequeue();
            if (frameWorkMs > targetFrameIntervalMs * 1.5) DroppedFrames++;
        }

        public double Percentile95Ms
        {
            get
            {
                if (_samples.Count == 0) return 0;
                var a = _samples.OrderBy(x => x).ToArray();
                var index = (int)Math.Ceiling(a.Length * 0.95) - 1;
                return a[Math.Max(0, Math.Min(a.Length - 1, index))];
            }
        }

        public bool ShouldDegrade(double targetFrameIntervalMs)
        {
            return _samples.Count >= 10 && Percentile95Ms > targetFrameIntervalMs * 1.35;
        }
    }
}
