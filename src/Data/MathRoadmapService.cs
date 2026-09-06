using System;
using System.Globalization;

namespace WAHU.Data
{
    public sealed class MathRoadmapGroupProgress
    {
        public string GroupId { get; set; }
        public int SkillRows { get; set; }
        public int Attempts { get; set; }
        public double MasteryAverage { get; set; }
        public bool HasEvidence { get { return Attempts > 0 && SkillRows > 0; } }
    }

    public sealed class MathRoadmapSnapshot
    {
        public MathRoadmapGroupProgress NumberSense { get; set; }
        public MathRoadmapGroupProgress Mental20 { get; set; }
        public MathRoadmapGroupProgress Written1000 { get; set; }
        public MathRoadmapGroupProgress Tables25 { get; set; }
        public MathRoadmapGroupProgress Measurement { get; set; }
        public MathRoadmapGroupProgress Chance { get; set; }
        public int TotalTrackedAttempts
        {
            get
            {
                return Attempts(NumberSense) + Attempts(Mental20) + Attempts(Written1000) + Attempts(Tables25) + Attempts(Measurement) + Attempts(Chance);
            }
        }

        private static int Attempts(MathRoadmapGroupProgress group) { return group == null ? 0 : group.Attempts; }
    }

    public sealed class MathRoadmapService
    {
        private readonly LearningDatabase _database;

        public MathRoadmapService(LearningDatabase database)
        {
            _database = database ?? throw new ArgumentNullException("database");
        }

        public MathRoadmapSnapshot Read(string childId)
        {
            if (string.IsNullOrWhiteSpace(childId)) throw new ArgumentException("childId is required.");
            var numberSense = NewGroup("number_sense_1000");
            var mental = NewGroup("mental_20");
            var written = NewGroup("written_1000");
            var tables = NewGroup("tables_2_5");
            var measurement = NewGroup("measurement_geometry");
            var chance = NewGroup("chance_events");
            var numberSenseScore = 0.0;
            var mentalScore = 0.0;
            var writtenScore = 0.0;
            var tableScore = 0.0;
            var measurementScore = 0.0;
            var chanceScore = 0.0;

            using (var connection = _database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT skill_id,mastery_score,attempts_count
FROM child_skill WHERE child_id=@child AND subject='math';";
                command.Parameters.AddWithValue("@child", childId);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var skill = Convert.ToString(reader[0], CultureInfo.InvariantCulture);
                        var mastery = Clamp01(Convert.ToDouble(reader[1], CultureInfo.InvariantCulture));
                        var attempts = Math.Max(0, Convert.ToInt32(reader[2], CultureInfo.InvariantCulture));
                        if (skill == "PLACE_VALUE_HUNDREDS_TENS_ONES" || skill == "NUM_EXPANDED_FORM_HTO" ||
                            skill == "NUM_PREDECESSOR_SUCCESSOR" || skill == "NUM_COMPARE_0_1000" ||
                            skill == "NUM_FULL_HUNDREDS_RECOGNIZE" || skill == "NUMBER_RAY_FILL" ||
                            skill == "NUM_MIN_MAX_UP_TO_4" || skill == "NUM_SORT_UP_TO_4")
                            Add(numberSense, ref numberSenseScore, mastery, attempts);
                        else if (skill == "MENTAL_ADD_SUB_WITHIN_20")
                            Add(mental, ref mentalScore, mastery, attempts);
                        else if (skill == "TIMES_TABLE_2" || skill == "TIMES_TABLE_5" ||
                                 skill == "DIVIDE_TABLE_2" || skill == "DIVIDE_TABLE_5" ||
                                 skill == "MULTIPLICATION_MEANING" || skill == "DIVISION_MEANING" ||
                                 skill == "MULTIPLICATION_COMPONENTS" || skill == "DIVISION_COMPONENTS")
                            Add(tables, ref tableScore, mastery, attempts);
                        else if (skill == "POLYLINE_LENGTH_SUM_SEGMENTS" || skill == "CLOCK_MINUTE_HAND_AT_3_OR_6" ||
                                 skill == "HEAVIER_LIGHTER" || skill == "MASS_KG_READ_WRITE" || skill == "CAPACITY_LITER_READ_WRITE" ||
                                 skill == "LENGTH_DM_M_KM_RECOGNIZE_RELATION" || skill == "TIME_DAY_24_HOURS" ||
                                 skill == "TIME_HOUR_60_MINUTES" || skill == "CALENDAR_DAYS_IN_MONTH_DATE" || IsGeometrySkill(skill))
                            Add(measurement, ref measurementScore, mastery, attempts);
                        else if (skill == "EVENT_POSSIBLE" || skill == "EVENT_CERTAIN" || skill == "EVENT_IMPOSSIBLE" ||
                                 skill == "PICTOGRAPH_READ_DESCRIBE" || skill == "PICTOGRAPH_SIMPLE_INFERENCE")
                            Add(chance, ref chanceScore, mastery, attempts);
                        else if (!string.IsNullOrWhiteSpace(skill) &&
                                 (skill.StartsWith("ADD_WITHIN_1000", StringComparison.Ordinal) ||
                                  skill.StartsWith("SUB_WITHIN_1000", StringComparison.Ordinal) ||
                                  skill.StartsWith("WP_ONE_STEP_", StringComparison.Ordinal) ||
                                  skill == "ADD_COMPONENTS_RECOGNIZE" || skill == "SUB_COMPONENTS_RECOGNIZE" ||
                                  skill == "OPERATION_MEANING_FROM_VISUAL" || skill == "WP_SELECT_OPERATION_ONE_STEP" ||
                                  skill == "ADD_SUB_TWO_OPERATORS_LEFT_TO_RIGHT" || skill == "MENTAL_ADD_SUB_ROUND_TENS_HUNDREDS_1000"))
                            Add(written, ref writtenScore, mastery, attempts);
                    }
                }
            }

