using System;
using System.Collections.Generic;

namespace WAHU.Typing.Targets
{
    // Local mirror of the frozen 10-AI TypingTarget contract. AI10 may adapt this
    // to AI01's shared contract without changing any field semantics below.
    public sealed class TypingTarget
    {
        public string Id { get; set; }
        public string DisplayText { get; set; }
        public IList<string> AcceptedInputs { get; set; }
        public string Language { get; set; }
        public int Difficulty { get; set; }
        public string Kind { get; set; }
        public int RewardValue { get; set; }
    }

    // Frozen minimum event payload shape.
    public sealed class TypingTargetEvent
    {
        public string Type { get; set; }
        public string TargetId { get; set; }
        public int? Value { get; set; }
        public string Text { get; set; }
        public long Timestamp { get; set; }
    }

    public enum TypingTargetLifecycleState
    {
        Queued = 0,
        Spawned = 1,
        Available = 2,
        Locked = 3,
        Resolved = 4,
        Despawned = 5
    }

    public enum TypingTargetMovementKind
    {
        Asteroid = 0,
        Ufo = 1,
        Satellite = 2
    }

    public sealed class TypingTargetSpawnRequest
    {
        public TypingTarget Target { get; set; }
        public TypingTargetMovementKind MovementKind { get; set; }
        public int? PreferredLane { get; set; }
    }

    public sealed class TypingTargetSnapshot
    {
        public TypingTarget Target { get; set; }
        public TypingTargetLifecycleState State { get; set; }
        public TypingTargetMovementKind MovementKind { get; set; }
        public int Lane { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double SpeedPerSecond { get; set; }
        public bool IsLocked { get; set; }
        public bool TypingProtected { get; set; }
        public long SpawnSequence { get; set; }
        public long SpawnedAtMs { get; set; }
    }

    public interface ITypingTargetDifficultyPolicy
    {
        int SpawnIntervalMs(TypingTarget target, int queuedCount, int activeCount);
        double SpeedPerSecond(TypingTarget target, TypingTargetMovementKind movementKind);
    }

    public sealed class SafeTypingTargetDifficultyPolicy : ITypingTargetDifficultyPolicy
    {
        public int SpawnIntervalMs(TypingTarget target, int queuedCount, int activeCount)
        {
            var difficulty = ClampDifficulty(target == null ? 1 : target.Difficulty);
            var interval = 3300 - ((difficulty - 1) * 220);
            if (queuedCount > 6) interval -= 120;
            if (activeCount >= 2) interval += 450;
            return Math.Max(1800, Math.Min(4200, interval));
        }

        public double SpeedPerSecond(TypingTarget target, TypingTargetMovementKind movementKind)
        {
            var difficulty = ClampDifficulty(target == null ? 1 : target.Difficulty);
            var speed = 0.038 + ((difficulty - 1) * 0.006);
            if (movementKind == TypingTargetMovementKind.Ufo) speed *= 1.08;
            if (movementKind == TypingTargetMovementKind.Satellite) speed *= 0.78;
            return Math.Max(0.025, Math.Min(0.085, speed));
        }

        private static int ClampDifficulty(int value)
        {
            return Math.Max(1, Math.Min(5, value));
        }
    }

    public interface ITypingTargetRandom
    {
        int Next(int minInclusive, int maxExclusive);
    }

    public sealed class SeededTypingTargetRandom : ITypingTargetRandom
    {
        private readonly Random _random;
        public SeededTypingTargetRandom(int seed) { _random = new Random(seed); }
        public int Next(int minInclusive, int maxExclusive) { return _random.Next(minInclusive, maxExclusive); }
    }

    public sealed class TypingTargetSystemOptions
    {
        public int LaneCount { get; set; }
        public int MaxConcurrentTargets { get; set; }
        public int ConflictRetryMs { get; set; }
        public int MaxAmbiguousPrefixDeferralMs { get; set; }
        public int TypingProtectionHoldMs { get; set; }
        public int ResolvedDisplayMs { get; set; }
        public double SpawnX { get; set; }
        public double DespawnX { get; set; }
        public double SafeFloorX { get; set; }
        public double BossFloorX { get; set; }
        public double TypingSpeedMultiplier { get; set; }

        public static TypingTargetSystemOptions ChildSafeDefaults()
        {
            return new TypingTargetSystemOptions
            {
                LaneCount = 3,
                MaxConcurrentTargets = 3,
                ConflictRetryMs = 500,
                MaxAmbiguousPrefixDeferralMs = 6000,
                TypingProtectionHoldMs = 1400,
                ResolvedDisplayMs = 420,
                SpawnX = 1.04,
                DespawnX = 0.02,
                SafeFloorX = 0.28,
                BossFloorX = 0.22,
                TypingSpeedMultiplier = 0.20
            };
        }
    }
}
