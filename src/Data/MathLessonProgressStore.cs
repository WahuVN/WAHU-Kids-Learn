using System;
using System.Collections.Generic;
using System.Globalization;

namespace WAHU.Data
{
    public sealed class MathLessonProgressRecord
    {
        public string ChildId { get; set; }
        public string LessonId { get; set; }
        public string SkillId { get; set; }
        public int StartedCount { get; set; }
        public int CompletedCount { get; set; }
        public double? LastScorePercent { get; set; }
        public double? BestScorePercent { get; set; }
        public DateTime? LastStartedAtUtc { get; set; }
        public DateTime? LastCompletedAtUtc { get; set; }
    }

    public sealed class MathLessonProgressStore
    {
        private readonly LearningDatabase _database;

        internal const string UntrustedCompletionOnStartPredicate = @"math_lesson_progress.completed_count>math_lesson_progress.started_count
    OR math_lesson_progress.skill_id<>@skill
    OR (math_lesson_progress.completed_count>0 AND (
        math_lesson_progress.last_started_at_utc IS NULL OR math_lesson_progress.last_completed_at_utc IS NULL
        OR julianday(math_lesson_progress.last_started_at_utc) IS NULL OR julianday(math_lesson_progress.last_completed_at_utc) IS NULL
        OR (math_lesson_progress.completed_count=math_lesson_progress.started_count
            AND julianday(math_lesson_progress.last_completed_at_utc)<julianday(math_lesson_progress.last_started_at_utc))))
    OR (math_lesson_progress.completed_count=0 AND (
        math_lesson_progress.last_score_percent IS NOT NULL OR math_lesson_progress.best_score_percent IS NOT NULL
        OR math_lesson_progress.last_completed_at_utc IS NOT NULL))";

        public MathLessonProgressStore(LearningDatabase database)
        {
            _database = database ?? throw new ArgumentNullException("database");
        }

