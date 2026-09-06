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

        public MathLessonProgressRecord CompleteActiveSession(
            string sessionId,
            string childId,
            string lessonId,
            string skillId,
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
WHERE id=@id AND child_id=@child AND state IN ('started','active') AND ended_at_utc IS NULL;";
                    session.Parameters.AddWithValue("@utc", utc);
                    session.Parameters.AddWithValue("@summary", string.IsNullOrWhiteSpace(summaryJson) ? (object)DBNull.Value : summaryJson);
                    session.Parameters.AddWithValue("@behavior", string.IsNullOrWhiteSpace(behaviorSummaryJson) ? (object)DBNull.Value : behaviorSummaryJson);
                    session.Parameters.AddWithValue("@id", sessionId);
                    session.Parameters.AddWithValue("@child", childId);
                    if (session.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("Targeted Math session is not active or does not exist.");
                }

                using (var progress = connection.CreateCommand())
                {
                    progress.Transaction = transaction;
                    progress.CommandText = @"INSERT INTO math_lesson_progress(
child_id,lesson_id,skill_id,started_count,completed_count,last_score_percent,best_score_percent,last_started_at_utc,last_completed_at_utc,updated_at_utc)
VALUES(@child,@lesson,@skill,1,1,@score,@score,@utc,@utc,@utc)
ON CONFLICT(child_id,lesson_id) DO UPDATE SET
skill_id=excluded.skill_id,
completed_count=math_lesson_progress.completed_count+1,
last_score_percent=excluded.last_score_percent,
best_score_percent=CASE
    WHEN math_lesson_progress.best_score_percent IS NULL OR excluded.best_score_percent > math_lesson_progress.best_score_percent
    THEN excluded.best_score_percent ELSE math_lesson_progress.best_score_percent END,
last_completed_at_utc=excluded.last_completed_at_utc,
updated_at_utc=excluded.updated_at_utc;";
                    progress.Parameters.AddWithValue("@child", childId);
                    progress.Parameters.AddWithValue("@lesson", lessonId);
                    progress.Parameters.AddWithValue("@skill", skillId);
                    progress.Parameters.AddWithValue("@score", score);
                    progress.Parameters.AddWithValue("@utc", utc);
                    progress.ExecuteNonQuery();
                }
            });

            return LoadOne(childId, lessonId);
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
