using System;
using System.Collections.Generic;

namespace WAHU.Learning
{
    public sealed class SkillSnapshot
    {
        public string SkillId { get; set; }
        public double MasteryScore { get; set; }
        public double Confidence { get; set; }
        public int AttemptsCount { get; set; }
        public int IndependentSuccessCount { get; set; }
        public int HintedSuccessCount { get; set; }
        public int TransferSuccessCount { get; set; }
        public DateTime? LastSeenAtUtc { get; set; }
        public DateTime? LastSuccessAtUtc { get; set; }
        public DateTime? NextReviewAtUtc { get; set; }
        public string LearningState { get; set; }
    }

    public sealed class MathTemplateRef
    {
        public string TemplateId { get; set; }
        public string SkillId { get; set; }
        public string SourceTemplateId { get; set; }
        public string FixedContextVi { get; set; }
        public string StatementVi { get; set; }
        public string AnswerText { get; set; }
    }

    public sealed class MathQuestion
    {
        public string QuestionId { get; set; }
        public string TemplateId { get; set; }
        public string SkillId { get; set; }
        public string PromptVi { get; set; }
        public int CorrectAnswer { get; set; }
        public IList<int> Choices { get; set; }
        public string AnswerKind { get; set; }
        public string CorrectAnswerText { get; set; }
        public IList<string> ChoiceTexts { get; set; }
        public string IllustrationData { get; set; }
        public string Representation { get; set; }
        public string HintLevel1 { get; set; }
        public string HintLevel2 { get; set; }
        public double DifficultyFit { get; set; }

        public bool UsesTextChoices
        {
            get { return string.Equals(AnswerKind, "text", StringComparison.Ordinal); }
        }

        public string CorrectAnswerDisplay
        {
            get
            {
                return UsesTextChoices ? (CorrectAnswerText ?? string.Empty) : CorrectAnswer.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        public IList<string> DisplayChoices
        {
            get
            {
                if (ChoiceTexts != null && ChoiceTexts.Count > 0) return ChoiceTexts;
                var values = new List<string>();
                if (Choices != null)
                    foreach (var value in Choices) values.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return values;
            }
        }

        public bool IsCorrectAnswer(string answer)
        {
            if (UsesTextChoices)
                return string.Equals((answer ?? string.Empty).Trim(), (CorrectAnswerText ?? string.Empty).Trim(), StringComparison.Ordinal);
            int parsed;
            return int.TryParse(answer, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out parsed) && parsed == CorrectAnswer;
        }
    }

    public sealed class MathSelectionDecision
    {
        public MathTemplateRef Template { get; set; }
        public double Score { get; set; }
        public double DifficultyFit { get; set; }
        public IList<string> Reasons { get; set; }
        public IList<string> CandidateSummary { get; set; }
    }

    public sealed class MathErrorClassification
    {
        public string ErrorType { get; set; }
        public double Confidence { get; set; }
        public IList<string> Evidence { get; set; }
    }

    public sealed class MasteryUpdate
    {
        public double ScoreBefore { get; set; }
        public double ScoreAfter { get; set; }
        public double Delta { get; set; }
        public double ConfidenceAfter { get; set; }
        public int AttemptsCount { get; set; }
        public int IndependentSuccessCount { get; set; }
        public int HintedSuccessCount { get; set; }
        public int TransferSuccessCount { get; set; }
        public string LearningState { get; set; }
        public string EventType { get; set; }
        public IList<string> Reasons { get; set; }
    }

    public sealed class ReviewUpdate
    {
        public DateTime DueAtUtc { get; set; }
        public double IntervalDays { get; set; }
        public string Reason { get; set; }
    }
}
