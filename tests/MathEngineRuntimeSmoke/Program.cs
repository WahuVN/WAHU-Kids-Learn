using System;
using System.Collections.Generic;
using WAHU.Learning;

namespace WAHU.MathEngineRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        private static void Main()
        {
            TestIntegerEquivalence();
            TestDecimalAndFractionEquivalence();
            TestTextMultipleAnswersAndUnits();
            TestExpressionSafety();
            TestClassifierUsesValidator();
            TestMasteryScoringSemantics();
            Console.WriteLine("MATH_ENGINE_RUNTIME_SMOKE_PASS assertions=" + _assertions);
        }

        private static void TestIntegerEquivalence()
        {
            var q = NumberQuestion("integer", "2", 2);
            A(q.IsCorrectAnswer("2"), "integer_exact");
            A(q.IsCorrectAnswer("  +2  "), "integer_whitespace_sign");
            A(q.IsCorrectAnswer("2.0"), "integer_decimal_equivalent");
            A(q.IsCorrectAnswer("2,0"), "integer_comma_decimal_equivalent");
            A(q.IsCorrectAnswer("4/2"), "integer_fraction_equivalent");
            A(!q.IsCorrectAnswer("2abc"), "integer_rejects_trailing_junk");
            A(!q.IsCorrectAnswer(""), "integer_rejects_empty");
            A(!q.IsCorrectAnswer("2/0"), "integer_rejects_division_by_zero");
            A(!q.IsCorrectAnswer("1,2,3"), "integer_rejects_multiple_decimal_separators");

            var negative = NumberQuestion("integer", "-3", -3);
            A(negative.IsCorrectAnswer("−3"), "unicode_minus_supported");
            A(negative.IsCorrectAnswer("-6/2"), "negative_fraction_equivalent");

            var zero = NumberQuestion("integer", "0", 0);
            A(zero.IsCorrectAnswer("0/7"), "zero_fraction_equivalent");

            var large = NumberQuestion("number", "123456789012345678901234567890", 0);
            A(large.IsCorrectAnswer("123456789012345678901234567890"), "large_integer_bigint_supported");
            A(!large.IsCorrectAnswer("123456789012345678901234567891"), "large_integer_difference_detected");
        }

        private static void TestDecimalAndFractionEquivalence()
        {
            var exact = NumberQuestion("decimal", "1.25", 0);
            A(exact.IsCorrectAnswer("1,25"), "decimal_comma_dot_equivalent");
            A(exact.IsCorrectAnswer("5/4"), "decimal_fraction_equivalent");
            A(!exact.IsCorrectAnswer("1.2501"), "decimal_exact_without_tolerance");

            exact.NumericTolerance = 0.001;
            A(exact.IsCorrectAnswer("1.2509"), "decimal_tolerance_accepts_near_value");
            A(!exact.IsCorrectAnswer("1.253"), "decimal_tolerance_rejects_far_value");

            var fraction = NumberQuestion("fraction", "2/3", 0);
            A(fraction.IsCorrectAnswer("4/6"), "equivalent_fraction_reduced_exactly");
            A(fraction.IsCorrectAnswer("-2/-3"), "fraction_sign_normalized");
            A(!fraction.IsCorrectAnswer("0.6666"), "fraction_decimal_not_exact_without_tolerance");
            A(!fraction.IsCorrectAnswer("2//3"), "fraction_malformed_rejected");
        }

        private static void TestTextMultipleAnswersAndUnits()
        {
            var text = new MathQuestion
            {
                AnswerKind = "text",
                CorrectAnswerText = "Có thể",
                AcceptedAnswers = new[] { "có khả năng" }
            };
            A(text.IsCorrectAnswer("  CÓ   THỂ "), "text_case_and_whitespace_normalized");
            A(text.IsCorrectAnswer("có khả năng"), "multiple_valid_text_answer");
            A(!text.IsCorrectAnswer("chắc chắn"), "text_wrong_choice_rejected");

            var unit = new MathQuestion
            {
                AnswerKind = "unit",
                CorrectAnswerText = "12 cm",
                ExpectedUnit = "cm",
                AcceptedUnits = new[] { "centimét" }
            };
            A(unit.IsCorrectAnswer("12cm"), "unit_without_space");
            A(unit.IsCorrectAnswer("12 CM."), "unit_case_period_normalized");
            A(unit.IsCorrectAnswer("12 centimét"), "unit_alias_accepted");
            A(unit.IsCorrectAnswer("24/2 cm"), "unit_numeric_fraction_equivalent");
            A(!unit.IsCorrectAnswer("12 m"), "wrong_unit_rejected");
            A(!unit.IsCorrectAnswer("12"), "missing_unit_rejected");
        }

        private static void TestExpressionSafety()
        {
            var expression = NumberQuestion("expression", "2*(3+4)", 0);
            A(expression.IsCorrectAnswer("7+7"), "expression_equivalent_addition");
            A(expression.IsCorrectAnswer("28/2"), "expression_equivalent_division");
            A(expression.IsCorrectAnswer("14"), "expression_literal_equivalent");
            A(!expression.IsCorrectAnswer("2*x"), "expression_rejects_variables");
            A(!expression.IsCorrectAnswer("1/0"), "expression_rejects_division_by_zero");
            A(!expression.IsCorrectAnswer("Math.Sqrt(196)"), "expression_rejects_functions");

            var unknown = NumberQuestion("future_unknown_kind", "2+2", 0);
            A(!unknown.IsCorrectAnswer("4"), "unknown_kind_does_not_widen_parser");
            A(unknown.IsCorrectAnswer("2+2"), "unknown_kind_exact_text_still_supported");
        }

        private static void TestClassifierUsesValidator()
        {
            var classifier = new MathErrorClassifierV1();
            var q = new MathQuestion { TemplateId = "times_table_2", AnswerKind = "integer", CorrectAnswer = 2, CorrectAnswerText = "2" };
            A(classifier.Classify(q, "4/2") == null, "classifier_respects_fraction_equivalent_correct_answer");
            var malformed = classifier.Classify(q, "2abc");
            A(malformed != null && malformed.ErrorType == "INPUT_FORMAT_ERROR", "classifier_marks_malformed_numeric_input");
            var numericWrong = classifier.Classify(q, "3/2");
            A(numericWrong != null && numericWrong.ErrorType == "UNKNOWN", "classifier_does_not_call_fraction_numeric_input_malformed");
        }

        private static void TestMasteryScoringSemantics()
        {
            var engine = new MasteryEngineV1();
            var current = new SkillSnapshot { SkillId = "T", MasteryScore = 0.25, Confidence = 0.20, LearningState = "NEW" };
            var firstTry = engine.Evaluate(current, true, 0, false, false);
            var hinted = engine.Evaluate(current, true, 2, false, false);
            var wrong = engine.Evaluate(current, false, 0, false, false);
            var protectedWrong = engine.Evaluate(current, false, 0, false, true);
            A(firstTry.Delta > hinted.Delta && hinted.Delta > 0, "first_try_scores_above_hinted_success");
            A(firstTry.IndependentSuccessCount == 1 && hinted.HintedSuccessCount == 1, "success_counters_split_by_hint");
            A(wrong.Delta < 0, "wrong_reduces_mastery");
            A(Math.Abs(protectedWrong.Delta) < 0.0000001, "protected_failure_is_zero_delta");
        }

        private static MathQuestion NumberQuestion(string kind, string expected, int fallback)
        {
            return new MathQuestion { AnswerKind = kind, CorrectAnswerText = expected, CorrectAnswer = fallback };
        }

        private static void A(bool condition, string name)
        {
            if (!condition) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }
    }
}
