using System;
using System.Globalization;

namespace WAHU.Data
{
    public sealed class ParentLearningSummary
    {
        public int SessionCount { get; set; }
        public int AttemptCount { get; set; }
        public int SkillCount { get; set; }
        public int StableSkillCount { get; set; }
        public int LearningSkillCount { get; set; }
        public int ReviewSkillCount { get; set; }
    }

    public static class ParentSummaryService
    {
        public static ParentLearningSummary Read(LearningDatabase database)
        {
            if (database == null) throw new ArgumentNullException("database");
            using (var connection = database.OpenConnection())
            {
                return new ParentLearningSummary
                {
                    SessionCount = Count(connection, "SELECT count(*) FROM session;"),
                    AttemptCount = Count(connection, "SELECT count(*) FROM attempt;"),
                    SkillCount = Count(connection, "SELECT count(*) FROM child_skill;"),
                    StableSkillCount = Count(connection, "SELECT count(*) FROM child_skill WHERE learning_state IN ('MASTERED','STABLE','VUNG');"),
                    LearningSkillCount = Count(connection, "SELECT count(*) FROM child_skill WHERE learning_state IN ('LEARNING','DEVELOPING','DANG_HOC');"),
                    ReviewSkillCount = Count(connection, "SELECT count(*) FROM child_skill WHERE learning_state IN ('REVIEW','NEEDS_REVIEW','CAN_ON');")
                };
            }
        }

        private static int Count(System.Data.SQLite.SQLiteConnection connection, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }
    }
}
