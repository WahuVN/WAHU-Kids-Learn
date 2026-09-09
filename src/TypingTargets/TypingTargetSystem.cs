using System;
using System.Collections.Generic;
using System.Linq;

namespace WAHU.Typing.Targets
{
    public sealed class TypingTargetSystem
    {
        public const string CoreSpawnEvent = "TYPING_TARGET_SPAWNED";
        public const string InternalQueuedEvent = "TARGET_SYSTEM_QUEUED";
        public const string InternalLockedEvent = "TARGET_SYSTEM_LOCKED";
        public const string InternalUnlockedEvent = "TARGET_SYSTEM_UNLOCKED";
        public const string InternalResolvedEvent = "TARGET_SYSTEM_RESOLVED";
        public const string InternalDespawnedEvent = "TARGET_SYSTEM_DESPAWNED";
        public const string InternalPausedEvent = "TARGET_SYSTEM_PAUSED";
        public const string InternalResumedEvent = "TARGET_SYSTEM_RESUMED";

        private readonly TypingTargetSystemOptions _options;
        private readonly ITypingTargetDifficultyPolicy _difficulty;
        private readonly ITypingTargetRandom _random;
        private readonly List<RuntimeTarget> _queue = new List<RuntimeTarget>();
        private readonly Dictionary<string, RuntimeTarget> _active = new Dictionary<string, RuntimeTarget>(StringComparer.Ordinal);
        private readonly List<TypingTargetEvent> _events = new List<TypingTargetEvent>();
        private long _enqueueSequence;
        private long _spawnSequence;
        private long _nextSpawnAtMs = -1;
        private string _lockedTargetId;
        private bool _paused;
        private long _pausedAtMs;

        public TypingTargetSystem(
            TypingTargetSystemOptions options,
            ITypingTargetDifficultyPolicy difficulty,
            ITypingTargetRandom random)
        {
            _options = options ?? TypingTargetSystemOptions.ChildSafeDefaults();
            _difficulty = difficulty ?? new SafeTypingTargetDifficultyPolicy();
            _random = random ?? new SeededTypingTargetRandom(1);
            ValidateOptions(_options);
        }

        public int QueuedCount { get { return _queue.Count; } }
        public int ActiveCount { get { return _active.Count; } }
        public bool Paused { get { return _paused; } }
        public string LockedTargetId { get { return _lockedTargetId; } }

        public void Enqueue(TypingTargetSpawnRequest request, long nowMs)
        {
            if (request == null) throw new ArgumentNullException("request");
            ValidateTarget(request.Target);
            if (_active.ContainsKey(request.Target.Id) || _queue.Any(x => string.Equals(x.Target.Id, request.Target.Id, StringComparison.Ordinal)))
                throw new InvalidOperationException("Duplicate typing target id: " + request.Target.Id);
            if (request.PreferredLane.HasValue && (request.PreferredLane.Value < 0 || request.PreferredLane.Value >= _options.LaneCount))
                throw new ArgumentOutOfRangeException("request.PreferredLane");

            var runtime = new RuntimeTarget
            {
                Target = CloneTarget(request.Target),
                MovementKind = request.MovementKind,
                PreferredLane = request.PreferredLane,
                State = TypingTargetLifecycleState.Queued,
                EnqueuedAtMs = nowMs,
                EnqueueSequence = ++_enqueueSequence
            };
            _queue.Add(runtime);
            Emit(InternalQueuedEvent, runtime.Target.Id, null, runtime.Target.DisplayText, nowMs);
            if (_nextSpawnAtMs < 0) _nextSpawnAtMs = nowMs;
        }

        public void Tick(long nowMs, int deltaMs)
        {
            if (deltaMs < 0) throw new ArgumentOutOfRangeException("deltaMs");
            if (_paused) return;

            UpdateActive(nowMs, deltaMs);
            SpawnAtMostOne(nowMs);
        }

