using System;
using System.Collections.Generic;

namespace WAHU.TypingSpace.Combat
{
    public static class TypingCombatEventTypes
    {
        public const string TargetSpawned = "TYPING_TARGET_SPAWNED";
        public const string CharCorrect = "TYPING_CHAR_CORRECT";
        public const string CharWrong = "TYPING_CHAR_WRONG";
        public const string WordCompleted = "TYPING_WORD_COMPLETED";
        public const string TargetDestroyed = "TARGET_DESTROYED";
        public const string TargetRescued = "TARGET_RESCUED";
        public const string BossHit = "BOSS_HIT";
        public const string BossDefeated = "BOSS_DEFEATED";
        public const string LevelCompleted = "LEVEL_COMPLETED";
        public const string RewardGranted = "REWARD_GRANTED";
        public const string GamePaused = "GAME_PAUSED";
        public const string GameResumed = "GAME_RESUMED";
    }

    public static class TypingCombatTargetKinds
    {
        public const string Shoot = "shoot";
        public const string Rescue = "rescue";
        public const string Unlock = "unlock";
        public const string Boss = "boss";

        public static bool IsSupported(string kind)
        {
            return string.Equals(kind, Shoot, StringComparison.Ordinal) ||
                   string.Equals(kind, Rescue, StringComparison.Ordinal) ||
                   string.Equals(kind, Unlock, StringComparison.Ordinal) ||
                   string.Equals(kind, Boss, StringComparison.Ordinal);
        }
    }

    public enum ShipActionMode
    {
        Idle = 0,
        Charging = 1,
        Firing = 2,
        RescueBeam = 3,
        UnlockBeam = 4,
        BossStrike = 5
    }

    public sealed class TypingCombatEvent
    {
        public string Type { get; set; }
        public string TargetId { get; set; }
        public int? Value { get; set; }
        public string Text { get; set; }
        public long Timestamp { get; set; }
    }

    public sealed class TypingCombatTargetBinding
    {
        public string Id { get; set; }
        public string Kind { get; set; }
    }

    public sealed class CombatOutputEvent
    {
        public string Type { get; set; }
        public string TargetId { get; set; }
        public int? Value { get; set; }
        public string Text { get; set; }
        public long Timestamp { get; set; }
    }

    public sealed class BossSnapshot
    {
        public int RequiredHits { get; set; }
        public int HitsTaken { get; set; }
        public int RemainingHits { get; set; }
        public int PhaseNumber { get; set; }
        public bool Active { get; set; }
        public bool Defeated { get; set; }
    }

    public sealed class ShipActionSnapshot
    {
        public ShipActionMode Mode { get; set; }
        public string ActiveTargetId { get; set; }
        public int Charge { get; set; }
        public int MaxCharge { get; set; }
        public long ActionUntilTimestamp { get; set; }
        public long LastTimestamp { get; set; }
        public long ActionSequence { get; set; }
        public bool Paused { get; set; }
        public bool InputLocked { get; set; }
        public BossSnapshot Boss { get; set; }
    }

    public sealed class ShipActionChangedEventArgs : EventArgs
    {
        public ShipActionSnapshot Previous { get; set; }
        public ShipActionSnapshot Current { get; set; }
        public string Reason { get; set; }
    }

    public sealed class CombatOutputEventArgs : EventArgs
    {
        public CombatOutputEvent Event { get; set; }
    }

    public sealed class TypingSpaceCombatSettings
    {
        public int MaxCharge { get; set; }
        public int FireDurationMs { get; set; }
        public int RescueBeamDurationMs { get; set; }
        public int UnlockBeamDurationMs { get; set; }
        public int BossStrikeDurationMs { get; set; }
        public int BossRequiredHits { get; set; }

        public static TypingSpaceCombatSettings CreateDefault()
        {
            return new TypingSpaceCombatSettings
            {
                MaxCharge = 12,
                FireDurationMs = 180,
                RescueBeamDurationMs = 220,
                UnlockBeamDurationMs = 220,
                BossStrikeDurationMs = 240,
                BossRequiredHits = 3
            };
        }

        internal void Validate()
        {
            if (MaxCharge < 1 || MaxCharge > 100) throw new ArgumentOutOfRangeException("MaxCharge");
            if (FireDurationMs < 0 || FireDurationMs > 2000) throw new ArgumentOutOfRangeException("FireDurationMs");
            if (RescueBeamDurationMs < 0 || RescueBeamDurationMs > 2000) throw new ArgumentOutOfRangeException("RescueBeamDurationMs");
            if (UnlockBeamDurationMs < 0 || UnlockBeamDurationMs > 2000) throw new ArgumentOutOfRangeException("UnlockBeamDurationMs");
            if (BossStrikeDurationMs < 0 || BossStrikeDurationMs > 2000) throw new ArgumentOutOfRangeException("BossStrikeDurationMs");
            if (BossRequiredHits < 1 || BossRequiredHits > 12) throw new ArgumentOutOfRangeException("BossRequiredHits");
        }
    }
}
