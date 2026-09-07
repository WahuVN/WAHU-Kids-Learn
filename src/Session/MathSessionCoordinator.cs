using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using WAHU.Content;
using WAHU.Data;
using WAHU.Learning;

namespace WAHU.Session
{
    public sealed class MathSessionCoordinator : IDisposable
    {
        public const string PackId = "math_grade2_verified_templates_v1";
        public const string PackVersion = "1.9.0";
        public const int DefaultTargetQuestionCount = 8;
        public const int TargetedLessonQuestionCount = 3;
        public const int MaxAttemptsPerQuestion = 2;

        private readonly LearningDatabase _database;
        private readonly string _templatePath;
        private readonly string _performanceProfile;
        private readonly int _requestedSeed;
        private readonly int _requestedTargetQuestionCount;
        private readonly string _requestedLessonId;
        private readonly LearnerSessionService _sessionService;
        private readonly MathLessonProgressStore _lessonProgressStore;
        private readonly MathSessionRuntimeService _runtime;
        private readonly AnswerCommitService _answerCommit;
        private readonly BehaviorDecisionAuditService _behaviorAudit;
        private readonly AdaptiveDecisionAuditService _adaptiveAudit;
        private readonly GameWorldRewardService _gameWorld;
        private BehaviorController _behavior = new BehaviorController();
        private readonly AdaptiveMathSelector _selector = new AdaptiveMathSelector();
        private readonly MasteryEngineV1 _mastery = new MasteryEngineV1();
        private readonly ReviewSchedulerV1 _scheduler = new ReviewSchedulerV1();
        private readonly MathErrorClassifierV1 _errorClassifier = new MathErrorClassifierV1();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private readonly List<string> _recentTemplates = new List<string>();
        private readonly List<string> _recentSkills = new List<string>();
        private readonly HashSet<string> _distinctSkills = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, MathSkillMasteryChange> _masteryChanges =
            new Dictionary<string, MathSkillMasteryChange>(StringComparer.Ordinal);
        private readonly object _submitGate = new object();

        private IList<MathTemplateRef> _templates;
        private MathLessonCatalogSnapshot _lessonCatalog;
        private MathAuthoredQuestionBank _authoredBank;
        private MathLessonDescriptor _targetLesson;
        private IList<MathQuestion> _targetQuestionPool;
        private IList<MathQuestion> _targetQuestions;
        private IList<string> _selectedContentQuestionIds = new List<string>();
        private string _sessionMode = "adaptive";
        private string _targetLessonId;
        private IDictionary<string, SkillSnapshot> _skills;
        private LearnerProfile _profile;
        private LearnerSessionHandle _session;
        private MathQuestion _currentQuestion;
        private MathSelectionDecision _currentSelection;
        private DateTime _questionStartedAtUtc;
        private BehaviorDecision _lastBehavior;
        private string _forcedRepairTemplateId;
        private bool _active;
        private int _seed;
        private int _targetQuestionCount;
        private int _generatedQuestionCount;
        private int _attempts;
        private int _answerAttempts;
        private int _correct;
        private int _independentCorrect;
        private int _hintedCorrect;
        private int _retriedQuestions;
        private int _retriedCorrect;
        private int _wrong;
        private int _currentAttemptIndex = 1;

        public MathSessionCoordinator(LearningDatabase database, string templatePath, string performanceProfile, int seed)
            : this(database, templatePath, performanceProfile, seed, DefaultTargetQuestionCount, null) { }

        public MathSessionCoordinator(LearningDatabase database, string templatePath, string performanceProfile, int seed, int targetQuestionCount)
            : this(database, templatePath, performanceProfile, seed, targetQuestionCount, null) { }

        public MathSessionCoordinator(LearningDatabase database, string templatePath, string performanceProfile, int seed, string lessonId)
            : this(database, templatePath, performanceProfile, seed, DefaultTargetQuestionCount, lessonId) { }

        private MathSessionCoordinator(LearningDatabase database, string templatePath, string performanceProfile, int seed, int targetQuestionCount, string lessonId)
        {
            _database = database ?? throw new ArgumentNullException("database");
            if (string.IsNullOrWhiteSpace(templatePath)) throw new ArgumentException("templatePath");
            _templatePath = templatePath;
            _performanceProfile = performanceProfile == "NORMAL" ? "NORMAL" : "LOW";
            if (targetQuestionCount < 1 || targetQuestionCount > 40) throw new ArgumentOutOfRangeException("targetQuestionCount");
            _requestedSeed = seed;
            _requestedTargetQuestionCount = targetQuestionCount;
            _requestedLessonId = string.IsNullOrWhiteSpace(lessonId) ? null : lessonId.Trim();
            _seed = seed;
            _targetQuestionCount = targetQuestionCount;
            _sessionService = new LearnerSessionService(database);
            _lessonProgressStore = new MathLessonProgressStore(database);
            _runtime = new MathSessionRuntimeService(database);
            _answerCommit = new AnswerCommitService(database);
            _behaviorAudit = new BehaviorDecisionAuditService(database);
            _adaptiveAudit = new AdaptiveDecisionAuditService(database);
            _gameWorld = new GameWorldRewardService(database);
        }

        public bool IsActive { get { return _active; } }
        public bool HasOpenQuestion { get { return _currentQuestion != null; } }
        public MathSessionSummary Summary { get { return BuildSummary(null); } }

        public MathSessionStartResult Start(string displayName)
        {
            if (_active || _session != null) throw new InvalidOperationException("Math session already started.");
            LoadTemplates();
            if (!string.IsNullOrWhiteSpace(_requestedLessonId)) LoadLessonContent();
            _profile = _sessionService.EnsurePrimaryChild(displayName);

            var resumed = false;
            var restoredOpenQuestion = false;
            var discardedCorruptOpenQuestion = false;
            var recovered = 0;
            var resumable = LoadLatestResumableRecoveringCorrupt(ref recovered);
            if (resumable != null)
            {
                if (!string.IsNullOrWhiteSpace(_requestedLessonId) &&
                    (!string.Equals(resumable.SessionMode, "lesson", StringComparison.Ordinal) ||
                     !string.Equals(resumable.TargetLessonId, _requestedLessonId, StringComparison.Ordinal)))
                    throw new InvalidOperationException("Một phiên Toán khác đang học dở. Hãy tiếp tục hoặc kết thúc phiên đó trước khi mở bài này.");
                RestoreSession(resumable, out restoredOpenQuestion, out discardedCorruptOpenQuestion);
                resumed = true;
            }
            else
            {
                recovered += _sessionService.RecoverDanglingSessions(_profile.ChildId);
                try
                {
                    _seed = _requestedSeed;
                    _sessionMode = string.IsNullOrWhiteSpace(_requestedLessonId) ? "adaptive" : "lesson";
                    _targetLessonId = _requestedLessonId;
                    _targetQuestionCount = _requestedTargetQuestionCount;
                    _generatedQuestionCount = 0;
                    if (string.Equals(_sessionMode, "lesson", StringComparison.Ordinal))
                    {
                        PrepareTargetLessonForStart();
                        SelectTargetLessonQuestionsForFreshSession();
                        _targetQuestionCount = _targetQuestions.Count;
                    }

                    _session = _runtime.TryCreateSession(
                        _profile.ChildId, _performanceProfile, _seed, _targetQuestionCount, _sessionMode, _targetLessonId,
                        _targetLesson == null ? null : _targetLesson.SkillId, PackId, PackVersion);
                    if (_session == null)
                    {
                        var raced = LoadLatestResumableRecoveringCorrupt(ref recovered);
                        if (raced == null)
                            throw new InvalidOperationException("Một phiên Toán khác vừa được mở. Hãy thử tiếp tục lại phiên đang học.");
                        if (!string.IsNullOrWhiteSpace(_requestedLessonId) &&
                            (!string.Equals(raced.SessionMode, "lesson", StringComparison.Ordinal) ||
                             !string.Equals(raced.TargetLessonId, _requestedLessonId, StringComparison.Ordinal)))
                            throw new InvalidOperationException("Một phiên Toán khác đang học dở. Hãy tiếp tục hoặc kết thúc phiên đó trước khi mở bài này.");
                        RestoreSession(raced, out restoredOpenQuestion, out discardedCorruptOpenQuestion);
                        resumed = true;
                    }
                    else
                    {
                        _skills = _sessionService.LoadSkillSnapshots(_profile.ChildId, "math");
                        if (string.Equals(_sessionMode, "lesson", StringComparison.Ordinal))
                            _runtime.SaveCheckpoint(_session.SessionId, 0, null, SerializeLessonSelectionCheckpoint());
                        _active = true;
                    }
                }
                catch
                {
                    // Once session + runtime (+ lesson-start progress) are durable, a local read/restore
                    // failure must leave that session resumable instead of aborting shared durable state.
                    _session = null;
                    _skills = null;
                    _active = false;
                    throw;
                }
            }

            return new MathSessionStartResult
            {
                SessionId = _session.SessionId,
                ChildId = _profile.ChildId,
                DisplayName = _profile.DisplayName,
                RecoveredDanglingSessions = recovered,
                TargetQuestionCount = _targetQuestionCount,
                CompletedQuestionCount = _attempts,
                ResumedExistingSession = resumed,
                RestoredOpenQuestion = restoredOpenQuestion,
                DiscardedCorruptOpenQuestion = discardedCorruptOpenQuestion,
                SessionMode = _sessionMode,
                TargetLessonId = _targetLessonId,
                TargetLessonTitleVi = _targetLesson == null ? null : _targetLesson.TitleVi,
                SelectedContentQuestionIds = new List<string>(_selectedContentQuestionIds ?? new List<string>()),
                LessonAccess = string.IsNullOrWhiteSpace(_targetLessonId) ? null : CurrentLessonAccess(),
                RetryPending = _currentQuestion != null && _currentAttemptIndex > 1,
                CurrentAttemptIndex = _currentQuestion == null ? 1 : _currentAttemptIndex
            };
        }

