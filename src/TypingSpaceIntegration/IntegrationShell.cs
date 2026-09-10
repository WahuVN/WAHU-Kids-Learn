using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WAHU.TypingSpace
{
    public sealed class TypingSpaceIntegrationShell
    {
        private readonly ITypingSpaceEventSink _events;
        private readonly ITypingSpaceRewardAdapter _rewards;
        private readonly ITypingSpaceAssetAdapter _assets;
        private readonly List<TypingTarget> _targets;
        private int _targetIndex;
        private string _typed = string.Empty;
        private int _errors;
        private bool _rewardEmitted;
        private GamePhase _phaseBeforePause;

        public TypingSpaceIntegrationShell(ITypingSpaceEventSink events, ITypingSpaceRewardAdapter rewards, ITypingSpaceAssetAdapter assets, IEnumerable<TypingTarget> targets)
        {
            _events = events ?? throw new ArgumentNullException("events");
            _rewards = rewards ?? throw new ArgumentNullException("rewards");
            _assets = assets ?? throw new ArgumentNullException("assets");
            _targets = (targets ?? throw new ArgumentNullException("targets")).ToList();
            if (_targets.Count == 0) throw new ArgumentException("At least one target is required.", "targets");
            ValidateTargets(_targets);
            Phase = GamePhase.Intro;
        }

        public GamePhase Phase { get; private set; }
        public TypingTarget CurrentTarget { get { return _targetIndex >= 0 && _targetIndex < _targets.Count ? _targets[_targetIndex] : null; } }
        public TypingProgress Progress { get { return BuildProgress(); } }
        public string SceneAsset { get { return _assets.ResolveOrFallback("typing_space_scene"); } }

        public void Start()
        {
            _targetIndex = 0;
            _typed = string.Empty;
            _errors = 0;
            _rewardEmitted = false;
            Phase = CurrentTarget.Kind == "boss" ? GamePhase.Boss : GamePhase.Playing;
            Publish("TYPING_TARGET_SPAWNED", CurrentTarget, null, CurrentTarget.DisplayText);
        }

        public void Pause()
        {
            if (Phase == GamePhase.Paused || Phase == GamePhase.Complete || Phase == GamePhase.Intro) return;
            _phaseBeforePause = Phase;
            Phase = GamePhase.Paused;
            Publish("GAME_PAUSED", CurrentTarget, null, null);
        }

        public void Resume()
        {
            if (Phase != GamePhase.Paused) return;
            Phase = _phaseBeforePause;
            Publish("GAME_RESUMED", CurrentTarget, null, null);
        }

        public bool Input(TypingInput input)
        {
            if (Phase == GamePhase.Paused || Phase == GamePhase.Complete || Phase == GamePhase.Intro || CurrentTarget == null) return false;
            if (input == null) throw new ArgumentNullException("input");
            var key = input.NormalizedKey ?? string.Empty;
            if (key.Length == 0 || key == "\r" || key == "\n" || key == "\t") return false;
            var candidate = _typed + key;
            var prefixMatch = CurrentTarget.AcceptedInputs.Any(x => x.StartsWith(candidate, StringComparison.OrdinalIgnoreCase));
            if (!prefixMatch)
            {
                _errors++;
                Phase = GamePhase.Feedback;
                Publish("TYPING_CHAR_WRONG", CurrentTarget, null, key);
                Phase = CurrentTarget.Kind == "boss" ? GamePhase.Boss : GamePhase.Playing;
                return false;
            }

            _typed = candidate;
            Publish("TYPING_CHAR_CORRECT", CurrentTarget, _typed.Length, key);
            var completed = CurrentTarget.AcceptedInputs.Any(x => string.Equals(x, _typed, StringComparison.OrdinalIgnoreCase));
            if (!completed) return true;

            Publish("TYPING_WORD_COMPLETED", CurrentTarget, CurrentTarget.RewardValue, CurrentTarget.DisplayText);
            PublishAction(CurrentTarget);
            Advance();
            return true;
        }

        public void Replay()
        {
            if (Phase != GamePhase.Complete) return;
            Start();
        }

        public void InjectDuplicateRewardEventForQa()
        {
            TryGrantCompletionReward();
        }

        private void Advance()
        {
            _targetIndex++;
            _typed = string.Empty;
            _errors = 0;
            if (_targetIndex >= _targets.Count)
            {
                Phase = GamePhase.Complete;
                Publish("LEVEL_COMPLETED", null, null, null);
                TryGrantCompletionReward();
                return;
            }
            Phase = CurrentTarget.Kind == "boss" ? GamePhase.Boss : GamePhase.Playing;
            Publish("TYPING_TARGET_SPAWNED", CurrentTarget, null, CurrentTarget.DisplayText);
        }

        private void TryGrantCompletionReward()
        {
            if (_rewardEmitted) return;
            if (_rewards.TryGrant("typing-space:v1:completion", 1))
            {
                _rewardEmitted = true;
                Publish("REWARD_GRANTED", null, 1, "typing-space:v1:completion");
            }
        }

        private void PublishAction(TypingTarget target)
        {
            if (target.Kind == "rescue") Publish("TARGET_RESCUED", target, target.RewardValue, null);
            else if (target.Kind == "boss")
            {
                Publish("BOSS_HIT", target, target.RewardValue, null);
                var hasLaterBoss = _targets.Skip(_targetIndex + 1).Any(x => x.Kind == "boss");
                if (!hasLaterBoss) Publish("BOSS_DEFEATED", target, target.RewardValue, null);
            }
            else Publish("TARGET_DESTROYED", target, target.RewardValue, null);
        }

        private TypingProgress BuildProgress()
        {
            var target = CurrentTarget;
            var total = target == null || target.AcceptedInputs == null || target.AcceptedInputs.Count == 0 ? 0 : target.AcceptedInputs.Min(x => x.Length);
            return new TypingProgress { TargetId = target == null ? null : target.Id, TypedCount = _typed.Length, TotalCount = total, Completed = target != null && target.AcceptedInputs.Any(x => string.Equals(x, _typed, StringComparison.OrdinalIgnoreCase)), ErrorCount = _errors };
        }

        private void Publish(string type, TypingTarget target, int? value, string text)
        {
            _events.Publish(new CoreEvent { Type = type, TargetId = target == null ? null : target.Id, Value = value, Text = text, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        }

        private static void ValidateTargets(IEnumerable<TypingTarget> targets)
        {
            foreach (var t in targets)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.Id) || string.IsNullOrWhiteSpace(t.DisplayText)) throw new ArgumentException("Invalid target.");
                if (t.AcceptedInputs == null || t.AcceptedInputs.Count == 0 || t.AcceptedInputs.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Target must have acceptedInputs.");
                if (t.Language != "vi" && t.Language != "en") throw new ArgumentException("Target language must be vi/en.");
                if (t.Kind != "shoot" && t.Kind != "rescue" && t.Kind != "unlock" && t.Kind != "boss") throw new ArgumentException("Invalid target kind.");
            }
        }
    }

    public static class TypingSpaceMockFactory
    {
        public static IList<TypingTarget> CreateV1Flow()
        {
            return new List<TypingTarget>
            {
                T("en-cat", "cat", "en", "shoot", 1, "cat"),
                T("vi-meo", "mèo", "vi", "rescue", 1, "mèo", "meo"),
                T("en-key", "key", "en", "unlock", 1, "key"),
                T("boss-1", "star", "en", "boss", 1, "star"),
                T("boss-2", "moon", "en", "boss", 1, "moon"),
                T("boss-3", "sun", "en", "boss", 1, "sun")
            };
        }

        private static TypingTarget T(string id, string display, string language, string kind, int reward, params string[] accepted)
        {
            return new TypingTarget { Id = id, DisplayText = display, Language = language, Kind = kind, RewardValue = reward, Difficulty = 1, AcceptedInputs = new List<string>(accepted) };
        }
    }
}
