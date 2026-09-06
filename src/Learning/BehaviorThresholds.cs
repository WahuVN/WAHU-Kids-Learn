namespace WAHU.Learning
{
    public sealed class BehaviorThresholds
    {
        public int RecentAttemptWindow { get; set; } = 8;
        public int MinimumEvidenceAttempts { get; set; } = 4;
        public int MinimumDistinctSkillsForFatigue { get; set; } = 2;
        public int StateEnterConsecutiveEvaluations { get; set; } = 2;
        public int StateExitConsecutiveEvaluations { get; set; } = 1;
        public int StateChangeCooldownQuestions { get; set; } = 1;

        public double FlowAccuracyMin { get; set; } = 0.70;
        public double FlowAccuracyMax { get; set; } = 0.90;
        public double FlowMaxHintRate { get; set; } = 0.25;
        public double FlowMaxRapidWrongRate { get; set; } = 0.10;
        public double FlowResponseRatioMin { get; set; } = 0.70;
        public double FlowResponseRatioMax { get; set; } = 1.35;

        public double BoredAccuracyMin { get; set; } = 0.92;
        public double BoredMasteryMin { get; set; } = 0.80;
        public double BoredFastResponseRatioMax { get; set; } = 0.75;
        public int BoredSameRepresentationCountMin { get; set; } = 4;

        public int StrainedRecentErrorCountMin { get; set; } = 2;
        public double StrainedResponseRatioMin { get; set; } = 1.35;
        public double StrainedHintRateMin { get; set; } = 0.35;

        public int FrustratedSameErrorPatternCountMin { get; set; } = 2;
        public int FrustratedMaxHintEventsRecentMin { get; set; } = 1;
        public double FrustratedRapidWrongRateMin { get; set; } = 0.25;
        public int FrustratedSkipOrExitSignalMin { get; set; } = 1;
        public double FrustratedResponseVariabilityRatioMin { get; set; } = 1.60;

        public double FatigueAccuracyDropMin { get; set; } = 0.18;
        public double FatigueResponseRatioMin { get; set; } = 1.45;
        public double FatigueInputMissRateMin { get; set; } = 0.12;
        public double FatigueFocusMinutesSoftSignal { get; set; } = 18.0;

        public int MaxConsecutiveTargetFailuresBeforeRepair { get; set; } = 2;
    }
}