        public MathQuestion NextQuestion()
        {
            EnsureActive();
            if (_currentQuestion != null) return _currentQuestion;
            if (_attempts >= _targetQuestionCount) return null;

            var consumedRepair = false;
            var requestedRepair = _forcedRepairTemplateId;
            var nextOrdinal = checked(_generatedQuestionCount + 1);
            if (string.Equals(_sessionMode, "lesson", StringComparison.Ordinal))
            {
                EnsureTargetLessonLoaded();
                if (_generatedQuestionCount >= _targetQuestions.Count) return null;
                var authored = _targetQuestions[_generatedQuestionCount];
                _currentQuestion = MathAuthoredQuestionSource.CreateRuntimeInstance(authored);
                _currentSelection = new MathSelectionDecision
                {
                    Template = new MathTemplateRef { TemplateId = _currentQuestion.TemplateId, SkillId = _currentQuestion.SkillId },
                    Score = 1.0,
                    DifficultyFit = _currentQuestion.DifficultyFit,
                    Reasons = new List<string> { "lesson_targeted", "authored_content", _currentQuestion.Difficulty ?? "unknown_difficulty" },
                    CandidateSummary = _targetQuestions.Select(x => x.ContentQuestionId).ToList()
                };
                _forcedRepairTemplateId = null;
            }
            else
            {
                IEnumerable<MathTemplateRef> candidates = _templates;
                if (!string.IsNullOrWhiteSpace(requestedRepair))
                {
                    var repair = _templates.Where(x => string.Equals(x.TemplateId, requestedRepair, StringComparison.Ordinal)).ToList();
                    if (repair.Count > 0) candidates = repair;
                }

                _currentSelection = _selector.Select(candidates, _skills, DateTime.UtcNow, _recentTemplates, _recentSkills);
                consumedRepair = !string.IsNullOrWhiteSpace(requestedRepair);
                if (consumedRepair)
                {
                    if (_currentSelection.Reasons == null) _currentSelection.Reasons = new List<string>();
                    _currentSelection.Reasons.Add("prerequisite_repair");
                    _forcedRepairTemplateId = null;
                }
                var generator = new MathQuestionGenerator(QuestionSeed(_seed, nextOrdinal, _currentSelection.Template.TemplateId));
                _currentQuestion = generator.Generate(_currentSelection);
            }
            _questionStartedAtUtc = DateTime.UtcNow;
            _currentAttemptIndex = 1;
            _generatedQuestionCount = nextOrdinal;

            try
            {
                _runtime.SaveOpenQuestion(
                    _session.SessionId,
                    _generatedQuestionCount,
                    _json.Serialize(_currentQuestion),
                    SerializeSelection(_currentSelection),
                    _questionStartedAtUtc,
                    _forcedRepairTemplateId);
            }
            catch
            {
                _generatedQuestionCount--;
                _currentQuestion = null;
                _currentSelection = null;
                if (consumedRepair) _forcedRepairTemplateId = requestedRepair;
                throw;
            }

            try
            {
                _adaptiveAudit.Record(new AdaptiveDecisionAuditRequest
                {
                    Id = "adaptive-" + Guid.NewGuid().ToString("N"),
                    SessionId = _session.SessionId,
                    ChildId = _profile.ChildId,
                    PackId = PackId,
                    PackVersion = PackVersion,
                    Question = _currentQuestion,
                    Selection = _currentSelection,
                    Behavior = _lastBehavior,
                    CreatedAtUtc = _questionStartedAtUtc
                });
            }
            catch
            {
                // Diagnostic only. The exact open question is already durable and must not be regenerated.
            }
            return _currentQuestion;
        }

        public MathAnswerOutcome SubmitAnswer(int answer, int hintLevel, string inputMethod)
        {
            return SubmitAnswer(answer.ToString(System.Globalization.CultureInfo.InvariantCulture), hintLevel, inputMethod);
        }

        public MathAnswerOutcome SubmitAnswer(string answer, int hintLevel, string inputMethod)
        {
            var answered = DateTime.UtcNow;
            var responseMs = (int)Math.Min(int.MaxValue, Math.Max(0, (answered - _questionStartedAtUtc).TotalMilliseconds));
            return SubmitAnswerAt(answer, hintLevel, inputMethod, answered, responseMs);
        }

        public MathAnswerOutcome SubmitAnswerAt(int answer, int hintLevel, string inputMethod, DateTime answeredAtUtc, int responseMs)
        {
            return SubmitAnswerAt(answer.ToString(System.Globalization.CultureInfo.InvariantCulture), hintLevel, inputMethod, answeredAtUtc, responseMs);
        }

        public MathAnswerOutcome SubmitAnswerAt(string answer, int hintLevel, string inputMethod, DateTime answeredAtUtc, int responseMs)
        {
            lock (_submitGate)
            {
                return SubmitAnswerAtCore(answer, hintLevel, inputMethod, answeredAtUtc, responseMs, false, 1);
            }
        }

        public MathAnswerOutcome SubmitAnswerWithRetry(int answer, int hintLevel, string inputMethod)
        {
            return SubmitAnswerWithRetry(answer.ToString(System.Globalization.CultureInfo.InvariantCulture), hintLevel, inputMethod);
        }

        public MathAnswerOutcome SubmitAnswerWithRetry(string answer, int hintLevel, string inputMethod)
        {
            var answered = DateTime.UtcNow;
            var responseMs = (int)Math.Min(int.MaxValue, Math.Max(0, (answered - _questionStartedAtUtc).TotalMilliseconds));
            return SubmitAnswerWithRetryAt(answer, hintLevel, inputMethod, answered, responseMs);
        }

        public MathAnswerOutcome SubmitAnswerWithRetryAt(int answer, int hintLevel, string inputMethod, DateTime answeredAtUtc, int responseMs)
        {
            return SubmitAnswerWithRetryAt(answer.ToString(System.Globalization.CultureInfo.InvariantCulture), hintLevel, inputMethod, answeredAtUtc, responseMs);
        }

        public MathAnswerOutcome SubmitAnswerWithRetryAt(string answer, int hintLevel, string inputMethod, DateTime answeredAtUtc, int responseMs)
        {
            lock (_submitGate)
            {
                return SubmitAnswerAtCore(answer, hintLevel, inputMethod, answeredAtUtc, responseMs, true, 1);
            }
        }

        public MathAnswerOutcome SubmitRetryAnswer(int answer, int hintLevel, string inputMethod)
        {
            return SubmitRetryAnswer(answer.ToString(System.Globalization.CultureInfo.InvariantCulture), hintLevel, inputMethod);
        }

        public MathAnswerOutcome SubmitRetryAnswer(string answer, int hintLevel, string inputMethod)
        {
            var answered = DateTime.UtcNow;
            var responseMs = (int)Math.Min(int.MaxValue, Math.Max(0, (answered - _questionStartedAtUtc).TotalMilliseconds));
            return SubmitRetryAnswerAt(answer, hintLevel, inputMethod, answered, responseMs);
        }

        public MathAnswerOutcome SubmitRetryAnswerAt(int answer, int hintLevel, string inputMethod, DateTime answeredAtUtc, int responseMs)
        {
            return SubmitRetryAnswerAt(answer.ToString(System.Globalization.CultureInfo.InvariantCulture), hintLevel, inputMethod, answeredAtUtc, responseMs);
        }

        public MathAnswerOutcome SubmitRetryAnswerAt(string answer, int hintLevel, string inputMethod, DateTime answeredAtUtc, int responseMs)
        {
            lock (_submitGate)
            {
                return SubmitAnswerAtCore(answer, hintLevel, inputMethod, answeredAtUtc, responseMs, true, 2);
            }
        }

