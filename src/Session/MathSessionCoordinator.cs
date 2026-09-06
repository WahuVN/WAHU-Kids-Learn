using System;
using System.Collections.Generic;
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
        public const string PackVersion = "1.3.0";
        public const int DefaultTargetQuestionCount = 8;

        private readonly LearningDatabase _database;
        private readonly string _templatePath;
        private readonly string _performanceProfile;
        private readonly int _targetQuestionCount;
        private readonly LearnerSessionService _sessionService;
        private readonly AnswerCommitService _answerCommit;
        private readonly BehaviorDecisionAuditService _behaviorAudit;
        private readonly AdaptiveDecisionAuditService _adaptiveAudit;
        private readonly GameWorldRewardService _gameWorld;
        private readonly BehaviorController _behavior = new BehaviorController();
        private readonly AdaptiveMathSelector _selector = new AdaptiveMathSelector();
        private readonly MathQuestionGenerator _generator;
        private readonly MasteryEngineV1 _mastery = new MasteryEngineV1();
        private readonly ReviewSchedulerV1 _scheduler = new ReviewSchedulerV1();
        private readonly MathErrorClassifierV1 _errorClassifier = new MathErrorClassifierV1();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private readonly List<string> _recentTemplates = new List<string>();
        private readonly List<string> _recentSkills = new List<string>();
        private readonly HashSet<string> _distinctSkills = new HashSet<string>(StringComparer.Ordinal);

        private IList<MathTemplateRef> _templates;
        private IDictionary<string, SkillSnapshot> _skills;
        private LearnerProfile _profile;
        private LearnerSessionHandle _session;
        private MathQuestion _currentQuestion;
        private MathSelectionDecision _currentSelection;
        private DateTime _questionStartedAtUtc;
        private BehaviorDecision _lastBehavior;
        private string _forcedRepairTemplateId;
        private bool _active;
        private int _attempts;
        private int _correct;
        private int _hintedCorrect;
        private int _wrong;

        public MathSessionCoordinator(LearningDatabase database, string templatePath, string performanceProfile, int seed)
            : this(database, templatePath, performanceProfile, seed, DefaultTargetQuestionCount) { }

        public MathSessionCoordinator(LearningDatabase database, string templatePath, string performanceProfile, int seed, int targetQuestionCount)
        {
            _database = database ?? throw new ArgumentNullException("database");
            if (string.IsNullOrWhiteSpace(templatePath)) throw new ArgumentException("templatePath");
            _templatePath = templatePath;
            _performanceProfile = performanceProfile == "NORMAL" ? "NORMAL" : "LOW";
            if (targetQuestionCount < 1 || targetQuestionCount > 40) throw new ArgumentOutOfRangeException("targetQuestionCount");
            _targetQuestionCount = targetQuestionCount;
            _generator = new MathQuestionGenerator(seed);
            _sessionService = new LearnerSessionService(database);
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

            _profile = _sessionService.EnsurePrimaryChild(displayName);
            var recovered = _sessionService.RecoverDanglingSessions();
            try
            {
                _session = _sessionService.BeginSession(_profile.ChildId, "math", _performanceProfile);
                _skills = _sessionService.LoadSkillSnapshots(_profile.ChildId, "math");
                _active = true;
            }
            catch
            {
                if (_session != null)
                {
                    try
                    {
                        _sessionService.CompleteSession(_session.SessionId, true,
                            _json.Serialize(new Dictionary<string, object> { { "reason", "session_start_failed" }, { "attempts", 0 } }),
                            _json.Serialize(new Dictionary<string, object> { { "final_state", BehaviorState.READY.ToString() } }));
                    }
                    catch { }
                }
                _session = null;
                _skills = null;
                _active = false;
                throw;
            }
            return new MathSessionStartResult
            {
                SessionId = _session.SessionId,
                ChildId = _profile.ChildId,
                DisplayName = _profile.DisplayName,
                RecoveredDanglingSessions = recovered,
                TargetQuestionCount = _targetQuestionCount
            };
        }

        public MathQuestion NextQuestion()
        {
            EnsureActive();
            if (_currentQuestion != null) throw new InvalidOperationException("Current question has not been answered.");
            if (_attempts >= _targetQuestionCount) return null;

            IEnumerable<MathTemplateRef> candidates = _templates;
            if (!string.IsNullOrWhiteSpace(_forcedRepairTemplateId))
            {
                var repair = _templates.Where(x => string.Equals(x.TemplateId, _forcedRepairTemplateId, StringComparison.Ordinal)).ToList();
                if (repair.Count > 0) candidates = repair;
            }

            _currentSelection = _selector.Select(candidates, _skills, DateTime.UtcNow, _recentTemplates, _recentSkills);
            if (!string.IsNullOrWhiteSpace(_forcedRepairTemplateId))
            {
                if (_currentSelection.Reasons == null) _currentSelection.Reasons = new List<string>();
                _currentSelection.Reasons.Add("prerequisite_repair");
                _forcedRepairTemplateId = null;
            }
            _currentQuestion = _generator.Generate(_currentSelection);
            _questionStartedAtUtc = DateTime.UtcNow;

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
            var mastery = _mastery.Evaluate(current, isCorrect, hintLevel, false, behaviorDecision.ProtectMasteryFromNegativeUpdate);
            var review = _scheduler.Schedule(answeredUtc, mastery, isCorrect, hintLevel);
            var attemptId = "attempt-" + Guid.NewGuid().ToString("N");

            _answerCommit.Commit(new AnswerCommitRequest
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
                AttemptIndex = 1,
                ListenCount = 0,
                Error = error == null ? null : new ErrorEventWrite
                {
                    Id = "error-" + Guid.NewGuid().ToString("N"),
                    ErrorType = error.ErrorType,
                    Confidence = error.Confidence,
                    EvidenceJson = _json.Serialize(error.Evidence),
                    ClassifierVersion = MathErrorClassifierV1.Version
                },
                Mastery = new MasteryEventWrite
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
                ChildSkill = new ChildSkillWrite
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
                Review = new ReviewScheduleWrite
                {
                    DueAtUtc = review.DueAtUtc,
                    IntervalDays = review.IntervalDays,
                    Reason = review.Reason,
                    SchedulerVersion = ReviewSchedulerV1.Version
                }
            });

            // AnswerCommit ở trên là source-of-truth durable. Diagnostics/audit lỗi sau commit
            // không được làm UI nghĩ câu chưa lưu rồi submit lại thành attempt mới.
            try
            {
                _behaviorAudit.Record(new BehaviorDecisionAuditRequest
                {
                    Id = "behavior-" + Guid.NewGuid().ToString("N"),
                    SessionId = _session.SessionId,
                    ChildId = _profile.ChildId,
                    AttemptId = attemptId,
                    Decision = behaviorDecision,
                    ControllerVersion = "behavior-v1",
                    CreatedAtUtc = answeredUtc
                });
            }
            catch
            {
                // Best-effort diagnostic only. Learning state is already committed atomically.
            }

            _skills[question.SkillId] = Apply(current, mastery, answeredUtc, review.DueAtUtc, isCorrect);
            AddRecent(_recentTemplates, question.TemplateId);
            AddRecent(_recentSkills, question.SkillId);
            _distinctSkills.Add(question.SkillId);
            _attempts++;
            if (isCorrect) { _correct++; if (hintLevel > 0) _hintedCorrect++; } else _wrong++;
            _lastBehavior = behaviorDecision;
            if (behaviorDecision.TriggerPrerequisiteRepair) _forcedRepairTemplateId = RepairTemplateFor(question);

            var outcome = new MathAnswerOutcome
            {
                IsCorrect = isCorrect,
                CorrectAnswer = question.CorrectAnswer,
                CorrectAnswerDisplay = question.CorrectAnswerDisplay,
                HintLevel = hintLevel,
                FeedbackVi = BuildFeedback(isCorrect, hintLevel, error),
                Behavior = behaviorDecision,
                Mastery = mastery,
                Review = review,
                Error = error,
                OfferBreak = behaviorDecision.Actions != null && behaviorDecision.Actions.Contains("offer_break"),
                SuggestPositiveEnd = behaviorDecision.State == BehaviorState.FATIGUED_LIKELY && _attempts >= 4,
                CompletedQuestionCount = _attempts,
                TargetQuestionCount = _targetQuestionCount
            };
            _currentQuestion = null;
            _currentSelection = null;
            return outcome;
        }

        public MathSessionSummary Complete()
        {
            EnsureActive();
            var ended = DateTime.UtcNow;
            var summary = BuildSummary(ended);
            _sessionService.CompleteSession(_session.SessionId, false,
                _json.Serialize(new Dictionary<string, object>
                {
                    { "attempts", summary.Attempts }, { "correct", summary.Correct }, { "hinted_correct", summary.HintedCorrect },
                    { "wrong", summary.Wrong }, { "distinct_skills", summary.DistinctSkills }, { "subject", "math" }
                }),
                _json.Serialize(new Dictionary<string, object> { { "final_state", summary.FinalBehaviorState.ToString() } }));
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
            _active = false;
            return summary;
        }

        public void Dispose()
        {
            if (_active)
            {
                try { Abort("coordinator_disposed"); }
                catch { }
            }
        }

        private MathSessionSummary BuildSummary(DateTime? ended)
        {
            return new MathSessionSummary
            {
                Attempts = _attempts,
                Correct = _correct,
                HintedCorrect = _hintedCorrect,
                Wrong = _wrong,
                DistinctSkills = _distinctSkills.Count,
                FinalBehaviorState = _lastBehavior == null ? BehaviorState.READY : _lastBehavior.State,
                StartedAtUtc = _session == null ? DateTime.MinValue : _session.StartedAtUtc,
                EndedAtUtc = ended
            };
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
