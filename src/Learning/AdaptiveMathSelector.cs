using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WAHU.Learning
{
    public sealed class AdaptiveMathSelector
    {
        public const string EngineVersion = "adaptive-math-v1";

        public MathSelectionDecision Select(
            IEnumerable<MathTemplateRef> templates,
            IDictionary<string, SkillSnapshot> skills,
            DateTime utcNow,
            IList<string> recentTemplateIds,
            IList<string> recentSkillIds)
        {
            if (templates == null) throw new ArgumentNullException("templates");
            var candidates = templates.Where(IsSupported).ToList();
            if (candidates.Count == 0) throw new InvalidOperationException("Không có Math VERIFIED template được generator V1 hỗ trợ.");
            skills = skills ?? new Dictionary<string, SkillSnapshot>(StringComparer.Ordinal);
            recentTemplateIds = recentTemplateIds ?? new List<string>();
            recentSkillIds = recentSkillIds ?? new List<string>();

            MathSelectionDecision best = null;
            var summaries = new List<string>();
            foreach (var candidate in candidates.OrderBy(x => x.TemplateId, StringComparer.Ordinal))
            {
                SkillSnapshot skill;
                if (!skills.TryGetValue(candidate.SkillId, out skill) || skill == null)
                    skill = NewSkill(candidate.SkillId);

                var reasons = new List<string>();
                var score = 0.0;
                if (skill.NextReviewAtUtc.HasValue && skill.NextReviewAtUtc.Value <= utcNow)
                {
                    score += 100;
                    reasons.Add("due_review");
                }
                else if (skill.AttemptsCount == 0)
                {
                    score += 52;
                    reasons.Add("new_grade_level_skill");
                }
                else
                {
                    score += (1.0 - Clamp(skill.MasteryScore)) * 45.0;
                    reasons.Add("mastery_gap");
                }

                if (string.Equals(skill.LearningState, "REVIEW", StringComparison.Ordinal))
                {
                    score += 24;
                    reasons.Add("needs_review_state");
                }
                else if (string.Equals(skill.LearningState, "LEARNING", StringComparison.Ordinal))
                {
                    score += 10;
                    reasons.Add("current_learning_state");
                }

                var recentTemplateCount = recentTemplateIds.Count(x => string.Equals(x, candidate.TemplateId, StringComparison.Ordinal));
                var recentSkillCount = recentSkillIds.Count(x => string.Equals(x, candidate.SkillId, StringComparison.Ordinal));
                if (recentTemplateCount > 0)
                {
                    score -= 32 * recentTemplateCount;
                    reasons.Add("recent_template_penalty");
                }
                if (recentSkillCount >= 2)
                {
                    score -= 18 * (recentSkillCount - 1);
                    reasons.Add("recent_skill_penalty");
                }

                var desiredSuccess = 0.80;
                var estimatedSuccess = 0.55 + 0.40 * Clamp(skill.MasteryScore);
                var difficultyFit = 1.0 - Math.Min(1.0, Math.Abs(desiredSuccess - estimatedSuccess) / 0.45);
                score += difficultyFit * 16.0;
                reasons.Add("difficulty_fit_" + difficultyFit.ToString("0.00", CultureInfo.InvariantCulture));
                summaries.Add(candidate.TemplateId + ":" + score.ToString("0.00", CultureInfo.InvariantCulture));

                if (best == null || score > best.Score ||
                    (Math.Abs(score - best.Score) < 0.000001 && string.CompareOrdinal(candidate.TemplateId, best.Template.TemplateId) < 0))
                {
                    best = new MathSelectionDecision
                    {
                        Template = candidate,
                        Score = score,
                        DifficultyFit = difficultyFit,
                        Reasons = reasons,
                        CandidateSummary = summaries
                    };
                }
            }
            if (best != null) best.CandidateSummary = summaries;
            return best;
        }

        public static bool IsSupported(MathTemplateRef template)
        {
            if (template == null || string.IsNullOrWhiteSpace(template.TemplateId) || string.IsNullOrWhiteSpace(template.SkillId)) return false;
            if (template.TemplateId.StartsWith("possible_certain_impossible_die__", StringComparison.Ordinal) ||
                template.TemplateId.StartsWith("geometry_identify_basic__", StringComparison.Ordinal) ||
                template.TemplateId.StartsWith("pictograph_animals_legend1__", StringComparison.Ordinal))
                return !string.IsNullOrWhiteSpace(template.FixedContextVi) &&
                       !string.IsNullOrWhiteSpace(template.StatementVi) &&
                       !string.IsNullOrWhiteSpace(template.AnswerText);
            switch (template.TemplateId)
            {
                case "place_value_decompose_3digit":
                case "expanded_form_3digit":
                case "predecessor_successor":
                case "compare_two_numbers_1000":
                case "mental_add_within_20":
                case "mental_sub_within_20":
                case "times_table_2":
                case "times_table_5":
                case "divide_table_2_exact":
                case "divide_table_5_exact":
                case "add_within_1000_no_carry":
                case "add_within_1000_one_carry":
                case "subtract_within_1000_no_borrow":
                case "subtract_within_1000_one_borrow":
                case "polyline_length":
                case "clock_read_minute_hand_3_or_6":
                case "word_problem_add_more":
                case "word_problem_sub_less":
                case "word_problem_more_than":
                case "word_problem_less_than":
                case "word_problem_multiply_groups_2_5":
                case "word_problem_divide_groups_2_5":
                case "add_components_recognize":
                case "sub_components_recognize":
                case "multiplication_meaning_groups":
                case "division_meaning_share":
                case "multiplication_components_recognize":
                case "division_components_recognize":
                case "operation_meaning_from_visual":
                case "word_problem_select_operation_one_step":
                case "full_hundreds_recognize":
                case "number_ray_fill_1000":
                case "min_max_up_to_4":
                case "sort_up_to_4":
                case "add_sub_two_operators_left_to_right":
                case "mental_round_tens_hundreds_1000":
                case "heavier_lighter_balance":
                case "mass_kg_read_write":
                case "capacity_liter_read_write":
                case "length_dm_m_km_relation":
                case "time_day_24_hours":
                case "time_hour_60_minutes":
                case "calendar_days_in_month_date":
                case "measure_with_ruler_cm":
                case "measure_with_common_scale":
                case "measurement_convert_calculate_learned_units":
                case "measurement_real_world_one_step":
                case "data_collect_classify_count":
                case "count_place_value_to_1000":
                case "read_number_to_1000":
                case "write_number_to_1000":
                case "estimate_objects_by_tens":
                case "measurement_estimate_reference_10cm":
                    return true;
                default:
                    return false;
            }
        }

        private static SkillSnapshot NewSkill(string skillId)
        {
            return new SkillSnapshot { SkillId = skillId, MasteryScore = 0.25, Confidence = 0.20, LearningState = "NEW" };
        }

        private static double Clamp(double value) { return Math.Max(0.0, Math.Min(1.0, value)); }
    }
}