        private MathAnswerOutcome SubmitAnswerAtCore(string answer, int hintLevel, string inputMethod, DateTime answeredAtUtc, int responseMs, bool retryEnabled, int expectedAttemptIndex)
        {
            EnsureActive();
            if (_currentQuestion == null || _currentSelection == null) throw new InvalidOperationException("No active question.");
            if (hintLevel < 0 || hintLevel > 2) throw new ArgumentOutOfRangeException("hintLevel");
            if (responseMs < 0) throw new ArgumentOutOfRangeException("responseMs");
            if (string.IsNullOrWhiteSpace(inputMethod)) inputMethod = "mouse";
            var answeredUtc = answeredAtUtc.Kind == DateTimeKind.Utc ? answeredAtUtc : answeredAtUtc.ToUniversalTime();
            var question = _currentQuestion;
            var normalizedAnswer = answer == null ? string.Empty : answer.Trim();
            var isCorrect = question.IsCorrectAnswer(normalizedAnswer);
            var error = _errorClassifier.Classify(question, normalizedAnswer);
            var attemptIndex = _currentAttemptIndex;
            if (attemptIndex < 1 || attemptIndex > MaxAttemptsPerQuestion)
                throw new InvalidOperationException("Invalid Math question attempt index.");
            if (expectedAttemptIndex != attemptIndex)
                throw new InvalidOperationException("Math answer intent does not match the current attempt_index.");
            if (!retryEnabled && attemptIndex > 1)
                throw new InvalidOperationException("A retry-pending Math question must use SubmitRetryAnswer.");

            SkillSnapshot current;
            if (!_skills.TryGetValue(question.SkillId, out current) || current == null)
                current = NewSkill(question.SkillId);

            var behaviorDecision = _behavior.Observe(new BehaviorObservation
            {
                SkillId = question.SkillId,
                IsCorrect = isCorrect,
                ResponseMs = responseMs,
                HintLevel = hintLevel,
                UsedMaxHint = hintLevel >= 2,
                RapidWrong = !isCorrect && responseMs <= 550,
                SkippedOrExited = false,
                InputMiss = false,
                ErrorType = error == null ? null : error.ErrorType,
                Representation = question.Representation,
                MasteryScore = current.MasteryScore,
                SessionElapsedMinutes = Math.Max(0, (answeredUtc - _session.StartedAtUtc).TotalMinutes)
            });
            var suggestPositiveEndBeforeFinalize = behaviorDecision.State == BehaviorState.FATIGUED_LIKELY && _attempts >= 4;
            var retryPending = retryEnabled && !isCorrect && attemptIndex < MaxAttemptsPerQuestion && !suggestPositiveEndBeforeFinalize;
            var finalizesQuestion = !retryPending;
            var masteryHintLevel = attemptIndex > 1 ? Math.Max(1, hintLevel) : hintLevel;
            MasteryUpdate mastery = null;
            ReviewUpdate review = null;
            if (finalizesQuestion)
            {
                mastery = _mastery.Evaluate(current, isCorrect, masteryHintLevel, false, behaviorDecision.ProtectMasteryFromNegativeUpdate);
                if (attemptIndex > 1)
                {
                    if (mastery.Reasons == null) mastery.Reasons = new List<string>();
                    mastery.Reasons.Add("retry_assisted_attempt");
                }
                review = _scheduler.Schedule(answeredUtc, mastery, isCorrect, masteryHintLevel);
            }
            var attemptId = "attempt-" + Guid.NewGuid().ToString("N");

            AnswerCommitResult commit;
            try
            {
                commit = _answerCommit.Commit(new AnswerCommitRequest
                {
                AttemptId = attemptId,
                SessionId = _session.SessionId,
                ChildId = _profile.ChildId,
                PackId = PackId,
                PackVersion = PackVersion,
                QuestionId = question.QuestionId,
                SkillId = question.SkillId,
                Subject = "math",
                StartedAtUtc = answeredUtc.AddMilliseconds(-responseMs),
                AnsweredAtUtc = answeredUtc,
                AnswerJson = _json.Serialize(new Dictionary<string, object> { { "answer", AnswerValueForJson(question, normalizedAnswer) } }),
                IsCorrect = isCorrect,
                ResponseMs = responseMs,
                HintLevel = hintLevel,
                Representation = question.Representation,
                InputMethod = inputMethod,
                AttemptIndex = attemptIndex,
                ListenCount = 0,
                ExpectedSkillMasteryScore = mastery == null ? null : (double?)mastery.ScoreBefore,
                ExpectedSkillAttemptsCount = mastery == null ? null : (int?)current.AttemptsCount,
                Error = error == null ? null : new ErrorEventWrite
                {
                    Id = "error-" + Guid.NewGuid().ToString("N"),
                    ErrorType = error.ErrorType,
                    Confidence = error.Confidence,
                    EvidenceJson = _json.Serialize(error.Evidence),
                    ClassifierVersion = MathErrorClassifierV1.Version
                },
                Mastery = mastery == null ? null : new MasteryEventWrite
                {
                    Id = "mastery-" + Guid.NewGuid().ToString("N"),
                    EventType = mastery.EventType,
                    Delta = mastery.Delta,
                    ScoreBefore = mastery.ScoreBefore,
                    ScoreAfter = mastery.ScoreAfter,
                    ConfidenceAfter = mastery.ConfidenceAfter,
                    ReasonJson = _json.Serialize(mastery.Reasons),
                    MasteryEngineVersion = MasteryEngineV1.Version
                },
                ChildSkill = mastery == null ? null : new ChildSkillWrite
                {
                    MasteryScore = mastery.ScoreAfter,
                    Confidence = mastery.ConfidenceAfter,
                    AttemptsCount = mastery.AttemptsCount,
                    IndependentSuccessCount = mastery.IndependentSuccessCount,
                    HintedSuccessCount = mastery.HintedSuccessCount,
                    TransferSuccessCount = mastery.TransferSuccessCount,
                    LastSeenAtUtc = answeredUtc,
                    LastSuccessAtUtc = isCorrect ? (DateTime?)answeredUtc : current.LastSuccessAtUtc,
                    NextReviewAtUtc = review.DueAtUtc,
                    LearningState = mastery.LearningState,
                    MasteryEngineVersion = MasteryEngineV1.Version
                },
                Review = review == null ? null : new ReviewScheduleWrite
                {
                    DueAtUtc = review.DueAtUtc,
                    IntervalDays = review.IntervalDays,
                    Reason = review.Reason,
                    SchedulerVersion = ReviewSchedulerV1.Version
                }
                });
            }
            catch
            {
                ReconcileAfterFailedAnswerCommit(question);
                throw;
            }

            // AnswerCommit ở trên là source-of-truth durable. Diagnostics/audit lỗi sau commit
            // không được làm UI nghĩ câu chưa lưu rồi submit lại thành attempt mới.
            try
            {
                _behaviorAudit.Record(new BehaviorDecisionAuditRequest
                {
                    Id = "behavior-" + Guid.NewGuid().ToString("N"),
                    SessionId = _session.SessionId,
                    ChildId = _profile.ChildId,
                    AttemptId = commit.AttemptId,
                    Decision = behaviorDecision,
                    ControllerVersion = "behavior-v1",
                    CreatedAtUtc = answeredUtc
                });
            }
            catch
            {
                // Best-effort diagnostic only. Learning state is already committed atomically.
            }

            bool questionCompleted;
            bool canRetry;
            if (commit.AlreadyCommitted)
            {
                var committedAttempts = _runtime.LoadCommittedAttempts(_session.SessionId);
                _skills = _sessionService.LoadSkillSnapshots(_profile.ChildId, "math");
                RebuildFromCommittedAttempts(committedAttempts);
                var nextAttemptIndex = NextAttemptIndexForQuestion(committedAttempts, question.QuestionId);
                questionCompleted = nextAttemptIndex == 0;
                canRetry = retryEnabled && !questionCompleted && nextAttemptIndex <= MaxAttemptsPerQuestion;
                if (questionCompleted && string.Equals(_sessionMode, "adaptive", StringComparison.Ordinal) &&
                    _lastBehavior != null && _lastBehavior.TriggerPrerequisiteRepair)
                    _forcedRepairTemplateId = RepairTemplateFor(question);
                if (!questionCompleted) _currentAttemptIndex = nextAttemptIndex;
            }
            else
            {
                _answerAttempts++;
                _lastBehavior = behaviorDecision;
                questionCompleted = finalizesQuestion;
                canRetry = retryPending;
                if (questionCompleted)
                {
                    _skills[question.SkillId] = Apply(current, mastery, answeredUtc, review.DueAtUtc, isCorrect);
                    ApplyMasteryChange(question.SkillId, mastery.ScoreBefore, mastery.ScoreAfter);
                    AddRecent(_recentTemplates, question.TemplateId);
                    AddRecent(_recentSkills, question.SkillId);
                    _distinctSkills.Add(question.SkillId);
                    _attempts++;
                    if (attemptIndex > 1) _retriedQuestions++;
                    if (isCorrect)
                    {
                        _correct++;
                        if (attemptIndex == 1 && hintLevel <= 0) _independentCorrect++;
                        if (hintLevel > 0) _hintedCorrect++;
                        if (attemptIndex > 1) _retriedCorrect++;
                    }
                    else _wrong++;
                    if (string.Equals(_sessionMode, "adaptive", StringComparison.Ordinal) && behaviorDecision.TriggerPrerequisiteRepair)
                        _forcedRepairTemplateId = RepairTemplateFor(question);
                }
                else
                {
                    _currentAttemptIndex = attemptIndex + 1;
                }
            }

            if (questionCompleted)
            {
                try { _runtime.SaveCheckpoint(_session.SessionId, _generatedQuestionCount, _forcedRepairTemplateId, RuntimeCheckpointSelectionJson()); }
                catch
                {
                    // Final attempt is already durable. Resume detects the final mastery-bearing attempt and drops stale cache.
                }
            }
            else
            {
                _questionStartedAtUtc = answeredUtc;
                try
                {
                    _runtime.SaveOpenQuestion(_session.SessionId, _generatedQuestionCount, _json.Serialize(question),
                        SerializeSelection(_currentSelection), _questionStartedAtUtc, _forcedRepairTemplateId);
                }
                catch
                {
                    // The original open-question checkpoint is still usable; retry index is reconstructed from durable attempts.
                }
            }

            var outcome = new MathAnswerOutcome
            {
                IsCorrect = isCorrect,
                CorrectAnswer = question.CorrectAnswer,
                CorrectAnswerDisplay = question.CorrectAnswerFeedbackDisplay,
                HintLevel = hintLevel,
                FeedbackVi = canRetry ? BuildRetryFeedback(error) : BuildFeedback(isCorrect, masteryHintLevel, error),
                Behavior = behaviorDecision,
                Mastery = mastery,
                Review = review,
                Error = error,
                OfferBreak = behaviorDecision.Actions != null && behaviorDecision.Actions.Contains("offer_break"),
                SuggestPositiveEnd = behaviorDecision.State == BehaviorState.FATIGUED_LIKELY && _attempts >= 4,
                CompletedQuestionCount = _attempts,
                TargetQuestionCount = _targetQuestionCount,
                AttemptIndex = attemptIndex,
                QuestionCompleted = questionCompleted,
                CanRetry = canRetry,
                IsRetry = attemptIndex > 1,
                IndependentSuccess = questionCompleted && isCorrect && attemptIndex == 1 && hintLevel <= 0
            };
            if (questionCompleted)
            {
                _currentQuestion = null;
                _currentSelection = null;
                _currentAttemptIndex = 1;
            }
            return outcome;
        }

