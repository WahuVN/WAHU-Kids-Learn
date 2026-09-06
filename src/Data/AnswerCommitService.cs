using System;
using System.Data.SQLite;

namespace WAHU.Data
{
    public sealed class AnswerCommitRequest
    {
        public string AttemptId { get; set; }
        public string SessionId { get; set; }
        public string ChildId { get; set; }
        public string PackId { get; set; }
        public string PackVersion { get; set; }
        public string QuestionId { get; set; }
        public string SkillId { get; set; }
        public string Subject { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime AnsweredAtUtc { get; set; }
        public string AnswerJson { get; set; }
        public bool IsCorrect { get; set; }
        public int ResponseMs { get; set; }
        public int HintLevel { get; set; }
        public string Representation { get; set; }
        public string InputMethod { get; set; }
        public int AttemptIndex { get; set; }
        public int ListenCount { get; set; }
        public ErrorEventWrite Error { get; set; }
        public MasteryEventWrite Mastery { get; set; }
        public ChildSkillWrite ChildSkill { get; set; }
        public ReviewScheduleWrite Review { get; set; }
    }

    public sealed class ErrorEventWrite
    {
        public string Id { get; set; }
        public string ErrorType { get; set; }
        public double Confidence { get; set; }
        public string EvidenceJson { get; set; }
        public string ClassifierVersion { get; set; }
    }

    public sealed class MasteryEventWrite
    {
        public string Id { get; set; }
        public string EventType { get; set; }
        public double Delta { get; set; }
        public double ScoreBefore { get; set; }
        public double ScoreAfter { get; set; }
        public double? ConfidenceAfter { get; set; }
        public string ReasonJson { get; set; }
        public string MasteryEngineVersion { get; set; }
    }

    public sealed class ChildSkillWrite
    {
        public double MasteryScore { get; set; }
        public double Confidence { get; set; }
        public int AttemptsCount { get; set; }
        public int IndependentSuccessCount { get; set; }
        public int HintedSuccessCount { get; set; }
        public int TransferSuccessCount { get; set; }
        public DateTime? LastSeenAtUtc { get; set; }
        public DateTime? LastSuccessAtUtc { get; set; }
        public DateTime? NextReviewAtUtc { get; set; }
        public string LearningState { get; set; }
        public string MasteryEngineVersion { get; set; }
    }

    public sealed class ReviewScheduleWrite
    {
        public DateTime DueAtUtc { get; set; }
        public double IntervalDays { get; set; }
        public string Reason { get; set; }
        public string SchedulerVersion { get; set; }
    }

    public sealed class AnswerCommitResult
    {
        public string AttemptId { get; set; }
        public bool AlreadyCommitted { get; set; }
        public bool ErrorWritten { get; set; }
        public bool MasteryWritten { get; set; }
        public bool ChildSkillWritten { get; set; }
        public bool ReviewWritten { get; set; }
    }

