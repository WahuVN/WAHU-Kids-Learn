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
        public int CompletedQuestionCount { get; set; }
        public bool ResumedExistingSession { get; set; }
        public bool RestoredOpenQuestion { get; set; }
        public bool DiscardedCorruptOpenQuestion { get; set; }
        public string SessionMode { get; set; }
        public string TargetLessonId { get; set; }
        public string TargetLessonTitleVi { get; set; }
        public IList<string> SelectedContentQuestionIds { get; set; }
        public MathLessonAccessSnapshot LessonAccess { get; set; }
        public bool RetryPending { get; set; }
        public int CurrentAttemptIndex { get; set; }
    }

    public sealed class MathAnswerOutcome
    {
        public bool IsCorrect { get; set; }
        public int CorrectAnswer { get; set; }
        public string CorrectAnswerDisplay { get; set; }
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
        public int AttemptIndex { get; set; }
        public bool QuestionCompleted { get; set; }
        public bool CanRetry { get; set; }
        public bool IsRetry { get; set; }
        public bool IndependentSuccess { get; set; }
    }

    public sealed class MathSkillMasteryChange
    {
        public string SkillId { get; set; }
        public double ScoreBefore { get; set; }
        public double ScoreAfter { get; set; }
        public double Delta { get; set; }
    }

    public sealed class MathSessionSummary
    {
        public int Attempts { get; set; }
        public int AnswerAttempts { get; set; }
        public int Correct { get; set; }
        public int IndependentCorrect { get; set; }
        public int HintedCorrect { get; set; }
        public int RetriedQuestions { get; set; }
        public int RetriedCorrect { get; set; }
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
        public string SessionMode { get; set; }
        public string TargetLessonId { get; set; }
        public bool LessonCompleted { get; set; }
        public double? LessonScorePercent { get; set; }
        public double? LessonBestScorePercent { get; set; }
        public IList<MathSkillMasteryChange> MasteryChanges { get; set; }
        public int ImprovedSkillCount { get; set; }
        public double? TargetSkillMasteryBefore { get; set; }
        public double? TargetSkillMasteryAfter { get; set; }
        public double? TargetSkillMasteryDelta { get; set; }
        public string NextLessonId { get; set; }
        public string NextLessonTitleVi { get; set; }
    }
}