        public MathSessionSummary Complete()
        {
            EnsureActive();
            var ended = DateTime.UtcNow;
            var summary = BuildSummary(ended);
            var summaryData = new Dictionary<string, object>
            {
                { "attempts", summary.Attempts }, { "answer_attempts", summary.AnswerAttempts },
                { "correct", summary.Correct }, { "independent_correct", summary.IndependentCorrect },
                { "hinted_correct", summary.HintedCorrect }, { "retried_questions", summary.RetriedQuestions },
                { "retried_correct", summary.RetriedCorrect }, { "wrong", summary.Wrong },
                { "distinct_skills", summary.DistinctSkills }, { "subject", "math" },
                { "session_mode", _sessionMode }, { "mastery_changes", summary.MasteryChanges ?? new List<MathSkillMasteryChange>() },
                { "improved_skill_count", summary.ImprovedSkillCount }
            };
            if (!string.IsNullOrWhiteSpace(_targetLessonId)) summaryData["target_lesson_id"] = _targetLessonId;
            if (summary.TargetSkillMasteryDelta.HasValue)
            {
                summaryData["target_skill_mastery_before"] = summary.TargetSkillMasteryBefore;
                summaryData["target_skill_mastery_after"] = summary.TargetSkillMasteryAfter;
                summaryData["target_skill_mastery_delta"] = summary.TargetSkillMasteryDelta;
            }
            var behaviorJson = _json.Serialize(new Dictionary<string, object> { { "final_state", summary.FinalBehaviorState.ToString() } });

            if (string.Equals(_sessionMode, "lesson", StringComparison.Ordinal) && _targetLesson != null &&
                _attempts >= _targetQuestionCount && _targetQuestionCount > 0)
            {
                var score = 100.0 * _correct / _targetQuestionCount;
                var existing = _lessonProgressStore.LoadOne(_profile.ChildId, _targetLesson.Id);
                var best = existing == null || !existing.BestScorePercent.HasValue
                    ? score : Math.Max(existing.BestScorePercent.Value, score);
                summary.LessonCompleted = true;
                summary.LessonScorePercent = score;
                summary.LessonBestScorePercent = best;
                summaryData["lesson_completed"] = true;
                summaryData["lesson_score_percent"] = score;
                summaryData["lesson_best_score_percent"] = best;
                _lessonProgressStore.CompleteActiveSession(
                    _session.SessionId, _profile.ChildId, _targetLesson.Id, _targetLesson.SkillId,
                    _correct, _targetQuestionCount, _json.Serialize(summaryData), behaviorJson, ended);
            }
            else
            {
                summaryData["lesson_completed"] = false;
                _sessionService.CompleteSession(_session.SessionId, false, _json.Serialize(summaryData), behaviorJson);
            }

            // From this point the terminal learning transaction is durable. Any downstream enrichment or
            // cleanup failure must not leave the coordinator looking active or make the caller retry completion.
            _active = false;
            if (summary.LessonCompleted)
            {
                try { PopulateNextLesson(summary); }
                catch
                {
                    // Next-lesson recommendation is derived UX metadata, never part of durable completion.
                }
            }
            try { _runtime.Delete(_session.SessionId); } catch { }
            try
            {
                var reward = _gameWorld.GrantCompletedMathSession(_profile.ChildId, _session.SessionId, summary.Attempts);
                summary.GardenGrowthSteps = reward.GrowthSteps;
                summary.GardenUnlockedItemIds = reward.NewlyUnlockedItems == null
                    ? new List<string>() : reward.NewlyUnlockedItems.ToList();
                summary.SessionsUntilNextGardenMilestone = reward.SessionsUntilNextMilestone;
                summary.NextGardenMilestoneItemId = reward.NextMilestoneItemId;
                if (summary.GardenUnlockedItemIds.Count > 0)
                    summary.GardenUnlockMessage = BuildGardenUnlockMessage(summary.GardenUnlockedItemIds);
            }
            catch
            {
                // Game-world reward is downstream of durable learning. Reward failure must never roll back learning.
            }
            _active = false;
            return summary;
        }

        public MathSessionSummary Abort(string reason)
        {
            if (!_active) return BuildSummary(DateTime.UtcNow);
            var ended = DateTime.UtcNow;
            var summary = BuildSummary(ended);
            _sessionService.CompleteSession(_session.SessionId, true,
                _json.Serialize(new Dictionary<string, object>
                {
                    { "attempts", summary.Attempts }, { "correct", summary.Correct }, { "wrong", summary.Wrong },
                    { "reason", string.IsNullOrWhiteSpace(reason) ? "user_exit" : reason }
                }),
                _json.Serialize(new Dictionary<string, object> { { "final_state", summary.FinalBehaviorState.ToString() } }));
            try { _runtime.Delete(_session.SessionId); } catch { }
            _active = false;
            return summary;
        }

        public MathSessionSummary Suspend(string reason)
        {
            if (!_active) return BuildSummary(null);
            try { _runtime.Touch(_session.SessionId); }
            catch
            {
                // Existing runtime checkpoint remains valid even if touching updated_at fails.
            }
            _active = false;
            return BuildSummary(null);
        }

        public void Dispose()
        {
            if (_active)
            {
                try { Suspend("coordinator_disposed"); }
                catch { }
            }
        }

        private void LoadTemplates()
        {
            var descriptors = new MathVerifiedTemplateSource().Load(_templatePath);
            _templates = descriptors.Select(x => new MathTemplateRef
            {
                TemplateId = x.Id,
                SkillId = x.SkillId,
                SourceTemplateId = x.SourceTemplateId,
                FixedContextVi = x.FixedContextVi,
                StatementVi = x.StatementVi,
                AnswerText = x.AnswerText
            }).Where(AdaptiveMathSelector.IsSupported).ToList();
            if (_templates.Count == 0) throw new InvalidOperationException("Không có template Toán VERIFIED được runtime hỗ trợ.");
        }

