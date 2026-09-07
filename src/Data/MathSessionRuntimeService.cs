using System;
using System.Collections.Generic;
using System.Globalization;

namespace WAHU.Data
{
    public sealed class MathSessionRuntimeSnapshot
    {
        public string SessionId { get; set; }
        public string ChildId { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public string PerformanceProfile { get; set; }
        public int Seed { get; set; }
        public int TargetQuestionCount { get; set; }
        public int GeneratedQuestionCount { get; set; }
        public string SessionMode { get; set; }
        public string TargetLessonId { get; set; }
        public string CurrentQuestionJson { get; set; }
        public string CurrentSelectionJson { get; set; }
        public DateTime? QuestionStartedAtUtc { get; set; }
        public string ForcedRepairTemplateId { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public sealed class MathCommittedAttemptSnapshot
    {
        public string AttemptId { get; set; }
        public string QuestionId { get; set; }
        public string SkillId { get; set; }
        public bool IsCorrect { get; set; }
        public int ResponseMs { get; set; }
        public int HintLevel { get; set; }
        public int AttemptIndex { get; set; }
        public string Representation { get; set; }
        public DateTime AnsweredAtUtc { get; set; }
        public double MasteryScoreBefore { get; set; }
        public double? MasteryScoreAfter { get; set; }
        public double? MasteryDelta { get; set; }
        public string ErrorType { get; set; }
    }

    public sealed class MathSessionRuntimeService
    {
        private readonly LearningDatabase _database;

        public MathSessionRuntimeService(LearningDatabase database)
        {
            _database = database ?? throw new ArgumentNullException("database");
        }

        public LearnerSessionHandle TryCreateSession(
            string childId,
            string performanceProfile,
            int seed,
            int targetQuestionCount,
            string sessionMode,
            string targetLessonId,
            string targetSkillId = null)
        {
            Require(childId, "childId");
            if (targetQuestionCount < 1 || targetQuestionCount > 40) throw new ArgumentOutOfRangeException("targetQuestionCount");
            if (sessionMode != "adaptive" && sessionMode != "lesson") throw new ArgumentException("Invalid Math session mode.");
            if (sessionMode == "lesson")
            {
                Require(targetLessonId, "targetLessonId");
                Require(targetSkillId, "targetSkillId");
            }
            else
            {
                targetLessonId = null;
                targetSkillId = null;
            }
            if (performanceProfile != "LOW" && performanceProfile != "NORMAL") performanceProfile = "LOW";

            var sessionId = "session-" + Guid.NewGuid().ToString("N");
            var startedAtUtc = DateTime.UtcNow;
            return _database.Writes.Execute((connection, transaction) =>
            {
                using (var session = connection.CreateCommand())
                {
                    session.Transaction = transaction;
                    session.CommandText = @"INSERT INTO session(id,child_id,started_at_utc,state,planned_subject,performance_profile)
SELECT @id,@child,@started,'active','math',@profile
WHERE NOT EXISTS (
    SELECT 1 FROM session
    WHERE child_id=@child AND planned_subject='math'
      AND state IN ('started','active') AND ended_at_utc IS NULL
);";
                    session.Parameters.AddWithValue("@id", sessionId);
                    session.Parameters.AddWithValue("@child", childId);
                    session.Parameters.AddWithValue("@started", Utc(startedAtUtc));
                    session.Parameters.AddWithValue("@profile", performanceProfile);
                    if (session.ExecuteNonQuery() != 1) return null;
                }

                using (var runtime = connection.CreateCommand())
                {
                    runtime.Transaction = transaction;
                    runtime.CommandText = @"INSERT INTO math_session_runtime(
session_id,seed,target_question_count,generated_question_count,session_mode,target_lesson_id,updated_at_utc)
VALUES(@session,@seed,@target,0,@mode,@lesson,@utc);";
                    runtime.Parameters.AddWithValue("@session", sessionId);
                    runtime.Parameters.AddWithValue("@seed", seed);
                    runtime.Parameters.AddWithValue("@target", targetQuestionCount);
                    runtime.Parameters.AddWithValue("@mode", sessionMode);
                    runtime.Parameters.AddWithValue("@lesson", string.IsNullOrWhiteSpace(targetLessonId) ? (object)DBNull.Value : targetLessonId);
                    runtime.Parameters.AddWithValue("@utc", Utc(startedAtUtc));
                    runtime.ExecuteNonQuery();
                }

                if (sessionMode == "lesson")
                {
                    using (var progress = connection.CreateCommand())
                    {
                        progress.Transaction = transaction;
                        progress.CommandText = @"INSERT INTO math_lesson_progress(
child_id,lesson_id,skill_id,started_count,completed_count,last_started_at_utc,updated_at_utc)
VALUES(@child,@lesson,@skill,1,0,@utc,@utc)
ON CONFLICT(child_id,lesson_id) DO UPDATE SET
skill_id=excluded.skill_id,
started_count=math_lesson_progress.started_count+1,
last_started_at_utc=excluded.last_started_at_utc,
updated_at_utc=excluded.updated_at_utc;";
                        progress.Parameters.AddWithValue("@child", childId);
                        progress.Parameters.AddWithValue("@lesson", targetLessonId);
                        progress.Parameters.AddWithValue("@skill", targetSkillId);
                        progress.Parameters.AddWithValue("@utc", Utc(startedAtUtc));
                        progress.ExecuteNonQuery();
                    }
                }

                return new LearnerSessionHandle
                {
                    SessionId = sessionId,
                    ChildId = childId,
                    StartedAtUtc = startedAtUtc,
                    Subject = "math",
                    PerformanceProfile = performanceProfile
                };
            });
        }

        public void Create(string sessionId, int seed, int targetQuestionCount)
        {
            Create(sessionId, seed, targetQuestionCount, "adaptive", null);
        }

        public void Create(string sessionId, int seed, int targetQuestionCount, string sessionMode, string targetLessonId)
        {
            Require(sessionId, "sessionId");
            if (targetQuestionCount < 1 || targetQuestionCount > 40) throw new ArgumentOutOfRangeException("targetQuestionCount");
            if (sessionMode != "adaptive" && sessionMode != "lesson") throw new ArgumentException("Invalid Math session mode.");
            if (sessionMode == "lesson") Require(targetLessonId, "targetLessonId");
            else targetLessonId = null;
            _database.Writes.Execute((connection, transaction) =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"INSERT INTO math_session_runtime(
session_id,seed,target_question_count,generated_question_count,session_mode,target_lesson_id,updated_at_utc)
VALUES(@session,@seed,@target,0,@mode,@lesson,@utc);";
                    command.Parameters.AddWithValue("@session", sessionId);
                    command.Parameters.AddWithValue("@seed", seed);
                    command.Parameters.AddWithValue("@target", targetQuestionCount);
                    command.Parameters.AddWithValue("@mode", sessionMode);
                    command.Parameters.AddWithValue("@lesson", string.IsNullOrWhiteSpace(targetLessonId) ? (object)DBNull.Value : targetLessonId);
                    command.Parameters.AddWithValue("@utc", Utc(DateTime.UtcNow));
                    command.ExecuteNonQuery();
                }
            });
        }

        public MathSessionRuntimeSnapshot LoadLatestResumable(string childId)
        {
            Require(childId, "childId");
            using (var connection = _database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT s.id,s.child_id,s.started_at_utc,s.performance_profile,
r.seed,r.target_question_count,r.generated_question_count,r.session_mode,r.target_lesson_id,r.current_question_json,
r.current_selection_json,r.question_started_at_utc,r.forced_repair_template_id,r.updated_at_utc
FROM session s
JOIN math_session_runtime r ON r.session_id=s.id
WHERE s.child_id=@child AND s.planned_subject='math'
  AND s.state IN ('started','active') AND s.ended_at_utc IS NULL
ORDER BY r.updated_at_utc DESC,s.started_at_utc DESC
LIMIT 1;";
                command.Parameters.AddWithValue("@child", childId);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new MathSessionRuntimeSnapshot
                    {
                        SessionId = Text(reader[0]),
                        ChildId = Text(reader[1]),
                        StartedAtUtc = ReadUtc(reader[2]),
                        PerformanceProfile = Text(reader[3]),
                        Seed = Convert.ToInt32(reader[4], CultureInfo.InvariantCulture),
                        TargetQuestionCount = Convert.ToInt32(reader[5], CultureInfo.InvariantCulture),
                        GeneratedQuestionCount = Convert.ToInt32(reader[6], CultureInfo.InvariantCulture),
                        SessionMode = Text(reader[7]),
                        TargetLessonId = NullableText(reader[8]),
                        CurrentQuestionJson = NullableText(reader[9]),
                        CurrentSelectionJson = NullableText(reader[10]),
                        QuestionStartedAtUtc = ReadNullableUtc(reader[11]),
                        ForcedRepairTemplateId = NullableText(reader[12]),
                        UpdatedAtUtc = ReadUtc(reader[13])
                    };
                }
            }
        }

        public void SaveOpenQuestion(
            string sessionId,
            int generatedQuestionCount,
            string questionJson,
            string selectionJson,
            DateTime questionStartedAtUtc,
            string forcedRepairTemplateId)
        {
            Require(sessionId, "sessionId");
            if (generatedQuestionCount < 1) throw new ArgumentOutOfRangeException("generatedQuestionCount");
            Require(questionJson, "questionJson");
            Require(selectionJson, "selectionJson");
            UpdateRuntime(sessionId, generatedQuestionCount, questionJson, selectionJson,
                (object)Utc(questionStartedAtUtc), forcedRepairTemplateId);
        }

        public void SaveCheckpoint(string sessionId, int generatedQuestionCount, string forcedRepairTemplateId)
        {
            Require(sessionId, "sessionId");
            if (generatedQuestionCount < 0) throw new ArgumentOutOfRangeException("generatedQuestionCount");
            UpdateRuntime(sessionId, generatedQuestionCount, null, null, DBNull.Value, forcedRepairTemplateId);
        }

        public void ClearOpenQuestion(string sessionId)
        {
            Require(sessionId, "sessionId");
            _database.Writes.Execute((connection, transaction) =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"UPDATE math_session_runtime
SET current_question_json=NULL,current_selection_json=NULL,question_started_at_utc=NULL,updated_at_utc=@utc
WHERE session_id=@session;";
                    command.Parameters.AddWithValue("@session", sessionId);
                    command.Parameters.AddWithValue("@utc", Utc(DateTime.UtcNow));
                    if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Math runtime session does not exist.");
                }
            });
        }

        public void Touch(string sessionId)
        {
            Require(sessionId, "sessionId");
            _database.Writes.Execute((connection, transaction) =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE math_session_runtime SET updated_at_utc=@utc WHERE session_id=@session;";
                    command.Parameters.AddWithValue("@session", sessionId);
                    command.Parameters.AddWithValue("@utc", Utc(DateTime.UtcNow));
                    if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Math runtime session does not exist.");
                }
            });
        }

        public void Delete(string sessionId)
        {
            Require(sessionId, "sessionId");
            _database.Writes.Execute((connection, transaction) =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "DELETE FROM math_session_runtime WHERE session_id=@session;";
                    command.Parameters.AddWithValue("@session", sessionId);
                    command.ExecuteNonQuery();
                }
            });
        }

        public bool HasCommittedAttempt(string sessionId, string questionId, int attemptIndex)
        {
            Require(sessionId, "sessionId");
            Require(questionId, "questionId");
            if (attemptIndex < 1) throw new ArgumentOutOfRangeException("attemptIndex");
            using (var connection = _database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT count(*) FROM attempt_commit_key
WHERE session_id=@session AND question_id=@question AND attempt_index=@attemptIndex;";
                command.Parameters.AddWithValue("@session", sessionId);
                command.Parameters.AddWithValue("@question", questionId);
                command.Parameters.AddWithValue("@attemptIndex", attemptIndex);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
            }
        }

        public IList<MathCommittedAttemptSnapshot> LoadCommittedAttempts(string sessionId)
        {
            Require(sessionId, "sessionId");
            var result = new List<MathCommittedAttemptSnapshot>();
            using (var connection = _database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT a.id,a.question_id,a.skill_id,a.is_correct,a.response_ms,a.hint_level,a.attempt_index,
a.representation,a.answered_at_utc,
COALESCE((SELECT m.score_before FROM mastery_event m WHERE m.attempt_id=a.id ORDER BY m.created_at_utc ASC LIMIT 1),0.25),
(SELECT m.score_after FROM mastery_event m WHERE m.attempt_id=a.id ORDER BY m.created_at_utc ASC LIMIT 1),
(SELECT m.delta FROM mastery_event m WHERE m.attempt_id=a.id ORDER BY m.created_at_utc ASC LIMIT 1),
(SELECT e.error_type FROM error_event e WHERE e.attempt_id=a.id ORDER BY e.created_at_utc ASC LIMIT 1)
FROM attempt a
WHERE a.session_id=@session AND a.subject='math' AND a.answered_at_utc IS NOT NULL
ORDER BY a.answered_at_utc ASC,a.started_at_utc ASC,a.id ASC;";
                command.Parameters.AddWithValue("@session", sessionId);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new MathCommittedAttemptSnapshot
                        {
                            AttemptId = Text(reader[0]),
                            QuestionId = Text(reader[1]),
                            SkillId = Text(reader[2]),
                            IsCorrect = Convert.ToInt32(reader[3], CultureInfo.InvariantCulture) != 0,
                            ResponseMs = reader[4] == DBNull.Value ? 0 : Convert.ToInt32(reader[4], CultureInfo.InvariantCulture),
                            HintLevel = Convert.ToInt32(reader[5], CultureInfo.InvariantCulture),
                            AttemptIndex = Convert.ToInt32(reader[6], CultureInfo.InvariantCulture),
                            Representation = NullableText(reader[7]),
                            AnsweredAtUtc = ReadUtc(reader[8]),
                            MasteryScoreBefore = Convert.ToDouble(reader[9], CultureInfo.InvariantCulture),
                            MasteryScoreAfter = NullableDouble(reader[10]),
                            MasteryDelta = NullableDouble(reader[11]),
                            ErrorType = NullableText(reader[12])
                        });
                    }
                }
            }
            return result;
        }

        private void UpdateRuntime(
            string sessionId,
            int generatedQuestionCount,
            string questionJson,
            string selectionJson,
            object questionStartedAtUtc,
            string forcedRepairTemplateId)
        {
            _database.Writes.Execute((connection, transaction) =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"UPDATE math_session_runtime
SET generated_question_count=@generated,current_question_json=@question,current_selection_json=@selection,
    question_started_at_utc=@started,forced_repair_template_id=@repair,updated_at_utc=@utc
WHERE session_id=@session;";
                    command.Parameters.AddWithValue("@generated", generatedQuestionCount);
                    command.Parameters.AddWithValue("@question", questionJson == null ? (object)DBNull.Value : questionJson);
                    command.Parameters.AddWithValue("@selection", selectionJson == null ? (object)DBNull.Value : selectionJson);
                    command.Parameters.AddWithValue("@started", questionStartedAtUtc ?? DBNull.Value);
                    command.Parameters.AddWithValue("@repair", string.IsNullOrWhiteSpace(forcedRepairTemplateId) ? (object)DBNull.Value : forcedRepairTemplateId);
                    command.Parameters.AddWithValue("@utc", Utc(DateTime.UtcNow));
                    command.Parameters.AddWithValue("@session", sessionId);
                    if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Math runtime session does not exist.");
                }
            });
        }

        private static string NullableText(object value)
        {
            return value == null || value == DBNull.Value ? null : Text(value);
        }

        private static double? NullableDouble(object value)
        {
            return value == null || value == DBNull.Value
                ? (double?)null
                : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static string Text(object value)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static DateTime ReadUtc(object value)
        {
            var parsed = ReadNullableUtc(value);
            if (!parsed.HasValue) throw new InvalidOperationException("Invalid UTC timestamp in Math runtime persistence.");
            return parsed.Value;
        }

        private static DateTime? ReadNullableUtc(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            DateTime parsed;
            if (!DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed)) return null;
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

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
