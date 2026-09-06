using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace WAHU.Motion
{
    public sealed class StopwatchMotionClock : IMotionClock
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        public double ElapsedMilliseconds { get { return _stopwatch.Elapsed.TotalMilliseconds; } }
    }

    public sealed class MotionScheduler : IDisposable
    {
        private readonly object _gate = new object();
        private readonly List<IMotionTrack> _tracks = new List<IMotionTrack>();
        private readonly SynchronizationContext _uiContext;
        private readonly IMotionClock _clock;
        private readonly double _frameIntervalMs;
        private Timer _wakeTimer;
        private double _lastPresentedMs;
        private int _uiPostPending;
        private bool _disposed;

        public MotionScheduler(int fpsCap, SynchronizationContext uiContext = null, IMotionClock clock = null, bool autoStart = true)
        {
            if (fpsCap <= 0 || fpsCap > 60) throw new ArgumentOutOfRangeException("fpsCap");
            FpsCap = fpsCap;
            _frameIntervalMs = 1000.0 / fpsCap;
            _uiContext = uiContext ?? SynchronizationContext.Current;
            if (_uiContext == null) throw new InvalidOperationException("MotionScheduler requires a UI SynchronizationContext; do not silently execute UI animation on a ThreadPool context.");
            _clock = clock ?? new StopwatchMotionClock();
            _lastPresentedMs = _clock.ElapsedMilliseconds;
            if (autoStart)
            {
                var wakePeriod = Math.Max(15, (int)Math.Floor(_frameIntervalMs / 2.0));
                _wakeTimer = new Timer(_ => Pulse(), null, wakePeriod, wakePeriod);
            }
        }

        public int FpsCap { get; private set; }
        public double FrameIntervalMs { get { return _frameIntervalMs; } }
        public int ActiveTrackCount { get { lock (_gate) return _tracks.Count(x => !x.IsFinished && x.IsVisible); } }
        public int PendingUiUpdateCount { get { return Volatile.Read(ref _uiPostPending); } }

        public void Register(IMotionTrack track)
        {
            if (track == null) throw new ArgumentNullException("track");
            ThrowIfDisposed();
            lock (_gate)
            {
                if (_tracks.Contains(track)) return;
                track.StartAt(_clock.ElapsedMilliseconds);
                _tracks.Add(track);
            }
        }

        public void Unregister(IMotionTrack track)
        {
            if (track == null) return;
            lock (_gate) _tracks.Remove(track);
        }

        public void Pulse()
        {
            if (_disposed) return;
            var now = _clock.ElapsedMilliseconds;
            if (now - _lastPresentedMs < _frameIntervalMs) return;
            if (Interlocked.CompareExchange(ref _uiPostPending, 1, 0) != 0) return;
            _uiContext.Post(_ => PresentLatestFrame(), null);
        }

        private void PresentLatestFrame()
        {
            try
            {
                if (_disposed) return;
                var now = _clock.ElapsedMilliseconds;
                if (now - _lastPresentedMs < _frameIntervalMs) return;
                _lastPresentedMs = now;

                IMotionTrack[] snapshot;
                lock (_gate) snapshot = _tracks.ToArray();
                foreach (var track in snapshot)
                {
                    if (track.IsFinished || !track.IsVisible) continue;
                    track.Tick(now);
                }
                lock (_gate) _tracks.RemoveAll(x => x.IsFinished || !x.IsVisible);
            }
            finally
            {
                Interlocked.Exchange(ref _uiPostPending, 0);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var timer = Interlocked.Exchange(ref _wakeTimer, null);
            if (timer != null) timer.Dispose();
            lock (_gate) _tracks.Clear();
            Interlocked.Exchange(ref _uiPostPending, 0);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException("MotionScheduler");
        }
    }
}
