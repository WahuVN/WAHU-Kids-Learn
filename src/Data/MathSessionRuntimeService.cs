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
        public string PackId { get; set; }
        public string PackVersion { get; set; }
        public string CurrentQuestionJson { get; set; }
        public string CurrentSelectionJson { get; set; }
        public DateTime? QuestionStartedAtUtc { get; set; }
        public string ForcedRepairTemplateId { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public sealed class MathSessionRuntimeCorruptException : InvalidOperationException
    {
        public string SessionId { get; private set; }

        public MathSessionRuntimeCorruptException(string sessionId, string message, Exception innerException)
            : base(message, innerException)
        {
            SessionId = sessionId;
        }
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
            string targetSkillId = null,
            string packId = null,
            string packVersion = null)
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
            ValidatePackIdentityArguments(packId, packVersion);
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
session_id,seed,target_question_count,generated_question_count,session_mode,target_lesson_id,pack_id,pack_version,updated_at_utc)
VALUES(@session,@seed,@target,0,@mode,@lesson,@packId,@packVersion,@utc);";
                    runtime.Parameters.AddWithValue("@session", sessionId);
                    runtime.Parameters.AddWithValue("@seed", seed);
                    runtime.Parameters.AddWithValue("@target", targetQuestionCount);
                    runtime.Parameters.AddWithValue("@mode", sessionMode);
                    runtime.Parameters.AddWithValue("@lesson", string.IsNullOrWhiteSpace(targetLessonId) ? (object)DBNull.Value : targetLessonId);
                    runtime.Parameters.AddWithValue("@packId", string.IsNullOrWhiteSpace(packId) ? (object)DBNull.Value : packId);
                    runtime.Parameters.AddWithValue("@packVersion", string.IsNullOrWhiteSpace(packVersion) ? (object)DBNull.Value : packVersion);
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
            Create(sessionId, seed, targetQuestionCount, "adaptive", null, null, null);
        }

        public void Create(string sessionId, int seed, int targetQuestionCount, string sessionMode, string targetLessonId)
        {
            Create(sessionId, seed, targetQuestionCount, sessionMode, targetLessonId, null, null);
        }

        public void Create(
            string sessionId,
            int seed,
            int targetQuestionCount,
            string sessionMode,
            string targetLessonId,
            string packId,
            string packVersion)
        {
            Require(sessionId, "sessionId");
            if (targetQuestionCount < 1 || targetQuestionCount > 40) throw new ArgumentOutOfRangeException("targetQuestionCount");
            if (sessionMode != "adaptive" && sessionMode != "lesson") throw new ArgumentException("Invalid Math session mode.");
            if (sessionMode == "lesson") Require(targetLessonId, "targetLessonId");
            else targetLessonId = null;
            ValidatePackIdentityArguments(packId, packVersion);
            _database.Writes.Execute((connection, transaction) =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"INSERT INTO math_session_runtime(
session_id,seed,target_question_count,generated_question_count,session_mode,target_lesson_id,pack_id,pack_version,updated_at_utc)
VALUES(@session,@seed,@target,0,@mode,@lesson,@packId,@packVersion,@utc);";
                    command.Parameters.AddWithValue("@session", sessionId);
                    command.Parameters.AddWithValue("@seed", seed);
                    command.Parameters.AddWithValue("@target", targetQuestionCount);
                    command.Parameters.AddWithValue("@mode", sessionMode);
                    command.Parameters.AddWithValue("@lesson", string.IsNullOrWhiteSpace(targetLessonId) ? (object)DBNull.Value : targetLessonId);
                    command.Parameters.AddWithValue("@packId", string.IsNullOrWhiteSpace(packId) ? (object)DBNull.Value : packId);
                    command.Parameters.AddWithValue("@packVersion", string.IsNullOrWhiteSpace(packVersion) ? (object)DBNull.Value : packVersion);
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
r.seed,r.target_question_count,r.generated_question_count,r.session_mode,r.target_lesson_id,r.pack_id,r.pack_version,r.current_question_json,
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

                    // Copy raw DB values first. Operational reader/SQLite failures must escape normally;
                    // only deterministic parsing/metadata failures below are classified as corruption.
                    var values = new object[16];
                    reader.GetValues(values);
                    var sessionId = Text(values[0]);
                    try
                    {
                        var mode = Text(values[7]);
                        if (string.IsNullOrWhiteSpace(mode)) mode = "adaptive";
                        var snapshot = new MathSessionRuntimeSnapshot
                        {
                            SessionId = sessionId,
                            ChildId = Text(values[1]),
                            StartedAtUtc = ReadUtc(values[2]),
                            PerformanceProfile = Text(values[3]),
                            Seed = Convert.ToInt32(values[4], CultureInfo.InvariantCulture),
                            TargetQuestionCount = Convert.ToInt32(values[5], CultureInfo.InvariantCulture),
                            GeneratedQuestionCount = Convert.ToInt32(values[6], CultureInfo.InvariantCulture),
                            SessionMode = mode,
                            TargetLessonId = NullableText(values[8]),
                            PackId = NullableText(values[9]),
                            PackVersion = NullableText(values[10]),
                            CurrentQuestionJson = NullableText(values[11]),
                            CurrentSelectionJson = NullableText(values[12]),
                            QuestionStartedAtUtc = ReadNullableUtc(values[13]),
                            ForcedRepairTemplateId = NullableText(values[14]),
                            UpdatedAtUtc = ReadUtc(values[15])
                        };
                        ValidateRuntimeSnapshot(snapshot);
                        return snapshot;
                    }
                    catch (MathSessionRuntimeCorruptException)
                    {
                        throw;
                    }
                    catch (FormatException ex)
                    {
                        throw Corrupt(sessionId, ex);
                    }
                    catch (OverflowException ex)
                    {
                        throw Corrupt(sessionId, ex);
                    }
                    catch (InvalidCastException ex)
                    {
                        throw Corrupt(sessionId, ex);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw Corrupt(sessionId, ex);
                    }
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
            SaveCheckpoint(sessionId, generatedQuestionCount, forcedRepairTemplateId, null);
        }

        public void SaveCheckpoint(string sessionId, int generatedQuestionCount, string forcedRepairTemplateId, string selectionJson)
        {
            Require(sessionId, "sessionId");
            if (generatedQuestionCount < 0) throw new ArgumentOutOfRangeException("generatedQuestionCount");
            UpdateRuntime(sessionId, generatedQuestionCount, null, selectionJson, DBNull.Value, forcedRepairTemplateId);
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

        public int DeleteTerminalCheckpoints(string childId)
        {
            Require(childId, "childId");
            return _database.Writes.Execute((connection, transaction) =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"DELETE FROM math_session_runtime
WHERE session_id IN (
    SELECT id FROM session
    WHERE child_id=@child AND planned_subject='math'
      AND (state NOT IN ('started','active') OR ended_at_utc IS NOT NULL)
);";
                    command.Parameters.AddWithValue("@child", childId);
                    return command.ExecuteNonQuery();
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

        public MathSessionRuntimeSnapshot EnsurePackIdentity(
            MathSessionRuntimeSnapshot snapshot,
            string currentPackId,
            string currentPackVersion)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            Require(currentPackId, "currentPackId");
            Require(currentPackVersion, "currentPackVersion");
            ValidateSnapshotPackPair(snapshot);
            if (!string.IsNullOrWhiteSpace(snapshot.PackId)) return snapshot;

            return _database.Writes.Execute((connection, transaction) =>
            {
                string persistedPackId;
                string persistedPackVersion;
                using (var current = connection.CreateCommand())
                {
                    current.Transaction = transaction;
                    current.CommandText = @"SELECT r.pack_id,r.pack_version
FROM math_session_runtime r
JOIN session s ON s.id=r.session_id
WHERE r.session_id=@session AND s.child_id=@child AND s.planned_subject='math'
  AND s.state IN ('started','active') AND s.ended_at_utc IS NULL;";
                    current.Parameters.AddWithValue("@session", snapshot.SessionId);
                    current.Parameters.AddWithValue("@child", snapshot.ChildId);
                    using (var reader = current.ExecuteReader())
                    {
                        if (!reader.Read()) throw new InvalidOperationException("Math runtime session is no longer active.");
                        persistedPackId = NullableText(reader[0]);
                        persistedPackVersion = NullableText(reader[1]);
                    }
                }

                if (string.IsNullOrWhiteSpace(persistedPackId) != string.IsNullOrWhiteSpace(persistedPackVersion))
                    throw Corrupt(snapshot.SessionId, new InvalidOperationException("Math runtime pack identity is partially persisted."));
                if (!string.IsNullOrWhiteSpace(persistedPackId))
                {
                    snapshot.PackId = persistedPackId;
                    snapshot.PackVersion = persistedPackVersion;
                    return snapshot;
                }

                var identities = new List<Tuple<string, string>>();
                using (var attempts = connection.CreateCommand())
                {
                    attempts.Transaction = transaction;
                    attempts.CommandText = @"SELECT a.pack_id,a.pack_version
FROM attempt a
WHERE a.session_id=@session AND a.subject='math' AND a.answered_at_utc IS NOT NULL
GROUP BY a.pack_id,a.pack_version
ORDER BY MIN(a.answered_at_utc) ASC,MIN(a.started_at_utc) ASC,MIN(a.id) ASC
LIMIT 2;";
                    attempts.Parameters.AddWithValue("@session", snapshot.SessionId);
                    using (var reader = attempts.ExecuteReader())
                    {
                        while (reader.Read())
                            identities.Add(Tuple.Create(Text(reader[0]), Text(reader[1])));
                    }
                }

                if (identities.Count > 1)
                    throw Corrupt(snapshot.SessionId, new InvalidOperationException("Legacy Math session contains multiple content-pack identities."));
                var bindPackId = identities.Count == 1 ? identities[0].Item1 : currentPackId;
                var bindPackVersion = identities.Count == 1 ? identities[0].Item2 : currentPackVersion;
                try
                {
                    ValidatePackIdentityArguments(bindPackId, bindPackVersion);
                }
                catch (ArgumentException ex)
                {
                    throw Corrupt(snapshot.SessionId, ex);
                }

                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = @"UPDATE math_session_runtime
SET pack_id=@packId,pack_version=@packVersion,updated_at_utc=@utc
WHERE session_id=@session AND pack_id IS NULL AND pack_version IS NULL
  AND EXISTS (
      SELECT 1 FROM session s
      WHERE s.id=@session AND s.child_id=@child AND s.planned_subject='math'
        AND s.state IN ('started','active') AND s.ended_at_utc IS NULL
  );";
                    update.Parameters.AddWithValue("@packId", bindPackId);
                    update.Parameters.AddWithValue("@packVersion", bindPackVersion);
                    update.Parameters.AddWithValue("@utc", Utc(DateTime.UtcNow));
                    update.Parameters.AddWithValue("@session", snapshot.SessionId);
                    update.Parameters.AddWithValue("@child", snapshot.ChildId);
                    if (update.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("Math runtime pack identity could not be bound to the active session.");
                }

                snapshot.PackId = bindPackId;
                snapshot.PackVersion = bindPackVersion;
                return snapshot;
            });
        }

        public bool RecoverCorruptRuntimeSession(string childId, string sessionId)
        {
            return RecoverRuntimeSession(childId, sessionId, "corrupt_math_runtime");
        }

        public bool RecoverIncompatiblePackSession(string childId, string sessionId)
        {
            return RecoverRuntimeSession(childId, sessionId, "content_pack_version_mismatch");
        }

        private bool RecoverRuntimeSession(string childId, string sessionId, string reason)
        {
            Require(childId, "childId");
            Require(sessionId, "sessionId");
            Require(reason, "reason");
            return _database.Writes.Execute((connection, transaction) =>
            {
                using (var session = connection.CreateCommand())
                {
                    session.Transaction = transaction;
                    session.CommandText = @"UPDATE session
SET state='recovered',ended_at_utc=@utc,summary_json=COALESCE(summary_json,@summary)
WHERE id=@session AND child_id=@child AND planned_subject='math'
  AND state IN ('started','active') AND ended_at_utc IS NULL;";
                    session.Parameters.AddWithValue("@session", sessionId);
                    session.Parameters.AddWithValue("@child", childId);
                    session.Parameters.AddWithValue("@utc", Utc(DateTime.UtcNow));
                    session.Parameters.AddWithValue("@summary", "{\"reason\":\"" + reason + "\"}");
                    if (session.ExecuteNonQuery() != 1) return false;
                }

                using (var runtime = connection.CreateCommand())
                {
                    runtime.Transaction = transaction;
                    runtime.CommandText = "DELETE FROM math_session_runtime WHERE session_id=@session;";
                    runtime.Parameters.AddWithValue("@session", sessionId);
                    runtime.ExecuteNonQuery();
                }
                return true;
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

        private static void ValidateRuntimeSnapshot(MathSessionRuntimeSnapshot snapshot)
        {
            if (snapshot == null) throw new InvalidOperationException("Missing Math runtime snapshot.");
            if (string.IsNullOrWhiteSpace(snapshot.SessionId)) throw new InvalidOperationException("Math runtime session id is missing.");
            if (string.IsNullOrWhiteSpace(snapshot.ChildId)) throw new InvalidOperationException("Math runtime child id is missing.");
            if (snapshot.TargetQuestionCount < 1 || snapshot.TargetQuestionCount > 40)
                throw new InvalidOperationException("Math runtime target_question_count is outside the supported range.");
            if (snapshot.GeneratedQuestionCount < 0 || snapshot.GeneratedQuestionCount > snapshot.TargetQuestionCount)
                throw new InvalidOperationException("Math runtime generated_question_count is inconsistent with target_question_count.");
            if (snapshot.StartedAtUtc > snapshot.UpdatedAtUtc)
                throw new InvalidOperationException("Math runtime session started_at_utc cannot be later than runtime updated_at_utc.");
            if (!string.Equals(snapshot.SessionMode, "adaptive", StringComparison.Ordinal) &&
                !string.Equals(snapshot.SessionMode, "lesson", StringComparison.Ordinal))
                throw new InvalidOperationException("Math runtime session_mode is invalid.");
            if (string.Equals(snapshot.SessionMode, "lesson", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(snapshot.TargetLessonId))
                throw new InvalidOperationException("Targeted Math runtime is missing target_lesson_id.");
            ValidateSnapshotPackPair(snapshot);
        }

        private static void ValidatePackIdentityArguments(string packId, string packVersion)
        {
            if (packId == null && packVersion == null) return;
            if (string.IsNullOrWhiteSpace(packId) || string.IsNullOrWhiteSpace(packVersion))
                throw new ArgumentException("Math runtime pack_id and pack_version must be supplied together as non-empty values.");
        }

        private static void ValidateSnapshotPackPair(MathSessionRuntimeSnapshot snapshot)
        {
            var hasPackId = !string.IsNullOrWhiteSpace(snapshot.PackId);
            var hasPackVersion = !string.IsNullOrWhiteSpace(snapshot.PackVersion);
            if (hasPackId != hasPackVersion)
                throw Corrupt(snapshot.SessionId, new InvalidOperationException("Math runtime pack identity is incomplete."));
        }

        private static MathSessionRuntimeCorruptException Corrupt(string sessionId, Exception innerException)
        {
            return new MathSessionRuntimeCorruptException(
                sessionId,
                "Persisted Math runtime metadata is malformed and cannot be resumed safely.",
                innerException);
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
