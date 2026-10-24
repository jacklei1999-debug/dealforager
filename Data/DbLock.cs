using System.Threading;

namespace WebUIMVC.Data
{
    /// <summary>
    /// Global SQLite write synchronization primitive to prevent 'database is locked' errors
    /// across concurrent HTTP requests and background ingestion workers.
    /// </summary>
    public static class DbLock
    {
        public static readonly SemaphoreSlim WriteLock = new SemaphoreSlim(1, 1);
    }
}

