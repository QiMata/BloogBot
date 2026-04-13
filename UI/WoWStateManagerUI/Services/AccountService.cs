using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace WoWStateManagerUI.Services
{
    public class AccountInfo
    {
        public uint Id { get; set; }
        public string Username { get; set; } = "";
        public int GmLevel { get; set; }
        public bool Online { get; set; }
        public bool Banned { get; set; }
        public DateTime JoinDate { get; set; }
        public DateTime LastLogin { get; set; }
        public string LastIp { get; set; } = "";
        public int FailedLogins { get; set; }
        public int NumCharacters { get; set; }
        public int Expansion { get; set; }
    }

    /// <summary>
    /// Read-only MySQL queries against the realmd database for account listing,
    /// plus SOAP-based mutations for create/delete.
    /// </summary>
    public class AccountService
    {
        private readonly string _connectionString;

        public AccountService(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// List all accounts with character counts from the realmd database.
        /// </summary>
        public async Task<List<AccountInfo>> GetAllAccountsAsync()
        {
            var accounts = new List<AccountInfo>();

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            // Left join realmcharacters to get character counts per account
            var sql = @"
                SELECT
                    a.id, a.username, a.gmlevel, a.online, a.banned,
                    a.joindate, a.last_login, a.last_ip, a.failed_logins,
                    a.expansion,
                    COALESCE(rc.numchars, 0) AS numchars
                FROM account a
                LEFT JOIN realmcharacters rc ON a.id = rc.acctid
                ORDER BY a.id";

            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = (MySqlDataReader) await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                accounts.Add(new AccountInfo
                {
                    Id = reader.GetUInt32(reader.GetOrdinal("id")),
                    Username = reader.GetString(reader.GetOrdinal("username")),
                    GmLevel = reader.GetInt32(reader.GetOrdinal("gmlevel")),
                    Online = reader.GetBoolean(reader.GetOrdinal("online")),
                    Banned = reader.GetBoolean(reader.GetOrdinal("banned")),
                    JoinDate = reader.GetDateTime(reader.GetOrdinal("joindate")),
                    LastLogin = TryGetDateTime(reader, reader.GetOrdinal("last_login")),
                    LastIp = reader.GetString(reader.GetOrdinal("last_ip")),
                    FailedLogins = reader.GetInt32(reader.GetOrdinal("failed_logins")),
                    Expansion = reader.GetInt32(reader.GetOrdinal("expansion")),
                    NumCharacters = reader.GetInt32(reader.GetOrdinal("numchars")),
                });
            }

            return accounts;
        }

        /// <summary>
        /// Delete an account by ID. Direct SQL since MaNGOS has no .account delete SOAP command.
        /// Deletes from account, account_access, and realmcharacters.
        /// </summary>
        public async Task DeleteAccountAsync(uint accountId)
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var tx = await conn.BeginTransactionAsync();
            try
            {
                await ExecuteNonQueryAsync(conn, tx, "DELETE FROM realmcharacters WHERE acctid = @id", accountId);
                await ExecuteNonQueryAsync(conn, tx, "DELETE FROM account_access WHERE id = @id", accountId);
                await ExecuteNonQueryAsync(conn, tx, "DELETE FROM account WHERE id = @id", accountId);
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Test the database connection.
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static async Task ExecuteNonQueryAsync(MySqlConnection conn, MySqlTransaction tx, string sql, uint accountId)
        {
            await using var cmd = new MySqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@id", accountId);
            await cmd.ExecuteNonQueryAsync();
        }

        private static DateTime TryGetDateTime(MySqlDataReader reader, int ordinal)
        {
            try
            {
                return reader.IsDBNull(ordinal) ? DateTime.MinValue : reader.GetDateTime(ordinal);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }
}
