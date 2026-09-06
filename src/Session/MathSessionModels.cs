using System;
using System.Collections.Generic;
using WAHU.Learning;

namespace WAHU.Session
{
    public sealed class MathSessionStartResult
    {
        public string SessionId { get; set; }
        public string ChildId { get; set; }
        public string DisplayName { get; set; }
        public int RecoveredDanglingSessions { get; set; }
        public int TargetQuestionCount { get; set; }
    }

    public sealed class MathAnswerOutcome
    {
        public bool IsCorrect { get; set; }
        public int CorrectAnswer { get; set; }
        public int HintLevel { get; set; }
        public string FeedbackVi { get; set; }
        public BehaviorDecision Behavior { get; set; }
        public MasteryUpdate Mastery { get; set; }
        public ReviewUpdate Review { get; set; }
        public MathErrorClassification Error { get; set; }
        public bool OfferBreak { get; set; }
        public bool SuggestPositiveEnd { get; set; }
        public int CompletedQuestionCount { get; set; }
        public int TargetQuestionCount { get; set; }
    }

    public sealed class MathSessionSummary
    {
        public int Attempts { get; set; }
        public int Correct { get; set; }
        public int HintedCorrect { get; set; }
        public int Wrong { get; set; }
        public int DistinctSkills { get; set; }
        public BehaviorState FinalBehaviorState { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime? EndedAtUtc { get; set; }
        public int GardenGrowthSteps { get; set; }
        public string GardenUnlockMessage { get; set; }
        public IList<string> GardenUnlockedItemIds { get; set; }
        public int SessionsUntilNextGardenMilestone { get; set; }
        public string NextGardenMilestoneItemId { get; set; }
    }
}
