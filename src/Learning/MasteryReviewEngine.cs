using System;
using System.Collections.Generic;

namespace WAHU.Learning
{
    public sealed class MasteryEngineV1
    {
        public const string Version = "mastery-v1";

        public MasteryUpdate Evaluate(SkillSnapshot current, bool isCorrect, int hintLevel, bool transferSuccess, bool protectNegativeUpdate)
        {
            current = current ?? new SkillSnapshot { MasteryScore = 0.25, Confidence = 0.20, LearningState = "NEW" };
            var before = Clamp(current.MasteryScore);
            var confidence = Clamp(current.Confidence);
            var reasons = new List<string>();
            double delta;
            string eventType;

            if (isCorrect)
            {
                if (hintLevel <= 0)
                {
                    delta = 0.13 * (1.0 - before * 0.35);
                    reasons.Add("independent_correct");
                }
                else
                {
                    delta = 0.065 * (1.0 - before * 0.25);
                    reasons.Add("hinted_correct_lower_weight");
                }
                if (transferSuccess)
                {
                    delta += 0.035;
                    reasons.Add("representation_transfer_success");
                }
                eventType = "success";
                confidence = Clamp(confidence + (hintLevel <= 0 ? 0.10 : 0.055) + (transferSuccess ? 0.05 : 0));
            }
            else if (protectNegativeUpdate)
            {
                delta = 0;
                eventType = "protected_failure";
                reasons.Add("fatigue_or_state_protection");
                confidence = Clamp(confidence - 0.015);
            }
            else
            {
                delta = -(0.055 + 0.055 * Math.Max(0.20, confidence));
                eventType = "failure";
                reasons.Add("independent_or_hinted_failure");
                confidence = Clamp(confidence - 0.06);
            }

            var after = Clamp(before + delta);
            var attempts = current.AttemptsCount + 1;
            var independent = current.IndependentSuccessCount + (isCorrect && hintLevel <= 0 ? 1 : 0);
            var hinted = current.HintedSuccessCount + (isCorrect && hintLevel > 0 ? 1 : 0);
            var transfer = current.TransferSuccessCount + (isCorrect && transferSuccess ? 1 : 0);
            string state;
            if (after >= 0.80 && confidence >= 0.65 && independent >= 3)
                state = "STABLE";
            else if (!isCorrect && after < 0.45)
                state = "REVIEW";
            else if (attempts > 0)
                state = "LEARNING";
            else
                state = "NEW";

            return new MasteryUpdate
            {
                ScoreBefore = before,
                ScoreAfter = after,
                Delta = after - before,
                ConfidenceAfter = confidence,
                AttemptsCount = attempts,
                IndependentSuccessCount = independent,
                HintedSuccessCount = hinted,
                TransferSuccessCount = transfer,
                LearningState = state,
                EventType = eventType,
                Reasons = reasons
            };
        }

        private static double Clamp(double value) { return Math.Max(0.0, Math.Min(1.0, value)); }
    }

    public sealed class ReviewSchedulerV1
    {
        public const string Version = "review-v1";

        public ReviewUpdate Schedule(DateTime answeredAtUtc, MasteryUpdate mastery, bool isCorrect, int hintLevel)
        {
            if (mastery == null) throw new ArgumentNullException("mastery");
            var utc = answeredAtUtc.Kind == DateTimeKind.Utc ? answeredAtUtc : answeredAtUtc.ToUniversalTime();
            double interval;
            string reason;
            if (!isCorrect)
            {
                interval = 0.0208333333; // ~30 minutes, repair can still happen inside current session.
                reason = "incorrect_recall_short_repair_interval";
            }
            else if (hintLevel > 0)
            {
                interval = 0.25;
                reason = "hinted_success_short_recall";
            }
            else if (mastery.ScoreAfter >= 0.85 && mastery.IndependentSuccessCount >= 4)
            {
                interval = 7.0;
                reason = "stable_independent_success_week_review";
            }
            else if (mastery.ScoreAfter >= 0.70)
            {
                interval = 3.0;
                reason = "developing_mastery_multi_day_review";
            }
            else if (mastery.ScoreAfter >= 0.50)
            {
                interval = 1.0;
                reason = "acquiring_next_day_review";
            }
            else
            {
                interval = 0.5;
                reason = "early_acquisition_same_day_review";
            }
            return new ReviewUpdate { DueAtUtc = utc.AddDays(interval), IntervalDays = interval, Reason = reason };
        }
    }

