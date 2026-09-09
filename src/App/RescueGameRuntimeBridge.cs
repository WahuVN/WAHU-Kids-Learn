using System;
using System.Collections.Generic;
using System.IO;
using WAHU.Audio;
using WAHU.Data;
using WAHU.Learning;
using WAHU.Session;

namespace WAHUKidsLearn
{
    internal sealed class RescueGameRuntimeBridge : IDisposable
    {
        private readonly MathRescueGameplayCoordinator _gameplay;
        private readonly RescueGameRewardService _rewards;
        private readonly MathQuickRescueAdaptiveSelector _adaptive = new MathQuickRescueAdaptiveSelector();
        private readonly MathQuickRescueLearningPack _content;
        private readonly List<string> _recentQuestionIds = new List<string>();
        private readonly AudioCoordinator _audio;
        private readonly RescueGameAudioHooks _audioHooks;
        private readonly int _seed;
        private RescueRewardSnapshot _rewardSnapshot;
        private BehaviorDecision _lastBehavior;
        private bool _rescuePresentationActive;
        private bool _disposed;

        public RescueGameRuntimeBridge(
            LearningDatabase database,
            string templatePath,
            string eventCatalogPath,
            string performanceProfile,
            int seed,
            string eventId,
            string fallbackLessonId,
            string learningContentPath,
            string audioRoot)
        {
            if (database == null) throw new ArgumentNullException("database");
            _seed = seed;
            _gameplay = new MathRescueGameplayCoordinator(
                database, templatePath, eventCatalogPath, performanceProfile, seed, eventId, fallbackLessonId);
            _rewards = new RescueGameRewardService(database);
            MathQuickRescueLearningPack loadedContent = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(learningContentPath) && File.Exists(learningContentPath))
                    loadedContent = new MathQuickRescueContentSource().Load(learningContentPath);
            }
            catch
            {
                loadedContent = null;
            }
            _content = loadedContent != null &&
                string.Equals(loadedContent.TargetLessonId, fallbackLessonId, StringComparison.Ordinal)
                ? loadedContent
                : null;
            try
            {
                _audio = new AudioCoordinator(new SoundPlayerBackend(), 8L * 1024L * 1024L);
                _audioHooks = new RescueGameAudioHooks(_audio, audioRoot);
            }
            catch
            {
                _audio = null;
                _audioHooks = null;
            }
        }

        public MathRescueGameplaySnapshot CurrentState { get { return _gameplay.CurrentState; } }
        public bool IsPaused { get { return CurrentState.Paused; } }
        public bool IsInteractive { get { var s = CurrentState; return !s.Paused && !s.Completed && s.Resumable; } }
        public RescueRewardSnapshot RewardSnapshot { get { return _rewardSnapshot; } }

        public MathRescueGameplayStartResult Start(string displayName)
        {
            EnsureNotDisposed();
            var result = _gameplay.Start(displayName);
            _rescuePresentationActive = result.Learning != null && result.Learning.Event != null &&
                result.State != null && result.State.EventState != null && !result.State.EventState.FallbackToLessonPresentation;
            if (_rescuePresentationActive)
            {
                try { _rewardSnapshot = _rewards.BeginOrResume(result.State.SessionId).Snapshot; } catch { _rewardSnapshot = null; }
                TryAudio(delegate { return _audioHooks.TryStartMissionMusic(); });
            }
            else
            {
                _rewardSnapshot = null;
            }
            return result;
        }

        public MathRescueGameplaySnapshot EnsureQuestionActive()
        {
            EnsureNotDisposed();
            for (var guard = 0; guard < 8; guard++)
            {
                var state = _gameplay.CurrentState;
                switch (state.Phase)
                {
                    case MathRescueGameplayPhase.INTRO:
                        _gameplay.AcknowledgeIntro();
                        continue;
                    case MathRescueGameplayPhase.CHECKPOINT_COMPLETE:
                        _gameplay.AdvanceCheckpoint();
                        continue;
                    case MathRescueGameplayPhase.NEXT_CHECKPOINT:
                        var transition = _gameplay.ContinueAfterCheckpoint();
                        if (transition.State.Phase == MathRescueGameplayPhase.GAME_COMPLETE)
                        {
                            ReconcileFinalReward(transition.State.SessionId);
                            return transition.State;
                        }
                        continue;
                    case MathRescueGameplayPhase.CHECKPOINT_READY:
                        return _gameplay.BeginQuestion();
                    case MathRescueGameplayPhase.QUESTION_ACTIVE:
                    case MathRescueGameplayPhase.REPAIR:
                        return state;
                    case MathRescueGameplayPhase.ANSWER_FEEDBACK:
                        _gameplay.AdvanceAfterFeedback();
                        continue;
                    default:
                        return state;
                }
            }
            throw new InvalidOperationException("Rescue gameplay did not reach an interactive question state.");
        }

        public MathAnswerOutcome Submit(string answer, int hintLevel, string inputMethod, DateTime answeredAtUtc, int responseMs)
        {
            EnsureNotDisposed();
            var before = _gameplay.CurrentState;
            var question = before.CurrentQuestion;
            MathRescueGameplayAnswerResult result;
            if (before.Phase == MathRescueGameplayPhase.REPAIR)
                result = _gameplay.SubmitRepairAnswerAt(answer, hintLevel, inputMethod, answeredAtUtc, responseMs);
            else
                result = _gameplay.SubmitAnswerAt(answer, hintLevel, inputMethod, answeredAtUtc, responseMs);

            var learning = result.Learning.Learning;
            _lastBehavior = learning.Behavior;
            if (learning.QuestionCompleted && question != null && !string.IsNullOrWhiteSpace(question.ContentQuestionId))
            {
                _recentQuestionIds.Add(question.ContentQuestionId);
                while (_recentQuestionIds.Count > 6) _recentQuestionIds.RemoveAt(0);
            }
            if (_rescuePresentationActive && learning.IsCorrect)
                TryAudio(delegate { return _audioHooks.PlayPositiveAnswer(); });

            var after = _gameplay.AdvanceAfterFeedback();
            if (_rescuePresentationActive && after.Phase == MathRescueGameplayPhase.CHECKPOINT_COMPLETE)
            {
                try
                {
                    var transition = _rewards.ReadTransition(after.SessionId, _rewardSnapshot, false);
                    _rewardSnapshot = transition.Snapshot;
                    if (transition.Signal == RescueRewardSignal.CheckpointStarEarned)
                        TryAudio(delegate { return _audioHooks.PlayCheckpointStar(); });
                }
                catch { }
            }
            return learning;
        }

        public MathQuickRescueContentDecision CurrentSupportDecision()
        {
            if (!_rescuePresentationActive || _content == null) return null;
            try
            {
                var state = _gameplay.CurrentState;
                var checkpointNumber = Math.Max(1, Math.Min(3, state.CurrentCheckpoint));
                var decision = _adaptive.Select(_content, checkpointNumber, _lastBehavior, _recentQuestionIds, _seed);
                if (decision == null || state.CurrentQuestion == null ||
                    string.IsNullOrWhiteSpace(state.CurrentQuestion.ContentQuestionId)) return null;

                var checkpoint = _content.GetCheckpoint(checkpointNumber);
                if (checkpoint == null || checkpoint.QuestionOptions == null) return null;
                MathQuickRescueQuestionOption actual = null;
                foreach (var option in checkpoint.QuestionOptions)
                {
                    if (option != null && string.Equals(option.QuestionId, state.CurrentQuestion.ContentQuestionId, StringComparison.Ordinal))
                    {
                        actual = option;
                        break;
                    }
                }
                if (actual == null) return null;

                var adaptiveQuestionId = decision.QuestionId;
                decision.QuestionId = actual.QuestionId;
                decision.Variant = actual.Variant;
                if (!string.Equals(adaptiveQuestionId, actual.QuestionId, StringComparison.Ordinal))
                    decision.Reason = (decision.Reason ?? string.Empty) + ":reconciled_persisted_question";
                return decision;
            }
            catch { return null; }
        }

        public MathSessionSummary CompleteGame()
        {
            EnsureNotDisposed();
            var state = _gameplay.CurrentState;
            if (state.Phase == MathRescueGameplayPhase.CHECKPOINT_COMPLETE)
            {
                _gameplay.AdvanceCheckpoint();
                state = _gameplay.CurrentState;
            }
            MathRescueGameplayTransitionResult transition = null;
            if (state.Phase == MathRescueGameplayPhase.NEXT_CHECKPOINT)
                transition = _gameplay.ContinueAfterCheckpoint();
            state = _gameplay.CurrentState;
            if (state.Phase != MathRescueGameplayPhase.GAME_COMPLETE)
                throw new InvalidOperationException("Rescue game cannot complete before all three checkpoints are complete.");
            ReconcileFinalReward(state.SessionId);
            return transition != null && transition.Completion != null
                ? transition.Completion.LearningSummary
                : state.CompletionSummary;
        }

        public MathRescueGameplaySuspendResult SuspendForBreak(string reason)
        {
            EnsureNotDisposed();
            if (_audio != null) _audio.Stop();
            return _gameplay.SuspendForBreak(reason);
        }

        private void ReconcileFinalReward(string sessionId)
        {
            if (!_rescuePresentationActive) return;
            try
            {
                var transition = _rewards.ReadTransition(sessionId, _rewardSnapshot, true);
                _rewardSnapshot = transition.Snapshot;
                if (transition.Signal == RescueRewardSignal.MissionComplete)
                {
                    TryAudio(delegate { return _audioHooks.PlayMissionComplete(); });
                    TryAudio(delegate { return _audioHooks.PlayGardenUnlock(); });
                }
            }
            catch { }
        }

        private void TryAudio(Func<AudioPlaybackResult> action)
        {
            if (_audioHooks == null || action == null) return;
            try { action(); } catch { }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException("RescueGameRuntimeBridge");
        }

        public void Dispose()
        {
            if (_disposed) return;
            try { if (_audio != null) _audio.Dispose(); } catch { }
            try { _gameplay.Dispose(); } catch { }
            _disposed = true;
        }
    }
}
