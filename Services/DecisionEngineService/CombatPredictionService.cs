using Communication;
using Microsoft.Data.Sqlite;
using Microsoft.ML;
using Microsoft.Extensions.Logging;
using System.IO;
using System;

namespace DecisionEngineService
{
    public class CombatPredictionService : IDisposable
    {
        private readonly MLContext _mlContext;
        private PredictionEngine<WoWActivitySnapshot, WoWActivitySnapshot> _predictionEngine;
        private ITransformer _trainedModel;
        private FileSystemWatcher? _fileWatcher;
        private bool _disposed;
        private readonly string _connectionString;
        private readonly string _dataDirectory;
        private readonly string _processedDirectory;
        private readonly ILogger<CombatPredictionService> _logger;

        public CombatPredictionService(string connectionString, string dataDirectory, string processedDirectory, ILogger<CombatPredictionService> logger)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(processedDirectory);

            _mlContext = new MLContext();
            _connectionString = connectionString;
            _dataDirectory = dataDirectory;
            _processedDirectory = processedDirectory;
            _logger = logger;

            LogServiceConfiguration();

            // Load the initial trained model from the SQLite database
            _trainedModel = LoadModelFromDatabase();

            if (_trainedModel != null)
            {
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<WoWActivitySnapshot, WoWActivitySnapshot>(_trainedModel);
            }
            else
            {
                _logger.LogWarning("No trained model found in database — prediction engine not available until first training run");
            }

            // Start monitoring for new `.bin` files
            MonitorForNewData();
        }

        private void LogServiceConfiguration()
        {
            _logger.LogInformation(
                "Starting CombatPredictionService | ConnectionString: {ConnectionString} DataDirectory: {DataDirectory} ProcessedDirectory: {ProcessedDirectory}",
                _connectionString,
                _dataDirectory,
                _processedDirectory);
        }

        // Method to load the model from the SQLite database
        private ITransformer LoadModelFromDatabase()
        {
            ITransformer model = null;

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT ModelData FROM TrainedModel ORDER BY Id DESC LIMIT 1";

                if (command.ExecuteScalar() is byte[] modelData)
                {
                    using var memoryStream = new MemoryStream(modelData);
                    DataViewSchema modelSchema;
                    model = _mlContext.Model.Load(memoryStream, out modelSchema);
                }
            }

            return model;
        }

        // Method to predict the action based on input combat data
        public WoWActivitySnapshot PredictAction(WoWActivitySnapshot inputData)
        {
            if (_predictionEngine == null)
            {
                throw new InvalidOperationException("Prediction engine is not initialized.");
            }

            // Predict the action based on the input data
            WoWActivitySnapshot prediction = _predictionEngine.Predict(inputData);
            return prediction;
        }

        // Method to update the model if necessary
        public void UpdateModel()
        {
            _trainedModel = LoadModelFromDatabase();
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<WoWActivitySnapshot, WoWActivitySnapshot>(_trainedModel);
        }

        // Method to monitor the data directory for new `.bin` files
        private void MonitorForNewData()
        {
            if (!Directory.Exists(_dataDirectory))
            {
                _logger.LogWarning("Data directory does not exist: {DataDirectory} — file monitoring disabled", _dataDirectory);
                return;
            }

            _fileWatcher = new FileSystemWatcher
            {
                Path = _dataDirectory,
                Filter = "*.bin",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };

            _fileWatcher.Created += OnNewDataFile;
            _fileWatcher.EnableRaisingEvents = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _fileWatcher?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        // Event handler for when a new `.bin` file is detected
        private void OnNewDataFile(object source, FileSystemEventArgs e)
        {
            string filePath = e.FullPath;
            _logger.LogInformation($"New data file detected: {filePath}");

            try
            {
                // Load and process the new data file
                _logger.LogInformation("Processing data file {FilePath}", filePath);
                IDataView newData = LoadData(filePath);

                // Update the model with the new data
                RetrainModel(newData);

                // Move the processed file to the processed directory
                MoveProcessedFile(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing new data file {filePath}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // Method to load data from a `.bin` file
        private IDataView LoadData(string filePath)
        {
            // Assuming `.bin` files are in a format that ML.NET can load directly.
            // If not, you'll need to parse them and convert to IDataView.
            return _mlContext.Data.LoadFromBinary(filePath);
        }

        // Method to retrain the model with new data
        private void RetrainModel(IDataView newData)
        {
            try
            {
                // Assuming you're using a similar pipeline to the one in the initial model training
                var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("ActionTaken")
                    .Append(_mlContext.Transforms.Concatenate("Features",
                        "self.health",
                        "self.max_health",
                        "self.position.x",
                        "self.position.y",
                        "self.position.z",
                        "self.facing",
                        "target_id",
                        "nearby_units"))
                    .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("ActionTaken", "Features"))
                    .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedAction", "PredictedLabel"));

                // Combine the new data with any existing data if necessary
                var combinedData = CombineWithExistingData(newData);

                // Train the model
                _trainedModel = pipeline.Fit(combinedData);

                // Update the prediction engine with the new model
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<WoWActivitySnapshot, WoWActivitySnapshot>(_trainedModel);

                // Save the updated model to the SQLite database
                SaveModelToDatabase(_trainedModel);

                _logger.LogInformation("Model retraining completed");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retraining model: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // Method to combine new data with existing data
        private static IDataView CombineWithExistingData(IDataView newData)
        {
            // Load existing data if needed and combine with newData
            // Assuming the existing data is stored in a format you can load
            // For simplicity, this example assumes only new data is used
            return newData;
        }

        // Method to save the trained model to the SQLite database
        private void SaveModelToDatabase(ITransformer model)
        {
            using var memoryStream = new MemoryStream();
            _mlContext.Model.Save(model, null, memoryStream);
            var modelData = memoryStream.ToArray();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO TrainedModel (ModelData) VALUES (@ModelData)";
            command.Parameters.AddWithValue("@ModelData", modelData);
            command.ExecuteNonQuery();
        }

        // Method to move processed `.bin` file to another directory
        private void MoveProcessedFile(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            string destPath = Path.Combine(_processedDirectory, fileName);

            try
            {
                if (File.Exists(destPath))
                {
                    File.Delete(destPath);
                }

                File.Move(filePath, destPath);
                _logger.LogInformation("Moved processed file from {FilePath} to {DestinationPath}", filePath, destPath);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error moving processed file {filePath}: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
