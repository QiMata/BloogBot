using Communication;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DecisionEngineService
{
    public class DecisionEngine : IDisposable
    {
        private readonly MLModel _model;
        private readonly SQLiteDatabase _db;
        private readonly string _binFileDirectory;
        private readonly FileSystemWatcher _fileWatcher;
        private readonly CancellationTokenSource _watcherCts = new();
        private bool _disposed;

        public DecisionEngine(string binFileDirectory, SQLiteDatabase db)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(binFileDirectory);
            ArgumentNullException.ThrowIfNull(db);

            _binFileDirectory = binFileDirectory;
            _db = db;
            _model = LoadModelFromDatabase();
            _fileWatcher = InitializeFileWatcher();
        }

        private FileSystemWatcher InitializeFileWatcher()
        {
            var watcher = new FileSystemWatcher(_binFileDirectory, "*.bin")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };

            watcher.Created += (_, e) => _ = ProcessBinFileAsync(e.FullPath, _watcherCts.Token);
            watcher.EnableRaisingEvents = true;

            return watcher;
        }

        private async Task ProcessBinFileAsync(string filePath, CancellationToken cancellationToken)
        {
            try
            {
                var snapshots = await ReadBinFileAsync(filePath, cancellationToken).ConfigureAwait(false);
                foreach (var snapshot in snapshots)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _model.LearnFromSnapshot(snapshot);
                }

                SaveModelToDatabase();

                if (File.Exists(filePath))
                {
                    File.Delete(filePath); // Clean up after processing
                }
            }
            catch (OperationCanceledException)
            {
                // shutting down, ignore
            }
        }

        public static List<ActionMap> GetNextActions(WoWActivitySnapshot snapshot)
        {
            return MLModel.Predict(snapshot);
        }

        private static async Task<List<WoWActivitySnapshot>> ReadBinFileAsync(string filePath, CancellationToken cancellationToken)
        {
            const int maxAttempts = 5;
            const int delayBetweenAttemptsMs = 50;
            Exception? lastError = null;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    List<WoWActivitySnapshot> snapshots = [];

                    while (stream.Position < stream.Length)
                    {
                        WoWActivitySnapshot snapshot = WoWActivitySnapshot.Parser.ParseDelimitedFrom(stream);
                        if (snapshot is null)
                        {
                            break;
                        }

                        snapshots.Add(snapshot);
                    }

                    return snapshots;
                }
                catch (InvalidProtocolBufferException ex)
                {
                    lastError = ex;
                }
                catch (IOException ex)
                {
                    lastError = ex;
                }

                if (attempt < maxAttempts - 1)
                {
                    await Task.Delay(delayBetweenAttemptsMs, cancellationToken).ConfigureAwait(false);
                }
            }

            throw new IOException($"Unable to read {filePath} after {maxAttempts} attempts.", lastError);
        }

        private void SaveModelToDatabase()
        {
            _db.SaveModelWeights(_model.GetWeights());
        }

        private MLModel LoadModelFromDatabase()
        {
            var weights = _db.LoadModelWeights();
            return new MLModel(weights);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _watcherCts.Cancel();
                _fileWatcher?.Dispose();
                _watcherCts.Dispose();
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    public class SQLiteDatabase(string connectionString)
    {
        private readonly string _connectionString = connectionString;

        public void SaveModelWeights(List<float> weights)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            using var cmd = new SQLiteCommand("INSERT INTO ModelWeights (weights) VALUES (@weights)", connection);
            cmd.Parameters.Add("@weights", System.Data.DbType.Binary).Value = ToBinary(weights);
            cmd.ExecuteNonQuery();
        }

        public List<float> LoadModelWeights()
        {
            List<float> weights = [];
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                using var cmd = new SQLiteCommand("SELECT weights FROM ModelWeights ORDER BY id DESC LIMIT 1", connection);
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(0))
                    {
                        weights = FromBinary((byte[])reader["weights"]);
                    }
                }
            }

            return weights;
        }

        private static byte[] ToBinary(IReadOnlyList<float> weights)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(weights.Count);
            foreach (var weight in weights)
            {
                writer.Write(weight);
            }

            writer.Flush();
            return stream.ToArray();
        }

        private static List<float> FromBinary(byte[] data)
        {
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            var count = reader.ReadInt32();
            List<float> weights = new(count);
            for (int i = 0; i < count && stream.Position < stream.Length; i++)
            {
                weights.Add(reader.ReadSingle());
            }

            return weights;
        }
    }
}
