using System;
using System.Threading;

namespace WAHU.Platform
{
    public sealed class SingleInstanceGuard : IDisposable
    {
        private readonly Mutex _mutex;
        private bool _ownsMutex;

        private SingleInstanceGuard(Mutex mutex, bool ownsMutex)
        {
            _mutex = mutex;
            _ownsMutex = ownsMutex;
        }

        public bool IsPrimaryInstance { get { return _ownsMutex; } }

        public static SingleInstanceGuard TryAcquire(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name");
            var mutex = new Mutex(false, @"Local\" + name);
            var acquired = false;
            try
            {
                acquired = mutex.WaitOne(0, false);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }
            return new SingleInstanceGuard(mutex, acquired);
        }

        public void Dispose()
        {
            if (_ownsMutex)
            {
                try { _mutex.ReleaseMutex(); } catch { }
                _ownsMutex = false;
            }
            _mutex.Dispose();
        }
    }
}