        private string ContentSiblingPath(string fileName)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(_templatePath));
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Math content pack directory is unavailable.");
            return Path.Combine(directory, fileName);
        }

        private void LoadLessonContent()
        {
            if (_lessonCatalog == null)
                _lessonCatalog = new MathLessonCatalogSource().Load(ContentSiblingPath("lesson_catalog_v1.json"));
            if (_authoredBank == null)
                _authoredBank = new MathAuthoredQuestionSource().Load(ContentSiblingPath("question_bank_v1.json"));
        }

        private void PrepareTargetLessonForStart()
        {
            EnsureTargetLessonLoaded();
            var access = CurrentLessonAccess();
            if (!access.IsUnlocked) throw new MathLessonLockedException(access);
        }

        private void EnsureTargetLessonLoaded()
        {
            if (!string.Equals(_sessionMode, "lesson", StringComparison.Ordinal)) return;
            if (string.IsNullOrWhiteSpace(_targetLessonId)) throw new InvalidDataException("Targeted Math runtime is missing target_lesson_id.");
            LoadLessonContent();
            _targetLesson = _lessonCatalog.FindLesson(_targetLessonId);
            if (_targetLesson == null) throw new InvalidDataException("Targeted Math lesson no longer exists: " + _targetLessonId);
            if (_targetLesson.PracticeSets == null ||
                _targetLesson.PracticeSets.Basic == null || _targetLesson.PracticeSets.Basic.Count == 0 ||
                _targetLesson.PracticeSets.Medium == null || _targetLesson.PracticeSets.Medium.Count == 0 ||
                _targetLesson.PracticeSets.Application == null || _targetLesson.PracticeSets.Application.Count == 0)
                throw new InvalidDataException("Targeted Math lesson requires at least one authored question per difficulty: " + _targetLesson.Id);

            var orderedIds = new List<string>();
            orderedIds.AddRange(_targetLesson.PracticeSets.Basic);
            orderedIds.AddRange(_targetLesson.PracticeSets.Medium);
            orderedIds.AddRange(_targetLesson.PracticeSets.Application);
            if (orderedIds.Count < TargetedLessonQuestionCount || orderedIds.Count > 40 ||
                orderedIds.Distinct(StringComparer.Ordinal).Count() != orderedIds.Count)
                throw new InvalidDataException("Targeted Math lesson has invalid authored pool: " + _targetLesson.Id);

            _targetQuestionPool = new List<MathQuestion>();
            foreach (var id in orderedIds)
            {
                var question = _authoredBank.FindContentQuestion(id);
                if (question == null) throw new InvalidDataException("Lesson references missing authored question: " + id);
                if (!string.Equals(question.LessonId, _targetLesson.Id, StringComparison.Ordinal) ||
                    !string.Equals(question.SkillId, _targetLesson.SkillId, StringComparison.Ordinal))
                    throw new InvalidDataException("Authored question lesson/skill mismatch: " + id);
                _targetQuestionPool.Add(question);
            }

            if (_selectedContentQuestionIds != null && _selectedContentQuestionIds.Count > 0)
                ApplySelectedTargetQuestions();
            else
                _targetQuestions = new List<MathQuestion>();
        }

        private void SelectTargetLessonQuestionsForFreshSession()
        {
            if (_targetLesson == null || _targetQuestionPool == null) EnsureTargetLessonLoaded();
            _selectedContentQuestionIds = BuildDeterministicTargetLessonSelection();
            ApplySelectedTargetQuestions();
        }

        private IList<string> BuildDeterministicTargetLessonSelection()
        {
            if (_targetLesson == null) throw new InvalidOperationException("Targeted Math lesson is not loaded.");
            var practice = _targetLesson.PracticeSets;
            return new List<string>
            {
                SelectTargetBucketQuestion(practice.Basic, "basic"),
                SelectTargetBucketQuestion(practice.Medium, "medium"),
                SelectTargetBucketQuestion(practice.Application, "application")
            };
        }

        private string SelectTargetBucketQuestion(IList<string> ids, string bucketName)
        {
            if (ids == null || ids.Count == 0) throw new InvalidDataException("Targeted Math bucket is empty: " + bucketName);
            return ids[DeterministicBucketIndex(_seed, _targetLessonId, bucketName, ids.Count)];
        }

        private static int DeterministicBucketIndex(int seed, string lessonId, string bucketName, int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException("count");
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)seed) * 16777619u;
                var text = (lessonId ?? string.Empty) + "|" + (bucketName ?? string.Empty);
                for (var i = 0; i < text.Length; i++) hash = (hash ^ text[i]) * 16777619u;
                return (int)(hash % (uint)count);
            }
        }

        private void ApplySelectedTargetQuestions()
        {
            if (_targetLesson == null || _targetQuestionPool == null) throw new InvalidOperationException("Targeted Math pool is not loaded.");
            if (_selectedContentQuestionIds == null || _selectedContentQuestionIds.Count != TargetedLessonQuestionCount ||
                _selectedContentQuestionIds.Distinct(StringComparer.Ordinal).Count() != TargetedLessonQuestionCount)
                throw new InvalidDataException("Targeted Math selected question set must contain exactly three unique ids.");

            var practice = _targetLesson.PracticeSets;
            if (!practice.Basic.Contains(_selectedContentQuestionIds[0]) ||
                !practice.Medium.Contains(_selectedContentQuestionIds[1]) ||
                !practice.Application.Contains(_selectedContentQuestionIds[2]))
                throw new InvalidDataException("Targeted Math selected question set does not match basic/medium/application buckets.");

            var byId = _targetQuestionPool.ToDictionary(x => x.ContentQuestionId, StringComparer.Ordinal);
            _targetQuestions = new List<MathQuestion>();
            foreach (var id in _selectedContentQuestionIds)
            {
                MathQuestion question;
                if (!byId.TryGetValue(id, out question)) throw new InvalidDataException("Selected authored question is missing from lesson pool: " + id);
                _targetQuestions.Add(question);
            }
        }

        private MathLessonAccessSnapshot CurrentLessonAccess()
        {
            if (_profile == null || string.IsNullOrWhiteSpace(_targetLessonId)) return null;
            return new MathLessonProgressService(_database, ContentSiblingPath("lesson_catalog_v1.json"))
                .GetAccess(_profile.ChildId, _targetLessonId);
        }

        private MathSessionRuntimeSnapshot LoadLatestResumableRecoveringCorrupt(ref int recoveredCount)
        {
            for (var pass = 0; pass < 4; pass++)
            {
                try
                {
                    var runtime = _runtime.LoadLatestResumable(_profile.ChildId);
                    if (runtime == null) return null;
                    runtime = _runtime.EnsurePackIdentity(runtime, PackId, PackVersion);
                    if (string.Equals(runtime.PackId, PackId, StringComparison.Ordinal) &&
                        string.Equals(runtime.PackVersion, PackVersion, StringComparison.Ordinal))
                        return runtime;

                    if (_runtime.RecoverIncompatiblePackSession(_profile.ChildId, runtime.SessionId)) recoveredCount++;
                }
                catch (MathSessionRuntimeCorruptException ex)
                {
                    if (_runtime.RecoverCorruptRuntimeSession(_profile.ChildId, ex.SessionId)) recoveredCount++;
                }
            }

            throw new InvalidOperationException("Không thể hòa giải phiên Toán đang lưu với phiên bản nội dung hiện tại.");
        }

        private void RestoreSession(
            MathSessionRuntimeSnapshot runtime,
            out bool restoredOpenQuestion,
            out bool discardedCorruptOpenQuestion)
        {
            if (runtime == null) throw new ArgumentNullException("runtime");
            restoredOpenQuestion = false;
            discardedCorruptOpenQuestion = false;
            _session = new LearnerSessionHandle
            {
                SessionId = runtime.SessionId,
                ChildId = runtime.ChildId,
                StartedAtUtc = runtime.StartedAtUtc,
                Subject = "math",
                PerformanceProfile = runtime.PerformanceProfile
            };
            _seed = runtime.Seed;
            _targetQuestionCount = runtime.TargetQuestionCount;
            _generatedQuestionCount = runtime.GeneratedQuestionCount;
            _sessionMode = string.IsNullOrWhiteSpace(runtime.SessionMode) ? "adaptive" : runtime.SessionMode;
            _targetLessonId = runtime.TargetLessonId;
            _forcedRepairTemplateId = string.Equals(_sessionMode, "adaptive", StringComparison.Ordinal)
                ? runtime.ForcedRepairTemplateId : null;
            var hadPersistedLessonSelection = false;
            var lessonSelectionMetadataNeedsRepair = false;
            if (string.Equals(_sessionMode, "lesson", StringComparison.Ordinal))
            {
                EnsureTargetLessonLoaded();
                hadPersistedLessonSelection = TryRestorePersistedTargetSelection(
                    runtime.CurrentSelectionJson, out lessonSelectionMetadataNeedsRepair);
                if (!hadPersistedLessonSelection) SelectTargetLessonQuestionsForFreshSession();
                if (_targetQuestions == null || _targetQuestions.Count != TargetedLessonQuestionCount ||
                    _targetQuestionCount != TargetedLessonQuestionCount)
                    throw new InvalidDataException("Targeted Math selected question count changed during an active session.");
            }
            _skills = _sessionService.LoadSkillSnapshots(_profile.ChildId, "math");
            _active = true;

            var committedAttempts = _runtime.LoadCommittedAttempts(_session.SessionId);
            RebuildFromCommittedAttempts(committedAttempts);
            if (_generatedQuestionCount < _attempts) _generatedQuestionCount = _attempts;

            if (string.IsNullOrWhiteSpace(runtime.CurrentQuestionJson))
            {
                var hasUnexpectedSelectionPayload = !string.IsNullOrWhiteSpace(runtime.CurrentSelectionJson) &&
                    !string.Equals(_sessionMode, "lesson", StringComparison.Ordinal);
                if (hasUnexpectedSelectionPayload || runtime.QuestionStartedAtUtc.HasValue)
                {
                    discardedCorruptOpenQuestion = true;
                    ReconcileTargetedGeneratedOrdinalAfterDiscard();
                }
                if (string.Equals(_sessionMode, "lesson", StringComparison.Ordinal) &&
                    (!hadPersistedLessonSelection || lessonSelectionMetadataNeedsRepair))
                {
                    try { _runtime.SaveCheckpoint(_session.SessionId, _generatedQuestionCount, _forcedRepairTemplateId, RuntimeCheckpointSelectionJson()); } catch { }
                }
                return;
            }

            try
            {
                var question = _json.Deserialize<MathQuestion>(runtime.CurrentQuestionJson);
                if (!IsUsableRestoredQuestion(question)) throw new InvalidOperationException("Invalid cached Math question.");
                if (string.Equals(_sessionMode, "lesson", StringComparison.Ordinal))
                {
                    if (_generatedQuestionCount < 1 || _generatedQuestionCount > _targetQuestions.Count ||
                        !string.Equals(question.ContentQuestionId, _targetQuestions[_generatedQuestionCount - 1].ContentQuestionId, StringComparison.Ordinal))
                        throw new InvalidDataException("Cached targeted Math question is outside the persisted selected set.");
                }

                var nextAttemptIndex = NextAttemptIndexForQuestion(committedAttempts, question.QuestionId);
                if (nextAttemptIndex == 0)
                {
                    if (string.Equals(_sessionMode, "adaptive", StringComparison.Ordinal) &&
                        _lastBehavior != null && _lastBehavior.TriggerPrerequisiteRepair)
                        _forcedRepairTemplateId = RepairTemplateFor(question);
                    try { _runtime.SaveCheckpoint(_session.SessionId, _generatedQuestionCount, _forcedRepairTemplateId, RuntimeCheckpointSelectionJson()); } catch { }
                    return;
                }

                _currentQuestion = question;
                _currentSelection = DeserializeSelection(runtime.CurrentSelectionJson, question);
                _currentAttemptIndex = nextAttemptIndex;
                _questionStartedAtUtc = runtime.QuestionStartedAtUtc ?? DateTime.UtcNow;
                if (string.Equals(_sessionMode, "lesson", StringComparison.Ordinal) && lessonSelectionMetadataNeedsRepair)
                {
                    try
                    {
                        _runtime.SaveOpenQuestion(_session.SessionId, _generatedQuestionCount, _json.Serialize(question),
                            SerializeSelection(_currentSelection), _questionStartedAtUtc, _forcedRepairTemplateId);
                    }
                    catch { }
                }
                restoredOpenQuestion = true;
            }
            catch
            {
                _currentQuestion = null;
                _currentSelection = null;
                discardedCorruptOpenQuestion = true;
                ReconcileTargetedGeneratedOrdinalAfterDiscard();
                try { _runtime.SaveCheckpoint(_session.SessionId, _generatedQuestionCount, _forcedRepairTemplateId, RuntimeCheckpointSelectionJson()); } catch { }
            }
        }

        private void ReconcileTargetedGeneratedOrdinalAfterDiscard()
        {
            _currentAttemptIndex = 1;
            if (!string.Equals(_sessionMode, "lesson", StringComparison.Ordinal)) return;
            _generatedQuestionCount = Math.Max(0, Math.Min(_targetQuestionCount, _attempts));
        }

        private static int NextAttemptIndexForQuestion(IList<MathCommittedAttemptSnapshot> attempts, string questionId)
        {
            if (string.IsNullOrWhiteSpace(questionId)) throw new ArgumentException("questionId");
            var matches = (attempts ?? new List<MathCommittedAttemptSnapshot>())
                .Where(x => string.Equals(x.QuestionId, questionId, StringComparison.Ordinal))
                .OrderBy(x => x.AttemptIndex)
                .ToList();
            if (matches.Count == 0) return 1;

            var expected = 1;
            var finalized = false;
            foreach (var attempt in matches)
            {
                if (attempt.AttemptIndex != expected)
                    throw new InvalidDataException("Persisted Math retry attempt_index sequence is not contiguous.");
                if (finalized)
                    throw new InvalidDataException("Persisted Math retry has attempts after finalization.");
                if (attempt.MasteryScoreAfter.HasValue)
                {
                    finalized = true;
                }
                else if (attempt.IsCorrect)
                {
                    throw new InvalidDataException("Persisted pending Math retry cannot already be correct without finalization.");
                }
                expected++;
            }
            if (finalized) return 0;
            if (matches.Count >= MaxAttemptsPerQuestion)
                throw new InvalidDataException("Persisted Math retry exhausted attempts without a final mastery event.");
            return matches.Count + 1;
        }

        private void ReconcileAfterFailedAnswerCommit(MathQuestion question)
        {
            try
            {
                var committedAttempts = _runtime.LoadCommittedAttempts(_session.SessionId);
                var durableSkills = _sessionService.LoadSkillSnapshots(_profile.ChildId, "math");
                _skills = durableSkills;
                var nextAttemptIndex = question == null ? 1 : NextAttemptIndexForQuestion(committedAttempts, question.QuestionId);
                if (question != null && nextAttemptIndex == 0)
                {
                    RebuildFromCommittedAttempts(committedAttempts);
                    if (string.Equals(_sessionMode, "adaptive", StringComparison.Ordinal) &&
                        _lastBehavior != null && _lastBehavior.TriggerPrerequisiteRepair)
                        _forcedRepairTemplateId = RepairTemplateFor(question);
                    _currentQuestion = null;
                    _currentSelection = null;
                    _currentAttemptIndex = 1;
                    try { _runtime.SaveCheckpoint(_session.SessionId, _generatedQuestionCount, _forcedRepairTemplateId, RuntimeCheckpointSelectionJson()); } catch { }
                    return;
                }

                ResetBehaviorFromCommittedAttempts(committedAttempts);
                _currentAttemptIndex = nextAttemptIndex;
            }
            catch
            {
                // Never retain the observation that belonged to a failed/non-durable write.
                // If durable state cannot be re-read, fall back to conservative READY state;
                // the original commit exception remains the error surfaced to the caller.
                _behavior = new BehaviorController();
                _lastBehavior = null;
            }
        }

        private void ResetBehaviorFromCommittedAttempts(IList<MathCommittedAttemptSnapshot> attempts)
        {
            _behavior = new BehaviorController();
            _lastBehavior = null;
            if (attempts == null) return;

            foreach (var attempt in attempts)
            {
                var behaviorMastery = attempt.MasteryScoreBefore;
                if (!attempt.MasteryScoreAfter.HasValue)
                {
                    SkillSnapshot pendingSkill;
                    if (_skills != null && _skills.TryGetValue(attempt.SkillId, out pendingSkill) && pendingSkill != null)
                        behaviorMastery = pendingSkill.MasteryScore;
                }
                _lastBehavior = _behavior.Observe(new BehaviorObservation
                {
                    SkillId = attempt.SkillId,
                    IsCorrect = attempt.IsCorrect,
                    ResponseMs = Math.Max(0, attempt.ResponseMs),
                    HintLevel = Math.Max(0, attempt.HintLevel),
                    UsedMaxHint = attempt.HintLevel >= 2,
                    RapidWrong = !attempt.IsCorrect && attempt.ResponseMs <= 550,
                    SkippedOrExited = false,
                    InputMiss = false,
                    ErrorType = attempt.ErrorType,
                    Representation = attempt.Representation,
                    MasteryScore = Math.Max(0.0, Math.Min(1.0, behaviorMastery)),
                    SessionElapsedMinutes = Math.Max(0, (attempt.AnsweredAtUtc - _session.StartedAtUtc).TotalMinutes)
                });
            }
        }

        private void RebuildFromCommittedAttempts(IList<MathCommittedAttemptSnapshot> attempts)
        {
            _recentTemplates.Clear();
            _recentSkills.Clear();
            _distinctSkills.Clear();
            _masteryChanges.Clear();
            _attempts = 0;
            _answerAttempts = 0;
            _correct = 0;
            _independentCorrect = 0;
            _hintedCorrect = 0;
            _retriedQuestions = 0;
            _retriedCorrect = 0;
            _wrong = 0;
            _behavior = new BehaviorController();
            _lastBehavior = null;

            if (attempts == null) return;
            var finalizedQuestionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attempt in attempts)
            {
                if (attempt.AttemptIndex < 1 || attempt.AttemptIndex > MaxAttemptsPerQuestion)
                    throw new InvalidDataException("Persisted Math attempt_index is outside retry policy.");
                _answerAttempts++;

                var behaviorMastery = attempt.MasteryScoreBefore;
                if (!attempt.MasteryScoreAfter.HasValue)
                {
                    SkillSnapshot pendingSkill;
                    if (_skills != null && _skills.TryGetValue(attempt.SkillId, out pendingSkill) && pendingSkill != null)
                        behaviorMastery = pendingSkill.MasteryScore;
                }
                _lastBehavior = _behavior.Observe(new BehaviorObservation
                {
                    SkillId = attempt.SkillId,
                    IsCorrect = attempt.IsCorrect,
                    ResponseMs = Math.Max(0, attempt.ResponseMs),
                    HintLevel = Math.Max(0, attempt.HintLevel),
                    UsedMaxHint = attempt.HintLevel >= 2,
                    RapidWrong = !attempt.IsCorrect && attempt.ResponseMs <= 550,
                    SkippedOrExited = false,
                    InputMiss = false,
                    ErrorType = attempt.ErrorType,
                    Representation = attempt.Representation,
                    MasteryScore = Math.Max(0.0, Math.Min(1.0, behaviorMastery)),
                    SessionElapsedMinutes = Math.Max(0, (attempt.AnsweredAtUtc - _session.StartedAtUtc).TotalMinutes)
                });

                if (!attempt.MasteryScoreAfter.HasValue) continue;
                if (!finalizedQuestionIds.Add(attempt.QuestionId))
                    throw new InvalidDataException("Persisted Math question has multiple final mastery-bearing attempts.");

                _attempts++;
                if (attempt.AttemptIndex > 1) _retriedQuestions++;
                if (attempt.IsCorrect)
                {
                    _correct++;
                    if (attempt.AttemptIndex == 1 && attempt.HintLevel <= 0) _independentCorrect++;
                    if (attempt.HintLevel > 0) _hintedCorrect++;
                    if (attempt.AttemptIndex > 1) _retriedCorrect++;
                }
                else _wrong++;

                _distinctSkills.Add(attempt.SkillId);
                AddRecent(_recentSkills, attempt.SkillId);
                var templateId = TemplateIdFromQuestionId(attempt.QuestionId);
                if (!string.IsNullOrWhiteSpace(templateId)) AddRecent(_recentTemplates, templateId);
                var reconstructedDelta = attempt.MasteryScoreAfter.Value - attempt.MasteryScoreBefore;
                if (attempt.MasteryDelta.HasValue && Math.Abs(attempt.MasteryDelta.Value - reconstructedDelta) > 0.0000001)
                    throw new InvalidDataException("Persisted Math mastery event delta is inconsistent with score_before/score_after.");
                ApplyMasteryChange(attempt.SkillId, attempt.MasteryScoreBefore, attempt.MasteryScoreAfter.Value);
            }
        }

        private string SerializeSelection(MathSelectionDecision selection)
        {
            if (selection == null || selection.Template == null) throw new ArgumentNullException("selection");
            var data = new Dictionary<string, object>
            {
                { "template_id", selection.Template.TemplateId },
                { "skill_id", selection.Template.SkillId },
                { "score", selection.Score },
                { "difficulty_fit", selection.DifficultyFit },
                { "reasons", selection.Reasons ?? new string[0] },
                { "candidate_summary", selection.CandidateSummary ?? new string[0] }
            };
            AddLessonSelectedIds(data);
            return _json.Serialize(data);
        }

        private string SerializeLessonSelectionCheckpoint()
        {
            if (!string.Equals(_sessionMode, "lesson", StringComparison.Ordinal)) return null;
            var data = new Dictionary<string, object>();
            AddLessonSelectedIds(data);
            return _json.Serialize(data);
        }

        private void AddLessonSelectedIds(IDictionary<string, object> data)
        {
            if (!string.Equals(_sessionMode, "lesson", StringComparison.Ordinal)) return;
            if (_selectedContentQuestionIds == null || _selectedContentQuestionIds.Count != TargetedLessonQuestionCount ||
                _selectedContentQuestionIds.Any(string.IsNullOrWhiteSpace) ||
                _selectedContentQuestionIds.Distinct(StringComparer.Ordinal).Count() != TargetedLessonQuestionCount)
                throw new InvalidOperationException("Targeted Math selected question set is not initialized.");
            data["selected_content_question_ids"] = _selectedContentQuestionIds.ToArray();
        }

        private bool TryRestorePersistedTargetSelection(string json, out bool metadataNeedsRepair)
        {
            metadataNeedsRepair = false;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var persisted = DeserializeLessonSelectedContentQuestionIds(json);
                if (persisted.Count == 0)
                {
                    metadataNeedsRepair = true;
                    return false;
                }
                _selectedContentQuestionIds = persisted;
                ApplySelectedTargetQuestions();
                return true;
            }
            catch (ArgumentException)
            {
                metadataNeedsRepair = true;
            }
            catch (InvalidDataException)
            {
                metadataNeedsRepair = true;
            }
            catch (InvalidOperationException)
            {
                metadataNeedsRepair = true;
            }
            _selectedContentQuestionIds = new List<string>();
            _targetQuestions = new List<MathQuestion>();
            return false;
        }

        private IList<string> DeserializeLessonSelectedContentQuestionIds(string json)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(json)) return result;
            var data = _json.Deserialize<Dictionary<string, object>>(json);
            object raw;
            if (data == null || !data.TryGetValue("selected_content_question_ids", out raw)) return result;
            result = StringList(raw).ToList();
            if (result.Count != TargetedLessonQuestionCount || result.Any(string.IsNullOrWhiteSpace) ||
                result.Distinct(StringComparer.Ordinal).Count() != TargetedLessonQuestionCount)
                throw new InvalidDataException("Persisted targeted Math selected question set is invalid.");
            return result;
        }

        private string RuntimeCheckpointSelectionJson()
        {
            return string.Equals(_sessionMode, "lesson", StringComparison.Ordinal)
                ? SerializeLessonSelectionCheckpoint()
                : null;
        }

        private MathSelectionDecision DeserializeSelection(string json, MathQuestion question)
        {
            var template = _templates.FirstOrDefault(x => string.Equals(x.TemplateId, question.TemplateId, StringComparison.Ordinal));
            if (template == null)
                template = new MathTemplateRef { TemplateId = question.TemplateId, SkillId = question.SkillId };

            if (string.IsNullOrWhiteSpace(json))
                return new MathSelectionDecision { Template = template, DifficultyFit = question.DifficultyFit, Reasons = new List<string>(), CandidateSummary = new List<string>() };

            var data = _json.Deserialize<Dictionary<string, object>>(json);
            object value;
            var decision = new MathSelectionDecision { Template = template };
            if (data != null && data.TryGetValue("score", out value)) decision.Score = DoubleValue(value, 0);
            if (data != null && data.TryGetValue("difficulty_fit", out value)) decision.DifficultyFit = DoubleValue(value, question.DifficultyFit);
            else decision.DifficultyFit = question.DifficultyFit;
            decision.Reasons = data != null && data.TryGetValue("reasons", out value) ? StringList(value) : new List<string>();
            decision.CandidateSummary = data != null && data.TryGetValue("candidate_summary", out value) ? StringList(value) : new List<string>();
            return decision;
        }

        private static IList<string> StringList(object value)
        {
            var result = new List<string>();
            if (value == null) return result;
            var single = value as string;
            if (single != null) { result.Add(single); return result; }
            var enumerable = value as System.Collections.IEnumerable;
            if (enumerable == null) return result;
            foreach (var item in enumerable)
            {
                var text = Convert.ToString(item, System.Globalization.CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(text)) result.Add(text);
            }
            return result;
        }

        private static double DoubleValue(object value, double fallback)
        {
            if (value == null) return fallback;
            try { return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static bool IsUsableRestoredQuestion(MathQuestion question)
        {
            return question != null &&
                   !string.IsNullOrWhiteSpace(question.QuestionId) &&
                   !string.IsNullOrWhiteSpace(question.TemplateId) &&
                   !string.IsNullOrWhiteSpace(question.SkillId) &&
                   !string.IsNullOrWhiteSpace(question.PromptVi);
        }

        private static string TemplateIdFromQuestionId(string questionId)
        {
            if (string.IsNullOrWhiteSpace(questionId)) return null;
            var split = questionId.LastIndexOf('-');
            if (split <= 0 || questionId.Length - split - 1 != 32) return null;
            for (var i = split + 1; i < questionId.Length; i++)
            {
                var c = questionId[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return null;
            }
            return questionId.Substring(0, split);
        }

        private static int QuestionSeed(int baseSeed, int ordinal, string templateId)
        {
            unchecked
            {
                uint hash = 2166136261;
                Action<int> mix = value =>
                {
                    hash ^= (byte)value; hash *= 16777619;
                    hash ^= (byte)(value >> 8); hash *= 16777619;
                    hash ^= (byte)(value >> 16); hash *= 16777619;
                    hash ^= (byte)(value >> 24); hash *= 16777619;
                };
                mix(baseSeed);
                mix(ordinal);
                foreach (var c in templateId ?? string.Empty)
                {
                    hash ^= (byte)c; hash *= 16777619;
                    hash ^= (byte)(c >> 8); hash *= 16777619;
                }
                return (int)(hash & 0x7fffffff);
            }
        }

        private MathSessionSummary BuildSummary(DateTime? ended)
        {
            var masteryChanges = _masteryChanges.Values
                .OrderBy(x => x.SkillId, StringComparer.Ordinal)
                .Select(x => new MathSkillMasteryChange
                {
                    SkillId = x.SkillId,
                    ScoreBefore = x.ScoreBefore,
                    ScoreAfter = x.ScoreAfter,
                    Delta = x.Delta
                })
                .ToList();
            MathSkillMasteryChange targetMastery = null;
            if (_targetLesson != null)
                _masteryChanges.TryGetValue(_targetLesson.SkillId, out targetMastery);

            return new MathSessionSummary
            {
                Attempts = _attempts,
                AnswerAttempts = _answerAttempts,
                Correct = _correct,
                IndependentCorrect = _independentCorrect,
                HintedCorrect = _hintedCorrect,
                RetriedQuestions = _retriedQuestions,
                RetriedCorrect = _retriedCorrect,
                Wrong = _wrong,
                DistinctSkills = _distinctSkills.Count,
                FinalBehaviorState = _lastBehavior == null ? BehaviorState.READY : _lastBehavior.State,
                StartedAtUtc = _session == null ? DateTime.MinValue : _session.StartedAtUtc,
                EndedAtUtc = ended,
                SessionMode = _sessionMode,
                TargetLessonId = _targetLessonId,
                LessonCompleted = false,
                LessonScorePercent = string.Equals(_sessionMode, "lesson", StringComparison.Ordinal) && _attempts > 0
                    ? (double?)(100.0 * _correct / _attempts) : null,
                LessonBestScorePercent = null,
                MasteryChanges = masteryChanges,
                ImprovedSkillCount = masteryChanges.Count(x => x.Delta > 0.000000001),
                TargetSkillMasteryBefore = targetMastery == null ? null : (double?)targetMastery.ScoreBefore,
                TargetSkillMasteryAfter = targetMastery == null ? null : (double?)targetMastery.ScoreAfter,
                TargetSkillMasteryDelta = targetMastery == null ? null : (double?)targetMastery.Delta
            };
        }

        private void ApplyMasteryChange(string skillId, double scoreBefore, double scoreAfter)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return;
            MathSkillMasteryChange existing;
            if (!_masteryChanges.TryGetValue(skillId, out existing) || existing == null)
            {
                existing = new MathSkillMasteryChange
                {
                    SkillId = skillId,
                    ScoreBefore = scoreBefore,
                    ScoreAfter = scoreAfter
                };
                _masteryChanges[skillId] = existing;
            }
            else
            {
                existing.ScoreAfter = scoreAfter;
            }
            existing.Delta = existing.ScoreAfter - existing.ScoreBefore;
        }

        private void PopulateNextLesson(MathSessionSummary summary)
        {
            if (summary == null || !summary.LessonCompleted || _profile == null || _targetLesson == null) return;
            var next = new MathLessonProgressService(_database, ContentSiblingPath("lesson_catalog_v1.json"))
                .GetNextLessonAccess(_profile.ChildId, _targetLesson.Id);
            if (next == null || !next.IsUnlocked) return;
            summary.NextLessonId = next.LessonId;
            summary.NextLessonTitleVi = next.TitleVi;
        }

        private static SkillSnapshot Apply(SkillSnapshot current, MasteryUpdate update, DateTime now, DateTime due, bool success)
        {
            return new SkillSnapshot
            {
                SkillId = current.SkillId,
                MasteryScore = update.ScoreAfter,
                Confidence = update.ConfidenceAfter,
                AttemptsCount = update.AttemptsCount,
                IndependentSuccessCount = update.IndependentSuccessCount,
                HintedSuccessCount = update.HintedSuccessCount,
                TransferSuccessCount = update.TransferSuccessCount,
                LastSeenAtUtc = now,
                LastSuccessAtUtc = success ? (DateTime?)now : current.LastSuccessAtUtc,
                NextReviewAtUtc = due,
                LearningState = update.LearningState
            };
        }

        private static SkillSnapshot NewSkill(string id)
        {
            return new SkillSnapshot { SkillId = id, MasteryScore = 0.25, Confidence = 0.20, LearningState = "NEW" };
        }

        private static string RepairTemplateFor(MathQuestion question)
        {
            if (question == null || string.IsNullOrWhiteSpace(question.TemplateId)) return null;
            var templateId = question.TemplateId;
            switch (templateId)
            {
                case "expanded_form_3digit": return "place_value_decompose_3digit";
                case "compare_two_numbers_1000": return "place_value_decompose_3digit";
                case "predecessor_successor": return "compare_two_numbers_1000";
                case "divide_table_2_exact": return "times_table_2";
                case "divide_table_5_exact": return "times_table_5";
                case "polyline_length": return "mental_add_within_20";
                case "add_within_1000_one_carry": return "add_within_1000_no_carry";
                case "subtract_within_1000_one_borrow": return "subtract_within_1000_no_borrow";
                case "add_within_1000_no_carry": return "mental_add_within_20";
                case "subtract_within_1000_no_borrow": return "mental_sub_within_20";
                case "word_problem_add_more":
                case "word_problem_more_than": return "mental_add_within_20";
                case "word_problem_sub_less":
                case "word_problem_less_than": return "mental_sub_within_20";
                case "word_problem_multiply_groups_2_5":
                {
                    var factor = ReadIllustrationInt(question.IllustrationData, "wordgroups", 1);
                    return factor == 5 ? "times_table_5" : "times_table_2";
                }
                case "word_problem_divide_groups_2_5":
                {
                    var divisor = ReadIllustrationInt(question.IllustrationData, "wordshare", 2);
                    return divisor == 5 ? "times_table_5" : "times_table_2";
                }
                case "add_components_recognize": return "mental_add_within_20";
                case "sub_components_recognize": return "mental_sub_within_20";
                case "multiplication_components_recognize":
                {
                    var factor = ReadIllustrationInt(question.IllustrationData, "equationparts", 2);
                    return factor == 5 ? "times_table_5" : "times_table_2";
                }
                case "division_components_recognize":
                {
                    var divisor = ReadIllustrationInt(question.IllustrationData, "equationparts", 3);
                    return divisor == 5 ? "times_table_5" : "times_table_2";
                }
                case "multiplication_meaning_groups":
                {
                    var factor = ReadIllustrationInt(question.IllustrationData, "wordgroups", 1);
                    return factor == 5 ? "times_table_5" : "times_table_2";
                }
                case "division_meaning_share":
                {
                    var divisor = ReadIllustrationInt(question.IllustrationData, "wordshare", 2);
                    return divisor == 5 ? "times_table_5" : "times_table_2";
                }
                case "operation_meaning_from_visual":
                case "word_problem_select_operation_one_step":
                    return RepairByOperationModel(question);
                case "full_hundreds_recognize": return "place_value_decompose_3digit";
                case "number_ray_fill_1000": return "predecessor_successor";
                case "min_max_up_to_4":
                case "sort_up_to_4": return "compare_two_numbers_1000";
                case "add_sub_two_operators_left_to_right":
                case "mental_round_tens_hundreds_1000": return "mental_add_within_20";
                case "time_day_24_hours": return "time_hour_60_minutes";
                case "time_hour_60_minutes": return "clock_read_minute_hand_3_or_6";
                case "calendar_days_in_month_date": return "time_day_24_hours";
                case "measure_with_ruler_cm": return "measure_with_common_scale";
                case "measure_with_common_scale": return "number_ray_fill_1000";
                case "measurement_convert_calculate_learned_units": return "length_dm_m_km_relation";
                case "measurement_real_world_one_step": return "measurement_convert_calculate_learned_units";
                case "count_place_value_to_1000":
                case "read_number_to_1000":
                case "write_number_to_1000": return "place_value_decompose_3digit";
                case "estimate_objects_by_tens": return "mental_round_tens_hundreds_1000";
                case "measurement_estimate_reference_10cm": return "measure_with_ruler_cm";
                default: return templateId;
            }
        }

        private static string RepairByOperationModel(MathQuestion question)
        {
            var parts = (question == null ? string.Empty : question.IllustrationData ?? string.Empty).Split('|');
            if (parts.Length < 2) return null;
            if (parts[0] == "wordbar")
                return parts[1] == "add" || parts[1] == "more" ? "mental_add_within_20" : "mental_sub_within_20";
            if (parts[0] == "wordgroups")
            {
                var factor = ReadIllustrationInt(question.IllustrationData, "wordgroups", 1);
                return factor == 5 ? "times_table_5" : "times_table_2";
            }
            if (parts[0] == "wordshare")
            {
                var divisor = ReadIllustrationInt(question.IllustrationData, "wordshare", 2);
                return divisor == 5 ? "times_table_5" : "times_table_2";
            }
            return null;
        }

        private static int ReadIllustrationInt(string data, string prefix, int index)
        {
            var parts = (data ?? string.Empty).Split('|');
            int value;
            if (parts.Length <= index || !string.Equals(parts[0], prefix, StringComparison.Ordinal) ||
                !int.TryParse(parts[index], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out value)) return 0;
            return value;
        }

        private static object AnswerValueForJson(MathQuestion question, string answer)
        {
            if (question != null && !question.UsesTextChoices)
            {
                int numeric;
                if (int.TryParse(answer, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out numeric)) return numeric;
            }
            return answer ?? string.Empty;
        }

        private static string BuildGardenUnlockMessage(IList<string> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0) return null;
            var names = new List<string>();
            foreach (var id in itemIds)
            {
                switch (id)
                {
                    case "garden_seedling": names.Add("Mầm cây mới"); break;
                    case "garden_flower_patch": names.Add("Bồn hoa"); break;
                    case "garden_lantern": names.Add("Đèn vườn"); break;
                    case "garden_bench": names.Add("Ghế nhỏ"); break;
                }
            }
            return names.Count == 0 ? "Khu vườn vừa có thêm một món mới." : "Mở khóa: " + string.Join(", ", names) + ".";
        }

        private static string BuildRetryFeedback(MathErrorClassification error)
        {
            if (error == null)
                return "Chưa đúng. Con xem gợi ý rồi thử lại chính câu này nhé.";
            return BuildFeedback(false, 1, error) + " Con thử lại chính câu này nhé.";
        }

        private static string BuildFeedback(bool correct, int hintLevel, MathErrorClassification error)
        {
            if (correct && hintLevel <= 0) return "Đúng rồi — con tự làm được bước này.";
            if (correct) return "Đúng rồi. Gợi ý đã giúp con hoàn thành bước này.";
            if (error != null && error.ErrorType == "CARRY_MISSING") return "Chưa đúng. Con nhớ kiểm tra bước nhớ sang hàng bên trái nhé.";
            if (error != null && error.ErrorType == "BORROW_MISSING") return "Chưa đúng. Con kiểm tra lại bước mượn ở hàng cần trừ nhé.";
            if (error != null && error.ErrorType == "PLACE_VALUE_ERROR") return "Chưa đúng. Con nhìn lại từng hàng trăm, chục và đơn vị nhé.";
            if (error != null && error.ErrorType == "COMPARISON_ERROR") return "Chưa đúng. Mình so sánh từ hàng lớn nhất trước nhé.";
            if (error != null && error.ErrorType == "SEQUENCE_NEIGHBOR_ERROR") return "Chưa đúng. Số liền trước kém 1 và số liền sau hơn 1 nhé.";
            if (error != null && error.ErrorType == "MEASUREMENT_SUM_ERROR") return "Chưa đúng. Độ dài đường gấp khúc là tổng các đoạn của nó.";
            if (error != null && error.ErrorType == "TIME_READ_ERROR") return "Chưa đúng. Con đọc kim phút trước: số 3 là 15 phút, số 6 là 30 phút nhé.";
            if (error != null && error.ErrorType == "GEOMETRY_RECOGNITION_ERROR") return "Chưa đúng. Con nhìn lại đặc điểm của hình: nét, đầu mút, số cạnh hoặc dạng khối nhé.";
            if (error != null && error.ErrorType == "PICTOGRAPH_READ_ERROR") return "Chưa đúng. Con đếm lại từng hình trong biểu đồ rồi so sánh nhé.";
            if (error != null && error.ErrorType == "WORD_PROBLEM_RELATION_ERROR") return "Chưa đúng. Con xác định điều đã biết, điều cần tìm rồi nhìn lại sơ đồ quan hệ nhé.";
            if (error != null && error.ErrorType == "OPERATION_COMPONENT_ERROR") return "Chưa đúng. Con nhìn vị trí của số trong phép tính rồi gọi tên theo vai trò nhé.";
            if (error != null && error.ErrorType == "OPERATION_MEANING_ERROR") return "Chưa đúng. Con nhìn lại mô hình: gộp, bớt, nhóm bằng nhau hay chia đều nhé.";
            if (error != null && error.ErrorType == "HUNDREDS_RECOGNITION_ERROR") return "Chưa đúng. Mỗi ô lớn là một trăm; con đếm lại số ô trăm nhé.";
            if (error != null && error.ErrorType == "NUMBER_SEQUENCE_ERROR") return "Chưa đúng. Con nhìn khoảng cách đều giữa các mốc trên trục số nhé.";
            if (error != null && error.ErrorType == "NUMBER_ORDER_ERROR") return "Chưa đúng. Con so sánh từ hàng trăm rồi đến hàng chục và đơn vị nhé.";
            if (error != null && error.ErrorType == "TWO_STEP_CALCULATION_ERROR") return "Chưa đúng. Con làm phép tính thứ nhất trước rồi dùng kết quả cho bước thứ hai nhé.";
            if (error != null && error.ErrorType == "ROUND_NUMBER_FACT_ERROR") return "Chưa đúng. Con xem các số tròn chục hoặc tròn trăm thành những nhóm bằng nhau nhé.";
            if (error != null && error.ErrorType == "MEASUREMENT_COMPARE_ERROR") return "Chưa đúng. Con nhìn độ cao hai đĩa cân: bên nặng hơn sẽ thấp hơn nhé.";
            if (error != null && error.ErrorType == "MEASUREMENT_UNIT_ERROR") return "Chưa đúng. Con đọc lại số đo và đơn vị kg, lít, dm, m hoặc km nhé.";
            if (error != null && error.ErrorType == "TIME_RELATION_ERROR") return "Chưa đúng. Con nhớ 1 ngày = 24 giờ và 1 giờ = 60 phút nhé.";
            if (error != null && error.ErrorType == "CALENDAR_READ_ERROR") return "Chưa đúng. Con đọc lại tháng, ô ngày được đánh dấu và ngày cuối cùng của tháng nhé.";
            if (error != null && error.ErrorType == "MEASURE_READ_ERROR") return "Chưa đúng. Con đọc vị trí hai đầu A, B trên thước rồi lấy số cuối trừ số đầu nhé.";
            if (error != null && error.ErrorType == "SCALE_READ_ERROR") return "Chưa đúng. Con tìm bước tăng giữa hai vạch liền nhau rồi đọc lại mũi tên nhé.";
            if (error != null && error.ErrorType == "MEASUREMENT_CALC_ERROR") return "Chưa đúng. Con kiểm tra đơn vị trước, rồi mới đổi hoặc cộng trừ các số đo nhé.";
            if (error != null && error.ErrorType == "MEASUREMENT_WORD_ERROR") return "Chưa đúng. Con xác định số đo ban đầu và phần thêm hoặc bớt trước nhé.";
            if (error != null && error.ErrorType == "DATA_CLASSIFY_COUNT_ERROR") return "Chưa đúng. Con phân loại các hình cùng loại rồi chỉ đếm nhóm được hỏi nhé.";
            if (error != null && error.ErrorType == "NUMBER_READ_WRITE_ERROR") return "Chưa đúng. Con nhìn lại hàng trăm, chục, đơn vị và cách đọc số mốt, tư, lăm, linh nhé.";
            if (error != null && error.ErrorType == "ESTIMATION_ERROR") return "Chưa đúng. Đây là bài ước lượng: con so sánh với nhóm chục hoặc thanh mẫu rồi chọn mốc gần nhất nhé.";
            if (error != null && error.ErrorType == "EVENT_CLASSIFICATION_ERROR") return "Chưa đúng. Con đối chiếu câu này với tất cả kết quả có thể của xúc xắc nhé.";
            return "Chưa đúng. Mình xem gợi ý rồi thử câu tiếp theo nhé.";
        }

        private static void AddRecent(IList<string> list, string value)
        {
            list.Add(value);
            while (list.Count > 4) list.RemoveAt(0);
        }

        private void EnsureActive()
        {
            if (!_active || _session == null || _profile == null) throw new InvalidOperationException("Math session is not active.");
        }
    }
}