    public sealed class MathErrorClassifierV1
    {
        public const string Version = "math-error-v1";

        public MathErrorClassification Classify(MathQuestion question, int answer)
        {
            return Classify(question, answer.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public MathErrorClassification Classify(MathQuestion question, string answer)
        {
            if (question == null) throw new ArgumentNullException("question");
            if (question.IsCorrectAnswer(answer)) return null;

            if (question.UsesTextChoices)
            {
                if (question.TemplateId == "clock_read_minute_hand_3_or_6")
                    return New("TIME_READ_ERROR", 0.66, "wrong_clock_quarter_or_half_hour_choice");
                if (question.TemplateId != null && question.TemplateId.StartsWith("geometry_identify_basic__", StringComparison.Ordinal))
                    return New("GEOMETRY_RECOGNITION_ERROR", 0.64, "wrong_geometry_label_choice");
                if (question.TemplateId != null && question.TemplateId.StartsWith("pictograph_animals_legend1__", StringComparison.Ordinal))
                    return New("PICTOGRAPH_READ_ERROR", 0.62, "wrong_pictograph_read_or_inference_choice");
                if (question.TemplateId != null && question.TemplateId.StartsWith("possible_certain_impossible_die__", StringComparison.Ordinal))
                    return New("EVENT_CLASSIFICATION_ERROR", 0.64, "wrong_possible_certain_impossible_choice");
                if (question.TemplateId == "place_value_decompose_3digit" || question.TemplateId == "expanded_form_3digit")
                    return New("PLACE_VALUE_ERROR", 0.66, "wrong_place_value_choice");
                if (question.TemplateId == "compare_two_numbers_1000")
                    return New("COMPARISON_ERROR", 0.68, "wrong_comparison_symbol");
                if (question.TemplateId == "predecessor_successor")
                    return New("SEQUENCE_NEIGHBOR_ERROR", 0.62, "wrong_predecessor_successor_pair");
                if (question.TemplateId == "heavier_lighter_balance")
                    return New("MEASUREMENT_COMPARE_ERROR", 0.66, "wrong_heavier_lighter_visual_choice");
                if (question.TemplateId == "mass_kg_read_write" || question.TemplateId == "capacity_liter_read_write" || question.TemplateId == "length_dm_m_km_relation")
                    return New("MEASUREMENT_UNIT_ERROR", 0.66, "wrong_measurement_unit_read_or_relation_choice");
                if (question.TemplateId == "time_day_24_hours" || question.TemplateId == "time_hour_60_minutes")
                    return New("TIME_RELATION_ERROR", 0.68, "wrong_day_hour_or_hour_minute_relation");
                if (question.TemplateId == "calendar_days_in_month_date")
                    return New("CALENDAR_READ_ERROR", 0.64, "wrong_calendar_month_or_date_choice");
                if (question.TemplateId == "measure_with_ruler_cm")
                    return New("MEASURE_READ_ERROR", 0.68, "wrong_ruler_segment_length_choice");
                if (question.TemplateId == "measurement_convert_calculate_learned_units")
                    return New("MEASUREMENT_CALC_ERROR", 0.64, "wrong_unit_conversion_or_same_unit_calculation");
                if (question.TemplateId == "measurement_real_world_one_step")
                    return New("MEASUREMENT_WORD_ERROR", 0.58, "wrong_one_step_measurement_context_answer");
                if (question.TemplateId == "read_number_to_1000")
                    return New("NUMBER_READ_WRITE_ERROR", 0.68, "wrong_number_word_reading_choice");
                if (question.TemplateId == "measurement_estimate_reference_10cm")
                    return New("ESTIMATION_ERROR", 0.58, "wrong_reference_based_length_estimate");
                if (question.TemplateId == "sort_up_to_4")
                    return New("NUMBER_ORDER_ERROR", 0.66, "wrong_four_number_sort_order");
                if (question.TemplateId == "add_components_recognize" || question.TemplateId == "sub_components_recognize" ||
                    question.TemplateId == "multiplication_components_recognize" || question.TemplateId == "division_components_recognize")
                    return New("OPERATION_COMPONENT_ERROR", 0.64, "wrong_operation_component_role_choice");
                if (question.TemplateId == "multiplication_meaning_groups" || question.TemplateId == "division_meaning_share" ||
                    question.TemplateId == "operation_meaning_from_visual" || question.TemplateId == "word_problem_select_operation_one_step")
                    return New("OPERATION_MEANING_ERROR", 0.60, "wrong_operation_meaning_or_selection_choice");
                return New("UNKNOWN", 0.40, "wrong_text_choice_single_attempt");
            }

            int numeric;
            if (!MathAnswerValidator.TryParseInteger(answer, out numeric))
            {
                if (MathAnswerValidator.IsWellFormedNumericAnswer(answer))
                    return New("UNKNOWN", 0.40, "well_formed_numeric_answer_not_equal_to_expected_integer");
                return New("INPUT_FORMAT_ERROR", 0.90, "numeric_question_received_malformed_answer");
            }
            var difference = Math.Abs(question.CorrectAnswer - numeric);
            if (question.TemplateId == "add_within_1000_one_carry" && (difference == 10 || difference == 100))
                return New("CARRY_MISSING", 0.78, "answer_matches_common_missing_carry_offset");
            if (question.TemplateId == "subtract_within_1000_one_borrow" && (difference == 10 || difference == 100))
                return New("BORROW_MISSING", 0.76, "answer_matches_common_missing_borrow_offset");
            if (question.TemplateId == "mental_round_tens_hundreds_1000")
                return New("ROUND_NUMBER_FACT_ERROR", 0.62, "wrong_round_tens_or_hundreds_mental_result");
            if (question.TemplateId.StartsWith("mental_", StringComparison.Ordinal) ||
                question.TemplateId.StartsWith("times_table_", StringComparison.Ordinal) ||
                question.TemplateId.StartsWith("divide_table_", StringComparison.Ordinal))
                return New("FACT_ERROR", 0.62, "wrong_numeric_fact_answer");
            if (question.TemplateId == "polyline_length")
                return New("MEASUREMENT_SUM_ERROR", 0.58, "wrong_polyline_length_sum");
            if (question.TemplateId == "full_hundreds_recognize")
                return New("HUNDREDS_RECOGNITION_ERROR", 0.66, "wrong_full_hundreds_value");
            if (question.TemplateId == "number_ray_fill_1000")
                return New("NUMBER_SEQUENCE_ERROR", 0.64, "wrong_number_line_missing_value");
            if (question.TemplateId == "min_max_up_to_4")
                return New("NUMBER_ORDER_ERROR", 0.66, "wrong_min_or_max_choice");
            if (question.TemplateId == "add_sub_two_operators_left_to_right")
                return New("TWO_STEP_CALCULATION_ERROR", 0.60, "wrong_left_to_right_two_operator_result");
            if (question.TemplateId == "measure_with_common_scale")
                return New("SCALE_READ_ERROR", 0.66, "wrong_common_scale_tick_value");
            if (question.TemplateId == "data_collect_classify_count")
                return New("DATA_CLASSIFY_COUNT_ERROR", 0.64, "wrong_classified_group_count");
            if (question.TemplateId == "count_place_value_to_1000" || question.TemplateId == "write_number_to_1000")
                return New("NUMBER_READ_WRITE_ERROR", 0.68, "wrong_count_or_written_number_to_1000");
            if (question.TemplateId == "estimate_objects_by_tens")
                return New("ESTIMATION_ERROR", 0.58, "wrong_nearest_ten_object_estimate");
            if (question.TemplateId != null && question.TemplateId.StartsWith("word_problem_", StringComparison.Ordinal))
                return New("WORD_PROBLEM_RELATION_ERROR", 0.56, "wrong_one_step_relation_answer");
            return New("UNKNOWN", 0.40, "single_attempt_insufficient_for_specific_diagnosis");
        }

        private static MathErrorClassification New(string type, double confidence, string evidence)
        {
            return new MathErrorClassification { ErrorType = type, Confidence = confidence, Evidence = new[] { evidence } };
        }
    }
}
