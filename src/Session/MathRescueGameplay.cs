using System;
using System.Collections.Generic;
using System.Linq;
using WAHU.Data;
using WAHU.Learning;

namespace WAHU.Session
{
    public enum MathRescueGameplayPhase
    {
        IDLE = 0,
        INTRO = 1,
        CHECKPOINT_READY = 2,
        QUESTION_ACTIVE = 3,
        ANSWER_FEEDBACK = 4,
        REPAIR = 5,
        CHECKPOINT_COMPLETE = 6,
        NEXT_CHECKPOINT = 7,
        GAME_COMPLETE = 8
    }

    public enum MathRescueAnswerState
    {
        NONE = 0,
        CORRECT = 1,
        WRONG = 2
    }

    public enum MathRescueFeedbackState
    {
        NONE = 0,
        CORRECT = 1,
        TRY_AGAIN = 2,
        REPAIR = 3,
        CHECKPOINT_COMPLETE = 4,
        GAME_COMPLETE = 5
    }

    public sealed class MathRescueProgressSnapshot
    {
        public int CompletedCheckpoints { get; set; }
        public int TotalCheckpoints { get; set; }
        public double Fraction { get; set; }
    }

    public sealed class MathRescueGameplaySnapshot
    {
        public MathRescueGameplayPhase Phase { get; set; }
        public string SessionId { get; set; }
        public string EventId { get; set; }
        public int CurrentCheckpoint { get; set; }
        public MathQuestion CurrentQuestion { get; set; }
        public MathRescueProgressSnapshot Progress { get; set; }
        public MathRescueAnswerState AnswerState { get; set; }
        public MathRescueFeedbackState FeedbackState { get; set; }
        public string FeedbackVi { get; set; }
        public bool CanAnswer { get; set; }
        public bool Completed { get; set; }
        public bool Paused { get; set; }
        public bool Resumable { get; set; }
        public bool RetryPending { get; set; }
        public MathGameEventState EventState { get; set; }
        public MathSessionSummary CompletionSummary { get; set; }
    }

