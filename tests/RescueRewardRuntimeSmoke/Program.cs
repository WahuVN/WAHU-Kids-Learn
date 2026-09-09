using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

                const string noAttemptChild = "child-ai4-no-attempt";
                const string noAttemptSession = "session-ai4-no-attempt";
                InsertChildAndSession(database, noAttemptChild, noAttemptSession);
                CompleteSession(database, noAttemptSession);
                var noAttemptIgnored = new GameWorldRewardService(database)
                    .GrantCompletedMathSession(noAttemptChild, noAttemptSession, 3);
                Assert(!noAttemptIgnored.RewardCreated, "completed_session_without_durable_attempt_cannot_mint_reward");
                Assert(CountRewards(database, noAttemptChild) == 0, "no_attempt_session_mints_no_reward_row");
                var noAttemptProgress = new GameWorldRewardService(database).ReadProgress(noAttemptChild);
                Assert(noAttemptProgress.GrowthSteps == 0 && noAttemptProgress.CompletedMathSessions == 0,
                    "no_attempt_completed_session_does_not_advance_garden_progress");
                InsertForeignGardenReward(database, noAttemptChild, noAttemptSession, GameWorldRewardService.RewardId,
                    "session_completed", GameWorldRewardService.SourceKeyForSession(noAttemptSession));
                const string orphanSession = "session-ai4-orphan-reward";
                InsertForeignGardenReward(database, noAttemptChild, orphanSession, GameWorldRewardService.RewardId,
                    "session_completed", GameWorldRewardService.SourceKeyForSession(orphanSession));
                Assert(new GameWorldRewardService(database).ReadProgress(noAttemptChild).GrowthSteps == 0,
                    "canonical_looking_reward_requires_real_eligible_session_and_attempt");

                const string dirtyChild = "child-ai4-dirty-reward";
                const string dirtySession = "session-ai4-dirty-reward";
                InsertChildAndSession(database, dirtyChild, dirtySession);
                InsertCheckpoint(database, dirtySession, dirtyChild, "dirty-q1", "dirty-a1", 1);
                InsertCheckpoint(database, dirtySession, dirtyChild, "dirty-q2", "dirty-a2", 1);
                InsertCheckpoint(database, dirtySession, dirtyChild, "dirty-q3", "dirty-a3", 1);
                CompleteSession(database, dirtySession);
                InsertForeignGardenReward(database, dirtyChild, dirtySession, "legacy_growth", "session_completed", "dirty-legacy-reward");
                InsertForeignGardenReward(database, dirtyChild, dirtySession, GameWorldRewardService.RewardId, "manual_adjustment", "dirty-manual-reward");
                InsertForeignGardenReward(database, dirtyChild, dirtySession, GameWorldRewardService.RewardId, "session_completed", "dirty-wrong-source-key");
                var dirtyWorld = new GameWorldRewardService(database);
                Assert(dirtyWorld.ReadProgress(dirtyChild).GrowthSteps == 0, "noncanonical_garden_rows_do_not_inflate_growth");
                Assert(dirtyWorld.ReconcileMissingCompletedMathSessionRewards(dirtyChild) == 1, "noncanonical_garden_rows_do_not_block_reconciliation");
                Assert(CountCanonicalRewards(database, dirtyChild, dirtySession) == 1, "reconciliation_creates_exactly_one_canonical_growth_reward");
                Assert(dirtyWorld.ReadProgress(dirtyChild).GrowthSteps == 1, "garden_growth_counts_only_canonical_reward_rows");
                const string milestoneChild = "child-ai4-milestones";
                var milestoneWorld = new GameWorldRewardService(database);
                GameWorldRewardResult milestoneResult = null;
                for (var i = 1; i <= 10; i++)
                {
                    var milestoneSession = "session-ai4-milestone-" + i.ToString(CultureInfo.InvariantCulture);
                    if (i == 1) InsertChildAndSession(database, milestoneChild, milestoneSession);
                    else InsertSessionForExistingChild(database, milestoneChild, milestoneSession);
                    for (var q = 1; q <= 3; q++)
                        InsertCheckpoint(database, milestoneSession, milestoneChild,
                            "milestone-q" + i + "-" + q, "milestone-a" + i + "-" + q, 1);
                    CompleteSession(database, milestoneSession);
                    milestoneResult = milestoneWorld.GrantCompletedMathSession(milestoneChild, milestoneSession, 3);
                    Assert(milestoneResult.RewardCreated, "milestone_session_" + i + "_creates_one_reward");
                    if (i == 1)
                        Assert(milestoneResult.NewlyUnlockedItems.Contains("garden_seedling"), "milestone_1_unlocks_seedling");
                    if (i == 2)
                        Assert(!milestoneResult.NewlyUnlockedItems.Contains("garden_flower_patch"), "milestone_2_does_not_unlock_flower_early");
                    if (i == 3)
                        Assert(milestoneResult.NewlyUnlockedItems.Contains("garden_flower_patch"), "milestone_3_unlocks_flower_patch");
                    if (i == 5)
                        Assert(!milestoneResult.NewlyUnlockedItems.Contains("garden_lantern"), "milestone_5_does_not_unlock_lantern_early");
                    if (i == 6)
                        Assert(milestoneResult.NewlyUnlockedItems.Contains("garden_lantern"), "milestone_6_unlocks_lantern");
                    if (i == 9)
                        Assert(!milestoneResult.NewlyUnlockedItems.Contains("garden_bench"), "milestone_9_does_not_unlock_bench_early");
                    if (i == 10)
                        Assert(milestoneResult.NewlyUnlockedItems.Contains("garden_bench"), "milestone_10_unlocks_bench");
                }
                var milestoneProgress = milestoneWorld.ReadProgress(milestoneChild);
                Assert(milestoneProgress.GrowthSteps == 10 && milestoneProgress.CompletedMathSessions == 10,
                    "ten_completed_sessions_produce_ten_growth_steps");
                Assert(milestoneProgress.UnlockedItems.Contains("garden_seedling") &&
                       milestoneProgress.UnlockedItems.Contains("garden_flower_patch") &&
                       milestoneProgress.UnlockedItems.Contains("garden_lantern") &&
                       milestoneProgress.UnlockedItems.Contains("garden_bench"),
                    "all_garden_milestones_persist_after_ten_sessions");
                Assert(milestoneProgress.SessionsUntilNextMilestone == 0 && milestoneProgress.NextMilestoneItemId == null,
                    "garden_milestone_progress_finishes_cleanly_after_last_threshold");
                var milestoneReplay = milestoneWorld.GrantCompletedMathSession(milestoneChild, "session-ai4-milestone-10", 3);
                Assert(!milestoneReplay.RewardCreated && milestoneReplay.GrowthSteps == 10 &&
                       milestoneReplay.NewlyUnlockedItems.Count == 0,
                    "milestone_replay_does_not_duplicate_growth_or_inventory");

                const string rollbackChild = "child-ai4-rollback";
                const string rollbackSession = "session-ai4-rollback";
                InsertChildAndSession(database, rollbackChild, rollbackSession);
                InsertCheckpoint(database, rollbackSession, rollbackChild, "rollback-q1", "rollback-a1", 1);
                InsertCheckpoint(database, rollbackSession, rollbackChild, "rollback-q2", "rollback-a2", 1);
                InsertCheckpoint(database, rollbackSession, rollbackChild, "rollback-q3", "rollback-a3", 1);
                CompleteSession(database, rollbackSession);
                InstallInventoryFailureTrigger(database);
                var rollbackFaulted = false;
                try
                {
                    new GameWorldRewardService(database).GrantCompletedMathSession(rollbackChild, rollbackSession, 3);
                }
                catch (Exception)
                {
                    rollbackFaulted = true;
                }
                Assert(rollbackFaulted, "inventory_write_fault_aborts_reward_transaction");
                Assert(CountCanonicalRewards(database, rollbackChild, rollbackSession) == 0,
                    "inventory_write_fault_rolls_back_reward_row");
                Assert(new GameWorldRewardService(database).ReadProgress(rollbackChild).GrowthSteps == 0,
                    "inventory_write_fault_leaves_garden_growth_unchanged");
                DropInventoryFailureTrigger(database);
                var rollbackWorld = new GameWorldRewardService(database);
                Assert(rollbackWorld.ReconcileMissingCompletedMathSessionRewards(rollbackChild) == 1,
                    "next_reconcile_repairs_transient_reward_failure_once");
                var rollbackRecovered = rollbackWorld.ReadProgress(rollbackChild);
                Assert(rollbackRecovered.GrowthSteps == 1 &&
                       rollbackRecovered.UnlockedItems.Contains("garden_seedling"),
                    "reward_recovers_cleanly_after_transient_inventory_failure");
                Assert(CountCanonicalRewards(database, rollbackChild, rollbackSession) == 1,
                    "recovered_transaction_persists_one_canonical_reward");
                var rollbackRescue = new RescueGameRewardService(database).Read(rollbackSession);
                Assert(rollbackRescue.GardenRewardRecorded && rollbackRescue.FinalChestUnlocked,
                    "reconciled_reward_unlocks_rescue_chest_from_durable_state");
                Assert(rollbackWorld.ReconcileMissingCompletedMathSessionRewards(rollbackChild) == 0,
                    "reconcile_after_recovery_is_idempotent");

                const string collisionChild = "child-ai4-source-collision";
                const string collisionSession = "session-ai4-source-collision";
                InsertChildAndSession(database, collisionChild, collisionSession);
                InsertCheckpoint(database, collisionSession, collisionChild, "collision-q1", "collision-a1", 1);
                InsertCheckpoint(database, collisionSession, collisionChild, "collision-q2", "collision-a2", 1);
                InsertCheckpoint(database, collisionSession, collisionChild, "collision-q3", "collision-a3", 1);
                CompleteSession(database, collisionSession);
                InsertForeignGardenReward(database, collisionChild, collisionSession, "legacy_growth", "session_completed",
                    GameWorldRewardService.SourceKeyForSession(collisionSession));
                var collisionRepair = new GameWorldRewardService(database)
                    .GrantCompletedMathSession(collisionChild, collisionSession, 3);
                Assert(collisionRepair.RewardCreated && collisionRepair.GrowthSteps == 1,
                    "canonical_source_key_collision_is_self_healed_once");
                Assert(CountCanonicalRewards(database, collisionChild, collisionSession) == 1,
                    "source_key_collision_repairs_exactly_one_canonical_row");
                var collisionSnapshot = new RescueGameRewardService(database).Read(collisionSession);
                Assert(collisionSnapshot.GardenRewardRecorded && collisionSnapshot.FinalChestUnlocked,
                    "self_healed_source_key_collision_unlocks_chest_from_durable_state");

                const string concurrentChild = "child-ai4-concurrent";
                const string concurrentSession = "session-ai4-concurrent";
                InsertChildAndSession(database, concurrentChild, concurrentSession);
                InsertCheckpoint(database, concurrentSession, concurrentChild, "concurrent-q1", "concurrent-a1", 1);
                InsertCheckpoint(database, concurrentSession, concurrentChild, "concurrent-q2", "concurrent-a2", 1);
                InsertCheckpoint(database, concurrentSession, concurrentChild, "concurrent-q3", "concurrent-a3", 1);
                CompleteSession(database, concurrentSession);
                var concurrentWorld = new GameWorldRewardService(database);
                var grantTasks = Enumerable.Range(0, 8)
                    .Select(_ => Task.Run(() => concurrentWorld.GrantCompletedMathSession(concurrentChild, concurrentSession, 3)))
                    .ToArray();
                Task.WaitAll(grantTasks);
                Assert(grantTasks.Count(x => x.Result.RewardCreated) == 1, "concurrent_reward_grant_has_one_creator");
                Assert(CountCanonicalRewards(database, concurrentChild, concurrentSession) == 1,
                    "concurrent_reward_grant_persists_one_canonical_row");
                Assert(concurrentWorld.ReadProgress(concurrentChild).GrowthSteps == 1,
                    "concurrent_reward_grant_counts_one_growth_step");

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

        private static void InsertSessionForExistingChild(LearningDatabase database, string childId, string sessionId)
        {
            using (var connection = database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"INSERT INTO session(id,child_id,started_at_utc,state,planned_subject,performance_profile)
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

        private static void InstallInventoryFailureTrigger(LearningDatabase database)
        {
            using (var connection = database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"CREATE TRIGGER ai4_reward_inventory_abort
BEFORE INSERT ON inventory
WHEN NEW.child_id='child-ai4-rollback'
BEGIN
    SELECT RAISE(ABORT, 'ai4_inventory_fault');
END;";
                command.ExecuteNonQuery();
            }
        }

        private static void DropInventoryFailureTrigger(LearningDatabase database)
        {
            using (var connection = database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DROP TRIGGER IF EXISTS ai4_reward_inventory_abort;";
                command.ExecuteNonQuery();
            }
        }

        private static void InsertForeignGardenReward(
            LearningDatabase database,
            string childId,
            string sessionId,
            string rewardId,
            string sourceEvent,
            string sourceKey)
        {
            using (var connection = database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"INSERT INTO reward_event(
id,child_id,reward_type,reward_id,source_event,source_ref,source_key,created_at_utc)
VALUES(@id,@child,@type,@reward,@event,@session,@key,@utc);";
                command.Parameters.AddWithValue("@id", "foreign-" + Guid.NewGuid().ToString("N"));
                command.Parameters.AddWithValue("@child", childId);
                command.Parameters.AddWithValue("@type", GameWorldRewardService.RewardType);
                command.Parameters.AddWithValue("@reward", rewardId);
                command.Parameters.AddWithValue("@event", sourceEvent);
                command.Parameters.AddWithValue("@session", sessionId);
                command.Parameters.AddWithValue("@key", sourceKey);
                command.Parameters.AddWithValue("@utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                command.ExecuteNonQuery();
            }
        }

        private static int CountCanonicalRewards(LearningDatabase database, string childId, string sessionId)
        {
            using (var connection = database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT COUNT(*) FROM reward_event
WHERE child_id=@child AND reward_type=@type AND reward_id=@reward
  AND source_event='session_completed' AND source_ref=@session AND source_key=@sourceKey;";
                command.Parameters.AddWithValue("@child", childId);
                command.Parameters.AddWithValue("@type", GameWorldRewardService.RewardType);
                command.Parameters.AddWithValue("@reward", GameWorldRewardService.RewardId);
                command.Parameters.AddWithValue("@session", sessionId);
                command.Parameters.AddWithValue("@sourceKey", GameWorldRewardService.SourceKeyForSession(sessionId));
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
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
