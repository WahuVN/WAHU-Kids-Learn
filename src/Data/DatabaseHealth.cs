using System;
using System.Data.SQLite;
using System.Globalization;

namespace WAHU.Data
{
    public sealed class DatabaseHealthResult
    {
        public bool IsHealthy { get; set; }
        public string Integrity { get; set; }
        public int ForeignKeyIssues { get; set; }
        public string SchemaVersion { get; set; }
        public string JournalMode { get; set; }
    }

    public static class DatabaseHealth
    {
        public static DatabaseHealthResult Check(SQLiteConnection connection)
        {
            if (connection == null) throw new ArgumentNullException("connection");
            var result = new DatabaseHealthResult();
            result.Integrity = Convert.ToString(Scalar(connection, "PRAGMA integrity_check;"), CultureInfo.InvariantCulture);
            result.ForeignKeyIssues = Convert.ToInt32(Scalar(connection, "SELECT count(*) FROM pragma_foreign_key_check;"), CultureInfo.InvariantCulture);
            result.SchemaVersion = Convert.ToString(Scalar(connection, "SELECT value FROM app_meta WHERE key='schema_version';"), CultureInfo.InvariantCulture);
            result.JournalMode = Convert.ToString(Scalar(connection, "PRAGMA journal_mode;"), CultureInfo.InvariantCulture).ToUpperInvariant();
            result.IsHealthy = string.Equals(result.Integrity, "ok", StringComparison.OrdinalIgnoreCase) &&
                               result.ForeignKeyIssues == 0 &&
                               (result.SchemaVersion == "1" || result.SchemaVersion == "2" || result.SchemaVersion == "3" || result.SchemaVersion == "4");
            return result;
        }

        internal static object Scalar(SQLiteConnection connection, string sql)
        {
            using (var command = connection.CreateCommand()) { command.CommandText = sql; return command.ExecuteScalar(); }
        }
    }
}
