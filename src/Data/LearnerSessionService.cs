using System;
using System.Collections.Generic;
using System.Globalization;
using WAHU.Learning;

namespace WAHU.Data
{
    public sealed class LearnerProfile
    {
        public string ChildId { get; set; }
        public string DisplayName { get; set; }
        public int GradeLevel { get; set; }
    }

    public sealed class LearnerSessionHandle
    {
        public string SessionId { get; set; }
        public string ChildId { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public string Subject { get; set; }
        public string PerformanceProfile { get; set; }
    }

    public sealed class LearnerSessionService
    {
        public const string PrimaryChildId = "child-primary";
        private readonly LearningDatabase _database;

        public LearnerSessionService(LearningDatabase database)
        {
            _database = database ?? throw new ArgumentNullException("database");
        }

        public LearnerProfile EnsurePrimaryChild(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) displayName = "Bé học";
            var now = DateTime.UtcNow.ToString("o");
            _database.Writes.Execute((connection, transaction) =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"INSERT OR IGNORE INTO child(id,display_name,grade_level,created_at_utc,updated_at_utc)
VALUES(@id,@name,2,@utc,@utc);";
                    command.Parameters.AddWithValue("@id", PrimaryChildId);
                    command.Parameters.AddWithValue("@name", displayName.Trim());
                    command.Parameters.AddWithValue("@utc", now);
                    command.ExecuteNonQuery();
                }
                using (var setting = connection.CreateCommand())
                {
                    setting.Transaction = transaction;
                    setting.CommandText = @"INSERT OR IGNORE INTO child_setting(child_id,settings_json,updated_at_utc)
VALUES(@id,'{}',@utc);";
                    setting.Parameters.AddWithValue("@id", PrimaryChildId);
                    setting.Parameters.AddWithValue("@utc", now);
                    setting.ExecuteNonQuery();
                }
            });

            using (var connection = _database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id,display_name,grade_level FROM child WHERE id=@id;";
                command.Parameters.AddWithValue("@id", PrimaryChildId);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("Primary learner profile was not created.");
                    return new LearnerProfile
                    {
                        ChildId = Convert.ToString(reader[0], CultureInfo.InvariantCulture),
                        DisplayName = Convert.ToString(reader[1], CultureInfo.InvariantCulture),
                        GradeLevel = Convert.ToInt32(reader[2], CultureInfo.InvariantCulture)
                    };
                }
            }
        }

        public int RecoverDanglingSessions()
        {
            return _database.Writes.Execute((connection, transaction) =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"UPDATE session
SET state='recovered', ended_at_utc=@utc,
    summary_json=COALESCE(summary_json,'{""reason"":""unclean_previous_runtime""}')
WHERE planned_subject='math'
  AND state IN ('started','active') AND ended_at_utc IS NULL
  AND NOT EXISTS (SELECT 1 FROM math_session_runtime r WHERE r.session_id=session.id);";
                    command.Parameters.AddWithValue("@utc", DateTime.UtcNow.ToString("o"));
                    return command.ExecuteNonQuery();
                }
            });
        }

        public LearnerSessionHandle BeginSession(string childId, string subject, string performanceProfile)
        {
            Require(childId, "childId");
            if (subject != "math" && subject != "english" && subject != "mixed") throw new ArgumentException("Invalid subject.");
            if (performanceProfile != "LOW" && performanceProfile != "NORMAL") performanceProfile = "LOW";
            var id = "session-" + Guid.NewGuid().ToString("N");
            var started = DateTime.UtcNow;
            _database.Writes.Execute((connection, transaction) =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"INSERT INTO session(id,child_id,started_at_utc,state,planned_subject,performance_profile)
SELECT @id,@child,@utc,'active',@subject,@profile
WHERE NOT EXISTS (
    SELECT 1 FROM session
    WHERE child_id=@child AND planned_subject=@subject
      AND state IN ('started','active') AND ended_at_utc IS NULL
);";
                    command.Parameters.AddWithValue("@id", id);
                    command.Parameters.AddWithValue("@child", childId);
                    command.Parameters.AddWithValue("@utc", started.ToString("o"));
                    command.Parameters.AddWithValue("@subject", subject);
                    command.Parameters.AddWithValue("@profile", performanceProfile);
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("An active session for this child and subject already exists.");
                }
            });
            return new LearnerSessionHandle { SessionId = id, ChildId = childId, StartedAtUtc = started, Subject = subject, PerformanceProfile = performanceProfile };
        }

        public void CompleteSession(string sessionId, bool aborted, string summaryJson, string behaviorSummaryJson)
        {
            Require(sessionId, "sessionId");
            _database.Writes.Execute((connection, transaction) =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"UPDATE session
SET ended_at_utc=@utc,state=@state,summary_json=@summary,behavior_summary_json=@behavior
WHERE id=@id AND state IN ('started','active');";
                    command.Parameters.AddWithValue("@utc", DateTime.UtcNow.ToString("o"));
                    command.Parameters.AddWithValue("@state", aborted ? "aborted" : "completed");
                    command.Parameters.AddWithValue("@summary", string.IsNullOrWhiteSpace(summaryJson) ? (object)DBNull.Value : summaryJson);
                    command.Parameters.AddWithValue("@behavior", string.IsNullOrWhiteSpace(behaviorSummaryJson) ? (object)DBNull.Value : behaviorSummaryJson);
                    command.Parameters.AddWithValue("@id", sessionId);
                    if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Session is not active or does not exist.");
                }
            });
        }

        public IDictionary<string, SkillSnapshot> LoadSkillSnapshots(string childId, string subject)
        {
            Require(childId, "childId");
            if (subject != "math" && subject != "english") throw new ArgumentException("Invalid subject.");
            var result = new Dictionary<string, SkillSnapshot>(StringComparer.Ordinal);
            using (var connection = _database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT skill_id,mastery_score,confidence,attempts_count,independent_success_count,
hinted_success_count,transfer_success_count,last_seen_at_utc,last_success_at_utc,next_review_at_utc,learning_state
FROM child_skill WHERE child_id=@child AND subject=@subject;";
                command.Parameters.AddWithValue("@child", childId);
                command.Parameters.AddWithValue("@subject", subject);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var snapshot = new SkillSnapshot
                        {
                            SkillId = Convert.ToString(reader[0], CultureInfo.InvariantCulture),
                            MasteryScore = Convert.ToDouble(reader[1], CultureInfo.InvariantCulture),
                            Confidence = Convert.ToDouble(reader[2], CultureInfo.InvariantCulture),
                            AttemptsCount = Convert.ToInt32(reader[3], CultureInfo.InvariantCulture),
                            IndependentSuccessCount = Convert.ToInt32(reader[4], CultureInfo.InvariantCulture),
                            HintedSuccessCount = Convert.ToInt32(reader[5], CultureInfo.InvariantCulture),
                            TransferSuccessCount = Convert.ToInt32(reader[6], CultureInfo.InvariantCulture),
                            LastSeenAtUtc = ReadNullableUtc(reader[7]),
                            LastSuccessAtUtc = ReadNullableUtc(reader[8]),
                            NextReviewAtUtc = ReadNullableUtc(reader[9]),
                            LearningState = Convert.ToString(reader[10], CultureInfo.InvariantCulture)
                        };
                        result[snapshot.SkillId] = snapshot;
                    }
                }
            }
            return result;
        }

        private static DateTime? ReadNullableUtc(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            DateTime parsed;
            if (!DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed)) return null;
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        private static void Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(name + " is required.");
        }
    }
}
