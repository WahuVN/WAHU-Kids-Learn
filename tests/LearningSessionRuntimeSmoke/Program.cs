using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
            A(descriptors.Count == 53, "verified_template_source_flattens_all_verified_variants");
            A(descriptors.All(x => x.Status == "VERIFIED_A_TEMPLATE"), "template_source_filters_verified_a_only");
            var refs = descriptors.Select(x => new MathTemplateRef
            {
                TemplateId = x.Id,
                SkillId = x.SkillId,
                SourceTemplateId = x.SourceTemplateId,
                FixedContextVi = x.FixedContextVi,
                StatementVi = x.StatementVi,
                AnswerText = x.AnswerText
            }).Where(AdaptiveMathSelector.IsSupported).ToList();
            A(refs.Count == 53, "generator_supports_all_fifty_three_verified_runtime_candidates");
            var chanceRefs = refs.Where(x => x.TemplateId.StartsWith("possible_certain_impossible_die__", StringComparison.Ordinal)).ToList();
            A(chanceRefs.Count == 3, "compound_probability_template_flattens_three_variants");
            A(chanceRefs.All(x => x.SourceTemplateId == "possible_certain_impossible_die" &&
                                  !string.IsNullOrWhiteSpace(x.FixedContextVi) && !string.IsNullOrWhiteSpace(x.StatementVi) && !string.IsNullOrWhiteSpace(x.AnswerText)),
                "compound_probability_variant_metadata_preserved");
            A(new HashSet<string>(chanceRefs.Select(x => x.AnswerText), StringComparer.Ordinal).SetEquals(new[] { "có thể", "chắc chắn", "không thể" }),
                "compound_probability_verified_answers_preserved");
            var clockRefs = refs.Where(x => x.TemplateId == "clock_read_minute_hand_3_or_6").ToList();
            A(clockRefs.Count == 1 && clockRefs[0].SkillId == "CLOCK_MINUTE_HAND_AT_3_OR_6", "clock_verified_candidate_loaded_once");
            var geometryRefs = refs.Where(x => x.TemplateId.StartsWith("geometry_identify_basic__", StringComparison.Ordinal)).ToList();
            A(geometryRefs.Count == 9 && geometryRefs.Select(x => x.SkillId).Distinct(StringComparer.Ordinal).Count() == 9,
                "geometry_compound_template_flattens_nine_unique_skills");
            var pictographRefs = refs.Where(x => x.TemplateId.StartsWith("pictograph_animals_legend1__", StringComparison.Ordinal)).ToList();
            A(pictographRefs.Count == 5, "pictograph_compound_template_flattens_five_questions");
            A(pictographRefs.Select(x => x.TemplateId).Distinct(StringComparer.Ordinal).Count() == 5,
                "pictograph_duplicate_skill_variants_receive_unique_ids");
            A(pictographRefs.All(x => x.SourceTemplateId == "pictograph_animals_legend1" &&
                                     !string.IsNullOrWhiteSpace(x.StatementVi) && !string.IsNullOrWhiteSpace(x.AnswerText)),
                "pictograph_variant_provenance_preserved");
            var wordRefs = refs.Where(x => x.TemplateId.StartsWith("word_problem_", StringComparison.Ordinal)).ToList();
            A(wordRefs.Count == 7, "word_problem_pack_has_six_result_relations_plus_operation_selection");
            A(wordRefs.Select(x => x.SkillId).Distinct(StringComparer.Ordinal).Count() == 7,
                "word_problem_runtime_candidates_map_to_seven_distinct_skills");
            A(new HashSet<string>(wordRefs.Select(x => x.TemplateId), StringComparer.Ordinal).SetEquals(new[]
            {
                "word_problem_add_more", "word_problem_sub_less", "word_problem_more_than", "word_problem_less_than",
                "word_problem_multiply_groups_2_5", "word_problem_divide_groups_2_5", "word_problem_select_operation_one_step"
            }), "word_problem_expected_template_ids_present");
            var operationConceptRefs = refs.Where(x => new[]
            {
                "add_components_recognize", "sub_components_recognize", "multiplication_meaning_groups", "division_meaning_share",
                "multiplication_components_recognize", "division_components_recognize", "operation_meaning_from_visual",
                "word_problem_select_operation_one_step"
            }.Contains(x.TemplateId)).ToList();
            A(operationConceptRefs.Count == 8, "operation_concept_pack_has_eight_verified_candidates");
            A(operationConceptRefs.Select(x => x.SkillId).Distinct(StringComparer.Ordinal).Count() == 8,
                "operation_concept_candidates_map_to_eight_distinct_skills");

            var selector = new AdaptiveMathSelector();
            var empty = new Dictionary<string, SkillSnapshot>(StringComparer.Ordinal);
            var first = selector.Select(refs, empty, new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc), new string[0], new string[0]);
            A(first != null && first.Template != null, "selector_returns_candidate");
            A(first.DifficultyFit >= 0 && first.DifficultyFit <= 1, "selector_difficulty_fit_bounded");
            A(first.CandidateSummary.Count == 53, "selector_audits_all_candidates");

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
                A(q.Representation == ExpectedRepresentation(r.TemplateId), "representation_matches_instruction_visual_" + r.TemplateId);
                A(q.DisplayChoices.Count >= 2 && q.DisplayChoices.Count <= 4, "choice_count_2_to_4_" + r.TemplateId);
                A(q.DisplayChoices.Distinct(StringComparer.Ordinal).Count() == q.DisplayChoices.Count, "unique_display_choices_" + r.TemplateId);
                A(q.DisplayChoices.Contains(q.CorrectAnswerDisplay), "correct_choice_present_" + r.TemplateId);
                A(q.IsCorrectAnswer(q.CorrectAnswerDisplay), "correct_answer_roundtrip_" + r.TemplateId);
                if (r.TemplateId == "add_within_1000_no_carry") A(CarryCount(ParseA(q), ParseB(q)) == 0, "add_no_carry_constraint");
                if (r.TemplateId == "add_within_1000_one_carry") A(CarryCount(ParseA(q), ParseB(q)) == 1, "add_one_carry_constraint");
                if (r.TemplateId == "subtract_within_1000_no_borrow") A(BorrowCount(ParseA(q), ParseB(q)) == 0, "sub_no_borrow_constraint");
                if (r.TemplateId == "subtract_within_1000_one_borrow") A(BorrowCount(ParseA(q), ParseB(q)) == 1, "sub_one_borrow_constraint");
                if (r.TemplateId == "compare_two_numbers_1000") A(q.UsesTextChoices && q.DisplayChoices.Count == 2, "comparison_uses_two_text_choices");
                if (r.TemplateId == "place_value_decompose_3digit" || r.TemplateId == "expanded_form_3digit" || r.TemplateId == "predecessor_successor")
                    A(q.UsesTextChoices && q.DisplayChoices.Count == 4, "structured_number_question_uses_four_text_choices_" + r.TemplateId);
                if (r.TemplateId == "divide_table_2_exact" || r.TemplateId == "divide_table_5_exact")
                    A(!q.UsesTextChoices && q.CorrectAnswer >= 1 && q.CorrectAnswer <= 10, "division_exact_answer_range_" + r.TemplateId);
                if (r.TemplateId == "polyline_length") A(!q.UsesTextChoices && q.CorrectAnswer >= 3 && q.CorrectAnswer <= 60, "polyline_sum_range");
                if (r.TemplateId.StartsWith("possible_certain_impossible_die__", StringComparison.Ordinal))
                    A(q.UsesTextChoices && q.DisplayChoices.Count == 3 &&
                      new HashSet<string>(q.DisplayChoices, StringComparer.Ordinal).SetEquals(new[] { "có thể", "chắc chắn", "không thể" }),
                      "probability_variant_uses_three_verified_classification_choices_" + r.SkillId);
                if (r.TemplateId == "clock_read_minute_hand_3_or_6")
                    A(q.UsesTextChoices && q.DisplayChoices.Count == 4 && (q.IllustrationData ?? string.Empty).StartsWith("clock|", StringComparison.Ordinal) &&
                      !q.PromptVi.Any(char.IsDigit), "clock_uses_hidden_visual_data_without_prompt_answer_leak");
                if (r.TemplateId.StartsWith("geometry_identify_basic__", StringComparison.Ordinal))
                    A(q.UsesTextChoices && q.DisplayChoices.Count >= 3 && q.IllustrationData == "geometry|" + r.SkillId,
                      "geometry_variant_has_skill_bound_visual_data_" + r.SkillId);
                if (r.TemplateId.StartsWith("pictograph_animals_legend1__", StringComparison.Ordinal))
                    A(q.UsesTextChoices && q.DisplayChoices.Count >= 3 && (q.IllustrationData ?? string.Empty).StartsWith("pictograph|", StringComparison.Ordinal) &&
                      !q.PromptVi.Contains("3 mèo") && !q.PromptVi.Contains("2 chó") && !q.PromptVi.Contains("4 thỏ"),
                      "pictograph_visual_data_not_leaked_into_prompt_" + r.TemplateId);
                if (IsOperationConceptTemplate(r.TemplateId))
                    A(ValidateOperationConceptContract(q), "operation_concept_contract_" + r.TemplateId);
                if (IsNumberExtensionTemplate(r.TemplateId))
                    A(ValidateNumberExtensionContract(q), "number_extension_contract_" + r.TemplateId);
                if (r.TemplateId.StartsWith("word_problem_", StringComparison.Ordinal) && r.TemplateId != "word_problem_select_operation_one_step")
                    A(ValidateWordProblemContract(q), "word_problem_relation_contract_" + r.TemplateId);
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
            var carryQuestion = new MathQuestion { TemplateId = "add_within_1000_one_carry", CorrectAnswer = 85, AnswerKind = "integer", CorrectAnswerText = "85" };
            A(classifier.Classify(carryQuestion, 75).ErrorType == "CARRY_MISSING", "carry_missing_pattern_detected");
            var factQuestion = new MathQuestion { TemplateId = "times_table_2", CorrectAnswer = 12, AnswerKind = "integer", CorrectAnswerText = "12" };
            A(classifier.Classify(factQuestion, 10).ErrorType == "FACT_ERROR", "fact_error_classified");
            A(classifier.Classify(factQuestion, 12) == null, "correct_answer_has_no_error_event");
            var compareQuestion = TextQuestion("compare_two_numbers_1000", ">", new[] { ">", "<" });
            A(classifier.Classify(compareQuestion, "<").ErrorType == "COMPARISON_ERROR", "comparison_error_classified");
            var placeQuestion = TextQuestion("place_value_decompose_3digit", "4 trăm, 7 chục, 2 đơn vị", new[] { "4 trăm, 7 chục, 2 đơn vị", "4 trăm, 2 chục, 7 đơn vị" });
            A(classifier.Classify(placeQuestion, "4 trăm, 2 chục, 7 đơn vị").ErrorType == "PLACE_VALUE_ERROR", "place_value_error_classified");
            var neighborQuestion = TextQuestion("predecessor_successor", "471 và 473", new[] { "471 và 473", "470 và 473" });
            A(classifier.Classify(neighborQuestion, "470 và 473").ErrorType == "SEQUENCE_NEIGHBOR_ERROR", "neighbor_error_classified");
            var divideQuestion = new MathQuestion { TemplateId = "divide_table_5_exact", CorrectAnswer = 7, AnswerKind = "integer", CorrectAnswerText = "7" };
            A(classifier.Classify(divideQuestion, "6").ErrorType == "FACT_ERROR", "division_fact_error_classified");
            var polylineQuestion = new MathQuestion { TemplateId = "polyline_length", CorrectAnswer = 25, AnswerKind = "integer", CorrectAnswerText = "25" };
            A(classifier.Classify(polylineQuestion, "24").ErrorType == "MEASUREMENT_SUM_ERROR", "polyline_sum_error_classified");
            var eventQuestion = TextQuestion("possible_certain_impossible_die__event_possible", "có thể", new[] { "có thể", "chắc chắn", "không thể" });
            A(classifier.Classify(eventQuestion, "không thể").ErrorType == "EVENT_CLASSIFICATION_ERROR", "event_classification_error_classified");
            A(classifier.Classify(eventQuestion, "có thể") == null, "correct_event_classification_has_no_error");
            var clockQuestion = TextQuestion("clock_read_minute_hand_3_or_6", "3 giờ 30 phút", new[] { "3 giờ 30 phút", "3 giờ 15 phút" });
            A(classifier.Classify(clockQuestion, "3 giờ 15 phút").ErrorType == "TIME_READ_ERROR", "clock_read_error_classified");
            var geometryQuestion = TextQuestion("geometry_identify_basic__quadrilateral_recognize", "hình tứ giác", new[] { "hình tứ giác", "đường gấp khúc" });
            A(classifier.Classify(geometryQuestion, "đường gấp khúc").ErrorType == "GEOMETRY_RECOGNITION_ERROR", "geometry_error_classified");
            var pictographQuestion = TextQuestion("pictograph_animals_legend1__pictograph_read_describe", "3", new[] { "2", "3", "4" });
            A(classifier.Classify(pictographQuestion, "2").ErrorType == "PICTOGRAPH_READ_ERROR", "pictograph_error_classified");
            var wordQuestion = new MathQuestion { TemplateId = "word_problem_add_more", CorrectAnswer = 17, AnswerKind = "integer", CorrectAnswerText = "17" };
            A(classifier.Classify(wordQuestion, "16").ErrorType == "WORD_PROBLEM_RELATION_ERROR", "word_problem_relation_error_classified");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "word_problem_add_more" }) == "mental_add_within_20",
                "word_problem_add_repairs_to_mental_add");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "word_problem_less_than" }) == "mental_sub_within_20",
                "word_problem_less_repairs_to_mental_sub");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "word_problem_multiply_groups_2_5", IllustrationData = "wordgroups|5|4" }) == "times_table_5",
                "word_problem_multiply_5_repairs_to_table_5");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "word_problem_multiply_groups_2_5", IllustrationData = "wordgroups|2|4" }) == "times_table_2",
                "word_problem_multiply_2_repairs_to_table_2");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "word_problem_divide_groups_2_5", IllustrationData = "wordshare|20|5" }) == "times_table_5",
                "word_problem_divide_5_repairs_to_table_5");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "word_problem_divide_groups_2_5", IllustrationData = "wordshare|12|2" }) == "times_table_2",
                "word_problem_divide_2_repairs_to_table_2");
            var componentQuestion = TextQuestion("add_components_recognize", "số hạng", new[] { "số hạng", "tổng", "hiệu" });
            A(classifier.Classify(componentQuestion, "tổng").ErrorType == "OPERATION_COMPONENT_ERROR", "operation_component_error_classified");
            var meaningQuestion = TextQuestion("operation_meaning_from_visual", "nhân", new[] { "cộng", "trừ", "nhân", "chia" });
            A(classifier.Classify(meaningQuestion, "cộng").ErrorType == "OPERATION_MEANING_ERROR", "operation_meaning_error_classified");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "add_components_recognize" }) == "mental_add_within_20",
                "add_components_repair_to_mental_add");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "sub_components_recognize" }) == "mental_sub_within_20",
                "sub_components_repair_to_mental_sub");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "multiplication_components_recognize", IllustrationData = "equationparts|mul|5|4|20|0" }) == "times_table_5",
                "multiplication_components_factor5_repair_to_table5");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "division_components_recognize", IllustrationData = "equationparts|div|20|5|4|1" }) == "times_table_5",
                "division_components_divisor5_repair_to_table5");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "operation_meaning_from_visual", IllustrationData = "wordbar|sub|30|8" }) == "mental_sub_within_20",
                "operation_visual_sub_repairs_to_mental_sub");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "word_problem_select_operation_one_step", IllustrationData = "wordgroups|5|4" }) == "times_table_5",
                "word_problem_operation_selection_repairs_from_visual_relation");
            var hundredsQuestion = new MathQuestion { TemplateId = "full_hundreds_recognize", CorrectAnswer = 500, AnswerKind = "integer", CorrectAnswerText = "500" };
            A(classifier.Classify(hundredsQuestion, "400").ErrorType == "HUNDREDS_RECOGNITION_ERROR", "hundreds_recognition_error_classified");
            var lineQuestion = new MathQuestion { TemplateId = "number_ray_fill_1000", CorrectAnswer = 340, AnswerKind = "integer", CorrectAnswerText = "340" };
            A(classifier.Classify(lineQuestion, "330").ErrorType == "NUMBER_SEQUENCE_ERROR", "number_line_sequence_error_classified");
            var minMaxQuestion = new MathQuestion { TemplateId = "min_max_up_to_4", CorrectAnswer = 901, AnswerKind = "integer", CorrectAnswerText = "901" };
            A(classifier.Classify(minMaxQuestion, "810").ErrorType == "NUMBER_ORDER_ERROR", "minmax_number_order_error_classified");
            var sortQuestion = TextQuestion("sort_up_to_4", "12 < 54 < 210 < 700", new[] { "12 < 54 < 210 < 700", "54 < 12 < 210 < 700" });
            A(classifier.Classify(sortQuestion, "54 < 12 < 210 < 700").ErrorType == "NUMBER_ORDER_ERROR", "sort_number_order_error_classified");
            var twoStepQuestion = new MathQuestion { TemplateId = "add_sub_two_operators_left_to_right", CorrectAnswer = 45, AnswerKind = "integer", CorrectAnswerText = "45" };
            A(classifier.Classify(twoStepQuestion, "40").ErrorType == "TWO_STEP_CALCULATION_ERROR", "two_step_error_classified");
            var roundQuestion = new MathQuestion { TemplateId = "mental_round_tens_hundreds_1000", CorrectAnswer = 700, AnswerKind = "integer", CorrectAnswerText = "700" };
            A(classifier.Classify(roundQuestion, "600").ErrorType == "ROUND_NUMBER_FACT_ERROR", "round_number_fact_error_classified");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "full_hundreds_recognize" }) == "place_value_decompose_3digit", "hundreds_repairs_to_place_value");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "number_ray_fill_1000" }) == "predecessor_successor", "number_line_repairs_to_neighbor_skill");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "min_max_up_to_4" }) == "compare_two_numbers_1000", "minmax_repairs_to_compare");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "sort_up_to_4" }) == "compare_two_numbers_1000", "sort_repairs_to_compare");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "add_sub_two_operators_left_to_right" }) == "mental_add_within_20", "two_step_repairs_to_mental_add");
            A(RepairTemplateForSmoke(new MathQuestion { TemplateId = "mental_round_tens_hundreds_1000" }) == "mental_add_within_20", "round_number_repairs_to_mental_add");

            TestGeneratorFuzz(refs);
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
                    PackId = "math_grade2_verified_templates_v1", PackVersion = "1.4.0", Question = question, Selection = selection,
                    Behavior = lastBehavior, CreatedAtUtc = DateTime.UtcNow
                });

                var hintLevel = i == 2 ? 1 : 0;
                var answer = i == 1 ? FirstWrongChoice(question) : question.CorrectAnswerDisplay;
                var isCorrect = question.IsCorrectAnswer(answer);
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
                    PackId = "math_grade2_verified_templates_v1", PackVersion = "1.4.0", QuestionId = question.QuestionId,
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
            var roadmap = new MathRoadmapService(database).Read(profile.ChildId);
            A(roadmap.TotalTrackedAttempts == 6, "math_roadmap_tracks_all_supported_vertical_slice_attempts");
            A(RoadmapScoresBounded(roadmap), "math_roadmap_mastery_scores_bounded");
            var writtenAttemptsBeforeWordProblem = roadmap.Written1000.Attempts;
            using (var connection = database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"INSERT OR REPLACE INTO child_skill
(child_id,skill_id,subject,mastery_score,confidence,attempts_count,independent_success_count,hinted_success_count,transfer_success_count,learning_state,mastery_engine_version,updated_at_utc)
VALUES(@child,'WP_ONE_STEP_MORE_THAN','math',0.62,0.55,3,2,1,0,'LEARNING',@engine,@updated);";
                command.Parameters.AddWithValue("@child", profile.ChildId);
                command.Parameters.AddWithValue("@engine", MasteryEngineV1.Version);
                command.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                command.ExecuteNonQuery();
            }
            var roadmapWithWordProblem = new MathRoadmapService(database).Read(profile.ChildId);
            A(roadmapWithWordProblem.Written1000.Attempts == writtenAttemptsBeforeWordProblem + 3,
                "math_roadmap_groups_word_problem_attempts_into_operations_and_problems");
            A(roadmapWithWordProblem.TotalTrackedAttempts == roadmap.TotalTrackedAttempts + 3,
                "math_roadmap_total_includes_word_problem_attempts");
            var writtenBeforeConcepts = roadmapWithWordProblem.Written1000.Attempts;
            var tablesBeforeConcepts = roadmapWithWordProblem.Tables25.Attempts;
            var addComponentAttemptsBefore = ReadSkillAttempts(database, profile.ChildId, "ADD_COMPONENTS_RECOGNIZE");
            var multiplicationComponentAttemptsBefore = ReadSkillAttempts(database, profile.ChildId, "MULTIPLICATION_COMPONENTS");
            using (var connection = database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"INSERT OR REPLACE INTO child_skill
(child_id,skill_id,subject,mastery_score,confidence,attempts_count,independent_success_count,hinted_success_count,transfer_success_count,learning_state,mastery_engine_version,updated_at_utc)
VALUES(@child,'ADD_COMPONENTS_RECOGNIZE','math',0.58,0.50,@addAttempts,1,1,0,'LEARNING',@engine,@updated);
INSERT OR REPLACE INTO child_skill
(child_id,skill_id,subject,mastery_score,confidence,attempts_count,independent_success_count,hinted_success_count,transfer_success_count,learning_state,mastery_engine_version,updated_at_utc)
VALUES(@child,'MULTIPLICATION_COMPONENTS','math',0.64,0.56,@mulAttempts,3,1,0,'LEARNING',@engine,@updated);";
                command.Parameters.AddWithValue("@child", profile.ChildId);
                command.Parameters.AddWithValue("@addAttempts", addComponentAttemptsBefore + 2);
                command.Parameters.AddWithValue("@mulAttempts", multiplicationComponentAttemptsBefore + 4);
                command.Parameters.AddWithValue("@engine", MasteryEngineV1.Version);
                command.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                command.ExecuteNonQuery();
            }
            var roadmapWithConcepts = new MathRoadmapService(database).Read(profile.ChildId);
            A(roadmapWithConcepts.Written1000.Attempts == writtenBeforeConcepts + 2,
                "math_roadmap_groups_add_components_into_operations_and_problems");
            A(roadmapWithConcepts.Tables25.Attempts == tablesBeforeConcepts + 4,
                "math_roadmap_groups_multiplication_components_into_tables");
            A(roadmapWithConcepts.TotalTrackedAttempts == roadmapWithWordProblem.TotalTrackedAttempts + 6,
                "math_roadmap_total_includes_operation_concept_attempts");
            var numberSenseBeforeExtension = roadmapWithConcepts.NumberSense.Attempts;
            var writtenBeforeExtension = roadmapWithConcepts.Written1000.Attempts;
            var hundredsAttemptsBefore = ReadSkillAttempts(database, profile.ChildId, "NUM_FULL_HUNDREDS_RECOGNIZE");
            var roundedAttemptsBefore = ReadSkillAttempts(database, profile.ChildId, "MENTAL_ADD_SUB_ROUND_TENS_HUNDREDS_1000");
            using (var connection = database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"INSERT OR REPLACE INTO child_skill
(child_id,skill_id,subject,mastery_score,confidence,attempts_count,independent_success_count,hinted_success_count,transfer_success_count,learning_state,mastery_engine_version,updated_at_utc)
VALUES(@child,'NUM_FULL_HUNDREDS_RECOGNIZE','math',0.61,0.55,@hundredsAttempts,2,0,0,'LEARNING',@engine,@updated);
INSERT OR REPLACE INTO child_skill
(child_id,skill_id,subject,mastery_score,confidence,attempts_count,independent_success_count,hinted_success_count,transfer_success_count,learning_state,mastery_engine_version,updated_at_utc)
VALUES(@child,'MENTAL_ADD_SUB_ROUND_TENS_HUNDREDS_1000','math',0.59,0.52,@roundAttempts,3,1,0,'LEARNING',@engine,@updated);";
                command.Parameters.AddWithValue("@child", profile.ChildId);
                command.Parameters.AddWithValue("@hundredsAttempts", hundredsAttemptsBefore + 2);
                command.Parameters.AddWithValue("@roundAttempts", roundedAttemptsBefore + 4);
                command.Parameters.AddWithValue("@engine", MasteryEngineV1.Version);
                command.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                command.ExecuteNonQuery();
            }
            var roadmapWithExtension = new MathRoadmapService(database).Read(profile.ChildId);
            A(roadmapWithExtension.NumberSense.Attempts == numberSenseBeforeExtension + 2,
                "math_roadmap_groups_full_hundreds_into_number_sense");
            A(roadmapWithExtension.Written1000.Attempts == writtenBeforeExtension + 4,
                "math_roadmap_groups_round_mental_into_operations");
            A(roadmapWithExtension.TotalTrackedAttempts == roadmapWithConcepts.TotalTrackedAttempts + 6,
                "math_roadmap_total_includes_number_extension_attempts");
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
                    A(question != null && question.DisplayChoices.Count >= 2 && question.DisplayChoices.Count <= 4, "coordinator_question_" + i);
                    var answer = i == 1 ? FirstWrongChoice(question) : question.CorrectAnswerDisplay;
                    var outcome = coordinator.SubmitAnswerAt(answer, 0, "smoke", DateTime.UtcNow, 1200 + i * 100);
                    A(outcome.CompletedQuestionCount == i + 1, "coordinator_progress_" + i);
                    A(outcome.IsCorrect == (i != 1), "coordinator_correctness_" + i);
                }
                A(coordinator.NextQuestion() == null, "coordinator_stops_at_target_count");
                var summary = coordinator.Complete();
                A(summary.Attempts == 4, "coordinator_summary_attempts");
                A(summary.Correct == 3 && summary.Wrong == 1, "coordinator_summary_correct_wrong");
                A(summary.GardenGrowthSteps == 1, "completed_session_grants_one_garden_growth_step");
                A(!string.IsNullOrWhiteSpace(summary.GardenUnlockMessage), "first_completed_session_unlocks_seedling_message");
                A(summary.GardenUnlockedItemIds != null && summary.GardenUnlockedItemIds.Contains("garden_seedling"), "completion_exposes_exact_unlocked_garden_item");
                A(summary.SessionsUntilNextGardenMilestone == 2 && summary.NextGardenMilestoneItemId == "garden_flower_patch", "completion_exposes_predictable_next_garden_milestone");
                A(!coordinator.IsActive, "coordinator_inactive_after_complete");
                var world = new GameWorldRewardService(database);
                var progress = world.ReadProgress(started.ChildId);
                A(progress.GrowthSteps == 1 && progress.UnlockedItems.Contains("garden_seedling"), "garden_progress_persisted_after_complete");
                var replay = world.GrantCompletedMathSession(started.ChildId, started.SessionId, 4);
                A(!replay.RewardCreated && replay.GrowthSteps == 1, "garden_reward_idempotent_per_completed_session");
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

        private static void TestGeneratorFuzz(IList<MathTemplateRef> refs)
        {
            foreach (var template in refs)
            {
                for (var seed = 0; seed < 50; seed++)
                {
                    var generator = new MathQuestionGenerator(100000 + seed * 97 + template.TemplateId.GetHashCode());
                    var question = generator.Generate(new MathSelectionDecision
                    {
                        Template = template,
                        DifficultyFit = 0.8,
                        Reasons = new[] { "fuzz" },
                        CandidateSummary = new[] { template.TemplateId }
                    });
                    RequireGeneratedContract(question);
                }
                A(true, "generator_fuzz_50_seeds_" + template.TemplateId);
            }
        }

        private static void RequireGeneratedContract(MathQuestion q)
        {
            if (q == null || string.IsNullOrWhiteSpace(q.QuestionId) || string.IsNullOrWhiteSpace(q.PromptVi))
                throw new Exception("FUZZ_FAIL missing identity/prompt");
            if (q.DisplayChoices == null || q.DisplayChoices.Count < 2 || q.DisplayChoices.Count > 4)
                throw new Exception("FUZZ_FAIL invalid choice count: " + q.TemplateId);
            if (q.DisplayChoices.Distinct(StringComparer.Ordinal).Count() != q.DisplayChoices.Count)
                throw new Exception("FUZZ_FAIL duplicate choices: " + q.TemplateId);
            if (!q.DisplayChoices.Contains(q.CorrectAnswerDisplay) || !q.IsCorrectAnswer(q.CorrectAnswerDisplay))
                throw new Exception("FUZZ_FAIL missing correct answer: " + q.TemplateId);
            if (q.Representation != ExpectedRepresentation(q.TemplateId))
                throw new Exception("FUZZ_FAIL representation mismatch: " + q.TemplateId);

            if (q.TemplateId == "add_within_1000_no_carry" && CarryCount(ParseA(q), ParseB(q)) != 0)
                throw new Exception("FUZZ_FAIL no-carry generator");
            if (q.TemplateId == "add_within_1000_one_carry" && CarryCount(ParseA(q), ParseB(q)) != 1)
                throw new Exception("FUZZ_FAIL one-carry generator");
            if (q.TemplateId == "subtract_within_1000_no_borrow" && BorrowCount(ParseA(q), ParseB(q)) != 0)
                throw new Exception("FUZZ_FAIL no-borrow generator");
            if (q.TemplateId == "subtract_within_1000_one_borrow" && BorrowCount(ParseA(q), ParseB(q)) != 1)
                throw new Exception("FUZZ_FAIL one-borrow generator");
            if (q.TemplateId == "compare_two_numbers_1000" && (!q.UsesTextChoices || q.DisplayChoices.Count != 2))
                throw new Exception("FUZZ_FAIL compare choice contract");
            if (q.TemplateId == "predecessor_successor")
            {
                foreach (var choice in q.DisplayChoices)
                    if (ExtractInts(choice).Any(x => x < 0 || x > 1000))
                        throw new Exception("FUZZ_FAIL predecessor/successor distractor outside grade-2 domain");
            }
            if ((q.TemplateId == "place_value_decompose_3digit" || q.TemplateId == "expanded_form_3digit" || q.TemplateId == "predecessor_successor") &&
                (!q.UsesTextChoices || q.DisplayChoices.Count != 4))
                throw new Exception("FUZZ_FAIL structured text contract: " + q.TemplateId);
            if (q.TemplateId == "divide_table_2_exact" || q.TemplateId == "divide_table_5_exact")
            {
                var values = ExtractInts(q.PromptVi);
                if (values.Length < 2 || values[1] * q.CorrectAnswer != values[0])
                    throw new Exception("FUZZ_FAIL exact division: " + q.TemplateId);
            }
            if (q.TemplateId == "polyline_length")
            {
                var values = ExtractInts(q.PromptVi);
                if (values.Length < 3 || values[0] + values[1] + values[2] != q.CorrectAnswer)
                    throw new Exception("FUZZ_FAIL polyline sum");
            }
            if (q.TemplateId.StartsWith("possible_certain_impossible_die__", StringComparison.Ordinal))
            {
                if (!q.UsesTextChoices || q.DisplayChoices.Count != 3 ||
                    !new HashSet<string>(q.DisplayChoices, StringComparer.Ordinal).SetEquals(new[] { "có thể", "chắc chắn", "không thể" }))
                    throw new Exception("FUZZ_FAIL probability choice contract");
            }
            if (q.TemplateId == "clock_read_minute_hand_3_or_6")
            {
                var parts = (q.IllustrationData ?? string.Empty).Split('|');
                int hour, minute;
                if (parts.Length != 3 || parts[0] != "clock" || !int.TryParse(parts[1], out hour) || !int.TryParse(parts[2], out minute) ||
                    hour < 1 || hour > 12 || (minute != 15 && minute != 30) ||
                    q.CorrectAnswerDisplay != hour + " giờ " + minute.ToString("00", CultureInfo.InvariantCulture) + " phút" || q.PromptVi.Any(char.IsDigit))
                    throw new Exception("FUZZ_FAIL clock hidden-data contract");
            }
            if (q.TemplateId.StartsWith("geometry_identify_basic__", StringComparison.Ordinal))
            {
                if (!q.UsesTextChoices || q.DisplayChoices.Count < 3 || q.IllustrationData != "geometry|" + q.SkillId)
                    throw new Exception("FUZZ_FAIL geometry visual contract");
            }
            if (q.TemplateId.StartsWith("pictograph_animals_legend1__", StringComparison.Ordinal))
            {
                if (!q.UsesTextChoices || q.DisplayChoices.Count < 3 || q.IllustrationData != "pictograph|cat=3|dog=2|rabbit=4|legend=1" ||
                    q.PromptVi.Contains("3 mèo") || q.PromptVi.Contains("2 chó") || q.PromptVi.Contains("4 thỏ"))
                    throw new Exception("FUZZ_FAIL pictograph visual contract");
            }
            if (IsOperationConceptTemplate(q.TemplateId) && !ValidateOperationConceptContract(q))
                throw new Exception("FUZZ_FAIL operation concept contract: " + q.TemplateId);
            if (IsNumberExtensionTemplate(q.TemplateId) && !ValidateNumberExtensionContract(q))
                throw new Exception("FUZZ_FAIL number extension contract: " + q.TemplateId);
            if (q.TemplateId.StartsWith("word_problem_", StringComparison.Ordinal) && q.TemplateId != "word_problem_select_operation_one_step" && !ValidateWordProblemContract(q))
                throw new Exception("FUZZ_FAIL word problem relation contract: " + q.TemplateId);
        }

        private static bool IsOperationConceptTemplate(string templateId)
        {
            switch (templateId)
            {
                case "add_components_recognize":
                case "sub_components_recognize":
                case "multiplication_meaning_groups":
                case "division_meaning_share":
                case "multiplication_components_recognize":
                case "division_components_recognize":
                case "operation_meaning_from_visual":
                case "word_problem_select_operation_one_step":
                    return true;
                default:
                    return false;
            }
        }

        private static bool ValidateOperationConceptContract(MathQuestion q)
        {
            if (q == null || !q.UsesTextChoices || q.DisplayChoices == null || q.DisplayChoices.Count < 3 || q.DisplayChoices.Count > 4)
                return false;
            var parts = (q.IllustrationData ?? string.Empty).Split('|');
            if (q.TemplateId == "add_components_recognize" || q.TemplateId == "sub_components_recognize" ||
                q.TemplateId == "multiplication_components_recognize" || q.TemplateId == "division_components_recognize")
            {
                int a, b, result, target;
                if (q.Representation != "equation_components" || parts.Length != 6 || parts[0] != "equationparts" ||
                    !int.TryParse(parts[2], out a) || !int.TryParse(parts[3], out b) || !int.TryParse(parts[4], out result) ||
                    !int.TryParse(parts[5], out target) || target < 0 || target > 2) return false;
                if (parts[1] == "add")
                    return result == a + b && (q.CorrectAnswerDisplay == "số hạng" || q.CorrectAnswerDisplay == "tổng");
                if (parts[1] == "sub")
                    return result == a - b && (q.CorrectAnswerDisplay == "số bị trừ" || q.CorrectAnswerDisplay == "số trừ" || q.CorrectAnswerDisplay == "hiệu");
                if (parts[1] == "mul")
                    return result == a * b && (a == 2 || a == 5) && (q.CorrectAnswerDisplay == "thừa số" || q.CorrectAnswerDisplay == "tích");
                if (parts[1] == "div")
                    return b != 0 && a == b * result && (b == 2 || b == 5) &&
                           (q.CorrectAnswerDisplay == "số bị chia" || q.CorrectAnswerDisplay == "số chia" || q.CorrectAnswerDisplay == "thương");
                return false;
            }

            if (q.TemplateId == "multiplication_meaning_groups")
            {
                int factor, groups;
                return q.Representation == "operation_model" && parts.Length == 3 && parts[0] == "wordgroups" &&
                       int.TryParse(parts[1], out factor) && int.TryParse(parts[2], out groups) && (factor == 2 || factor == 5) && groups >= 2 && groups <= 9 &&
                       q.DisplayChoices.Count == 4 && q.CorrectAnswerDisplay == groups + " × " + factor;
            }
            if (q.TemplateId == "division_meaning_share")
            {
                int total, divisor;
                return q.Representation == "operation_model" && parts.Length == 3 && parts[0] == "wordshare" &&
                       int.TryParse(parts[1], out total) && int.TryParse(parts[2], out divisor) && (divisor == 2 || divisor == 5) && total % divisor == 0 &&
                       q.DisplayChoices.Count == 4 && q.CorrectAnswerDisplay == total + " : " + divisor;
            }

            var expectedOperations = new HashSet<string>(new[] { "cộng", "trừ", "nhân", "chia" }, StringComparer.Ordinal);
            if (q.TemplateId == "operation_meaning_from_visual" || q.TemplateId == "word_problem_select_operation_one_step")
            {
                var expectedRepresentation = q.TemplateId == "operation_meaning_from_visual" ? "operation_model" : "word_problem_model";
                if (q.Representation != expectedRepresentation || q.DisplayChoices.Count != 4 ||
                    !expectedOperations.SetEquals(q.DisplayChoices) || !expectedOperations.Contains(q.CorrectAnswerDisplay)) return false;
                return parts.Length >= 3 && (parts[0] == "wordbar" || parts[0] == "wordgroups" || parts[0] == "wordshare");
            }
            return false;
        }

        private static bool IsNumberExtensionTemplate(string templateId)
        {
            return templateId == "full_hundreds_recognize" || templateId == "number_ray_fill_1000" ||
                   templateId == "min_max_up_to_4" || templateId == "sort_up_to_4" ||
                   templateId == "add_sub_two_operators_left_to_right" || templateId == "mental_round_tens_hundreds_1000";
        }

        private static bool ValidateNumberExtensionContract(MathQuestion q)
        {
            if (q == null) return false;
            var parts = (q.IllustrationData ?? string.Empty).Split('|');
            if (q.TemplateId == "full_hundreds_recognize")
            {
                int count;
                return !q.UsesTextChoices && q.Representation == "hundreds_blocks" && parts.Length == 2 && parts[0] == "hundreds" &&
                       int.TryParse(parts[1], out count) && count >= 1 && count <= 9 && q.CorrectAnswer == count * 100 &&
                       q.DisplayChoices.Count == 4 && q.Choices.All(x => x >= 100 && x <= 900 && x % 100 == 0);
            }
            if (q.TemplateId == "number_ray_fill_1000")
            {
                int start, step, missing, count;
                return !q.UsesTextChoices && q.Representation == "number_line_fill" && parts.Length == 5 && parts[0] == "numberlinefill" &&
                       int.TryParse(parts[1], out start) && int.TryParse(parts[2], out step) && int.TryParse(parts[3], out missing) && int.TryParse(parts[4], out count) &&
                       (step == 10 || step == 100) && count == 5 && missing >= 1 && missing <= 3 && start >= 0 && start + 4 * step <= 1000 &&
                       q.CorrectAnswer == start + missing * step && q.Choices.All(x => x >= 0 && x <= 1000);
            }
            if (q.TemplateId == "min_max_up_to_4")
            {
                var values = ExtractInts(q.PromptVi).Take(4).ToArray();
                if (values.Length != 4 || values.Distinct().Count() != 4 || q.Representation != "number_cards" || q.DisplayChoices.Count != 4) return false;
                var askMax = q.PromptVi.Contains("lớn nhất");
                return q.CorrectAnswer == (askMax ? values.Max() : values.Min()) && new HashSet<int>(values).SetEquals(q.Choices);
            }
            if (q.TemplateId == "sort_up_to_4")
            {
                var values = ExtractInts(q.PromptVi).Take(4).ToArray();
                if (values.Length != 4 || values.Distinct().Count() != 4 || !q.UsesTextChoices || q.Representation != "number_cards" || q.DisplayChoices.Count != 4) return false;
                var ascending = q.PromptVi.Contains("từ bé đến lớn");
                var expected = string.Join(ascending ? " < " : " > ", ascending ? values.OrderBy(x => x) : values.OrderByDescending(x => x));
                return q.CorrectAnswerDisplay == expected;
            }
            if (q.TemplateId == "add_sub_two_operators_left_to_right")
            {
                int a, b, c, middle;
                if (parts.Length != 7 || parts[0] != "twostep" || !int.TryParse(parts[1], out a) || !int.TryParse(parts[3], out b) ||
                    !int.TryParse(parts[5], out c) || !int.TryParse(parts[6], out middle)) return false;
                var expectedMiddle = parts[2] == "+" ? a + b : a - b;
                var final = parts[4] == "+" ? expectedMiddle + c : expectedMiddle - c;
                return !q.UsesTextChoices && q.Representation == "two_step_strip" && middle == expectedMiddle && final == q.CorrectAnswer &&
                       middle >= 0 && middle <= 1000 && final >= 0 && final <= 1000;
            }
            if (q.TemplateId == "mental_round_tens_hundreds_1000")
            {
                int a, b, unit;
                if (parts.Length != 5 || parts[0] != "roundchunks" || !int.TryParse(parts[1], out a) || !int.TryParse(parts[3], out b) || !int.TryParse(parts[4], out unit)) return false;
                var expected = parts[2] == "+" ? a + b : a - b;
                return !q.UsesTextChoices && q.Representation == "round_number_chunks" && (unit == 10 || unit == 100) &&
                       a % unit == 0 && b % unit == 0 && q.CorrectAnswer == expected && expected >= 0 && expected <= 1000;
            }
            return false;
        }

        private static bool ValidateWordProblemContract(MathQuestion q)
        {
            if (q == null || q.UsesTextChoices || q.Representation != "word_problem_model") return false;
            var values = ExtractInts(q.PromptVi);
            if (values.Length < 2) return false;
            var parts = (q.IllustrationData ?? string.Empty).Split('|');
            switch (q.TemplateId)
            {
                case "word_problem_add_more":
                    return values[0] >= 5 && values[0] <= 50 && values[1] >= 1 && values[1] <= 40 &&
                           values[0] + values[1] <= 100 && q.CorrectAnswer == values[0] + values[1] &&
                           q.IllustrationData == "wordbar|add|" + values[0] + "|" + values[1];
                case "word_problem_sub_less":
                    return values[0] >= 10 && values[0] <= 100 && values[1] >= 1 && values[1] <= 40 &&
                           values[1] <= values[0] && q.CorrectAnswer == values[0] - values[1] &&
                           q.IllustrationData == "wordbar|sub|" + values[0] + "|" + values[1];
                case "word_problem_more_than":
                    return values[0] >= 5 && values[0] <= 50 && values[1] >= 1 && values[1] <= 30 &&
                           values[0] + values[1] <= 100 && q.CorrectAnswer == values[0] + values[1] &&
                           q.IllustrationData == "wordbar|more|" + values[0] + "|" + values[1];
                case "word_problem_less_than":
                    return values[0] >= 10 && values[0] <= 100 && values[1] >= 1 && values[1] <= 30 &&
                           values[1] <= values[0] && q.CorrectAnswer == values[0] - values[1] &&
                           q.IllustrationData == "wordbar|less|" + values[0] + "|" + values[1];
                case "word_problem_multiply_groups_2_5":
                    return values[0] >= 1 && values[0] <= 10 && (values[1] == 2 || values[1] == 5) &&
                           q.CorrectAnswer == values[0] * values[1] && parts.Length == 3 && parts[0] == "wordgroups" &&
                           parts[1] == values[1].ToString(CultureInfo.InvariantCulture) && parts[2] == values[0].ToString(CultureInfo.InvariantCulture);
                case "word_problem_divide_groups_2_5":
                    return values[0] >= 2 && values[0] <= 50 && (values[1] == 2 || values[1] == 5) && values[0] % values[1] == 0 &&
                           q.CorrectAnswer == values[0] / values[1] && q.CorrectAnswer >= 1 && q.CorrectAnswer <= 10 &&
                           parts.Length == 3 && parts[0] == "wordshare" && parts[1] == values[0].ToString(CultureInfo.InvariantCulture) &&
                           parts[2] == values[1].ToString(CultureInfo.InvariantCulture);
                default:
                    return false;
            }
        }

        private static string RepairTemplateForSmoke(MathQuestion question)
        {
            var method = typeof(MathSessionCoordinator).GetMethod("RepairTemplateFor", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null) throw new Exception("RepairTemplateFor reflection target missing");
            return method.Invoke(null, new object[] { question }) as string;
        }
        private static MathQuestion TextQuestion(string templateId, string correct, IList<string> choices)
        {
            return new MathQuestion
            {
                TemplateId = templateId,
                AnswerKind = "text",
                CorrectAnswerText = correct,
                ChoiceTexts = choices
            };
        }

        private static int[] ExtractInts(string text)
        {
            var values = new List<int>();
            var current = -1;
            foreach (var ch in text ?? string.Empty)
            {
                if (ch >= '0' && ch <= '9')
                {
                    if (current < 0) current = 0;
                    current = current * 10 + (ch - '0');
                }
                else if (current >= 0)
                {
                    values.Add(current);
                    current = -1;
                }
            }
            if (current >= 0) values.Add(current);
            return values.ToArray();
        }

        private static string FirstWrongChoice(MathQuestion q) { return q.DisplayChoices.First(x => !string.Equals(x, q.CorrectAnswerDisplay, StringComparison.Ordinal)); }
        private static void AddRecent(IList<string> list, string value) { list.Add(value); while (list.Count > 4) list.RemoveAt(0); }

        private static string ExpectedRepresentation(string templateId)
        {
            if (templateId == "add_components_recognize" || templateId == "sub_components_recognize" ||
                templateId == "multiplication_components_recognize" || templateId == "division_components_recognize") return "equation_components";
            if (templateId == "multiplication_meaning_groups" || templateId == "division_meaning_share" ||
                templateId == "operation_meaning_from_visual") return "operation_model";
            if (!string.IsNullOrWhiteSpace(templateId) && templateId.StartsWith("word_problem_", StringComparison.Ordinal)) return "word_problem_model";
            if (!string.IsNullOrWhiteSpace(templateId) && templateId.StartsWith("possible_certain_impossible_die__", StringComparison.Ordinal)) return "die_outcomes";
            if (!string.IsNullOrWhiteSpace(templateId) && templateId.StartsWith("geometry_identify_basic__", StringComparison.Ordinal)) return "geometry_basic";
            if (!string.IsNullOrWhiteSpace(templateId) && templateId.StartsWith("pictograph_animals_legend1__", StringComparison.Ordinal)) return "pictograph";
            if (templateId == "clock_read_minute_hand_3_or_6") return "clock";
            if (templateId == "full_hundreds_recognize") return "hundreds_blocks";
            if (templateId == "number_ray_fill_1000") return "number_line_fill";
            if (templateId == "min_max_up_to_4" || templateId == "sort_up_to_4") return "number_cards";
            if (templateId == "add_sub_two_operators_left_to_right") return "two_step_strip";
            if (templateId == "mental_round_tens_hundreds_1000") return "round_number_chunks";
            if (templateId == "place_value_decompose_3digit" || templateId == "expanded_form_3digit") return "place_value_blocks";
            if (templateId == "predecessor_successor" || templateId == "compare_two_numbers_1000") return "number_line_1000";
            if (templateId == "mental_add_within_20" || templateId == "mental_sub_within_20") return "number_ray";
            if (templateId == "times_table_2" || templateId == "times_table_5") return "equal_groups";
            if (templateId == "divide_table_2_exact" || templateId == "divide_table_5_exact") return "equal_groups_division";
            if (templateId.StartsWith("add_within_1000", StringComparison.Ordinal) || templateId.StartsWith("subtract_within_1000", StringComparison.Ordinal)) return "place_value";
            if (templateId == "polyline_length") return "polyline";
            return "symbolic";
        }

        private static bool RoadmapScoresBounded(MathRoadmapSnapshot roadmap)
        {
            if (roadmap == null || roadmap.NumberSense == null || roadmap.Mental20 == null || roadmap.Written1000 == null ||
                roadmap.Tables25 == null || roadmap.Measurement == null || roadmap.Chance == null) return false;
            return Bounded(roadmap.NumberSense.MasteryAverage) && Bounded(roadmap.Mental20.MasteryAverage) &&
                   Bounded(roadmap.Written1000.MasteryAverage) && Bounded(roadmap.Tables25.MasteryAverage) &&
                   Bounded(roadmap.Measurement.MasteryAverage) && Bounded(roadmap.Chance.MasteryAverage);
        }

        private static bool Bounded(double value) { return value >= 0 && value <= 1; }

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

        private static int ReadSkillAttempts(LearningDatabase database, string childId, string skillId)
        {
            using (var c = database.OpenConnection()) using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT COALESCE(attempts_count,0) FROM child_skill WHERE child_id=@child AND skill_id=@skill;";
                cmd.Parameters.AddWithValue("@child", childId);
                cmd.Parameters.AddWithValue("@skill", skillId);
                var value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
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
