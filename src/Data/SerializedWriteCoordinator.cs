using System;
using System.Data.SQLite;

namespace WAHU.Data
{
    public sealed class SerializedWriteCoordinator
    {
        private readonly object _gate = new object();
        private readonly SQLiteConnectionFactory _factory;

        public SerializedWriteCoordinator(SQLiteConnectionFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException("factory");
        }

        public void Execute(Action<SQLiteConnection, SQLiteTransaction> write)
        {
            if (write == null) throw new ArgumentNullException("write");
            Execute<object>((connection, transaction) =>
            {
                write(connection, transaction);
                return null;
            });
        }

        public T Execute<T>(Func<SQLiteConnection, SQLiteTransaction, T> write)
        {
            if (write == null) throw new ArgumentNullException("write");
            lock (_gate)
            {
                using (var connection = _factory.Open())
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var result = write(connection, transaction);
                        transaction.Commit();
                        return result;
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        public void ExecuteExclusive(Action action)
        {
            if (action == null) throw new ArgumentNullException("action");
            lock (_gate) action();
        }

        public T ExecuteExclusive<T>(Func<T> action)
        {
            if (action == null) throw new ArgumentNullException("action");
            lock (_gate) return action();
        }
    }
}