        public bool TryLockByInput(string normalizedKey, long nowMs, out string targetId)
        {
            targetId = null;
            if (_paused) return false;
            if (!string.IsNullOrWhiteSpace(_lockedTargetId))
            {
                RuntimeTarget already;
                if (_active.TryGetValue(_lockedTargetId, out already) && already.State == TypingTargetLifecycleState.Locked)
                {
                    targetId = already.Target.Id;
                    MarkTypingActivityInternal(already, nowMs);
                    return true;
                }
                _lockedTargetId = null;
            }

            var key = NormalizeLockKey(normalizedKey);
            if (key.Length == 0) return false;
            var candidates = _active.Values
                .Where(x => x.State == TypingTargetLifecycleState.Available && AcceptsPrefix(x.Target, key))
                .OrderBy(x => x.X)
                .ThenBy(x => x.SpawnSequence)
                .ThenBy(x => x.Target.Id, StringComparer.Ordinal)
                .ToList();
            if (candidates.Count == 0) return false;

            LockInternal(candidates[0], nowMs, "prefix:" + key);
            targetId = candidates[0].Target.Id;
            return true;
        }

        public bool FocusTarget(string targetId, long nowMs)
        {
            if (_paused || string.IsNullOrWhiteSpace(targetId)) return false;
            RuntimeTarget target;
            if (!_active.TryGetValue(targetId, out target)) return false;
            if (target.State != TypingTargetLifecycleState.Available && target.State != TypingTargetLifecycleState.Locked) return false;
            if (string.Equals(_lockedTargetId, targetId, StringComparison.Ordinal))
            {
                MarkTypingActivityInternal(target, nowMs);
                return true;
            }

            UnlockCurrentInternal(nowMs, "focus-switch");
            LockInternal(target, nowMs, "explicit-focus");
            return true;
        }

        public bool MarkTypingActivity(string targetId, long nowMs)
        {
            if (_paused) return false;
            RuntimeTarget target;
            if (string.IsNullOrWhiteSpace(targetId) || !_active.TryGetValue(targetId, out target)) return false;
            if (target.State != TypingTargetLifecycleState.Locked) return false;
            MarkTypingActivityInternal(target, nowMs);
            return true;
        }

        public bool ReleaseLock(long nowMs)
        {
            return UnlockCurrentInternal(nowMs, "explicit-release");
        }

        public bool ResolveTarget(string targetId, string resolutionText, long nowMs)
        {
            if (_paused) return false;
            RuntimeTarget target;
            if (string.IsNullOrWhiteSpace(targetId) || !_active.TryGetValue(targetId, out target)) return false;
            if (target.State == TypingTargetLifecycleState.Resolved || target.State == TypingTargetLifecycleState.Despawned) return false;
            if (target.State != TypingTargetLifecycleState.Available && target.State != TypingTargetLifecycleState.Locked && target.State != TypingTargetLifecycleState.Spawned)
                return false;

            if (string.Equals(_lockedTargetId, targetId, StringComparison.Ordinal)) _lockedTargetId = null;
            target.State = TypingTargetLifecycleState.Resolved;
            target.ResolvedAtMs = nowMs;
            target.TypingActive = false;
            Emit(InternalResolvedEvent, targetId, null, string.IsNullOrWhiteSpace(resolutionText) ? target.Target.Kind : resolutionText, nowMs);
            return true;
        }

        public void SetPaused(bool paused, long nowMs)
        {
            if (_paused == paused) return;
            if (paused)
            {
                _paused = true;
                _pausedAtMs = nowMs;
                Emit(InternalPausedEvent, null, null, null, nowMs);
                return;
            }

            var pausedFor = Math.Max(0, nowMs - _pausedAtMs);
            if (_nextSpawnAtMs >= 0) _nextSpawnAtMs += pausedFor;
            foreach (var target in _active.Values)
            {
                if (target.LastTypingActivityMs >= 0) target.LastTypingActivityMs += pausedFor;
                if (target.ResolvedAtMs >= 0) target.ResolvedAtMs += pausedFor;
            }
            _paused = false;
            _pausedAtMs = 0;
            Emit(InternalResumedEvent, null, null, null, nowMs);
        }

        public IList<TypingTargetSnapshot> Snapshot()
        {
            return _active.Values
                .OrderBy(x => x.SpawnSequence)
                .ThenBy(x => x.Target.Id, StringComparer.Ordinal)
                .Select(ToSnapshot)
                .ToList();
        }

        public IList<TypingTargetEvent> DrainEvents()
        {
            var copy = _events.Select(CloneEvent).ToList();
            _events.Clear();
            return copy;
        }

