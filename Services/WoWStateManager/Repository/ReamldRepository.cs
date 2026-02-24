using MySql.Data.MySqlClient;
using Serilog;
using System;

namespace WoWStateManager.Repository
{
    public class ReamldRepository
    {
        private static readonly string ConnectionString = "server=localhost;user=root;database=realmd;port=3306;password=root";

        public static bool CheckIfAccountExists(string accountName)
        {
            using MySqlConnection connection = new(ConnectionString);
            try
            {
                connection.Open();

                MySqlCommand command = connection.CreateCommand();
                command.CommandText = @$"SELECT * FROM account where username = '{accountName}'";

                using MySqlDataReader reader = command.ExecuteReader();
                return reader.Read();
            }
            catch (Exception ex)
            {
                Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
            }

            return false;
        }

        /// <summary>
        /// Set GM level directly in the account table. The SOAP command .account set gmlevel
        /// writes to account_access, but brotalnia's build reads from account.gmlevel.
        /// This method updates BOTH tables for compatibility.
        /// </summary>
        public static bool SetGMLevel(string accountName, int gmLevel)
        {
            using MySqlConnection connection = new(ConnectionString);
            try
            {
                connection.Open();

                // Update account.gmlevel (read by brotalnia's world server at login)
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE account SET gmlevel = @gm WHERE username = @name";
                cmd.Parameters.AddWithValue("@gm", gmLevel);
                cmd.Parameters.AddWithValue("@name", accountName);
                var rows = cmd.ExecuteNonQuery();

                Log.Information($"[MANGOS REPO] Set gmlevel={gmLevel} for '{accountName}' in account table ({rows} row(s))");
                return rows > 0;
            }
            catch (Exception ex)
            {
                Log.Error($"[MANGOS REPO] SetGMLevel failed: {ex.Message}");
                return false;
            }
        }
    }
}