    public sealed class AttemptCorrectionWrite
    {
        public string Id { get; set; }
        public string AttemptId { get; set; }
        public string CorrectionType { get; set; }
        public string BeforeJson { get; set; }
        public string AfterJson { get; set; }
        public string Reason { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class AnswerCommitService
    {
        private readonly LearningDatabase _database;

        public AnswerCommitService(LearningDatabase database)
        {
            _database = database ?? throw new ArgumentNullException("database");
        }

        public AnswerCommitResult Commit(AnswerCommitRequest request)
        {
            Validate(request);
            try
            {
                return _database.Writes.Execute((connection, transaction) =>
                {
                    var existing = FindExistingCommit(connection, transaction, request);
                    if (existing != null)
                    {
                        VerifyReplay(existing, request);
                        return ReplayResult(existing.AttemptId);
                    }

                    InsertAttempt(connection, transaction, request);
                    InsertAttemptCommitKey(connection, transaction, request);
                    var errorWritten = InsertError(connection, transaction, request);
                    var masteryWritten = InsertMastery(connection, transaction, request);
                    var skillWritten = UpsertChildSkill(connection, transaction, request);
                    var reviewWritten = UpsertReview(connection, transaction, request);

                    return new AnswerCommitResult
                    {
                        AttemptId = request.AttemptId,
                        AlreadyCommitted = false,
                        ErrorWritten = errorWritten,
                        MasteryWritten = masteryWritten,
                        ChildSkillWritten = skillWritten,
                        ReviewWritten = reviewWritten
                    };
                });
            }
            catch (SQLiteException)
            {
                // A second process can race the semantic-key insert. The losing transaction
                // rolls back completely; re-read the durable key and accept only an exact replay.
                using (var connection = _database.OpenConnection())
                {
                    var existing = FindExistingCommit(connection, null, request);
                    if (existing == null) throw;
                    VerifyReplay(existing, request);
                    return ReplayResult(existing.AttemptId);
                }
            }
        }

        public void RecordCorrection(AttemptCorrectionWrite correction)
        {
            if (correction == null) throw new ArgumentNullException("correction");
            Require(correction.Id, "correction.Id");
            Require(correction.AttemptId, "correction.AttemptId");
            Require(correction.CorrectionType, "correction.CorrectionType");
            Require(correction.Reason, "correction.Reason");
            if (correction.CreatedAtUtc == default(DateTime)) correction.CreatedAtUtc = DateTime.UtcNow;

            _database.Writes.Execute((connection, transaction) =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"INSERT INTO attempt_correction_event(
id,attempt_id,correction_type,before_json,after_json,reason,created_at_utc)
VALUES(@id,@attempt,@type,@before,@after,@reason,@utc);";
                    command.Parameters.AddWithValue("@id", correction.Id);
                    command.Parameters.AddWithValue("@attempt", correction.AttemptId);
                    command.Parameters.AddWithValue("@type", correction.CorrectionType);
                    command.Parameters.AddWithValue("@before", DbValue(correction.BeforeJson));
                    command.Parameters.AddWithValue("@after", DbValue(correction.AfterJson));
                    command.Parameters.AddWithValue("@reason", correction.Reason);
                    command.Parameters.AddWithValue("@utc", Utc(correction.CreatedAtUtc));
                    command.ExecuteNonQuery();
                }
            });
        }

        private sealed class ExistingAttemptCommit
        {
            public string AttemptId { get; set; }
            public string ChildId { get; set; }
            public string PackId { get; set; }
            public string PackVersion { get; set; }
            public string SkillId { get; set; }
            public string Subject { get; set; }
            public string AnswerJson { get; set; }
            public bool IsCorrect { get; set; }
            public int HintLevel { get; set; }
        }

        private static ExistingAttemptCommit FindExistingCommit(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            AnswerCommitRequest request)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"SELECT a.id,a.child_id,a.pack_id,a.pack_version,a.skill_id,a.subject,
a.answer_json,a.is_correct,a.hint_level
FROM attempt_commit_key k
JOIN attempt a ON a.id=k.attempt_id
WHERE k.session_id=@session AND k.question_id=@question AND k.attempt_index=@attemptIndex;";
                command.Parameters.AddWithValue("@session", request.SessionId);
                command.Parameters.AddWithValue("@question", request.QuestionId);
                command.Parameters.AddWithValue("@attemptIndex", request.AttemptIndex);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new ExistingAttemptCommit
                    {
                        AttemptId = Convert.ToString(reader[0], System.Globalization.CultureInfo.InvariantCulture),
                        ChildId = Convert.ToString(reader[1], System.Globalization.CultureInfo.InvariantCulture),
                        PackId = Convert.ToString(reader[2], System.Globalization.CultureInfo.InvariantCulture),
                        PackVersion = Convert.ToString(reader[3], System.Globalization.CultureInfo.InvariantCulture),
                        SkillId = Convert.ToString(reader[4], System.Globalization.CultureInfo.InvariantCulture),
                        Subject = Convert.ToString(reader[5], System.Globalization.CultureInfo.InvariantCulture),
                        AnswerJson = reader[6] == DBNull.Value ? null : Convert.ToString(reader[6], System.Globalization.CultureInfo.InvariantCulture),
                        IsCorrect = Convert.ToInt32(reader[7], System.Globalization.CultureInfo.InvariantCulture) != 0,
                        HintLevel = Convert.ToInt32(reader[8], System.Globalization.CultureInfo.InvariantCulture)
                    };
                }
            }
        }

        private static void VerifyReplay(ExistingAttemptCommit existing, AnswerCommitRequest request)
        {
            if (!string.Equals(existing.ChildId, request.ChildId, StringComparison.Ordinal) ||
                !string.Equals(existing.PackId, request.PackId, StringComparison.Ordinal) ||
                !string.Equals(existing.PackVersion, request.PackVersion, StringComparison.Ordinal) ||
                !string.Equals(existing.SkillId, request.SkillId, StringComparison.Ordinal) ||
                !string.Equals(existing.Subject, request.Subject, StringComparison.Ordinal) ||
                !string.Equals(existing.AnswerJson ?? string.Empty, request.AnswerJson ?? string.Empty, StringComparison.Ordinal) ||
                existing.IsCorrect != request.IsCorrect || existing.HintLevel != request.HintLevel)
                throw new InvalidOperationException(
                    "Attempt idempotency key conflict for session/question/attempt_index. Retry must reuse the same payload or increment attempt_index.");
        }

        private static AnswerCommitResult ReplayResult(string attemptId)
        {
            return new AnswerCommitResult
            {
                AttemptId = attemptId,
                AlreadyCommitted = true,
                ErrorWritten = false,
                MasteryWritten = false,
                ChildSkillWritten = false,
                ReviewWritten = false
            };
        }

        private static void InsertAttemptCommitKey(SQLiteConnection connection, SQLiteTransaction transaction, AnswerCommitRequest request)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"INSERT INTO attempt_commit_key(
