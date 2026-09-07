using System;
using System.Collections.Generic;
using System.Globalization;

namespace WAHU.Data
{
    public sealed class GameWorldProgress
    {
        public int GrowthSteps { get; set; }
        public int CompletedMathSessions { get; set; }
        public IList<string> UnlockedItems { get; set; }
        public int SessionsUntilNextMilestone { get; set; }
        public int NextMilestoneSessionCount { get; set; }
        public string NextMilestoneItemId { get; set; }
    }

    public sealed class GameWorldRewardResult
    {
        public bool RewardCreated { get; set; }
        public int GrowthSteps { get; set; }
        public int CompletedMathSessions { get; set; }
        public IList<string> NewlyUnlockedItems { get; set; }
        public int SessionsUntilNextMilestone { get; set; }
        public int NextMilestoneSessionCount { get; set; }
        public string NextMilestoneItemId { get; set; }
    }

    public sealed class GameWorldRewardService
    {
        public const string RewardType = "garden_growth";
        private readonly LearningDatabase _database;

        private static readonly KeyValuePair<int, string>[] Milestones =
        {
            new KeyValuePair<int, string>(1, "garden_seedling"),
            new KeyValuePair<int, string>(3, "garden_flower_patch"),
            new KeyValuePair<int, string>(6, "garden_lantern"),
            new KeyValuePair<int, string>(10, "garden_bench")
        };

        public GameWorldRewardService(LearningDatabase database)
        {
            _database = database ?? throw new ArgumentNullException("database");
        }

        public GameWorldRewardResult GrantCompletedMathSession(string childId, string sessionId, int attempts)
        {
            Require(childId, "childId");
            Require(sessionId, "sessionId");
            // Caller attempt count is advisory only. Durable SQLite evidence decides eligibility.

            var created = false;
            var unlocked = new List<string>();
            _database.Writes.Execute((connection, transaction) =>
            {
                using (var verify = connection.CreateCommand())
                {
                    verify.Transaction = transaction;
                    verify.CommandText = @"SELECT count(a.id)
FROM session s
LEFT JOIN attempt a ON a.session_id=s.id AND a.child_id=s.child_id AND a.subject='math'
WHERE s.id=@session AND s.child_id=@child AND s.state='completed' AND s.planned_subject='math'
GROUP BY s.id;";
                    verify.Parameters.AddWithValue("@session", sessionId);
                    verify.Parameters.AddWithValue("@child", childId);
                    var durableAttempts = verify.ExecuteScalar();
                    if (durableAttempts == null || durableAttempts == DBNull.Value)
                        throw new InvalidOperationException("Garden reward requires one completed math session.");
                    if (Convert.ToInt32(durableAttempts, CultureInfo.InvariantCulture) <= 0) return;
                }

                using (var reward = connection.CreateCommand())
                {
                    reward.Transaction = transaction;
                    reward.CommandText = @"INSERT OR IGNORE INTO reward_event(
id,child_id,reward_type,reward_id,source_event,source_ref,source_key,created_at_utc)
VALUES(@id,@child,@type,@reward,'session_completed',@session,@key,@utc);";
                    reward.Parameters.AddWithValue("@id", "reward-" + Guid.NewGuid().ToString("N"));
                    reward.Parameters.AddWithValue("@child", childId);
                    reward.Parameters.AddWithValue("@type", RewardType);
                    reward.Parameters.AddWithValue("@reward", "growth_step");
                    reward.Parameters.AddWithValue("@session", sessionId);
                    reward.Parameters.AddWithValue("@key", "garden_growth:session:" + sessionId);
                    reward.Parameters.AddWithValue("@utc", DateTime.UtcNow.ToString("o"));
                    created = reward.ExecuteNonQuery() == 1;
                }

                var completed = CountCompletedMathSessions(connection, transaction, childId);
                foreach (var milestone in Milestones)
                {
                    if (completed < milestone.Key) continue;
                    using (var item = connection.CreateCommand())
                    {
                        item.Transaction = transaction;
                        item.CommandText = @"INSERT OR IGNORE INTO inventory(child_id,item_id,unlocked_at_utc,equipped)
VALUES(@child,@item,@utc,0);";
                        item.Parameters.AddWithValue("@child", childId);
                        item.Parameters.AddWithValue("@item", milestone.Value);
                        item.Parameters.AddWithValue("@utc", DateTime.UtcNow.ToString("o"));
                        if (item.ExecuteNonQuery() == 1) unlocked.Add(milestone.Value);
                    }
                }
            });
            return SnapshotResult(childId, created, unlocked);
        }

