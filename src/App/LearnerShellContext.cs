using WAHU.Data;
using WAHU.Performance;
using WAHU.Platform;

namespace WAHUKidsLearn
{
    internal sealed class LearnerShellContext
    {
        public RuntimeConfigBundle Config { get; set; }
        public LearningDatabase LearningDatabase { get; set; }
        public PreflightReport Preflight { get; set; }
        public DatabaseBootstrapResult Database { get; set; }
        public RuntimeBootstrapIssue Issue { get; set; }
        public bool PreviousRunUnclean { get; set; }
        public RuntimePerformanceSettings Performance { get; set; }
        public string RequestedLessonId { get; set; }
        public MathQuickRescueEventPresentation RequestedEvent { get; set; }

        public bool IsLearnerReady
        {
            get
            {
                return (Issue == null || Issue.Kind == RuntimeIssueKind.None) &&
                    Database != null && Database.Health != null && Database.Health.IsHealthy;
            }
        }
    }
}
