using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using WAHU.Content;
using WAHU.Data;
using WAHU.Learning;
using WAHU.Session;

namespace WAHU.LearningSessionRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        private static void Main(string[] args)
        {
            var root = Directory.GetCurrentDirectory();
            var schema = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(root, "data", "schema", "001_initial.sql");
            var templates = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(root, "content_packs", "math_grade2_v1", "verified_templates_v1.json");
            if (!File.Exists(schema)) throw new FileNotFoundException("schema", schema);
            if (!File.Exists(templates)) throw new FileNotFoundException("templates", templates);

            var refs = TestVerifiedContentAndCore(templates);
            var temp = Path.Combine(Path.GetTempPath(), "wahu-learning-session-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                TestDatabaseVerticalSlice(temp, schema, refs);
                TestCoordinator(temp, schema, templates);
                Console.WriteLine("LEARNING_SESSION_RUNTIME_SMOKE_PASS assertions=" + _assertions);
            }
            finally { Directory.Delete(temp, true); }
        }

        private static IList<MathTemplateRef> TestVerifiedContentAndCore(string templatePath)
        {
            var descriptors = new MathVerifiedTemplateSource().Load(templatePath);
            A(descriptors.Count >= 15, "verified_template_source_loads_full_pack");
            A(descriptors.All(x => x.Status == "VERIFIED_A_TEMPLATE"), "template_source_filters_verified_a_only");
            var refs = descriptors.Select(x => new MathTemplateRef { TemplateId = x.Id, SkillId = x.SkillId })
                .Where(AdaptiveMathSelector.IsSupported).ToList();
            A(refs.Count == 8, "generator_supports_eight_verified_templates");

            var selector = new AdaptiveMathSelector();
            var empty = new Dictionary<string, SkillSnapshot>(StringComparer.Ordinal);
            var first = selector.Select(refs, empty, new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc), new string[0], new string[0]);
            A(first != null && first.Template != null, "selector_returns_candidate");
            A(first.DifficultyFit >= 0 && first.DifficultyFit <= 1, "selector_difficulty_fit_bounded");
            A(first.CandidateSummary.Count == 8, "selector_audits_all_candidates");

            var dueSkills = new Dictionary<string, SkillSnapshot>(StringComparer.Ordinal);
            foreach (var r in refs) dueSkills[r.SkillId] = new SkillSnapshot { SkillId = r.SkillId, MasteryScore = 0.20, Confidence = 0.20, AttemptsCount = 1, LearningState = "LEARNING" };
            dueSkills["TIMES_TABLE_2"] = new SkillSnapshot { SkillId = "TIMES_TABLE_2", MasteryScore = 0.90, Confidence = 0.90, AttemptsCount = 10, LearningState = "STABLE", NextReviewAtUtc = DateTime.UtcNow.AddDays(-1) };
            var due = selector.Select(refs, dueSkills, DateTime.UtcNow, new string[0], new string[0]);
            A(due.Template.SkillId == "TIMES_TABLE_2", "due_review_overrides_simple_mastery_gap");
            A(due.Reasons.Contains("due_review"), "due_review_reason_audited");

            var generator = new MathQuestionGenerator(20260906);
            foreach (var r in refs)
            {
                var q = generator.Generate(new MathSelectionDecision { Template = r, DifficultyFit = 0.8, Reasons = new[] { "smoke" }, CandidateSummary = new[] { r.TemplateId } });
                A(!string.IsNullOrWhiteSpace(q.QuestionId), "question_id_" + r.TemplateId);
                A(!string.IsNullOrWhiteSpace(q.PromptVi), "prompt_" + r.TemplateId);
                A(q.Choices.Count == 4 && q.Choices.Distinct().Count() == 4, "four_unique_choices_" + r.TemplateId);
                A(q.Choices.Contains(q.CorrectAnswer), "correct_choice_present_" + r.TemplateId);
                if (r.TemplateId == "add_within_1000_no_carry") A(CarryCount(ParseA(q), ParseB(q)) == 0, "add_no_carry_constraint");
                if (r.TemplateId == "add_within_1000_one_carry") A(CarryCount(ParseA(q), ParseB(q)) == 1, "add_one_carry_constraint");
                if (r.TemplateId == "subtract_within_1000_no_borrow") A(BorrowCount(ParseA(q), ParseB(q)) == 0, "sub_no_borrow_constraint");
                if (r.TemplateId == "subtract_within_1000_one_borrow") A(BorrowCount(ParseA(q), ParseB(q)) == 1, "sub_one_borrow_constraint");
            }

            var mastery = new MasteryEngineV1();
            var baseSkill = new SkillSnapshot { SkillId = "X", MasteryScore = 0.25, Confidence = 0.20, LearningState = "NEW" };
            var independent = mastery.Evaluate(baseSkill, true, 0, false, false);
            var hinted = mastery.Evaluate(baseSkill, true, 1, false, false);
            var wrong = mastery.Evaluate(baseSkill, false, 0, false, false);
            var protectedWrong = mastery.Evaluate(baseSkill, false, 0, false, true);
            A(independent.Delta > hinted.Delta && hinted.Delta > 0, "independent_success_weight_gt_hinted");
            A(wrong.Delta < 0, "wrong_answer_reduces_mastery_without_protection");
            A(Math.Abs(protectedWrong.Delta) < 0.000001, "protected_failure_does_not_reduce_mastery");
            A(independent.ConfidenceAfter > baseSkill.Confidence, "correct_increases_confidence");

            var evolving = baseSkill;
            MasteryUpdate last = null;
            for (var i = 0; i < 7; i++)
            {
                last = mastery.Evaluate(evolving, true, 0, false, false);
                evolving = Apply(evolving, last, DateTime.UtcNow);
            }
            A(last.ScoreAfter > 0.80, "repeated_independent_success_builds_high_mastery");
            A(last.IndependentSuccessCount >= 3, "independent_success_count_accumulates");
            A(last.LearningState == "STABLE", "stable_gate_requires_evidence");

            var scheduler = new ReviewSchedulerV1();
            var failReview = scheduler.Schedule(DateTime.UtcNow, wrong, false, 0);
            var stableReview = scheduler.Schedule(DateTime.UtcNow, last, true, 0);
            A(failReview.IntervalDays < 0.1, "incorrect_gets_short_review_interval");
            A(stableReview.IntervalDays >= 3, "stable_skill_gets_multi_day_review");

            var classifier = new MathErrorClassifierV1();
            var carryQuestion = new MathQuestion { TemplateId = "add_within_1000_one_carry", CorrectAnswer = 85 };
            A(classifier.Classify(carryQuestion, 75).ErrorType == "CARRY_MISSING", "carry_missing_pattern_detected");
            var factQuestion = new MathQuestion { TemplateId = "times_table_2", CorrectAnswer = 12 };
            A(classifier.Classify(factQuestion, 10).ErrorType == "FACT_ERROR", "fact_error_classified");
            A(classifier.Classify(factQuestion, 12) == null, "correct_answer_has_no_error_event");
            return refs;
        }

        private static void TestDatabaseVerticalSlice(string temp, string schemaPath, IList<MathTemplateRef> refs)
        {
            var schemaDir = Path.Combine(temp, "schema");
            Directory.CreateDirectory(schemaDir);
            File.Copy(schemaPath, Path.Combine(schemaDir, "001_initial.sql"), true);
            File.Copy(Path.Combine(Path.GetDirectoryName(schemaPath), "002_attempt_immutability.sql"), Path.Combine(schemaDir, "002_attempt_immutability.sql"), true);
            var database = new LearningDatabase(Path.Combine(temp, "learning.db"), Path.Combine(schemaDir, "001_initial.sql"));
            var init = database.Initialize("DELETE");
            A(init.Health.IsHealthy && init.SchemaVersion == 2, "vertical_slice_db_ready_v2");

            var sessions = new LearnerSessionService(database);
            var profile = sessions.EnsurePrimaryChild("Bé thử");
            var profile2 = sessions.EnsurePrimaryChild("Tên khác không ghi đè");
            A(profile.ChildId == LearnerSessionService.PrimaryChildId && profile2.ChildId == profile.ChildId, "primary_child_idempotent");
            A(profile.DisplayName == profile2.DisplayName, "primary_child_name_not_silently_overwritten");

            var dangling = sessions.BeginSession(profile.ChildId, "math", "LOW");
            A(sessions.RecoverDanglingSessions() == 1, "dangling_session_recovered_before_new_session");
            A(ReadSessionState(database, dangling.SessionId) == "recovered", "dangling_session_state_recovered");

            var session = sessions.BeginSession(profile.ChildId, "math", "LOW");
            var answerCommit = new AnswerCommitService(database);
            var behaviorAudit = new BehaviorDecisionAuditService(database);
            var adaptiveAudit = new AdaptiveDecisionAuditService(database);
            var behavior = new BehaviorController();
            var selector = new AdaptiveMathSelector();
            var generator = new MathQuestionGenerator(777);
            var masteryEngine = new MasteryEngineV1();
            var scheduler = new ReviewSchedulerV1();
            var errorClassifier = new MathErrorClassifierV1();
            var skills = sessions.LoadSkillSnapshots(profile.ChildId, "math");
            var recentTemplates = new List<string>();
            var recentSkills = new List<string>();
            BehaviorDecision lastBehavior = null;
            var correctCount = 0;

            for (var i = 0; i < 6; i++)
            {
                var selection = selector.Select(refs, skills, DateTime.UtcNow, recentTemplates, recentSkills);
                var question = generator.Generate(selection);
                adaptiveAudit.Record(new AdaptiveDecisionAuditRequest
                {
                    Id = "adaptive-" + Guid.NewGuid().ToString("N"), SessionId = session.SessionId, ChildId = profile.ChildId,
                    PackId = "math_grade2_verified_templates_v1", PackVersion = "1.0.0", Question = question, Selection = selection,
                    Behavior = lastBehavior, CreatedAtUtc = DateTime.UtcNow
                });

                var hintLevel = i == 2 ? 1 : 0;
                var answer = i == 1 ? FirstWrongChoice(question) : question.CorrectAnswer;
                var isCorrect = answer == question.CorrectAnswer;
                if (isCorrect) correctCount++;
                SkillSnapshot current;
                if (!skills.TryGetValue(question.SkillId, out current))
                    current = new SkillSnapshot { SkillId = question.SkillId, MasteryScore = 0.25, Confidence = 0.20, LearningState = "NEW" };
                var error = errorClassifier.Classify(question, answer);
                var answered = DateTime.UtcNow;
                var responseMs = 1100 + i * 170;
                var decision = behavior.Observe(new BehaviorObservation
                {
                    SkillId = question.SkillId, IsCorrect = isCorrect, ResponseMs = responseMs, HintLevel = hintLevel,
                    UsedMaxHint = hintLevel >= 2, RapidWrong = !isCorrect && responseMs < 600, SkippedOrExited = false,
                    InputMiss = false, ErrorType = error == null ? null : error.ErrorType, Representation = question.Representation,
                    MasteryScore = current.MasteryScore, SessionElapsedMinutes = Math.Max(0, (answered - session.StartedAtUtc).TotalMinutes)
                });
                var mastery = masteryEngine.Evaluate(current, isCorrect, hintLevel, false, decision.ProtectMasteryFromNegativeUpdate);
                var review = scheduler.Schedule(answered, mastery, isCorrect, hintLevel);
                var attemptId = "attempt-" + Guid.NewGuid().ToString("N");
                answerCommit.Commit(new AnswerCommitRequest
                {
                    AttemptId = attemptId, SessionId = session.SessionId, ChildId = profile.ChildId,
                    PackId = "math_grade2_verified_templates_v1", PackVersion = "1.0.0", QuestionId = question.QuestionId,
                    SkillId = question.SkillId, Subject = "math", StartedAtUtc = answered.AddMilliseconds(-responseMs), AnsweredAtUtc = answered,
                    AnswerJson = Json.Serialize(new Dictionary<string, object> { { "answer", answer } }), IsCorrect = isCorrect,
                    ResponseMs = responseMs, HintLevel = hintLevel, Representation = question.Representation, InputMethod = "mouse",
                    AttemptIndex = 1, ListenCount = 0,
                    Error = error == null ? null : new ErrorEventWrite
                    {
                        Id = "error-" + Guid.NewGuid().ToString("N"), ErrorType = error.ErrorType, Confidence = error.Confidence,
                        EvidenceJson = Json.Serialize(error.Evidence), ClassifierVersion = MathErrorClassifierV1.Version
                    },
                    Mastery = new MasteryEventWrite
                    {
                        Id = "mastery-" + Guid.NewGuid().ToString("N"), EventType = mastery.EventType, Delta = mastery.Delta,
                        ScoreBefore = mastery.ScoreBefore, ScoreAfter = mastery.ScoreAfter, ConfidenceAfter = mastery.ConfidenceAfter,
                        ReasonJson = Json.Serialize(mastery.Reasons), MasteryEngineVersion = MasteryEngineV1.Version
                    },
                    ChildSkill = new ChildSkillWrite
                    {
                        MasteryScore = mastery.ScoreAfter, Confidence = mastery.ConfidenceAfter, AttemptsCount = mastery.AttemptsCount,
                        IndependentSuccessCount = mastery.IndependentSuccessCount, HintedSuccessCount = mastery.HintedSuccessCount,
                        TransferSuccessCount = mastery.TransferSuccessCount, LastSeenAtUtc = answered,
                        LastSuccessAtUtc = isCorrect ? (DateTime?)answered : current.LastSuccessAtUtc, NextReviewAtUtc = review.DueAtUtc,
                        LearningState = mastery.LearningState, MasteryEngineVersion = MasteryEngineV1.Version
                    },
                    Review = new ReviewScheduleWrite { DueAtUtc = review.DueAtUtc, IntervalDays = review.IntervalDays, Reason = review.Reason, SchedulerVersion = ReviewSchedulerV1.Version }
                });
                behaviorAudit.Record(new BehaviorDecisionAuditRequest
                {
                    Id = "behavior-" + Guid.NewGuid().ToString("N"), SessionId = session.SessionId, ChildId = profile.ChildId,
                    AttemptId = attemptId, Decision = decision, ControllerVersion = "behavior-v1", CreatedAtUtc = answered
                });

                skills[question.SkillId] = Apply(current, mastery, answered, review.DueAtUtc, isCorrect);
                AddRecent(recentTemplates, question.TemplateId);
                AddRecent(recentSkills, question.SkillId);
                lastBehavior = decision;
            }

            sessions.CompleteSession(session.SessionId, false,
                Json.Serialize(new Dictionary<string, object> { { "attempts", 6 }, { "correct", correctCount }, { "subject", "math" } }),
                Json.Serialize(new Dictionary<string, object> { { "final_state", lastBehavior == null ? "READY" : lastBehavior.State.ToString() } }));

            A(ReadSessionState(database, session.SessionId) == "completed", "math_vertical_session_completed");
            A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + session.SessionId + "';") == 6, "six_attempts_committed");
            A(Count(database, "SELECT count(*) FROM adaptive_decision_event WHERE session_id='" + session.SessionId + "';") == 6, "six_adaptive_decisions_audited");
            A(Count(database, "SELECT count(*) FROM behavior_state_event WHERE session_id='" + session.SessionId + "';") == 6, "six_behavior_decisions_audited");
            A(Count(database, "SELECT count(*) FROM error_event;") == 1, "one_wrong_answer_writes_one_error_event");
            A(Count(database, "SELECT count(*) FROM mastery_event;") == 6, "every_attempt_writes_mastery_event");
            A(Count(database, "SELECT count(*) FROM review_schedule;") > 0, "review_schedule_materialized");
            var parent = ParentSummaryService.Read(database);
            A(parent.SessionCount == 2 && parent.AttemptCount == 6, "parent_summary_sees_recovered_and_completed_sessions");
            A(parent.SkillCount > 0, "parent_summary_sees_skill_state");
            A(correctCount == 5, "vertical_slice_fixture_correctness_expected");
        }

        private static void TestCoordinator(string tempRoot, string schemaPath, string templatePath)
        {
            var root = Path.Combine(tempRoot, "coordinator");
            var schemaDir = Path.Combine(root, "schema");
            Directory.CreateDirectory(schemaDir);
            File.Copy(schemaPath, Path.Combine(schemaDir, "001_initial.sql"), true);
            File.Copy(Path.Combine(Path.GetDirectoryName(schemaPath), "002_attempt_immutability.sql"), Path.Combine(schemaDir, "002_attempt_immutability.sql"), true);
            var database = new LearningDatabase(Path.Combine(root, "learning.db"), Path.Combine(schemaDir, "001_initial.sql"));
            database.Initialize("DELETE");

            using (var coordinator = new MathSessionCoordinator(database, templatePath, "LOW", 4242, 4))
            {
                var started = coordinator.Start("Bé coordinator");
                A(coordinator.IsActive, "coordinator_active_after_start");
                A(started.TargetQuestionCount == 4, "coordinator_target_question_count");
                A(started.RecoveredDanglingSessions == 0, "coordinator_clean_start_no_dangling_session");
                for (var i = 0; i < 4; i++)
                {
                    var question = coordinator.NextQuestion();
                    A(question != null && question.Choices.Count == 4, "coordinator_question_" + i);
                    var answer = i == 1 ? FirstWrongChoice(question) : question.CorrectAnswer;
                    var outcome = coordinator.SubmitAnswerAt(answer, 0, "smoke", DateTime.UtcNow, 1200 + i * 100);
                    A(outcome.CompletedQuestionCount == i + 1, "coordinator_progress_" + i);
                    A(outcome.IsCorrect == (i != 1), "coordinator_correctness_" + i);
                }
                A(coordinator.NextQuestion() == null, "coordinator_stops_at_target_count");
                var summary = coordinator.Complete();
                A(summary.Attempts == 4, "coordinator_summary_attempts");
                A(summary.Correct == 3 && summary.Wrong == 1, "coordinator_summary_correct_wrong");
                A(!coordinator.IsActive, "coordinator_inactive_after_complete");
            }
            var parent = ParentSummaryService.Read(database);
            A(parent.SessionCount == 1 && parent.AttemptCount == 4, "coordinator_persists_parent_summary");
            A(Count(database, "SELECT count(*) FROM adaptive_decision_event;") == 4, "coordinator_persists_adaptive_audit");
        }

        private static SkillSnapshot Apply(SkillSnapshot current, MasteryUpdate update, DateTime now, DateTime? due = null, bool success = true)
        {
            return new SkillSnapshot
            {
                SkillId = current.SkillId, MasteryScore = update.ScoreAfter, Confidence = update.ConfidenceAfter,
                AttemptsCount = update.AttemptsCount, IndependentSuccessCount = update.IndependentSuccessCount,
                HintedSuccessCount = update.HintedSuccessCount, TransferSuccessCount = update.TransferSuccessCount,
                LastSeenAtUtc = now, LastSuccessAtUtc = success ? (DateTime?)now : current.LastSuccessAtUtc,
                NextReviewAtUtc = due, LearningState = update.LearningState
            };
        }

        private static int FirstWrongChoice(MathQuestion q) { return q.Choices.First(x => x != q.CorrectAnswer); }
        private static void AddRecent(IList<string> list, string value) { list.Add(value); while (list.Count > 4) list.RemoveAt(0); }

        private static int ParseA(MathQuestion q) { return ParseBinary(q)[0]; }
        private static int ParseB(MathQuestion q) { return ParseBinary(q)[1]; }
        private static int[] ParseBinary(MathQuestion q)
        {
            var text = q.PromptVi.Replace("Tính: ", string.Empty).Replace(" = ?", string.Empty);
            var parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return new[] { int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[2], CultureInfo.InvariantCulture) };
        }

        private static int CarryCount(int a, int b)
        {
            var carry = 0; var count = 0;
            while (a > 0 || b > 0 || carry > 0)
            {
                var sum = a % 10 + b % 10 + carry;
                carry = sum >= 10 ? 1 : 0;
                if (carry > 0) count++;
                a /= 10; b /= 10;
            }
            return count;
        }

        private static int BorrowCount(int a, int b)
        {
            var borrow = 0; var count = 0;
            for (var i = 0; i < 4; i++)
            {
                var da = a % 10 - borrow; var db = b % 10;
                if (da < db) { borrow = 1; count++; } else borrow = 0;
                a /= 10; b /= 10;
            }
            return count;
        }

        private static string ReadSessionState(LearningDatabase database, string id)
        {
            using (var c = database.OpenConnection()) using (var cmd = c.CreateCommand())
            { cmd.CommandText = "SELECT state FROM session WHERE id=@id;"; cmd.Parameters.AddWithValue("@id", id); return Convert.ToString(cmd.ExecuteScalar(), CultureInfo.InvariantCulture); }
        }

        private static int Count(LearningDatabase database, string sql)
        {
            using (var c = database.OpenConnection()) using (var cmd = c.CreateCommand())
            { cmd.CommandText = sql; return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture); }
        }

        private static void A(bool ok, string name)
        {
            if (!ok) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }
    }
}
