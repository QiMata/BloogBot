using DecisionEngineService;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;

namespace PromptHandlingService.Tests
{
    public class DecisionEngineRuntimeTests
    {
        [Fact]
        public void FromConfigurationUsesRunnableDefaults()
        {
            var configuration = new ConfigurationBuilder().Build();

            var options = DecisionEngineRuntimeOptions.FromConfiguration(configuration);

            Assert.True(options.Enabled);
            Assert.Equal("127.0.0.1", options.ListenerIpAddress);
            Assert.Equal(8080, options.ListenerPort);
            Assert.True(Path.IsPathFullyQualified(options.DataDirectory));
            Assert.True(Path.IsPathFullyQualified(options.ProcessedDirectory));
            Assert.True(Path.IsPathFullyQualified(new SqliteConnectionStringBuilder(options.SqliteConnection).DataSource));
        }

        [Fact]
        public void FromConfigurationHonorsRuntimeOverrides()
        {
            var root = CreateTempRoot();
            try
            {
                var configuredData = Path.Combine(root, "snapshots");
                var configuredProcessed = Path.Combine(root, "processed");
                var configuredDatabase = Path.Combine(root, "models", "decision.db");
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["DecisionEngine:Enabled"] = "false",
                        ["DecisionEngine:DataDirectory"] = configuredData,
                        ["DecisionEngine:ProcessedDirectory"] = configuredProcessed,
                        ["DecisionEngine:SqliteConnection"] = $"Data Source={configuredDatabase}",
                        ["DecisionEngine:Listener:IpAddress"] = "0.0.0.0",
                        ["DecisionEngine:Listener:Port"] = "18080"
                    })
                    .Build();

                var options = DecisionEngineRuntimeOptions.FromConfiguration(configuration);

                Assert.False(options.Enabled);
                Assert.Equal(Path.GetFullPath(configuredData), options.DataDirectory);
                Assert.Equal(Path.GetFullPath(configuredProcessed), options.ProcessedDirectory);
                Assert.Equal(Path.GetFullPath(configuredDatabase), new SqliteConnectionStringBuilder(options.SqliteConnection).DataSource);
                Assert.Equal("0.0.0.0", options.ListenerIpAddress);
                Assert.Equal(18080, options.ListenerPort);
            }
            finally
            {
                TryDeleteDirectory(root);
            }
        }

        [Fact]
        public void EnsureRuntimeReadyCreatesWritableDirectoriesAndSchema()
        {
            var root = CreateTempRoot();
            try
            {
                var options = CreateTempRuntimeOptions(root);

                DecisionEngineRuntimeInitializer.EnsureRuntimeReady(options);

                AssertDirectoryWritable(options.DataDirectory);
                AssertDirectoryWritable(options.ProcessedDirectory);
                Assert.True(TableExists(options.SqliteConnection, "TrainedModel"));
                Assert.True(TableExists(options.SqliteConnection, "ModelWeights"));
            }
            finally
            {
                TryDeleteDirectory(root);
            }
        }

        [Fact]
        public void RuntimeStartsPredictionServiceAndSocketListener()
        {
            var root = CreateTempRoot();
            try
            {
                var options = CreateTempRuntimeOptions(root);

                using var runtime = new DecisionEngineRuntime(options, NullLoggerFactory.Instance);

                Assert.NotNull(runtime.PredictionService);
                Assert.NotNull(runtime.Listener);
                Assert.True(TableExists(options.SqliteConnection, "TrainedModel"));
            }
            finally
            {
                TryDeleteDirectory(root);
            }
        }

        private static DecisionEngineRuntimeOptions CreateTempRuntimeOptions(string root)
            => new()
            {
                Enabled = true,
                SqliteConnection = $"Data Source={Path.Combine(root, "db", "decision.db")}",
                DataDirectory = Path.Combine(root, "data"),
                ProcessedDirectory = Path.Combine(root, "processed"),
                ListenerIpAddress = "127.0.0.1",
                ListenerPort = 0
            };

        private static bool TableExists(string connectionString, string tableName)
        {
            SqliteProvider.EnsureInitialized();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
            command.Parameters.AddWithValue("$name", tableName);
            return Convert.ToInt32(command.ExecuteScalar()) == 1;
        }

        private static void AssertDirectoryWritable(string directory)
        {
            Assert.True(Directory.Exists(directory));
            var path = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
            File.WriteAllText(path, "probe");
            Assert.Equal("probe", File.ReadAllText(path));
            File.Delete(path);
        }

        private static string CreateTempRoot()
        {
            var path = Path.Combine(Path.GetTempPath(), $"wwow-decision-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