session_id,question_id,attempt_index,attempt_id,created_at_utc)
VALUES(@session,@question,@attemptIndex,@attempt,@utc);";
                command.Parameters.AddWithValue("@session", request.SessionId);
                command.Parameters.AddWithValue("@question", request.QuestionId);
                command.Parameters.AddWithValue("@attemptIndex", request.AttemptIndex);
                command.Parameters.AddWithValue("@attempt", request.AttemptId);
                command.Parameters.AddWithValue("@utc", Utc(request.AnsweredAtUtc));
                command.ExecuteNonQuery();
            }
        }

        private static void InsertAttempt(SQLiteConnection connection, SQLiteTransaction transaction, AnswerCommitRequest request)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"INSERT INTO attempt(
id,session_id,child_id,pack_id,pack_version,question_id,skill_id,subject,
started_at_utc,answered_at_utc,answer_json,is_correct,response_ms,hint_level,
representation,input_method,attempt_index,listen_count)
VALUES(@id,@session,@child,@pack,@packVersion,@question,@skill,@subject,
@started,@answered,@answer,@correct,@responseMs,@hint,@representation,@input,@attemptIndex,@listenCount);";
                command.Parameters.AddWithValue("@id", request.AttemptId);
                command.Parameters.AddWithValue("@session", request.SessionId);
                command.Parameters.AddWithValue("@child", request.ChildId);
                command.Parameters.AddWithValue("@pack", request.PackId);
                command.Parameters.AddWithValue("@packVersion", request.PackVersion);
                command.Parameters.AddWithValue("@question", request.QuestionId);
                command.Parameters.AddWithValue("@skill", request.SkillId);
                command.Parameters.AddWithValue("@subject", request.Subject);
                command.Parameters.AddWithValue("@started", Utc(request.StartedAtUtc));
                command.Parameters.AddWithValue("@answered", Utc(request.AnsweredAtUtc));
                command.Parameters.AddWithValue("@answer", DbValue(request.AnswerJson));
                command.Parameters.AddWithValue("@correct", request.IsCorrect ? 1 : 0);
                command.Parameters.AddWithValue("@responseMs", request.ResponseMs);
                command.Parameters.AddWithValue("@hint", request.HintLevel);
                command.Parameters.AddWithValue("@representation", DbValue(request.Representation));
                command.Parameters.AddWithValue("@input", DbValue(request.InputMethod));
                command.Parameters.AddWithValue("@attemptIndex", request.AttemptIndex);
                command.Parameters.AddWithValue("@listenCount", request.ListenCount);
                command.ExecuteNonQuery();
            }
        }

        private static bool InsertError(SQLiteConnection connection, SQLiteTransaction transaction, AnswerCommitRequest request)
        {
            if (request.Error == null) return false;
            Require(request.Error.Id, "error.Id");
            Require(request.Error.ErrorType, "error.ErrorType");
            Require(request.Error.EvidenceJson, "error.EvidenceJson");
            Require(request.Error.ClassifierVersion, "error.ClassifierVersion");

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"INSERT INTO error_event(
id,attempt_id,error_type,confidence,evidence_json,classifier_version,created_at_utc)
VALUES(@id,@attempt,@type,@confidence,@evidence,@version,@utc);";
                command.Parameters.AddWithValue("@id", request.Error.Id);
                command.Parameters.AddWithValue("@attempt", request.AttemptId);
                command.Parameters.AddWithValue("@type", request.Error.ErrorType);
                command.Parameters.AddWithValue("@confidence", request.Error.Confidence);
                command.Parameters.AddWithValue("@evidence", request.Error.EvidenceJson);
                command.Parameters.AddWithValue("@version", request.Error.ClassifierVersion);
                command.Parameters.AddWithValue("@utc", Utc(request.AnsweredAtUtc));
                command.ExecuteNonQuery();
            }
            return true;
        }

        private static bool InsertMastery(SQLiteConnection connection, SQLiteTransaction transaction, AnswerCommitRequest request)
        {
            if (request.Mastery == null) return false;
            Require(request.Mastery.Id, "mastery.Id");
            Require(request.Mastery.EventType, "mastery.EventType");
            Require(request.Mastery.ReasonJson, "mastery.ReasonJson");
            Require(request.Mastery.MasteryEngineVersion, "mastery.MasteryEngineVersion");

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"INSERT INTO mastery_event(
id,child_id,skill_id,attempt_id,event_type,delta,score_before,score_after,
confidence_after,reason_json,mastery_engine_version,created_at_utc)
VALUES(@id,@child,@skill,@attempt,@type,@delta,@before,@after,@confidence,@reason,@version,@utc);";
                command.Parameters.AddWithValue("@id", request.Mastery.Id);
                command.Parameters.AddWithValue("@child", request.ChildId);
                command.Parameters.AddWithValue("@skill", request.SkillId);
                command.Parameters.AddWithValue("@attempt", request.AttemptId);
                command.Parameters.AddWithValue("@type", request.Mastery.EventType);
                command.Parameters.AddWithValue("@delta", request.Mastery.Delta);
                command.Parameters.AddWithValue("@before", request.Mastery.ScoreBefore);
                command.Parameters.AddWithValue("@after", request.Mastery.ScoreAfter);
                command.Parameters.AddWithValue("@confidence", request.Mastery.ConfidenceAfter.HasValue ? (object)request.Mastery.ConfidenceAfter.Value : DBNull.Value);
                command.Parameters.AddWithValue("@reason", request.Mastery.ReasonJson);
                command.Parameters.AddWithValue("@version", request.Mastery.MasteryEngineVersion);
                command.Parameters.AddWithValue("@utc", Utc(request.AnsweredAtUtc));
                command.ExecuteNonQuery();
            }
            return true;
        }

        private static bool UpsertChildSkill(SQLiteConnection connection, SQLiteTransaction transaction, AnswerCommitRequest request)
        {
            if (request.ChildSkill == null) return false;
            Require(request.ChildSkill.LearningState, "childSkill.LearningState");
            Require(request.ChildSkill.MasteryEngineVersion, "childSkill.MasteryEngineVersion");

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"INSERT INTO child_skill(
child_id,skill_id,subject,mastery_score,confidence,attempts_count,
independent_success_count,hinted_success_count,transfer_success_count,last_seen_at_utc,
last_success_at_utc,next_review_at_utc,learning_state,mastery_engine_version,updated_at_utc)
VALUES(@child,@skill,@subject,@mastery,@confidence,@attempts,@independent,@hinted,@transfer,
@lastSeen,@lastSuccess,@nextReview,@state,@version,@updated)
ON CONFLICT(child_id,skill_id) DO UPDATE SET
subject=excluded.subject,
mastery_score=excluded.mastery_score,
confidence=excluded.confidence,
attempts_count=excluded.attempts_count,
independent_success_count=excluded.independent_success_count,
hinted_success_count=excluded.hinted_success_count,
transfer_success_count=excluded.transfer_success_count,
last_seen_at_utc=excluded.last_seen_at_utc,
last_success_at_utc=excluded.last_success_at_utc,
next_review_at_utc=excluded.next_review_at_utc,
learning_state=excluded.learning_state,
mastery_engine_version=excluded.mastery_engine_version,
updated_at_utc=excluded.updated_at_utc;";
                command.Parameters.AddWithValue("@child", request.ChildId);
                command.Parameters.AddWithValue("@skill", request.SkillId);
                command.Parameters.AddWithValue("@subject", request.Subject);
                command.Parameters.AddWithValue("@mastery", request.ChildSkill.MasteryScore);
                command.Parameters.AddWithValue("@confidence", request.ChildSkill.Confidence);
                command.Parameters.AddWithValue("@attempts", request.ChildSkill.AttemptsCount);
                command.Parameters.AddWithValue("@independent", request.ChildSkill.IndependentSuccessCount);
                command.Parameters.AddWithValue("@hinted", request.ChildSkill.HintedSuccessCount);
                command.Parameters.AddWithValue("@transfer", request.ChildSkill.TransferSuccessCount);
                command.Parameters.AddWithValue("@lastSeen", NullableUtc(request.ChildSkill.LastSeenAtUtc));
                command.Parameters.AddWithValue("@lastSuccess", NullableUtc(request.ChildSkill.LastSuccessAtUtc));
                command.Parameters.AddWithValue("@nextReview", NullableUtc(request.ChildSkill.NextReviewAtUtc));
                command.Parameters.AddWithValue("@state", request.ChildSkill.LearningState);
                command.Parameters.AddWithValue("@version", request.ChildSkill.MasteryEngineVersion);
                command.Parameters.AddWithValue("@updated", Utc(request.AnsweredAtUtc));
                command.ExecuteNonQuery();
            }
            return true;
        }

        private static bool UpsertReview(SQLiteConnection connection, SQLiteTransaction transaction, AnswerCommitRequest request)
        {
            if (request.Review == null) return false;
            Require(request.Review.Reason, "review.Reason");
            Require(request.Review.SchedulerVersion, "review.SchedulerVersion");

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"INSERT INTO review_schedule(
child_id,skill_id,due_at_utc,interval_days,reason,scheduler_version,updated_at_utc)
VALUES(@child,@skill,@due,@interval,@reason,@version,@updated)
ON CONFLICT(child_id,skill_id) DO UPDATE SET
due_at_utc=excluded.due_at_utc,
interval_days=excluded.interval_days,
reason=excluded.reason,
scheduler_version=excluded.scheduler_version,
updated_at_utc=excluded.updated_at_utc;";
                command.Parameters.AddWithValue("@child", request.ChildId);
                command.Parameters.AddWithValue("@skill", request.SkillId);
                command.Parameters.AddWithValue("@due", Utc(request.Review.DueAtUtc));
                command.Parameters.AddWithValue("@interval", request.Review.IntervalDays);
                command.Parameters.AddWithValue("@reason", request.Review.Reason);
                command.Parameters.AddWithValue("@version", request.Review.SchedulerVersion);
                command.Parameters.AddWithValue("@updated", Utc(request.AnsweredAtUtc));
                command.ExecuteNonQuery();
            }
            return true;
        }

        private static void Validate(AnswerCommitRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            Require(request.AttemptId, "AttemptId");
            Require(request.SessionId, "SessionId");
            Require(request.ChildId, "ChildId");
            Require(request.PackId, "PackId");
            Require(request.PackVersion, "PackVersion");
            Require(request.QuestionId, "QuestionId");
            Require(request.SkillId, "SkillId");
            if (request.Subject != "math" && request.Subject != "english") throw new ArgumentException("Subject must be math or english.");
            if (request.StartedAtUtc == default(DateTime) || request.AnsweredAtUtc == default(DateTime)) throw new ArgumentException("Attempt timestamps are required.");
            if (request.AnsweredAtUtc.ToUniversalTime() < request.StartedAtUtc.ToUniversalTime()) throw new ArgumentException("AnsweredAtUtc cannot precede StartedAtUtc.");
            if (request.ResponseMs < 0 || request.HintLevel < 0 || request.AttemptIndex < 1 || request.ListenCount < 0) throw new ArgumentOutOfRangeException("attempt metrics");
        }

        private static void Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(name + " is required.");
        }

        private static object DbValue(string value)
        {
            return value == null ? (object)DBNull.Value : value;
        }

        private static object NullableUtc(DateTime? value)
        {
            return value.HasValue ? (object)Utc(value.Value) : DBNull.Value;
        }

        private static string Utc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Local) value = value.ToUniversalTime();
            else if (value.Kind == DateTimeKind.Unspecified) value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return value.ToString("o");
        }
    }
}
