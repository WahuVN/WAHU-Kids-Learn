using System;
using System.Data.SQLite;

namespace WAHU.Data
{
    public sealed class SQLiteConnectionFactory
    {
        private readonly string _databasePath;
        private readonly int _busyTimeoutMs;

        public SQLiteConnectionFactory(string databasePath, int busyTimeoutMs)
        {
            if (string.IsNullOrWhiteSpace(databasePath)) throw new ArgumentException("databasePath");
            _databasePath = databasePath;
            _busyTimeoutMs = busyTimeoutMs > 0 ? busyTimeoutMs : 2500;
        }

        public SQLiteConnection Open()
        {
            var connection = new SQLiteConnection("Data Source=" + _databasePath + ";Version=3;Foreign Keys=True;Pooling=False;");
            connection.Open();
            Execute(connection, "PRAGMA foreign_keys=ON;");
            Execute(connection, "PRAGMA busy_timeout=" + _busyTimeoutMs + ";");
            return connection;
        }

        private static void Execute(SQLiteConnection connection, string sql)
        {
            using (var command = connection.CreateCommand()) { command.CommandText = sql; command.ExecuteNonQuery(); }
        }
    }
}
