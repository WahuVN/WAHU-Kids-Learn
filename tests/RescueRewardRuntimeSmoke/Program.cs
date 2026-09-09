using System;
using System.Globalization;
using System.IO;
using WAHU.Data;

namespace WAHU.RescueRewardRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        private static void Main()
        {
            var root = Environment.CurrentDirectory;
            var schema = Path.Combine(root, "data", "schema", "001_initial.sql");
            Assert(File.Exists(schema), "schema_found");

            var temp = Path.Combine(Path.GetTempPath(), "wahu-rescue-reward-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                var database = new LearningDatabase(Path.Combine(temp, "learning.db"), schema);
                var init = database.Initialize("DELETE");
                Assert(init.Health != null && init.Health.IsHealthy, "database_ready");

                const string child = "child-ai4-rescue";
                const string session = "session-ai4-rescue";
                InsertChildAndSession(database, child, session);

                var rewards = new RescueGameRewardService(database);
                var start = rewards.BeginOrResume(session);
                Assert(start.Signal == RescueRewardSignal.None, "new_game_does_not_emit_reward_signal");
                Assert(start.Snapshot.CheckpointStars == 0 && !start.Snapshot.FinalChestUnlocked, "new_game_reward_state_empty");
                Assert(CountRewards(database, child) == 0, "no_reward_event_before_checkpoint");

                InsertAttemptWithoutMastery(database, session, child, "checkpoint-q1", "attempt-q1-wrong", 1, false);
                var repairPending = rewards.ReadTransition(session, start.Snapshot, false);
                Assert(repairPending.Signal == RescueRewardSignal.None, "first_wrong_repair_does_not_emit_star");
                Assert(repairPending.Snapshot.CheckpointStars == 0, "first_wrong_repair_does_not_advance_checkpoint_reward");

                InsertCheckpoint(database, session, child, "checkpoint-q1", "attempt-q1-retry", 2);
                var cp1 = rewards.ReadTransition(session, repairPending.Snapshot, false);
                Assert(cp1.Signal == RescueRewardSignal.CheckpointStarEarned, "checkpoint_one_emits_star_after_repair_finishes");
                Assert(cp1.NewlyEarnedCheckpointStars == 1 && cp1.Snapshot.CheckpointStars == 1, "checkpoint_one_star_persisted");
                Assert(CountRewards(database, child) == 0, "checkpoint_star_does_not_mint_garden_reward");

                var resumed = rewards.BeginOrResume(session);
                Assert(resumed.Signal == RescueRewardSignal.None, "resume_does_not_replay_checkpoint_signal");
                Assert(resumed.Snapshot.CheckpointStars == 1, "resume_restores_checkpoint_star");

                InsertCheckpoint(database, session, child, "checkpoint-q1", "attempt-q1-replay", 3);
                var duplicateQuestion = rewards.ReadTransition(session, resumed.Snapshot, false);
                Assert(duplicateQuestion.Snapshot.CheckpointStars == 1, "retry_same_checkpoint_cannot_farm_star");
                Assert(duplicateQuestion.Signal == RescueRewardSignal.None, "retry_same_checkpoint_no_duplicate_star_signal");

                InsertCheckpoint(database, session, child, "checkpoint-q2", "attempt-q2", 1);
                var cp2 = rewards.ReadTransition(session, duplicateQuestion.Snapshot, false);
                Assert(cp2.Signal == RescueRewardSignal.CheckpointStarEarned && cp2.Snapshot.CheckpointStars == 2, "checkpoint_two_star_once");

                InsertCheckpoint(database, session, child, "checkpoint-q3", "attempt-q3", 1);
                var cp3 = rewards.ReadTransition(session, cp2.Snapshot, false);
                Assert(cp3.Signal == RescueRewardSignal.CheckpointStarEarned, "checkpoint_three_emits_star_signal");
                Assert(cp3.Snapshot.CheckpointStars == 3 && cp3.Snapshot.AllCheckpointStarsEarned, "three_checkpoint_stars_durable");
                Assert(!cp3.Snapshot.FinalChestUnlocked && CountRewards(database, child) == 0, "chest_locked_until_game_complete");

                var activeReconcile = rewards.ReconcileCompletedSession(session);
                Assert(!activeReconcile.FinalChestUnlocked && CountRewards(database, child) == 0, "active_session_cannot_mint_final_reward");

                CompleteSession(database, session);
                var completedBeforeRewardRepair = rewards.Read(session);
                Assert(completedBeforeRewardRepair.SessionCompleted && !completedBeforeRewardRepair.GardenRewardRecorded, "completed_learning_can_be_repaired_after_reward_write_gap");

                var final = rewards.ReadTransition(session, cp3.Snapshot, true);
                Assert(final.Signal == RescueRewardSignal.MissionComplete, "game_complete_emits_final_signal");
                Assert(final.FinalChestNewlyUnlocked && final.Snapshot.FinalChestUnlocked, "final_chest_unlocks_after_durable_garden_reward");
                Assert(final.Snapshot.GardenRewardRecorded && final.Snapshot.GardenGrowthSteps == 1, "garden_growth_persisted_once");
                Assert(final.Snapshot.UnlockedGardenItemIds.Contains("garden_seedling"), "first_garden_item_unlocked");
                Assert(final.Snapshot.RewardIsDeterministic, "reward_policy_is_deterministic");
                Assert(final.Snapshot.FinalChestRewardId == RescueGameRewardService.FinalChestRewardId, "final_chest_has_fixed_reward_id");
                Assert(final.Snapshot.FinalChestRewardId == GameWorldRewardService.RewardId, "final_chest_contract_matches_durable_reward_id");
                Assert(ReadRewardId(database, child, session) == GameWorldRewardService.RewardId, "durable_reward_row_uses_fixed_contract_id");
                Assert(CountRewards(database, child) == 1, "exactly_one_terminal_reward_event");

                var replaySameSession = rewards.ReconcileCompletedSession(session);
                Assert(replaySameSession.GardenGrowthSteps == 1 && CountRewards(database, child) == 1, "same_session_reward_idempotent");
                var repeatedTransition = rewards.ReadTransition(session, final.Snapshot, true);
                Assert(repeatedTransition.Signal == RescueRewardSignal.None && !repeatedTransition.FinalChestNewlyUnlocked, "resume_completed_game_does_not_replay_reward");
                Assert(CountRewards(database, child) == 1, "resume_completed_game_no_duplicate_reward");

                const string shortChild = "child-ai4-short";
                const string shortSession = "session-ai4-short";
                InsertChildAndSession(database, shortChild, shortSession);
                InsertCheckpoint(database, shortSession, shortChild, "short-q1", "short-a1", 1);
                InsertCheckpoint(database, shortSession, shortChild, "short-q2", "short-a2", 1);
                CompleteSession(database, shortSession);
                var shortReward = new RescueGameRewardService(database).ReconcileCompletedSession(shortSession);
                Assert(shortReward.CheckpointStars == 2 && !shortReward.AllCheckpointStarsEarned, "incomplete_three_checkpoint_game_keeps_two_stars");
                Assert(!shortReward.FinalChestUnlocked && !shortReward.GardenRewardRecorded, "incomplete_three_checkpoint_game_cannot_unlock_chest");
                Assert(CountRewards(database, shortChild) == 0, "incomplete_three_checkpoint_game_mints_no_reward");

                Console.WriteLine("RESCUE_REWARD_RUNTIME_SMOKE_PASS assertions=" + _assertions);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        private static void InsertChildAndSession(LearningDatabase database, string childId, string sessionId)
        {
            using (var connection = database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"INSERT INTO child(id,display_name,grade_level,created_at_utc,updated_at_utc)
VALUES(@child,'Bé cứu hộ',2,@now,@now);
INSERT INTO session(id,child_id,started_at_utc,state,planned_subject,performance_profile)
VALUES(@session,@child,@now,'active','math','LOW');";
                command.Parameters.AddWithValue("@child", childId);
                command.Parameters.AddWithValue("@session", sessionId);
                command.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                command.ExecuteNonQuery();
            }
        }

        private static void InsertAttemptWithoutMastery(
            LearningDatabase database,
            string sessionId,
            string childId,
            string questionId,
            string attemptId,
            int attemptIndex,
            bool isCorrect)
        {
            using (var connection = database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                command.CommandText = @"INSERT INTO attempt(
id,session_id,child_id,pack_id,pack_version,question_id,skill_id,subject,started_at_utc,answered_at_utc,
answer_json,is_correct,response_ms,hint_level,representation,input_method,attempt_index,listen_count)
VALUES(@id,@session,@child,'math_grade2_v1','1.0',@question,'SKILL_RESCUE_TEST','math',@now,@now,
'{}',@correct,500,0,'symbolic','mouse',@attemptIndex,0);";
                command.Parameters.AddWithValue("@id", attemptId);
                command.Parameters.AddWithValue("@session", sessionId);
                command.Parameters.AddWithValue("@child", childId);
                command.Parameters.AddWithValue("@question", questionId);
                command.Parameters.AddWithValue("@now", now);
                command.Parameters.AddWithValue("@correct", isCorrect ? 1 : 0);
                command.Parameters.AddWithValue("@attemptIndex", attemptIndex);
                command.ExecuteNonQuery();
            }
        }

        private static void InsertCheckpoint(
            LearningDatabase database,
            string sessionId,
            string childId,
            string questionId,
            string attemptId,
            int attemptIndex)
        {
            using (var connection = database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"INSERT INTO attempt(
id,session_id,child_id,pack_id,pack_version,question_id,skill_id,subject,started_at_utc,answered_at_utc,
answer_json,is_correct,response_ms,hint_level,representation,input_method,attempt_index,listen_count)
VALUES(@id,@session,@child,'math_grade2_v1','1.0',@question,'SKILL_RESCUE_TEST','math',@now,@now,
'{}',1,500,0,'symbolic','mouse',@attemptIndex,0);";
                    command.Parameters.AddWithValue("@id", attemptId);
                    command.Parameters.AddWithValue("@session", sessionId);
                    command.Parameters.AddWithValue("@child", childId);
                    command.Parameters.AddWithValue("@question", questionId);
                    command.Parameters.AddWithValue("@now", now);
                    command.Parameters.AddWithValue("@attemptIndex", attemptIndex);
                    command.ExecuteNonQuery();
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"INSERT INTO mastery_event(
id,child_id,skill_id,attempt_id,event_type,delta,score_before,score_after,confidence_after,
reason_json,mastery_engine_version,created_at_utc)
VALUES(@id,@child,'SKILL_RESCUE_TEST',@attempt,'PRACTICE_SUCCESS',0.05,0.25,0.30,0.30,'{}','ai4-smoke',@now);";
                    command.Parameters.AddWithValue("@id", "mastery-" + attemptId);
                    command.Parameters.AddWithValue("@child", childId);
                    command.Parameters.AddWithValue("@attempt", attemptId);
                    command.Parameters.AddWithValue("@now", now);
                    command.ExecuteNonQuery();
                }
                transaction.Commit();
            }
        }

        private static void CompleteSession(LearningDatabase database, string sessionId)
        {
            using (var connection = database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"UPDATE session
SET state='completed', ended_at_utc=@ended, summary_json='{""attempts"":3,""subject"":""math""}'
WHERE id=@session;";
                command.Parameters.AddWithValue("@ended", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("@session", sessionId);
                command.ExecuteNonQuery();
            }
        }

        private static int CountRewards(LearningDatabase database, string childId)
        {
            using (var connection = database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT COUNT(*) FROM reward_event
WHERE child_id=@child AND reward_type=@type;";
                command.Parameters.AddWithValue("@child", childId);
                command.Parameters.AddWithValue("@type", GameWorldRewardService.RewardType);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private static string ReadRewardId(LearningDatabase database, string childId, string sessionId)
        {
            using (var connection = database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT reward_id FROM reward_event
WHERE child_id=@child AND reward_type=@type AND source_event='session_completed' AND source_ref=@session
LIMIT 1;";
                command.Parameters.AddWithValue("@child", childId);
                command.Parameters.AddWithValue("@type", GameWorldRewardService.RewardType);
                command.Parameters.AddWithValue("@session", sessionId);
                var value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }
    }
}
