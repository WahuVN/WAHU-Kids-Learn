using System;
using System.Collections.Generic;
using System.Globalization;

namespace WAHU.Data
{
    public enum RescueRewardSignal
    {
        None = 0,
        CheckpointStarEarned = 1,
        MissionComplete = 2
    }

    public sealed class RescueRewardSnapshot
    {
        public string SessionId { get; set; }
        public string ChildId { get; set; }
        public int CheckpointStars { get; set; }
        public int CheckpointStarGoal { get; set; }
        public bool AllCheckpointStarsEarned { get; set; }
        public bool SessionCompleted { get; set; }
        public bool GardenRewardRecorded { get; set; }
        public bool FinalChestUnlocked { get; set; }
        public string CheckpointRewardId { get; set; }
        public string FinalChestRewardId { get; set; }
        public bool RewardIsDeterministic { get; set; }
        public int GardenGrowthSteps { get; set; }
        public int CompletedMathSessions { get; set; }
        public IList<string> UnlockedGardenItemIds { get; set; }
        public int SessionsUntilNextGardenMilestone { get; set; }
        public string NextGardenMilestoneItemId { get; set; }
    }

    public sealed class RescueRewardTransition
    {
        public RescueRewardSnapshot Snapshot { get; set; }
        public RescueRewardSignal Signal { get; set; }
        public int NewlyEarnedCheckpointStars { get; set; }
        public bool FinalChestNewlyUnlocked { get; set; }
    }

    /// <summary>
    /// Reward projection for the three-checkpoint rescue game.
    ///
    /// Checkpoint stars are deliberately derived from committed learning attempts instead of being
    /// inserted into reward_event. That makes checkpoint rewards durable across suspend/resume while
    /// preserving the existing contract that no durable Garden reward is minted before GAME_COMPLETE.
    /// The final chest is a deterministic presentation of the single idempotent Garden reward for the
    /// completed session; it is never a random/loot-box roll.
    /// </summary>
    public sealed class RescueGameRewardService
    {
        public const int CheckpointStarGoal = 3;
        public const string CheckpointRewardId = "rescue_checkpoint_star";
        public const string FinalChestRewardId = "garden_growth_step";

        private readonly LearningDatabase _database;
        private readonly GameWorldRewardService _garden;

        public RescueGameRewardService(LearningDatabase database)
        {
            _database = database ?? throw new ArgumentNullException("database");
            _garden = new GameWorldRewardService(database);
        }

        /// <summary>
        /// Safe entry point for new-game/resume UI. Reading state never emits a reward signal, so
        /// opening a resumed session cannot replay a checkpoint/chest celebration by itself.
        /// </summary>
        public RescueRewardTransition BeginOrResume(string sessionId)
        {
            return new RescueRewardTransition
            {
                Snapshot = Read(sessionId),
                Signal = RescueRewardSignal.None,
                NewlyEarnedCheckpointStars = 0,
                FinalChestNewlyUnlocked = false
            };
        }

        /// <summary>
        /// Reads the durable reward projection without creating anything.
        /// </summary>
        public RescueRewardSnapshot Read(string sessionId)
        {
            var identity = ReadSessionIdentity(sessionId);
            if (!string.Equals(identity.Subject, "math", StringComparison.Ordinal))
                throw new InvalidOperationException("Rescue reward state requires a math session.");

            var stars = CountDurableCheckpointStars(identity.SessionId, identity.ChildId);
            if (stars < 0) stars = 0;
            if (stars > CheckpointStarGoal) stars = CheckpointStarGoal;

            var gardenRecorded = HasGardenReward(identity.SessionId, identity.ChildId);
            var garden = _garden.ReadProgress(identity.ChildId);
            var sessionCompleted = string.Equals(identity.State, "completed", StringComparison.Ordinal);

            return new RescueRewardSnapshot
            {
                SessionId = identity.SessionId,
                ChildId = identity.ChildId,
                CheckpointStars = stars,
                CheckpointStarGoal = CheckpointStarGoal,
                AllCheckpointStarsEarned = stars >= CheckpointStarGoal,
                SessionCompleted = sessionCompleted,
                GardenRewardRecorded = gardenRecorded,
                FinalChestUnlocked = sessionCompleted && stars >= CheckpointStarGoal && gardenRecorded,
                CheckpointRewardId = CheckpointRewardId,
                FinalChestRewardId = FinalChestRewardId,
                RewardIsDeterministic = true,
                GardenGrowthSteps = garden.GrowthSteps,
                CompletedMathSessions = garden.CompletedMathSessions,
                UnlockedGardenItemIds = garden.UnlockedItems == null
                    ? new List<string>()
                    : new List<string>(garden.UnlockedItems),
                SessionsUntilNextGardenMilestone = garden.SessionsUntilNextMilestone,
                NextGardenMilestoneItemId = garden.NextMilestoneItemId
            };
        }