            FinalizeAverage(numberSense, numberSenseScore);
            FinalizeAverage(mental, mentalScore);
            FinalizeAverage(written, writtenScore);
            FinalizeAverage(tables, tableScore);
            FinalizeAverage(measurement, measurementScore);
            FinalizeAverage(chance, chanceScore);
            return new MathRoadmapSnapshot
            {
                NumberSense = numberSense,
                Mental20 = mental,
                Written1000 = written,
                Tables25 = tables,
                Measurement = measurement,
                Chance = chance
            };
        }

        private static MathRoadmapGroupProgress NewGroup(string id)
        {
            return new MathRoadmapGroupProgress { GroupId = id, SkillRows = 0, Attempts = 0, MasteryAverage = 0.0 };
        }

        private static void Add(MathRoadmapGroupProgress group, ref double scoreSum, double mastery, int attempts)
        {
            group.SkillRows++;
            group.Attempts += attempts;
            scoreSum += mastery;
        }

        private static void FinalizeAverage(MathRoadmapGroupProgress group, double sum)
        {
            group.MasteryAverage = group.SkillRows <= 0 ? 0.0 : Clamp01(sum / group.SkillRows);
        }

        private static bool IsGeometrySkill(string skill)
        {
            switch (skill)
            {
                case "POINT_RECOGNIZE":
                case "LINE_SEGMENT_RECOGNIZE":
                case "CURVE_RECOGNIZE":
                case "STRAIGHT_LINE_RECOGNIZE":
                case "POLYLINE_RECOGNIZE":
                case "THREE_COLLINEAR_POINTS":
                case "QUADRILATERAL_RECOGNIZE":
                case "CYLINDER_RECOGNIZE":
                case "SPHERE_RECOGNIZE":
                    return true;
                default:
                    return false;
            }
        }

        private static double Clamp01(double value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }
    }
}
