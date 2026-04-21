using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace DecisionEngineService
{
    public sealed class DecisionEngineRuntimeOptions
    {
        public bool Enabled { get; init; } = true;
        public string SqliteConnection { get; init; } = "";
        public string DataDirectory { get; init; } = "";
        public string ProcessedDirectory { get; init; } = "";
        public string ListenerIpAddress { get; init; } = "127.0.0.1";
        public int ListenerPort { get; init; } = 8080;

        public static DecisionEngineRuntimeOptions FromConfiguration(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var section = configuration.GetSection("DecisionEngine");
            var baseDirectory = AppContext.BaseDirectory;

            var dataDirectory = ResolvePath(
                section["DataDirectory"],
                Path.Combine(baseDirectory, "decision-engine", "data"));
            var processedDirectory = ResolvePath(
                section["ProcessedDirectory"],
                Path.Combine(baseDirectory, "decision-engine", "processed"));
            var sqliteConnection = NormalizeSqliteConnection(
                section["SqliteConnection"] ?? $"Data Source={Path.Combine(baseDirectory, "decision_engine.db")}",
                baseDirectory);

            return new DecisionEngineRuntimeOptions
            {
                Enabled = section.GetValue("Enabled", true),
                SqliteConnection = sqliteConnection,
                DataDirectory = dataDirectory,
                ProcessedDirectory = processedDirectory,
                ListenerIpAddress = section["Listener:IpAddress"]
                    ?? section["IpAddress"]
                    ?? "127.0.0.1",
                ListenerPort = section.GetValue("Listener:Port", section.GetValue("Port", 8080))
            };
        }

        private static string ResolvePath(string? configuredPath, string defaultPath)
        {
            var path = string.IsNullOrWhiteSpace(configuredPath) ? defaultPath : configuredPath;
            return Path.GetFullPath(path);
        }

        private static string NormalizeSqliteConnection(string connectionString, string baseDirectory)
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            if (!ShouldResolveDataSource(builder.DataSource))
                return builder.ConnectionString;

            builder.DataSource = Path.IsPathFullyQualified(builder.DataSource)
                ? builder.DataSource
                : Path.GetFullPath(builder.DataSource, baseDirectory);

            return builder.ConnectionString;
        }

        private static bool ShouldResolveDataSource(string? dataSource)
        {
            if (string.IsNullOrWhiteSpace(dataSource))
                return false;

            return !dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
                && !dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
        }
    }
}
