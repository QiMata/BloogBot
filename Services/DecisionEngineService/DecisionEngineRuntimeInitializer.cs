using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace DecisionEngineService
{
    internal static class DecisionEngineRuntimeInitializer
    {
        public static void EnsureRuntimeReady(DecisionEngineRuntimeOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            SqliteProvider.EnsureInitialized();

            CreateWritableDirectory(options.DataDirectory);
            CreateWritableDirectory(options.ProcessedDirectory);
            CreateSqliteParentDirectory(options.SqliteConnection);
            EnsureSqliteSchema(options.SqliteConnection);
        }

        private static void CreateWritableDirectory(string directory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);

            Directory.CreateDirectory(directory);

            var probePath = Path.Combine(directory, $".wwow-write-test-{Guid.NewGuid():N}.tmp");
            using (File.Create(probePath))
            {
            }

            File.Delete(probePath);
        }

        private static void CreateSqliteParentDirectory(string connectionString)
        {
            var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
            if (string.IsNullOrWhiteSpace(dataSource)
                || dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
                || dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var parent = Path.GetDirectoryName(Path.GetFullPath(dataSource));
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);
        }

        private static void EnsureSqliteSchema(string connectionString)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS TrainedModel (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ModelData BLOB NOT NULL,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS ModelWeights (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    weights TEXT NOT NULL,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
                );
                """;
            command.ExecuteNonQuery();
        }
    }
}
