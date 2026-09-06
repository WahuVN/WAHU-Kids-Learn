using System;
using System.Collections.Generic;
using System.Linq;
using WAHU.Content;
using WAHU.Data;
using WAHU.Learning;

namespace WAHU.Session
{
    public sealed class MathLessonAccessSnapshot
    {
        public string LessonId { get; set; }
        public string SkillId { get; set; }
        public string TitleVi { get; set; }
        public bool IsUnlocked { get; set; }
        public bool IsCompleted { get; set; }
        public int StartedCount { get; set; }
        public int CompletedCount { get; set; }
        public double? LastScorePercent { get; set; }
        public double? BestScorePercent { get; set; }
        public IList<string> PrerequisiteLessonIds { get; set; }
        public IList<string> UnsatisfiedPrerequisiteLessonIds { get; set; }
    }

    public sealed class MathLessonLockedException : InvalidOperationException
    {
        public string LessonId { get; private set; }
        public IList<string> UnsatisfiedPrerequisiteLessonIds { get; private set; }

        public MathLessonLockedException(MathLessonAccessSnapshot access)
            : base("Math lesson is locked by incomplete prerequisites: " + (access == null ? "unknown" : access.LessonId))
        {
            LessonId = access == null ? null : access.LessonId;
            UnsatisfiedPrerequisiteLessonIds = access == null || access.UnsatisfiedPrerequisiteLessonIds == null
                ? new List<string>() : access.UnsatisfiedPrerequisiteLessonIds.ToList();
        }
    }

    public sealed class MathLessonProgressService
    {
        private readonly LearningDatabase _database;
        private readonly MathLessonCatalogSnapshot _catalog;
        private readonly MathLessonProgressStore _store;
        private readonly LearnerSessionService _sessions;

        public MathLessonProgressService(LearningDatabase database, string catalogPath)
        {
            _database = database ?? throw new ArgumentNullException("database");
            _catalog = new MathLessonCatalogSource().Load(catalogPath);
            _store = new MathLessonProgressStore(database);
            _sessions = new LearnerSessionService(database);
        }

        public MathLessonCatalogSnapshot Catalog { get { return _catalog; } }

        public MathLessonAccessSnapshot GetAccess(string childId, string lessonId)
        {
            if (string.IsNullOrWhiteSpace(childId)) throw new ArgumentException("childId");
            if (string.IsNullOrWhiteSpace(lessonId)) throw new ArgumentException("lessonId");
            var lesson = _catalog.FindLesson(lessonId);
            if (lesson == null) throw new ArgumentOutOfRangeException("lessonId", "Unknown Math lesson: " + lessonId);
            var progress = _store.Load(childId);
            var skills = _sessions.LoadSkillSnapshots(childId, "math");
            return BuildAccess(lesson, progress, skills);
        }

        public IList<MathLessonAccessSnapshot> GetAllAccess(string childId)
        {
            if (string.IsNullOrWhiteSpace(childId)) throw new ArgumentException("childId");
            var progress = _store.Load(childId);
            var skills = _sessions.LoadSkillSnapshots(childId, "math");
            return (_catalog.Lessons ?? new List<MathLessonDescriptor>())
                .Select(x => BuildAccess(x, progress, skills))
                .ToList();
        }

        public MathLessonAccessSnapshot GetNextLessonAccess(string childId, string lessonId)
        {
            if (string.IsNullOrWhiteSpace(childId)) throw new ArgumentException("childId");
            if (string.IsNullOrWhiteSpace(lessonId)) throw new ArgumentException("lessonId");
            var lessons = _catalog.Lessons ?? new List<MathLessonDescriptor>();
            var currentIndex = -1;
            for (var i = 0; i < lessons.Count; i++)
            {
                if (string.Equals(lessons[i].Id, lessonId, StringComparison.Ordinal))
                {
                    currentIndex = i;
                    break;
                }
            }
            if (currentIndex < 0) throw new ArgumentOutOfRangeException("lessonId", "Unknown Math lesson: " + lessonId);
            if (currentIndex + 1 >= lessons.Count) return null;

            var progress = _store.Load(childId);
            var skills = _sessions.LoadSkillSnapshots(childId, "math");
            return BuildAccess(lessons[currentIndex + 1], progress, skills);
        }

        private MathLessonAccessSnapshot BuildAccess(
            MathLessonDescriptor lesson,
            IDictionary<string, MathLessonProgressRecord> progress,
            IDictionary<string, SkillSnapshot> skills)
        {
            MathLessonProgressRecord own;
            progress.TryGetValue(lesson.Id, out own);
            var prerequisiteLessonIds = new List<string>();
            var unsatisfied = new List<string>();
            foreach (var prerequisiteSkill in lesson.PrerequisiteSkills ?? new List<string>())
            {
                var prerequisiteLesson = _catalog.FindLessonBySkill(prerequisiteSkill);
                if (prerequisiteLesson == null)
                    throw new InvalidOperationException("Catalog prerequisite has no lesson: " + prerequisiteSkill);
                prerequisiteLessonIds.Add(prerequisiteLesson.Id);

                MathLessonProgressRecord prerequisiteProgress;
                SkillSnapshot prerequisiteMastery;
                var completed = progress.TryGetValue(prerequisiteLesson.Id, out prerequisiteProgress) &&
                                prerequisiteProgress != null && prerequisiteProgress.CompletedCount > 0;
                var stableLegacyMastery = skills.TryGetValue(prerequisiteSkill, out prerequisiteMastery) &&
                                          prerequisiteMastery != null &&
                                          string.Equals(prerequisiteMastery.LearningState, "STABLE", StringComparison.OrdinalIgnoreCase);
                if (!completed && !stableLegacyMastery) unsatisfied.Add(prerequisiteLesson.Id);
            }

            return new MathLessonAccessSnapshot
            {
                LessonId = lesson.Id,
                SkillId = lesson.SkillId,
                TitleVi = lesson.TitleVi,
                IsUnlocked = unsatisfied.Count == 0,
                IsCompleted = own != null && own.CompletedCount > 0,
                StartedCount = own == null ? 0 : own.StartedCount,
                CompletedCount = own == null ? 0 : own.CompletedCount,
                LastScorePercent = own == null ? null : own.LastScorePercent,
                BestScorePercent = own == null ? null : own.BestScorePercent,
                PrerequisiteLessonIds = prerequisiteLessonIds,
                UnsatisfiedPrerequisiteLessonIds = unsatisfied
            };
        }
    }
}
