using Database;
using Google.Protobuf.WellKnownTypes;
using MySql.Data.MySqlClient;
using MySql.Data.Types;
using System.Data;
using Serilog;
using System.Collections.Generic;
using System;

namespace DecisionEngineService.Repository
{
    public partial class MangosRepository
    {
        private const string DefaultConnectionString = "server=localhost;user=app;database=mangos;port=3306;password=app";
        private static string ConnectionString => GetConnectionString();

        public static int GetRowCountForTable(string tableName)
        {
            int count = 0;

            // Sanitize tableName to avoid SQL injection
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty.");
            }

            using (MySqlConnection connection = new(GetConnectionString()))
            {
                try
                {
                    connection.Open();

                    // Directly inject the sanitized table name into the query
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = $"SELECT COUNT(*) FROM `{tableName}`"; // Use backticks to escape the table name

                    count = Convert.ToInt32(command.ExecuteScalar());
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return count;
        }

        private static string GetConnectionString()
        {
            return Environment.GetEnvironmentVariable("WWOW_MANGOS_WORLD_CONNECTION_STRING")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__MangosWorld")
                ?? DefaultConnectionString;
        }

        private static Timestamp GetTimestampSafe(MySqlDataReader reader, string columnName)
        {
            MySqlDateTime mySqlDateTime = reader.GetMySqlDateTime(reader.GetOrdinal(columnName));

            // Check if the MySqlDateTime is a valid date or '0000-00-00 00:00:00'
            if (!mySqlDateTime.IsValidDateTime)
            {
                // Return a default value for '0000-00-00 00:00:00' or invalid dates
                return Timestamp.FromDateTime(DateTime.MinValue.ToUniversalTime());
            }

            // Convert to a valid DateTime and return the Timestamp
            return Timestamp.FromDateTime(mySqlDateTime.GetDateTime().ToUniversalTime());
        }

    }
}