        private void SpawnAtMostOne(long nowMs)
        {
            if (_queue.Count == 0 || _active.Count >= _options.MaxConcurrentTargets) return;
            if (_nextSpawnAtMs >= 0 && nowMs < _nextSpawnAtMs) return;

            var candidate = SelectSpawnCandidate(nowMs);
            if (candidate == null)
            {
                _nextSpawnAtMs = nowMs + _options.ConflictRetryMs;
                return;
            }

            _queue.Remove(candidate);
            candidate.State = TypingTargetLifecycleState.Spawned;
            candidate.SpawnSequence = ++_spawnSequence;
            candidate.SpawnedAtMs = nowMs;
            candidate.X = _options.SpawnX;
            candidate.Lane = candidate.PreferredLane ?? PickLane();
            candidate.BaseY = LaneY(candidate.Lane);
            candidate.Y = candidate.BaseY;
            candidate.PhaseOffset = _random.Next(0, 6284) / 1000.0;
            candidate.SpeedPerSecond = _difficulty.SpeedPerSecond(candidate.Target, candidate.MovementKind);
            candidate.LastTypingActivityMs = -1;
            candidate.ResolvedAtMs = -1;
            _active.Add(candidate.Target.Id, candidate);
            Emit(CoreSpawnEvent, candidate.Target.Id, candidate.Target.RewardValue, candidate.Target.DisplayText, nowMs);
            candidate.State = TypingTargetLifecycleState.Available;

            _nextSpawnAtMs = nowMs + _difficulty.SpawnIntervalMs(candidate.Target, _queue.Count, _active.Count);
        }

        private RuntimeTarget SelectSpawnCandidate(long nowMs)
        {
            var ordered = _queue.OrderBy(x => x.EnqueueSequence).ThenBy(x => x.Target.Id, StringComparer.Ordinal).ToList();
            foreach (var candidate in ordered)
                if (!WouldCreatePrefixAmbiguity(candidate.Target)) return candidate;

            if (ordered.Count == 0) return null;
            if (_active.Count == 0) return ordered[0];
            var oldest = ordered[0];
            if (nowMs - oldest.EnqueuedAtMs >= _options.MaxAmbiguousPrefixDeferralMs) return oldest;
            return null;
        }

        private bool WouldCreatePrefixAmbiguity(TypingTarget target)
        {
            var keys = LockKeys(target);
            if (keys.Count == 0) return false;
            foreach (var active in _active.Values)
            {
                if (active.State != TypingTargetLifecycleState.Available && active.State != TypingTargetLifecycleState.Locked) continue;
                var activeKeys = LockKeys(active.Target);
                if (keys.Overlaps(activeKeys)) return true;
            }
            return false;
        }

        private void UpdateActive(long nowMs, int deltaMs)
        {
            if (_active.Count == 0) return;
            var deltaSeconds = deltaMs / 1000.0;
            var despawn = new List<string>();
            foreach (var target in _active.Values.OrderBy(x => x.SpawnSequence).ToList())
            {
                if (target.State == TypingTargetLifecycleState.Resolved)
                {
                    if (target.ResolvedAtMs >= 0 && nowMs - target.ResolvedAtMs >= _options.ResolvedDisplayMs)
                        despawn.Add(target.Target.Id);
                    continue;
                }
                if (target.State != TypingTargetLifecycleState.Available && target.State != TypingTargetLifecycleState.Locked) continue;

                var protectedTyping = IsTypingProtected(target, nowMs);
                target.TypingActive = protectedTyping;
                var speed = target.SpeedPerSecond * (protectedTyping ? _options.TypingSpeedMultiplier : 1.0);
                target.X -= speed * deltaSeconds;
                var floor = string.Equals(target.Target.Kind, "boss", StringComparison.OrdinalIgnoreCase)
                    ? _options.BossFloorX
                    : (protectedTyping ? _options.SafeFloorX : _options.DespawnX);
                if (target.X < floor) target.X = floor;

                var ageSeconds = Math.Max(0, nowMs - target.SpawnedAtMs) / 1000.0;
                if (target.MovementKind == TypingTargetMovementKind.Ufo)
                    target.Y = Clamp01(target.BaseY + Math.Sin((ageSeconds * 1.35) + target.PhaseOffset) * 0.035);
                else if (target.MovementKind == TypingTargetMovementKind.Satellite)
                    target.Y = Clamp01(target.BaseY + Math.Cos((ageSeconds * 0.72) + target.PhaseOffset) * 0.018);
                else
                    target.Y = target.BaseY;

                if (!protectedTyping && !string.Equals(target.Target.Kind, "boss", StringComparison.OrdinalIgnoreCase) && target.X <= _options.DespawnX + 0.000001)
                    despawn.Add(target.Target.Id);
            }

            foreach (var id in despawn) DespawnInternal(id, nowMs);
        }

