using System;
using System.Web.Script.Serialization;
using WAHU.Learning;

namespace WAHU.Data
{
    public sealed class AdaptiveDecisionAuditRequest
    {
        public string Id { get; set; }
        public string SessionId { get; set; }
        public string ChildId { get; set; }
        public string PackId { get; set; }
        public string PackVersion { get; set; }
        public MathQuestion Question { get; set; }
        public MathSelectionDecision Selection { get; set; }
        public BehaviorDecision Behavior { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class AdaptiveDecisionAuditService
    {
        private readonly LearningDatabase _database;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public AdaptiveDecisionAuditService(LearningDatabase database)
        {
            _database = database ?? throw new ArgumentNullException("database");
        }

        public void Record(AdaptiveDecisionAuditRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            Require(request.Id, "Id"); Require(request.SessionId, "SessionId"); Require(request.ChildId, "ChildId");
            Require(request.PackId, "PackId"); Require(request.PackVersion, "PackVersion");
            if (request.Question == null || request.Selection == null) throw new ArgumentException("Question and Selection are required.");
            if (request.CreatedAtUtc == default(DateTime)) request.CreatedAtUtc = DateTime.UtcNow;

            var behaviorState = request.Behavior == null ? null : request.Behavior.State.ToString();
            var behaviorConfidence = request.Behavior == null ? (double?)null : request.Behavior.Confidence;
            var reasonJson = _json.Serialize(request.Selection.Reasons ?? new string[0]);
            var candidatesJson = _json.Serialize(request.Selection.CandidateSummary ?? new string[0]);

            _database.Writes.Execute((connection, transaction) =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"INSERT INTO adaptive_decision_event(
id,session_id,child_id,chosen_pack_id,chosen_pack_version,chosen_question_id,primary_skill_id,
behavior_state,behavior_confidence,difficulty_fit,decision_reason_json,candidate_summary_json,engine_version,created_at_utc)
VALUES(@id,@session,@child,@pack,@version,@question,@skill,@behavior,@confidence,@fit,@reason,@candidates,@engine,@utc);";
                    command.Parameters.AddWithValue("@id", request.Id);
                    command.Parameters.AddWithValue("@session", request.SessionId);
                    command.Parameters.AddWithValue("@child", request.ChildId);
                    command.Parameters.AddWithValue("@pack", request.PackId);
                    command.Parameters.AddWithValue("@version", request.PackVersion);
                    command.Parameters.AddWithValue("@question", request.Question.QuestionId);
                    command.Parameters.AddWithValue("@skill", request.Question.SkillId);
                    command.Parameters.AddWithValue("@behavior", string.IsNullOrWhiteSpace(behaviorState) ? (object)DBNull.Value : behaviorState);
                    command.Parameters.AddWithValue("@confidence", behaviorConfidence.HasValue ? (object)behaviorConfidence.Value : DBNull.Value);
                    command.Parameters.AddWithValue("@fit", request.Selection.DifficultyFit);
                    command.Parameters.AddWithValue("@reason", reasonJson);
                    command.Parameters.AddWithValue("@candidates", candidatesJson);
                    command.Parameters.AddWithValue("@engine", AdaptiveMathSelector.EngineVersion);
                    command.Parameters.AddWithValue("@utc", Utc(request.CreatedAtUtc));
                    command.ExecuteNonQuery();
                }
            });
        }

        private static string Utc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Local) value = value.ToUniversalTime();
            else if (value.Kind == DateTimeKind.Unspecified) value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return value.ToString("o");
        }

        private static void Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(name + " is required.");
        }
    }
}