        public void MarkStarted(string childId, string lessonId, string skillId, DateTime startedAtUtc)
        {
            Require(childId, "childId");
            Require(lessonId, "lessonId");
            Require(skillId, "skillId");
            var utc = Utc(startedAtUtc);
            _database.Writes.Execute((connection, transaction) =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"INSERT INTO math_lesson_progress(
child_id,lesson_id,skill_id,started_count,completed_count,last_started_at_utc,updated_at_utc)
VALUES(@child,@lesson,@skill,1,0,@utc,@utc)
ON CONFLICT(child_id,lesson_id) DO UPDATE SET
skill_id=excluded.skill_id,
started_count=math_lesson_progress.started_count+1,
completed_count=CASE WHEN " + UntrustedCompletionOnStartPredicate + @" THEN 0 ELSE math_lesson_progress.completed_count END,
last_score_percent=CASE WHEN " + UntrustedCompletionOnStartPredicate + @" THEN NULL ELSE math_lesson_progress.last_score_percent END,
best_score_percent=CASE WHEN " + UntrustedCompletionOnStartPredicate + @" THEN NULL
    WHEN math_lesson_progress.last_score_percent IS NOT NULL AND
         (math_lesson_progress.best_score_percent IS NULL OR math_lesson_progress.best_score_percent<math_lesson_progress.last_score_percent)
    THEN math_lesson_progress.last_score_percent
    ELSE math_lesson_progress.best_score_percent END,
last_completed_at_utc=CASE WHEN " + UntrustedCompletionOnStartPredicate + @" THEN NULL ELSE math_lesson_progress.last_completed_at_utc END,
last_started_at_utc=excluded.last_started_at_utc,
updated_at_utc=excluded.updated_at_utc;";
                    command.Parameters.AddWithValue("@child", childId);
                    command.Parameters.AddWithValue("@lesson", lessonId);
                    command.Parameters.AddWithValue("@skill", skillId);
                    command.Parameters.AddWithValue("@utc", utc);
                    command.ExecuteNonQuery();
                }
            });
        }

        public void ReconcileResumedTargetedSession(
            string childId,
            string lessonId,
            string skillId,
            DateTime sessionStartedAtUtc)
        {
            Require(childId, "childId");
            Require(lessonId, "lessonId");
            Require(skillId, "skillId");
            var startedUtc = Utc(sessionStartedAtUtc);
            var updatedUtc = Utc(DateTime.UtcNow);
            _database.Writes.Execute((connection, transaction) =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"INSERT INTO math_lesson_progress(
child_id,lesson_id,skill_id,started_count,completed_count,last_started_at_utc,updated_at_utc)
VALUES(@child,@lesson,@skill,1,0,@started,@updated)
ON CONFLICT(child_id,lesson_id) DO UPDATE SET
skill_id=@skill,
started_count=CASE WHEN math_lesson_progress.started_count<1 THEN 1 ELSE math_lesson_progress.started_count END,
completed_count=CASE WHEN " + UntrustedCompletionOnStartPredicate + @" THEN 0 ELSE math_lesson_progress.completed_count END,
last_score_percent=CASE WHEN " + UntrustedCompletionOnStartPredicate + @" THEN NULL ELSE math_lesson_progress.last_score_percent END,
best_score_percent=CASE WHEN " + UntrustedCompletionOnStartPredicate + @" THEN NULL
    WHEN math_lesson_progress.last_score_percent IS NOT NULL AND
         (math_lesson_progress.best_score_percent IS NULL OR math_lesson_progress.best_score_percent<math_lesson_progress.last_score_percent)
    THEN math_lesson_progress.last_score_percent
    ELSE math_lesson_progress.best_score_percent END,
last_completed_at_utc=CASE WHEN " + UntrustedCompletionOnStartPredicate + @" THEN NULL ELSE math_lesson_progress.last_completed_at_utc END,
last_started_at_utc=@started,
updated_at_utc=@updated;";
                    command.Parameters.AddWithValue("@child", childId);
                    command.Parameters.AddWithValue("@lesson", lessonId);
                    command.Parameters.AddWithValue("@skill", skillId);
                    command.Parameters.AddWithValue("@started", startedUtc);
                    command.Parameters.AddWithValue("@updated", updatedUtc);
                    command.ExecuteNonQuery();
                }
            });
        }

        public MathLessonProgressRecord CompleteActiveSession(
            string sessionId,
            string childId,
            string lessonId,
            string skillId,
            string packId,
            string packVersion,
            int correct,
            int attempts,
            string summaryJson,
            string behaviorSummaryJson,
            DateTime completedAtUtc)
        {
            Require(sessionId, "sessionId");
            Require(childId, "childId");
            Require(lessonId, "lessonId");
            Require(skillId, "skillId");
            Require(packId, "packId");
            Require(packVersion, "packVersion");
            if (attempts < 1) throw new ArgumentOutOfRangeException("attempts");
            if (correct < 0 || correct > attempts) throw new ArgumentOutOfRangeException("correct");
            var score = 100.0 * correct / attempts;
            var utc = Utc(completedAtUtc);

            _database.Writes.Execute((connection, transaction) =>
            {
                using (var session = connection.CreateCommand())
                {
                    session.Transaction = transaction;
                    session.CommandText = @"UPDATE session
SET ended_at_utc=@utc,state='completed',summary_json=@summary,behavior_summary_json=@behavior
WHERE id=@id AND child_id=@child AND planned_subject='math'
  AND state IN ('started','active') AND ended_at_utc IS NULL
  AND EXISTS (
      SELECT 1 FROM math_session_runtime r
      WHERE r.session_id=@id AND r.session_mode='lesson'
        AND r.target_lesson_id=@lesson AND r.target_question_count=@attempts
        AND r.pack_id=@packId AND r.pack_version=@packVersion
  );";
                    session.Parameters.AddWithValue("@utc", utc);
                    session.Parameters.AddWithValue("@summary", string.IsNullOrWhiteSpace(summaryJson) ? (object)DBNull.Value : summaryJson);
                    session.Parameters.AddWithValue("@behavior", string.IsNullOrWhiteSpace(behaviorSummaryJson) ? (object)DBNull.Value : behaviorSummaryJson);
                    session.Parameters.AddWithValue("@id", sessionId);
                    session.Parameters.AddWithValue("@child", childId);
                    session.Parameters.AddWithValue("@lesson", lessonId);
                    session.Parameters.AddWithValue("@attempts", attempts);
                    session.Parameters.AddWithValue("@packId", packId);
                    session.Parameters.AddWithValue("@packVersion", packVersion);
                    if (session.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("Targeted Math session is not active or does not exist.");
                }

                using (var progress = connection.CreateCommand())
                {
                    progress.Transaction = transaction;

                    progress.CommandText = @"UPDATE math_lesson_progress
SET completed_count=math_lesson_progress.completed_count+1,
    last_score_percent=@score,
    best_score_percent=CASE
        WHEN math_lesson_progress.best_score_percent IS NULL
          OR @score > math_lesson_progress.best_score_percent
          OR (math_lesson_progress.last_score_percent IS NOT NULL
              AND math_lesson_progress.last_score_percent > math_lesson_progress.best_score_percent)
        THEN CASE WHEN math_lesson_progress.last_score_percent IS NOT NULL
                       AND math_lesson_progress.last_score_percent > @score
                  THEN math_lesson_progress.last_score_percent ELSE @score END
        ELSE math_lesson_progress.best_score_percent END,
    last_completed_at_utc=@utc,
    updated_at_utc=@utc
WHERE child_id=@child AND lesson_id=@lesson
  AND skill_id=@skill
  AND started_count>completed_count
  AND last_started_at_utc IS NOT NULL
  AND julianday(last_started_at_utc) IS NOT NULL
  AND julianday(last_started_at_utc)=julianday((
      SELECT started_at_utc FROM session WHERE id=@id AND child_id=@child
  ))
  AND NOT (" + UntrustedCompletionOnStartPredicate + @");";
                    progress.Parameters.AddWithValue("@id", sessionId);

                    progress.Parameters.AddWithValue("@child", childId);
                    progress.Parameters.AddWithValue("@lesson", lessonId);
                    progress.Parameters.AddWithValue("@skill", skillId);
                    progress.Parameters.AddWithValue("@score", score);
                    progress.Parameters.AddWithValue("@utc", utc);
                    if (progress.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("Targeted Math lesson progress is not a trusted pending completion.");
                }
            });

            return LoadOne(childId, lessonId);
        }

        public MathLessonProgressRecord LoadTrustedCompletionEvidence(string childId, string lessonId, string expectedSkillId)
        {
            Require(childId, "childId");
            Require(lessonId, "lessonId");
            Require(expectedSkillId, "expectedSkillId");
            var progress = LoadOne(childId, lessonId);
            if (!HasTrustedCompletionEvidence(progress, expectedSkillId)) return null;

            progress.LastScorePercent = IsValidScorePercent(progress.LastScorePercent) ? progress.LastScorePercent : null;
            progress.BestScorePercent = IsValidScorePercent(progress.BestScorePercent) ? progress.BestScorePercent : null;
            if (progress.LastScorePercent.HasValue &&
                (!progress.BestScorePercent.HasValue || progress.LastScorePercent.Value > progress.BestScorePercent.Value))
                progress.BestScorePercent = progress.LastScorePercent;
            return progress;
        }

        public double? LoadTrustedBestScorePercent(string childId, string lessonId, string expectedSkillId)
        {
            var progress = LoadTrustedCompletionEvidence(childId, lessonId, expectedSkillId);
            return progress == null ? (double?)null : progress.BestScorePercent;
        }

        private static bool HasTrustedCompletionEvidence(MathLessonProgressRecord progress, string expectedSkillId)
        {
            if (progress == null || progress.CompletedCount <= 0) return false;
            if (!string.Equals(progress.SkillId, expectedSkillId, StringComparison.Ordinal)) return false;
            if (progress.StartedCount <= 0 || progress.CompletedCount > progress.StartedCount) return false;
            if (!progress.LastStartedAtUtc.HasValue || !progress.LastCompletedAtUtc.HasValue) return false;
            if (progress.CompletedCount == progress.StartedCount &&
                progress.LastCompletedAtUtc.Value < progress.LastStartedAtUtc.Value) return false;
            return true;
        }

        private static bool IsValidScorePercent(double? value)
        {
            return value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value) &&
                   value.Value >= 0.0 && value.Value <= 100.0;
        }

        public IDictionary<string, MathLessonProgressRecord> Load(string childId)
        {
            Require(childId, "childId");
            var result = new Dictionary<string, MathLessonProgressRecord>(StringComparer.Ordinal);
            using (var connection = _database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT child_id,lesson_id,skill_id,started_count,completed_count,
last_score_percent,best_score_percent,last_started_at_utc,last_completed_at_utc
FROM math_lesson_progress WHERE child_id=@child;";
                command.Parameters.AddWithValue("@child", childId);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var record = Read(reader);
                        result[record.LessonId] = record;
                    }
                }
            }
            return result;
        }

        public MathLessonProgressRecord LoadOne(string childId, string lessonId)
        {
            Require(childId, "childId");
            Require(lessonId, "lessonId");
            using (var connection = _database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT child_id,lesson_id,skill_id,started_count,completed_count,
last_score_percent,best_score_percent,last_started_at_utc,last_completed_at_utc
FROM math_lesson_progress WHERE child_id=@child AND lesson_id=@lesson;";
                command.Parameters.AddWithValue("@child", childId);
                command.Parameters.AddWithValue("@lesson", lessonId);
                using (var reader = command.ExecuteReader())
                    return reader.Read() ? Read(reader) : null;
            }
        }

        private static MathLessonProgressRecord Read(System.Data.SQLite.SQLiteDataReader reader)
        {
            return new MathLessonProgressRecord
            {
                ChildId = Text(reader[0]),
                LessonId = Text(reader[1]),
                SkillId = Text(reader[2]),
                StartedCount = Convert.ToInt32(reader[3], CultureInfo.InvariantCulture),
                CompletedCount = Convert.ToInt32(reader[4], CultureInfo.InvariantCulture),
                LastScorePercent = NullableDouble(reader[5]),
                BestScorePercent = NullableDouble(reader[6]),
                LastStartedAtUtc = ReadNullableUtc(reader[7]),
                LastCompletedAtUtc = ReadNullableUtc(reader[8])
            };
        }

        private static double? NullableDouble(object value)
        {
            return value == null || value == DBNull.Value ? (double?)null : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static DateTime? ReadNullableUtc(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            DateTime parsed;
            if (!DateTime.TryParse(Text(value), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed)) return null;
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        private static string Text(object value) { return Convert.ToString(value, CultureInfo.InvariantCulture); }

        private static string Utc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Local) value = value.ToUniversalTime();
            else if (value.Kind == DateTimeKind.Unspecified) value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return value.ToString("o", CultureInfo.InvariantCulture);
        }

        private static void Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(name + " is required.");
        }
    }
}
