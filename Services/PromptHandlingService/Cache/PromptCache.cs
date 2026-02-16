using System;
using System.Linq;
using PromptHandlingService.Cache.Models;
using SQLite;

namespace PromptHandlingService.Cache
{
    public class PromptCache : IDisposable
    {
        private readonly SQLiteConnection _conn;
        private readonly string _databasePath;
        private bool _disposed;

        public PromptCache(string sqlitePath)
        {
            _databasePath = sqlitePath;
            _conn = new SQLiteConnection(sqlitePath);
            _conn.CreateTable<PreviousPrompts>();
        }

        public string? CheckForPreviousRun(string prompt)
        {
            EnsureNotDisposed();
            var hash = prompt.GetHashCode(StringComparison.OrdinalIgnoreCase);
            var results = _conn.Query<PreviousPrompts>("select * from PreviousPrompts where PromptHash = ?", hash);
            if (results.Count <= 0)
            {
                return null;
            }

            return results.First().Response;
        }

        public void AddPrevious(string prompt, string response)
        {
            EnsureNotDisposed();
            _conn.Insert(new PreviousPrompts
            {
                PromptHash = prompt.GetHashCode(StringComparison.OrdinalIgnoreCase),
                Response = response,
                TotalPrompt = prompt
            });
        }

        public string DatabasePath => _databasePath;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || _disposed)
            {
                return;
            }

            _conn.Dispose();
            _disposed = true;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PromptCache));
            }
        }
    }
}
