using System;
using System.Web.Script.Serialization;
using WAHU.Learning;

namespace WAHU.Data
{
    public sealed class BehaviorDecisionAuditRequest
    {
        public string Id { get; set; }
        public string SessionId { get; set; }
        public string ChildId { get; set; }
        public string AttemptId { get; set; }
        public BehaviorDecision Decision { get; set; }
        public string ControllerVersion { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class BehaviorDecisionAuditService
    {
        private readonly LearningDatabase _database;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public BehaviorDecisionAuditService(LearningDatabase database)
        {
            _database = database ?? throw new ArgumentNullException("database");
        }

        public void Record(BehaviorDecisionAuditRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            Require(request.Id, "Id");
            Require(request.SessionId, "SessionId");
            Require(request.ChildId, "ChildId");
            Require(request.ControllerVersion, "ControllerVersion");
            if (request.Decision == null) throw new ArgumentException("Decision is required.");
            if (request.Decision.Confidence < 0 || request.Decision.Confidence > 1)
                throw new ArgumentOutOfRangeException("Decision.Confidence");
            if (request.CreatedAtUtc == default(DateTime)) request.CreatedAtUtc = DateTime.UtcNow;

            var state = request.Decision.State.ToString();
            var evidenceJson = _json.Serialize(request.Decision.Evidence ?? new string[0]);
            var actionJson = _json.Serialize(request.Decision.Actions ?? new string[0]);

            _database.Writes.Execute((connection, transaction) =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"INSERT INTO behavior_state_event(
id,session_id,child_id,attempt_id,state_label,confidence,evidence_json,action_taken,controller_version,created_at_utc)
VALUES(@id,@session,@child,@attempt,@state,@confidence,@evidence,@action,@version,@utc);";
                    command.Parameters.AddWithValue("@id", request.Id);
                    command.Parameters.AddWithValue("@session", request.SessionId);
                    command.Parameters.AddWithValue("@child", request.ChildId);
                    command.Parameters.AddWithValue("@attempt", string.IsNullOrWhiteSpace(request.AttemptId) ? (object)DBNull.Value : request.AttemptId);
                    command.Parameters.AddWithValue("@state", state);
                    command.Parameters.AddWithValue("@confidence", request.Decision.Confidence);
                    command.Parameters.AddWithValue("@evidence", evidenceJson);
                    command.Parameters.AddWithValue("@action", actionJson);
                    command.Parameters.AddWithValue("@version", request.ControllerVersion);
                    command.Parameters.AddWithValue("@utc", Utc(request.CreatedAtUtc));
                    command.ExecuteNonQuery();
                }
            });
        }

        private static void Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(name + " is required.");
        }

        private static string Utc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Local) value = value.ToUniversalTime();
            else if (value.Kind == DateTimeKind.Unspecified) value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return value.ToString("o");
        }
    }
}