    // AI4/persistence may serialize this token beside its own UI state. The token contains only
    // gameplay presentation state; durable learning truth remains in MathSessionCoordinator/SQLite.
    public sealed class MathRescueGameplayResumeToken
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; }
        public string SessionId { get; set; }
        public MathRescueGameplayPhase Phase { get; set; }
        public int CurrentCheckpoint { get; set; }
        public int CompletedCheckpointCount { get; set; }
        public MathQuestion CurrentQuestion { get; set; }
        public MathRescueAnswerState AnswerState { get; set; }
        public MathRescueFeedbackState FeedbackState { get; set; }
        public string FeedbackVi { get; set; }
        public bool RetryPending { get; set; }
        public bool LastAnswerCanRetry { get; set; }
    }

    public sealed class MathRescueGameplayStartResult
    {
        public MathGameEventStartResult Learning { get; set; }
        public MathRescueGameplaySnapshot State { get; set; }
        public bool ResumedFromGameplayToken { get; set; }
    }

    public sealed class MathRescueGameplayAnswerResult
    {
        public MathGameEventAnswerResult Learning { get; set; }
        public MathRescueGameplaySnapshot State { get; set; }
    }

    public sealed class MathRescueGameplaySuspendResult
    {
        public MathSessionSummary LearningSummary { get; set; }
        public MathRescueGameplayResumeToken ResumeToken { get; set; }
        public MathRescueGameplaySnapshot State { get; set; }
    }

    public sealed class MathRescueGameplayTransitionResult
    {
        public MathGameEventCompletionResult Completion { get; set; }
        public MathRescueGameplaySnapshot State { get; set; }
    }

    public sealed class MathRescueGameplayStateChangedEventArgs : EventArgs
    {
        public string Reason { get; set; }
        public MathRescueGameplaySnapshot Previous { get; set; }
        public MathRescueGameplaySnapshot Current { get; set; }
    }

    /// <summary>
    /// Deterministic, headless state machine for the three-stage quick-rescue Math game.
    /// UI code drives explicit transitions and renders CurrentState; it does not infer progress
    /// from button clicks. One gate protects every gameplay transition, so rapid double-clicks
    /// cannot submit twice or advance two states at once.
    /// </summary>
    public sealed class MathRescueGameplayCoordinator : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MathGameEventCoordinator _game;
        private MathRescueGameplayPhase _phase = MathRescueGameplayPhase.IDLE;
        private MathQuestion _currentQuestion;
        private MathRescueAnswerState _answerState = MathRescueAnswerState.NONE;
        private MathRescueFeedbackState _feedbackState = MathRescueFeedbackState.NONE;
        private string _feedbackVi;
        private bool _lastAnswerCanRetry;
        private bool _started;
        private bool _paused;
        private bool _disposed;
        private int _currentCheckpoint = 1;
        private MathGameEventStartResult _start;
        private MathGameEventCompletionResult _completion;

        public MathRescueGameplayCoordinator(
            LearningDatabase database,
            string templatePath,
            string eventCatalogPath,
            string performanceProfile,
            int seed,
            string eventId,
            string fallbackLessonId)
        {
            _game = new MathGameEventCoordinator(
                database, templatePath, eventCatalogPath, performanceProfile, seed, eventId, fallbackLessonId);
        }

        public event EventHandler<MathRescueGameplayStateChangedEventArgs> StateChanged;

        public MathRescueGameplaySnapshot CurrentState
        {
            get
            {
                lock (_gate)
                {
                    EnsureNotDisposedLocked();
                    return BuildSnapshotLocked();
                }
            }
        }

        public MathRescueGameplayStartResult Start(string displayName)
        {
            return StartCore(displayName, null, false);
        }

        public MathRescueGameplayStartResult Resume(string displayName, MathRescueGameplayResumeToken token)
        {
            if (token == null) throw new ArgumentNullException("token");
            return StartCore(displayName, token, true);
        }

        private MathRescueGameplayStartResult StartCore(string displayName, MathRescueGameplayResumeToken token, bool requireToken)
        {
            MathRescueGameplayStateChangedEventArgs change;
            MathRescueGameplayStartResult result;
            lock (_gate)
            {
                EnsureNotDisposedLocked();
                RequirePhaseLocked(MathRescueGameplayPhase.IDLE);
                var previous = BuildSnapshotLocked();
                _start = _game.Start(displayName);
                _started = true;
                _paused = false;
                _completion = null;
                _answerState = MathRescueAnswerState.NONE;
                _feedbackState = MathRescueFeedbackState.NONE;
                _feedbackVi = null;
                _lastAnswerCanRetry = false;
                _currentQuestion = null;

                var restoredFromToken = token != null;
                try
                {
                    if (token != null)
                        RestoreGameplayTokenLocked(token);
                    else
                    {
                        if (requireToken) throw new InvalidOperationException("Gameplay resume token is required.");
                        RestoreFromDurableLearningLocked();
                    }
                }
                catch
                {
                    // A bad/stale presentation token must never terminalize or mutate durable learning.
                    // Freeze the already-open durable session so a fresh gameplay coordinator can
                    // recover it from SQLite (with or without a repaired token) on the next attempt.
                    try { _game.SuspendForBreak("invalid_gameplay_resume_token"); } catch { }
                    _paused = true;
                    throw;
                }

                var current = BuildSnapshotLocked();
                result = new MathRescueGameplayStartResult
                {
                    Learning = _start,
                    State = current,
                    ResumedFromGameplayToken = restoredFromToken
                };
                change = NewChange(previous, current, restoredFromToken ? "resume" : "start");
            }
            Publish(change);
            return result;
        }

        public MathRescueGameplaySnapshot AcknowledgeIntro()
        {
            return TransitionSimple(MathRescueGameplayPhase.INTRO, MathRescueGameplayPhase.CHECKPOINT_READY, "intro_acknowledged", delegate
            {
                _currentCheckpoint = 1;
                ResetFeedbackLocked();
            });
        }

        public MathRescueGameplaySnapshot BeginQuestion()
        {
            MathRescueGameplayStateChangedEventArgs change;
            MathRescueGameplaySnapshot current;
            lock (_gate)
            {
                EnsureInteractiveLocked();
                RequirePhaseLocked(MathRescueGameplayPhase.CHECKPOINT_READY);
                var previous = BuildSnapshotLocked();
                var question = _game.NextQuestion();
                if (question == null)
                    throw new InvalidOperationException("No Math question is available for the current rescue checkpoint.");
                _currentQuestion = CloneQuestion(question);
                _phase = MathRescueGameplayPhase.QUESTION_ACTIVE;
                ResetFeedbackLocked();
                current = BuildSnapshotLocked();
                change = NewChange(previous, current, "question_started");
            }
            Publish(change);
            return current;
        }

        public MathRescueGameplayAnswerResult SubmitAnswerAt(
            string answer,
            int hintLevel,
            string inputMethod,
            DateTime answeredAtUtc,
            int responseMs)
        {
            return SubmitAnswerCore(answer, hintLevel, inputMethod, answeredAtUtc, responseMs, false);
        }

        public MathRescueGameplayAnswerResult SubmitRepairAnswerAt(
            string answer,
            int hintLevel,
            string inputMethod,
            DateTime answeredAtUtc,
            int responseMs)
        {
            return SubmitAnswerCore(answer, hintLevel, inputMethod, answeredAtUtc, responseMs, true);
        }

        private MathRescueGameplayAnswerResult SubmitAnswerCore(
            string answer,
            int hintLevel,
            string inputMethod,
            DateTime answeredAtUtc,
            int responseMs,
            bool repair)
        {
            MathRescueGameplayStateChangedEventArgs change;
            MathRescueGameplayAnswerResult result;
            lock (_gate)
            {
                EnsureInteractiveLocked();
                RequirePhaseLocked(repair ? MathRescueGameplayPhase.REPAIR : MathRescueGameplayPhase.QUESTION_ACTIVE);
                var previous = BuildSnapshotLocked();
                var learning = repair
                    ? _game.SubmitRetryAnswerAt(answer, hintLevel, inputMethod, answeredAtUtc, responseMs)
                    : _game.SubmitAnswerWithRetryAt(answer, hintLevel, inputMethod, answeredAtUtc, responseMs);

                _answerState = learning.Learning.IsCorrect ? MathRescueAnswerState.CORRECT : MathRescueAnswerState.WRONG;
                _lastAnswerCanRetry = learning.Learning.CanRetry;
                _feedbackState = learning.Learning.CanRetry
                    ? MathRescueFeedbackState.TRY_AGAIN
                    : (learning.Learning.IsCorrect ? MathRescueFeedbackState.CORRECT : MathRescueFeedbackState.CHECKPOINT_COMPLETE);
                _feedbackVi = learning.Learning.FeedbackVi;
                _phase = MathRescueGameplayPhase.ANSWER_FEEDBACK;
                var current = BuildSnapshotLocked();
                result = new MathRescueGameplayAnswerResult { Learning = learning, State = current };
                change = NewChange(previous, current, repair ? "repair_answer_submitted" : "answer_submitted");
            }
            Publish(change);
            return result;
        }

        public MathRescueGameplaySnapshot AdvanceAfterFeedback()
        {
            MathRescueGameplayStateChangedEventArgs change;
            MathRescueGameplaySnapshot current;
            lock (_gate)
            {
                EnsureInteractiveLocked();
                RequirePhaseLocked(MathRescueGameplayPhase.ANSWER_FEEDBACK);
                var previous = BuildSnapshotLocked();
                if (_lastAnswerCanRetry)
                {
                    _phase = MathRescueGameplayPhase.REPAIR;
                    _feedbackState = MathRescueFeedbackState.REPAIR;
                }
                else
                {
                    _phase = MathRescueGameplayPhase.CHECKPOINT_COMPLETE;
                    _feedbackState = MathRescueFeedbackState.CHECKPOINT_COMPLETE;
                    _currentQuestion = null;
                    var completed = Math.Max(0, Math.Min(MathSessionCoordinator.TargetedLessonQuestionCount, _game.CurrentState.CompletedCheckpointCount));
                    _currentCheckpoint = Math.Max(1, completed);
                }
                current = BuildSnapshotLocked();
                change = NewChange(previous, current, _lastAnswerCanRetry ? "repair_required" : "checkpoint_completed");
            }
            Publish(change);
            return current;
        }

        public MathRescueGameplaySnapshot AdvanceCheckpoint()
        {
            return TransitionSimple(MathRescueGameplayPhase.CHECKPOINT_COMPLETE, MathRescueGameplayPhase.NEXT_CHECKPOINT, "checkpoint_advanced", delegate
            {
                _answerState = MathRescueAnswerState.NONE;
                _feedbackState = MathRescueFeedbackState.NONE;
                _feedbackVi = null;
                _lastAnswerCanRetry = false;
            });
        }

        public MathRescueGameplayTransitionResult ContinueAfterCheckpoint()
        {
            MathRescueGameplayStateChangedEventArgs change;
            MathRescueGameplayTransitionResult result;
            lock (_gate)
            {
                EnsureInteractiveLocked();
                RequirePhaseLocked(MathRescueGameplayPhase.NEXT_CHECKPOINT);
                var previous = BuildSnapshotLocked();
                var eventState = _game.CurrentState;
                if (eventState.CompletedCheckpointCount >= MathSessionCoordinator.TargetedLessonQuestionCount)
                {
                    _completion = _game.Complete();
                    _phase = MathRescueGameplayPhase.GAME_COMPLETE;
                    _currentCheckpoint = MathSessionCoordinator.TargetedLessonQuestionCount;
                    _feedbackState = MathRescueFeedbackState.GAME_COMPLETE;
                    _feedbackVi = _game.Event == null ? null : _game.Event.CompletionVi;
                    _currentQuestion = null;
                }
                else
                {
                    _currentCheckpoint = Math.Max(1, Math.Min(MathSessionCoordinator.TargetedLessonQuestionCount,
                        eventState.CompletedCheckpointCount + 1));
                    _phase = MathRescueGameplayPhase.CHECKPOINT_READY;
                    ResetFeedbackLocked();
                }
                var current = BuildSnapshotLocked();
                result = new MathRescueGameplayTransitionResult { Completion = _completion, State = current };
                change = NewChange(previous, current,
                    _phase == MathRescueGameplayPhase.GAME_COMPLETE ? "game_completed" : "next_checkpoint_ready");
            }
            Publish(change);
            return result;
        }

        public MathRescueGameplaySuspendResult SuspendForBreak(string reason)
        {
            MathRescueGameplayStateChangedEventArgs change;
            MathRescueGameplaySuspendResult result;
            lock (_gate)
            {
                EnsureInteractiveLocked();
                if (_phase == MathRescueGameplayPhase.GAME_COMPLETE || _phase == MathRescueGameplayPhase.IDLE)
                    throw new InvalidOperationException("Completed or idle rescue gameplay cannot be suspended.");
                var previous = BuildSnapshotLocked();
                var token = BuildResumeTokenLocked();
                var summary = _game.SuspendForBreak(reason);
                _paused = true;
                var current = BuildSnapshotLocked();
                result = new MathRescueGameplaySuspendResult
                {
                    LearningSummary = summary,
                    ResumeToken = token,
                    State = current
                };
                change = NewChange(previous, current, "suspended");
            }
            Publish(change);
            return result;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _game.Dispose();
                _paused = _started && _phase != MathRescueGameplayPhase.GAME_COMPLETE;
                _disposed = true;
            }
        }

        private MathRescueGameplaySnapshot TransitionSimple(
            MathRescueGameplayPhase expected,
            MathRescueGameplayPhase next,
            string reason,
            Action mutation)
        {
            MathRescueGameplayStateChangedEventArgs change;
            MathRescueGameplaySnapshot current;
            lock (_gate)
            {
                EnsureInteractiveLocked();
                RequirePhaseLocked(expected);
                var previous = BuildSnapshotLocked();
                if (mutation != null) mutation();
                _phase = next;
                current = BuildSnapshotLocked();
                change = NewChange(previous, current, reason);
            }
            Publish(change);
            return current;
        }

        private void RestoreFromDurableLearningLocked()
        {
            var eventState = _game.CurrentState;
            if (!_start.Session.ResumedExistingSession)
            {
                _phase = MathRescueGameplayPhase.INTRO;
                _currentCheckpoint = 1;
                return;
            }

            if (_start.Session.RetryPending)
            {
                _currentQuestion = CloneQuestion(_game.NextQuestion());
                _phase = MathRescueGameplayPhase.REPAIR;
                _currentCheckpoint = Math.Max(1, eventState.CurrentCheckpointNumber);
                _answerState = MathRescueAnswerState.WRONG;
                _feedbackState = MathRescueFeedbackState.REPAIR;
                _lastAnswerCanRetry = true;
                return;
            }
            if (_start.Session.RestoredOpenQuestion)
            {
                _currentQuestion = CloneQuestion(_game.NextQuestion());
                _phase = MathRescueGameplayPhase.QUESTION_ACTIVE;
                _currentCheckpoint = Math.Max(1, eventState.CurrentCheckpointNumber);
                return;
            }
            if (_start.Session.CompletedQuestionCount > 0)
            {
                _phase = MathRescueGameplayPhase.CHECKPOINT_COMPLETE;
                _currentCheckpoint = Math.Min(MathSessionCoordinator.TargetedLessonQuestionCount, _start.Session.CompletedQuestionCount);
                _feedbackState = MathRescueFeedbackState.CHECKPOINT_COMPLETE;
                return;
            }

            _phase = MathRescueGameplayPhase.INTRO;
            _currentCheckpoint = 1;
        }

        private void RestoreGameplayTokenLocked(MathRescueGameplayResumeToken token)
        {
            if (token.SchemaVersion != MathRescueGameplayResumeToken.CurrentSchemaVersion)
                throw new InvalidOperationException("Unsupported rescue gameplay resume token schema.");
            if (!_start.Session.ResumedExistingSession ||
                !string.Equals(token.SessionId, _start.Session.SessionId, StringComparison.Ordinal))
                throw new InvalidOperationException("Rescue gameplay resume token does not match the durable active session.");
            if (token.Phase == MathRescueGameplayPhase.IDLE || token.Phase == MathRescueGameplayPhase.GAME_COMPLETE)
                throw new InvalidOperationException("Rescue gameplay resume token contains a non-resumable phase.");
            if (token.CurrentCheckpoint < 1 || token.CurrentCheckpoint > MathSessionCoordinator.TargetedLessonQuestionCount)
                throw new InvalidOperationException("Rescue gameplay resume token contains an invalid checkpoint.");
            if (token.CompletedCheckpointCount != _start.Session.CompletedQuestionCount)
                throw new InvalidOperationException("Rescue gameplay resume token progress does not match durable learning progress.");
            if (token.RetryPending != _start.Session.RetryPending)
                throw new InvalidOperationException("Rescue gameplay resume token retry state does not match durable learning progress.");

            var requiresOpenQuestion = token.Phase == MathRescueGameplayPhase.QUESTION_ACTIVE ||
                token.Phase == MathRescueGameplayPhase.REPAIR ||
                (token.Phase == MathRescueGameplayPhase.ANSWER_FEEDBACK && token.LastAnswerCanRetry);
            if (requiresOpenQuestion)
            {
                var durableQuestion = _game.NextQuestion();
                if (durableQuestion == null || token.CurrentQuestion == null ||
                    !string.Equals(durableQuestion.QuestionId, token.CurrentQuestion.QuestionId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Rescue gameplay resume token question does not match the durable open question.");
                _currentQuestion = CloneQuestion(durableQuestion);
            }
            else
            {
                _currentQuestion = CloneQuestion(token.CurrentQuestion);
            }

            if (token.Phase == MathRescueGameplayPhase.REPAIR && !token.RetryPending)
                throw new InvalidOperationException("Repair phase requires a durable retry-pending question.");
            if (token.Phase == MathRescueGameplayPhase.QUESTION_ACTIVE && token.RetryPending)
                throw new InvalidOperationException("Question-active phase cannot hide a durable retry-pending question.");

            _phase = token.Phase;
            _currentCheckpoint = token.CurrentCheckpoint;
            _answerState = token.AnswerState;
            _feedbackState = token.FeedbackState;
            _feedbackVi = token.FeedbackVi;
            _lastAnswerCanRetry = token.LastAnswerCanRetry;
        }

        private MathRescueGameplayResumeToken BuildResumeTokenLocked()
        {
            var eventState = _game.CurrentState;
            return new MathRescueGameplayResumeToken
            {
                SchemaVersion = MathRescueGameplayResumeToken.CurrentSchemaVersion,
                SessionId = _start == null ? null : _start.Session.SessionId,
                Phase = _phase,
                CurrentCheckpoint = _currentCheckpoint,
                CompletedCheckpointCount = eventState.CompletedCheckpointCount,
                CurrentQuestion = CloneQuestion(_currentQuestion),
                AnswerState = _answerState,
                FeedbackState = _feedbackState,
                FeedbackVi = _feedbackVi,
                RetryPending = eventState.RetryPending,
                LastAnswerCanRetry = _lastAnswerCanRetry
            };
        }

        private MathRescueGameplaySnapshot BuildSnapshotLocked()
        {
            var eventState = _started ? _game.CurrentState : null;
            var completedCount = eventState == null ? 0 : Math.Max(0,
                Math.Min(MathSessionCoordinator.TargetedLessonQuestionCount, eventState.CompletedCheckpointCount));
            var total = MathSessionCoordinator.TargetedLessonQuestionCount;
            var completed = _phase == MathRescueGameplayPhase.GAME_COMPLETE;
            return new MathRescueGameplaySnapshot
            {
                Phase = _phase,
                SessionId = _start == null || _start.Session == null ? null : _start.Session.SessionId,
                EventId = _game.Event == null ? null : _game.Event.Id,
                CurrentCheckpoint = _currentCheckpoint,
                CurrentQuestion = CloneQuestion(_currentQuestion),
                Progress = new MathRescueProgressSnapshot
                {
                    CompletedCheckpoints = completedCount,
                    TotalCheckpoints = total,
                    Fraction = total <= 0 ? 0.0 : (double)completedCount / total
                },
                AnswerState = _answerState,
                FeedbackState = _feedbackState,
                FeedbackVi = _feedbackVi,
                CanAnswer = _started && !_paused &&
                    (_phase == MathRescueGameplayPhase.QUESTION_ACTIVE || _phase == MathRescueGameplayPhase.REPAIR) &&
                    _currentQuestion != null,
                Completed = completed,
                Paused = _paused,
                Resumable = _started && !completed,
                RetryPending = eventState != null && eventState.RetryPending,
                EventState = eventState,
                CompletionSummary = _completion == null ? null : _completion.LearningSummary
            };
        }

        private void ResetFeedbackLocked()
        {
            _answerState = MathRescueAnswerState.NONE;
            _feedbackState = MathRescueFeedbackState.NONE;
            _feedbackVi = null;
            _lastAnswerCanRetry = false;
        }

        private void EnsureInteractiveLocked()
        {
            EnsureNotDisposedLocked();
            if (!_started) throw new InvalidOperationException("Rescue gameplay has not started.");
            if (_paused) throw new InvalidOperationException("Rescue gameplay is paused and must be resumed by a new coordinator.");
            if (_phase == MathRescueGameplayPhase.GAME_COMPLETE)
                throw new InvalidOperationException("Rescue gameplay is already complete.");
        }

        private void EnsureNotDisposedLocked()
        {
            if (_disposed) throw new ObjectDisposedException("MathRescueGameplayCoordinator");
        }

        private void RequirePhaseLocked(MathRescueGameplayPhase expected)
        {
            if (_phase != expected)
                throw new InvalidOperationException("Invalid rescue gameplay transition from " + _phase + "; expected " + expected + ".");
        }

        private static MathRescueGameplayStateChangedEventArgs NewChange(
            MathRescueGameplaySnapshot previous,
            MathRescueGameplaySnapshot current,
            string reason)
        {
            return new MathRescueGameplayStateChangedEventArgs { Previous = previous, Current = current, Reason = reason };
        }

        private void Publish(MathRescueGameplayStateChangedEventArgs change)
        {
            var handler = StateChanged;
            if (handler != null && change != null) handler(this, change);
        }

        private static MathQuestion CloneQuestion(MathQuestion source)
        {
            if (source == null) return null;
            return new MathQuestion
            {
                QuestionId = source.QuestionId,
                ContentQuestionId = source.ContentQuestionId,
                LessonId = source.LessonId,
                QuestionType = source.QuestionType,
                Difficulty = source.Difficulty,
                TemplateId = source.TemplateId,
                SkillId = source.SkillId,
                PromptVi = source.PromptVi,
                ExplanationVi = source.ExplanationVi,
                CorrectAnswer = source.CorrectAnswer,
                Choices = source.Choices == null ? null : source.Choices.ToList(),
                AnswerKind = source.AnswerKind,
                CorrectAnswerText = source.CorrectAnswerText,
                AcceptedAnswers = source.AcceptedAnswers == null ? null : source.AcceptedAnswers.ToList(),
                NumericTolerance = source.NumericTolerance,
                ExpectedUnit = source.ExpectedUnit,
                AcceptedUnits = source.AcceptedUnits == null ? null : source.AcceptedUnits.ToList(),
                AnswerUnit = source.AnswerUnit,
                AllowedExpressionOperators = source.AllowedExpressionOperators == null ? null : source.AllowedExpressionOperators.ToList(),
                ChoiceTexts = source.ChoiceTexts == null ? null : source.ChoiceTexts.ToList(),
                IllustrationData = source.IllustrationData,
                Representation = source.Representation,
                HintLevel1 = source.HintLevel1,
                HintLevel2 = source.HintLevel2,
                DifficultyFit = source.DifficultyFit
            };
        }
    }
}