        /// <summary>
        /// Repairs a missing terminal Garden reward from durable learning state. This is safe to call
        /// repeatedly: GameWorldRewardService owns the unique source_key and inserts the reward once.
        /// Active/suspended sessions are read-only and cannot unlock the chest.
        /// </summary>
        public RescueRewardSnapshot ReconcileCompletedSession(string sessionId)
        {
            var snapshot = Read(sessionId);
            if (!snapshot.SessionCompleted || !snapshot.AllCheckpointStarsEarned || snapshot.GardenRewardRecorded)
                return snapshot;

            _garden.GrantCompletedMathSession(snapshot.ChildId, snapshot.SessionId, snapshot.CheckpointStars);
            return Read(sessionId);
        }

        /// <summary>
        /// Calculates a one-shot presentation signal by comparing a caller-held previous snapshot with
        /// current durable state. Passing previous=null intentionally yields no signal (resume-safe).
        /// Set reconcileTerminal=true when processing GAME_COMPLETE so a previously failed Garden write
        /// is repaired before the final-chest transition is calculated.
        /// </summary>
        public RescueRewardTransition ReadTransition(
            string sessionId,
            RescueRewardSnapshot previous,
            bool reconcileTerminal)
        {
            var current = reconcileTerminal ? ReconcileCompletedSession(sessionId) : Read(sessionId);
            if (previous == null)
            {
                return new RescueRewardTransition
                {
                    Snapshot = current,
                    Signal = RescueRewardSignal.None,
                    NewlyEarnedCheckpointStars = 0,
                    FinalChestNewlyUnlocked = false
                };
            }

            var newStars = current.CheckpointStars - previous.CheckpointStars;
            if (newStars < 0) newStars = 0;
            var chestNew = !previous.FinalChestUnlocked && current.FinalChestUnlocked;
            var signal = chestNew
                ? RescueRewardSignal.MissionComplete
                : (newStars > 0 ? RescueRewardSignal.CheckpointStarEarned : RescueRewardSignal.None);

            return new RescueRewardTransition
            {
                Snapshot = current,
                Signal = signal,
                NewlyEarnedCheckpointStars = newStars,
                FinalChestNewlyUnlocked = chestNew
            };
        }

        private SessionIdentity ReadSessionIdentity(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("sessionId");
            using (var connection = _database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT id, child_id, state, planned_subject
FROM session
WHERE id=@session
LIMIT 1;";
                command.Parameters.AddWithValue("@session", sessionId);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("Rescue session not found: " + sessionId);
                    return new SessionIdentity
                    {
                        SessionId = reader.GetString(0),
                        ChildId = reader.GetString(1),
                        State = reader.GetString(2),
                        Subject = reader.IsDBNull(3) ? null : reader.GetString(3)
                    };
                }
            }
        }

        private int CountDurableCheckpointStars(string sessionId, string childId)
        {
            using (var connection = _database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT COUNT(*)
FROM (
    SELECT a.question_id
    FROM attempt a
    INNER JOIN mastery_event m ON m.attempt_id=a.id
    WHERE a.session_id=@session
      AND a.child_id=@child
      AND a.subject='math'
      AND a.answered_at_utc IS NOT NULL
    GROUP BY a.question_id
);";
                command.Parameters.AddWithValue("@session", sessionId);
                command.Parameters.AddWithValue("@child", childId);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private bool HasGardenReward(string sessionId, string childId)
        {
            using (var connection = _database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT COUNT(*)
FROM reward_event
WHERE child_id=@child
  AND reward_type=@type
  AND source_event='session_completed'
  AND source_ref=@session;";
                command.Parameters.AddWithValue("@child", childId);
                command.Parameters.AddWithValue("@type", GameWorldRewardService.RewardType);
                command.Parameters.AddWithValue("@session", sessionId);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
            }
        }

        private sealed class SessionIdentity
        {
            public string SessionId { get; set; }
            public string ChildId { get; set; }
            public string State { get; set; }
            public string Subject { get; set; }
        }
    }
}
