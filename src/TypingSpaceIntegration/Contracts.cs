using System;
using System.Collections.Generic;

namespace WAHU.TypingSpace
{
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

    public enum GamePhase
    {
        Intro,
        Playing,
        Feedback,
        Boss,
        Complete,
        Paused
    }

    public sealed class CoreEvent
    {
        public string Type { get; set; }
        public string TargetId { get; set; }
        public int? Value { get; set; }
        public string Text { get; set; }
        public long Timestamp { get; set; }
    }

    public interface ITypingSpaceEventSink
    {
        void Publish(CoreEvent value);
    }

    public interface ITypingSpaceAssetAdapter
    {
        string ResolveOrFallback(string assetId);
    }

    public interface ITypingSpaceRewardAdapter
    {
        bool TryGrant(string rewardKey, int value);
        int GrantCount { get; }
    }

    public sealed class InMemoryEventSink : ITypingSpaceEventSink
    {
        private readonly List<CoreEvent> _events = new List<CoreEvent>();
        public IList<CoreEvent> Events { get { return _events.AsReadOnly(); } }
        public void Publish(CoreEvent value) { if (value != null) _events.Add(value); }
    }

    public sealed class MockAssetAdapter : ITypingSpaceAssetAdapter
    {
        public string ResolveOrFallback(string assetId)
        {
            return string.IsNullOrWhiteSpace(assetId) ? "fallback://placeholder" : "mock://" + assetId;
        }
    }

    public sealed class IdempotentMockRewardAdapter : ITypingSpaceRewardAdapter
    {
        private readonly HashSet<string> _keys = new HashSet<string>(StringComparer.Ordinal);
        public int GrantCount { get; private set; }
        public bool TryGrant(string rewardKey, int value)
        {
            if (string.IsNullOrWhiteSpace(rewardKey) || value < 0) return false;
            if (!_keys.Add(rewardKey)) return false;
            GrantCount++;
            return true;
        }
    }
}