        public int ReconcileMissingCompletedMathSessionRewards(string childId)
        {
            Require(childId, "childId");
            var missing = new List<KeyValuePair<string, int>>();
            using (var connection = _database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT s.id, count(a.id)
FROM session s
JOIN attempt a ON a.session_id=s.id AND a.child_id=s.child_id AND a.subject='math'
WHERE s.child_id=@child AND s.state='completed' AND s.planned_subject='math'
  AND NOT EXISTS (
      SELECT 1 FROM reward_event r
      WHERE r.child_id=s.child_id AND r.reward_type=@type
        AND r.source_event='session_completed' AND r.source_ref=s.id)
GROUP BY s.id
HAVING count(a.id)>0
ORDER BY s.started_at_utc,s.id;";
                command.Parameters.AddWithValue("@child", childId);
                command.Parameters.AddWithValue("@type", RewardType);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        missing.Add(new KeyValuePair<string, int>(
                            Convert.ToString(reader[0], CultureInfo.InvariantCulture),
                            Convert.ToInt32(reader[1], CultureInfo.InvariantCulture)));
                    }
                }
            }

            var repaired = 0;
            foreach (var item in missing)
            {
                var result = GrantCompletedMathSession(childId, item.Key, item.Value);
                if (result.RewardCreated) repaired++;
            }
            return repaired;
        }

        public GameWorldProgress ReadProgress(string childId)
        {
            Require(childId, "childId");
            using (var connection = _database.OpenConnection())
            {
                var progress = new GameWorldProgress
                {
                    GrowthSteps = Count(connection,
                        "SELECT count(*) FROM reward_event WHERE child_id=@child AND reward_type='garden_growth';", childId),
                    CompletedMathSessions = Count(connection,
                        @"SELECT count(*) FROM session s
WHERE s.child_id=@child AND s.state='completed' AND s.planned_subject='math'
  AND EXISTS (SELECT 1 FROM attempt a WHERE a.session_id=s.id AND a.child_id=s.child_id AND a.subject='math');", childId),
                    UnlockedItems = new List<string>()
                };
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT item_id FROM inventory WHERE child_id=@child ORDER BY unlocked_at_utc,item_id;";
                    command.Parameters.AddWithValue("@child", childId);
                    using (var reader = command.ExecuteReader())
                        while (reader.Read()) progress.UnlockedItems.Add(Convert.ToString(reader[0], CultureInfo.InvariantCulture));
                }
                PopulateNextMilestone(progress);
                return progress;
            }
        }

        private GameWorldRewardResult SnapshotResult(string childId, bool created, IList<string> unlocked)
        {
            var progress = ReadProgress(childId);
            return new GameWorldRewardResult
            {
                RewardCreated = created,
                GrowthSteps = progress.GrowthSteps,
                CompletedMathSessions = progress.CompletedMathSessions,
                NewlyUnlockedItems = unlocked,
                SessionsUntilNextMilestone = progress.SessionsUntilNextMilestone,
                NextMilestoneSessionCount = progress.NextMilestoneSessionCount,
                NextMilestoneItemId = progress.NextMilestoneItemId
            };
        }

        private static void PopulateNextMilestone(GameWorldProgress progress)
        {
            if (progress == null) return;
            foreach (var milestone in Milestones)
            {
                if (milestone.Key <= progress.CompletedMathSessions) continue;
                progress.NextMilestoneSessionCount = milestone.Key;
                progress.NextMilestoneItemId = milestone.Value;
                progress.SessionsUntilNextMilestone = milestone.Key - progress.CompletedMathSessions;
                return;
            }
            progress.NextMilestoneSessionCount = 0;
            progress.NextMilestoneItemId = null;
            progress.SessionsUntilNextMilestone = 0;
        }

        private static int CountCompletedMathSessions(System.Data.SQLite.SQLiteConnection connection,
            System.Data.SQLite.SQLiteTransaction transaction, string childId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"SELECT count(*) FROM session s
WHERE s.child_id=@child AND s.state='completed' AND s.planned_subject='math'
  AND EXISTS (SELECT 1 FROM attempt a WHERE a.session_id=s.id AND a.child_id=s.child_id AND a.subject='math');";
                command.Parameters.AddWithValue("@child", childId);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private static int Count(System.Data.SQLite.SQLiteConnection connection, string sql, string childId)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.AddWithValue("@child", childId);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private static void Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(name + " is required.");
        }
    }
}