        private bool IsTypingProtected(RuntimeTarget target, long nowMs)
        {
            return target != null &&
                   target.State == TypingTargetLifecycleState.Locked &&
                   string.Equals(_lockedTargetId, target.Target.Id, StringComparison.Ordinal) &&
                   target.LastTypingActivityMs >= 0 &&
                   nowMs - target.LastTypingActivityMs <= _options.TypingProtectionHoldMs;
        }

        private int PickLane()
        {
            if (_options.LaneCount <= 1) return 0;
            var occupancy = new int[_options.LaneCount];
            foreach (var item in _active.Values)
                if (item.Lane >= 0 && item.Lane < occupancy.Length) occupancy[item.Lane]++;
            var min = occupancy.Min();
            var candidates = new List<int>();
            for (var i = 0; i < occupancy.Length; i++) if (occupancy[i] == min) candidates.Add(i);
            return candidates[_random.Next(0, candidates.Count)];
        }

        private double LaneY(int lane)
        {
            if (_options.LaneCount <= 1) return 0.5;
            var top = 0.22;
            var bottom = 0.78;
            return top + ((bottom - top) * lane / (_options.LaneCount - 1.0));
        }

        private void LockInternal(RuntimeTarget target, long nowMs, string reason)
        {
            if (target == null) return;
            target.State = TypingTargetLifecycleState.Locked;
            target.LastTypingActivityMs = nowMs;
            target.TypingActive = true;
            _lockedTargetId = target.Target.Id;
            Emit(InternalLockedEvent, target.Target.Id, null, reason, nowMs);
        }

        private bool UnlockCurrentInternal(long nowMs, string reason)
        {
            if (string.IsNullOrWhiteSpace(_lockedTargetId)) return false;
            var id = _lockedTargetId;
            _lockedTargetId = null;
            RuntimeTarget target;
            if (_active.TryGetValue(id, out target) && target.State == TypingTargetLifecycleState.Locked)
            {
                target.State = TypingTargetLifecycleState.Available;
                target.TypingActive = false;
                Emit(InternalUnlockedEvent, id, null, reason, nowMs);
                return true;
            }
            return false;
        }

        private void MarkTypingActivityInternal(RuntimeTarget target, long nowMs)
        {
            target.LastTypingActivityMs = nowMs;
            target.TypingActive = true;
        }

        private void DespawnInternal(string targetId, long nowMs)
        {
            RuntimeTarget target;
            if (!_active.TryGetValue(targetId, out target)) return;
            if (string.Equals(_lockedTargetId, targetId, StringComparison.Ordinal)) _lockedTargetId = null;
            target.State = TypingTargetLifecycleState.Despawned;
            _active.Remove(targetId);
            Emit(InternalDespawnedEvent, targetId, null, target.Target.Kind, nowMs);
        }

        private TypingTargetSnapshot ToSnapshot(RuntimeTarget target)
        {
            return new TypingTargetSnapshot
            {
                Target = CloneTarget(target.Target),
                State = target.State,
                MovementKind = target.MovementKind,
                Lane = target.Lane,
                X = target.X,
                Y = target.Y,
                SpeedPerSecond = target.SpeedPerSecond,
                IsLocked = string.Equals(_lockedTargetId, target.Target.Id, StringComparison.Ordinal),
                TypingProtected = target.TypingActive,
                SpawnSequence = target.SpawnSequence,
                SpawnedAtMs = target.SpawnedAtMs
            };
        }

        private void Emit(string type, string targetId, int? value, string text, long nowMs)
        {
            _events.Add(new TypingTargetEvent { Type = type, TargetId = targetId, Value = value, Text = text, Timestamp = nowMs });
        }

        private static TypingTargetEvent CloneEvent(TypingTargetEvent source)
        {
            return new TypingTargetEvent { Type = source.Type, TargetId = source.TargetId, Value = source.Value, Text = source.Text, Timestamp = source.Timestamp };
        }

