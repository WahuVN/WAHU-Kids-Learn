using System;
using System.Collections.Generic;

namespace WAHU.Learning
{
    public enum BehaviorState
    {
        READY,
        FLOW_LIKELY,
        BORED_OR_UNDERCHALLENGED,
        STRAINED,
        FRUSTRATED_LIKELY,
        FATIGUED_LIKELY
    }

    public sealed class BehaviorObservation
    {
        public string SkillId { get; set; }
        public bool IsCorrect { get; set; }
        public int ResponseMs { get; set; }
        public int HintLevel { get; set; }
        public bool UsedMaxHint { get; set; }
        public bool RapidWrong { get; set; }
        public bool SkippedOrExited { get; set; }
        public bool InputMiss { get; set; }
        public string ErrorType { get; set; }
        public string Representation { get; set; }
        public double MasteryScore { get; set; }
        public double SessionElapsedMinutes { get; set; }
    }

    public sealed class BehaviorDecision
    {
        public BehaviorState State { get; set; }
        public BehaviorState CandidateState { get; set; }
        public double Confidence { get; set; }
        public IList<string> Evidence { get; set; }
        public IList<string> Actions { get; set; }
        public bool StateChanged { get; set; }
        public bool ProtectMasteryFromNegativeUpdate { get; set; }
        public bool TriggerPrerequisiteRepair { get; set; }
        public int RecentAttemptCount { get; set; }
        public int DistinctRecentSkillCount { get; set; }
        public double RecentAccuracy { get; set; }
        public double ResponseTimeToPersonalMedianRatio { get; set; }
    }

    public sealed class PersonalBehaviorBaseline
    {
        public double Accuracy { get; set; }
        public double MedianResponseMs { get; set; }
        public int SampleCount { get; set; }
    }
}
