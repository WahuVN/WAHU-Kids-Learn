using System;
using System.Collections.Generic;
using System.IO;

namespace WAHU.Motion
{
    public enum TypingGameFeelEffect
    {
        None = 0,
        ChargeGlow = 1,
        TinySpark = 2,
        GentleShake = 3,
        BeamBurst = 4,
        RescuePulse = 5,
        UnlockPulse = 6,
        BossHitPulse = 7,
        Celebration = 8
    }

    public sealed class TypingGameFeedbackEvent
    {
        public string Type { get; set; }
        public string TargetId { get; set; }
        public double? Value { get; set; }
        public string Text { get; set; }
        public long Timestamp { get; set; }
    }

    public sealed class TypingGameFeelCue
    {
        public TypingGameFeelEffect Effect { get; set; }
        public MotionClass MotionClass { get; set; }
        public int DurationMs { get; set; }
        public double Intensity { get; set; }
        public int ParticleCount { get; set; }
        public bool StaticFallback { get; set; }
    }

    public sealed class TypingGameFeelPlan
    {
        public TypingGameFeelPlan()
        {
            Cues = new List<TypingGameFeelCue>();
        }
        public List<TypingGameFeelCue> Cues { get; private set; }
        public bool IsEmpty { get { return Cues.Count == 0; } }
    }

    /// <summary>
    /// Pure event-to-feedback mapper. It has no gameplay dependency and can be fed directly by mock
    /// MASTER events. All counts/durations are finite; reduced/minimal/LOW modes degrade safely.
    /// </summary>
    public sealed class TypingGameFeelPlanner
    {
        public const int MaxParticlesNormal = 12;
        public const int MaxParticlesLow = 4;

        public TypingGameFeelPlan Plan(TypingGameFeedbackEvent gameEvent, MotionMode mode, PerformanceProfile profile)
        {
            var plan = new TypingGameFeelPlan();
            if (gameEvent == null || string.IsNullOrWhiteSpace(gameEvent.Type)) return plan;

            switch (gameEvent.Type)
            {
                case "TYPING_CHAR_CORRECT":
                    Add(plan, TypingGameFeelEffect.ChargeGlow, MotionClass.FEEDBACK, 90, 0.35, 0, mode, profile);
                    Add(plan, TypingGameFeelEffect.TinySpark, MotionClass.FEEDBACK, 120, 0.32, 3, mode, profile);
                    break;
                case "TYPING_CHAR_WRONG":
                    Add(plan, TypingGameFeelEffect.GentleShake, MotionClass.FEEDBACK, 80, 0.18, 0, mode, profile);
                    break;
                case "TYPING_WORD_COMPLETED":
                case "TARGET_DESTROYED":
                    Add(plan, TypingGameFeelEffect.BeamBurst, MotionClass.SIGNAL, 180, 0.72, 8, mode, profile);
                    break;
                case "TARGET_RESCUED":
                    Add(plan, TypingGameFeelEffect.RescuePulse, MotionClass.SIGNAL, 210, 0.62, 6, mode, profile);
                    break;
                case "REWARD_GRANTED":
                    Add(plan, TypingGameFeelEffect.UnlockPulse, MotionClass.SIGNAL, 220, 0.65, 6, mode, profile);
                    break;
                case "BOSS_HIT":
                    Add(plan, TypingGameFeelEffect.BossHitPulse, MotionClass.SIGNAL, 150, 0.68, 6, mode, profile);
                    break;
                case "BOSS_DEFEATED":
                case "LEVEL_COMPLETED":
                    Add(plan, TypingGameFeelEffect.Celebration, MotionClass.SIGNAL, 420, 0.86, 12, mode, profile);
                    break;
            }
            return plan;
        }

        private static void Add(TypingGameFeelPlan plan, TypingGameFeelEffect effect, MotionClass cls, int durationMs,
            double intensity, int particles, MotionMode mode, PerformanceProfile profile)
        {
            var low = profile == PerformanceProfile.LOW;
            var minimal = mode == MotionMode.Minimal;
            var reduced = mode == MotionMode.Reduced;

            if (minimal)
            {
                // Keep the semantic signal without motion. Host may render one-frame highlight/static icon.
                plan.Cues.Add(new TypingGameFeelCue
                {
                    Effect = effect,
                    MotionClass = cls,
                    DurationMs = 0,
                    Intensity = Math.Min(0.55, intensity),
                    ParticleCount = 0,
                    StaticFallback = true
                });
                return;
            }

            var particleCap = low ? MaxParticlesLow : MaxParticlesNormal;
            var count = Math.Min(particleCap, Math.Max(0, particles));
            if (reduced) count = Math.Min(count, 2);
            if (low) count = Math.Min(count, MaxParticlesLow);

            plan.Cues.Add(new TypingGameFeelCue
            {
                Effect = effect,
                MotionClass = cls,
                DurationMs = Math.Max(1, reduced || low ? (int)Math.Ceiling(durationMs * 0.7) : durationMs),
                Intensity = Math.Max(0, Math.Min(1, reduced || low ? intensity * 0.72 : intensity)),
                ParticleCount = count,
                StaticFallback = false
            });
        }
    }

    public sealed class GameFeelAssetResolution
    {
        public string RequestedKey { get; set; }
        public string ResolvedKey { get; set; }
        public string Path { get; set; }
        public bool Found { get; set; }
        public bool UsedFallback { get; set; }
        public string Reason { get; set; }
    }

    public sealed class GameFeelAssetRegistry
    {
        private sealed class Entry
        {
            public string Path;
            public string FallbackKey;
        }

        private readonly Dictionary<string, Entry> _entries =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public void Register(string key, string path, string fallbackKey = null)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Asset key is required.", "key");
            _entries[key] = new Entry { Path = path ?? string.Empty, FallbackKey = fallbackKey };
        }

        public GameFeelAssetResolution Resolve(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return Missing(key, "asset_key_invalid");

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = key;
            var usedFallback = false;
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (!visited.Add(current)) return Missing(key, "asset_fallback_cycle");
                Entry entry;
                if (!_entries.TryGetValue(current, out entry)) return Missing(key, "asset_key_unregistered");
                if (!string.IsNullOrWhiteSpace(entry.Path) && File.Exists(entry.Path))
                {
                    return new GameFeelAssetResolution
                    {
                        RequestedKey = key,
                        ResolvedKey = current,
                        Path = entry.Path,
                        Found = true,
                        UsedFallback = usedFallback,
                        Reason = usedFallback ? "fallback_resolved" : "resolved"
                    };
                }
                usedFallback = true;
                current = entry.FallbackKey;
            }
            return Missing(key, "asset_missing_no_fallback");
        }

        private static GameFeelAssetResolution Missing(string key, string reason)
        {
            return new GameFeelAssetResolution
            {
                RequestedKey = key,
                Found = false,
                UsedFallback = false,
                Reason = reason
            };
        }
    }
}