        private static TypingTarget CloneTarget(TypingTarget source)
        {
            return new TypingTarget
            {
                Id = source.Id,
                DisplayText = source.DisplayText,
                AcceptedInputs = source.AcceptedInputs == null ? new List<string>() : source.AcceptedInputs.ToList(),
                Language = source.Language,
                Difficulty = source.Difficulty,
                Kind = source.Kind,
                RewardValue = source.RewardValue
            };
        }

        private static HashSet<string> LockKeys(TypingTarget target)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (target == null || target.AcceptedInputs == null) return result;
            foreach (var input in target.AcceptedInputs)
            {
                var value = NormalizeLockKey(input);
                if (value.Length > 0) result.Add(value.Substring(0, 1));
            }
            return result;
        }

        private static bool AcceptsPrefix(TypingTarget target, string normalizedKey)
        {
            if (target == null || target.AcceptedInputs == null || string.IsNullOrWhiteSpace(normalizedKey)) return false;
            foreach (var input in target.AcceptedInputs)
            {
                var value = NormalizeLockKey(input);
                if (value.StartsWith(normalizedKey, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string NormalizeLockKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private static void ValidateTarget(TypingTarget target)
        {
            if (target == null) throw new ArgumentNullException("target");
            if (string.IsNullOrWhiteSpace(target.Id)) throw new ArgumentException("TypingTarget.id is required.");
            if (string.IsNullOrWhiteSpace(target.DisplayText)) throw new ArgumentException("TypingTarget.displayText is required.");
            if (target.AcceptedInputs == null || target.AcceptedInputs.Count == 0 || target.AcceptedInputs.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("TypingTarget.acceptedInputs requires at least one non-empty input.");
            if (!string.Equals(target.Language, "vi", StringComparison.Ordinal) && !string.Equals(target.Language, "en", StringComparison.Ordinal))
                throw new ArgumentException("TypingTarget.language must be vi or en.");
            if (target.Difficulty < 1) throw new ArgumentOutOfRangeException("target.Difficulty");
            if (!string.Equals(target.Kind, "shoot", StringComparison.Ordinal) &&
                !string.Equals(target.Kind, "rescue", StringComparison.Ordinal) &&
                !string.Equals(target.Kind, "unlock", StringComparison.Ordinal) &&
                !string.Equals(target.Kind, "boss", StringComparison.Ordinal))
                throw new ArgumentException("TypingTarget.kind must follow the frozen contract.");
            if (target.RewardValue < 0) throw new ArgumentOutOfRangeException("target.RewardValue");
        }

        private static void ValidateOptions(TypingTargetSystemOptions options)
        {
            if (options.LaneCount < 1) throw new ArgumentOutOfRangeException("options.LaneCount");
            if (options.MaxConcurrentTargets < 1) throw new ArgumentOutOfRangeException("options.MaxConcurrentTargets");
            if (options.ConflictRetryMs < 1 || options.MaxAmbiguousPrefixDeferralMs < options.ConflictRetryMs)
                throw new ArgumentOutOfRangeException("options.ConflictRetryMs");
            if (options.TypingProtectionHoldMs < 0 || options.ResolvedDisplayMs < 0)
                throw new ArgumentOutOfRangeException("options.TypingProtectionHoldMs");
            if (options.SpawnX <= options.SafeFloorX || options.SafeFloorX <= options.DespawnX || options.BossFloorX <= options.DespawnX)
                throw new ArgumentException("Typing target movement floors are invalid.");
            if (options.TypingSpeedMultiplier <= 0 || options.TypingSpeedMultiplier > 1)
                throw new ArgumentOutOfRangeException("options.TypingSpeedMultiplier");
        }

        private sealed class RuntimeTarget
        {
            public TypingTarget Target;
            public TypingTargetMovementKind MovementKind;
            public int? PreferredLane;
            public TypingTargetLifecycleState State;
            public long EnqueuedAtMs;
            public long EnqueueSequence;
            public long SpawnSequence;
            public long SpawnedAtMs;
            public long LastTypingActivityMs;
            public long ResolvedAtMs;
            public int Lane;
            public double X;
            public double Y;
            public double BaseY;
            public double SpeedPerSecond;
            public double PhaseOffset;
            public bool TypingActive;
        }
    }
}
