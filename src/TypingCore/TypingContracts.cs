using System;
using System.Collections.Generic;

namespace WAHU.TypingCore
{
    public static class TypingGamePhase
    {
        public const string Intro = "intro";
        public const string Playing = "playing";
        public const string Feedback = "feedback";
        public const string Boss = "boss";
        public const string Complete = "complete";
        public const string Paused = "paused";
    }

    public static class TypingInputSource
    {
        public const string Physical = "physical";
        public const string Onscreen = "onscreen";
    }

    public static class TypingTargetKind
    {
        public const string Shoot = "shoot";
        public const string Rescue = "rescue";
        public const string Unlock = "unlock";
        public const string Boss = "boss";
    }

    public static class TypingLanguage
    {
        public const string Vietnamese = "vi";
        public const string English = "en";
    }

    public static class TypingEventType
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

    public sealed class TypingInput
    {
        public string RawKey { get; set; }
        public string NormalizedKey { get; set; }
        public long Timestamp { get; set; }
        public string Source { get; set; }
    }

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

    public sealed class TypingProgress
    {
        public string TargetId { get; set; }
        public int TypedCount { get; set; }
        public int TotalCount { get; set; }
        public bool Completed { get; set; }
        public int ErrorCount { get; set; }
    }

    public sealed class TypingCoreEvent
    {
        public string Type { get; set; }
        public string TargetId { get; set; }
        public int? Value { get; set; }
        public string Text { get; set; }
        public long Timestamp { get; set; }
    }

    public sealed class TypingEngineSnapshot
    {
        public string Phase { get; set; }
        public TypingTarget ActiveTarget { get; set; }
        public TypingProgress Progress { get; set; }
        public bool Paused { get; set; }
    }

    public sealed class TypingEngineResult
    {
        public TypingEngineSnapshot Snapshot { get; set; }
        public IList<TypingCoreEvent> Events { get; set; }
        public bool Accepted { get; set; }
        public bool Ignored { get; set; }
        public string Reason { get; set; }
    }
}
