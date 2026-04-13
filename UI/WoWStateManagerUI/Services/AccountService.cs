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
        public bool Locked { get; set; }
    }

    /// <summary>
    /// MySQL queries against the dockerized realmd database for account listing.
    /// Schema: VMaNGOS realmd (maria-db container, port 3306, root:root).
    /// Bans live in account_banned (not a column on account).
    /// </summary>
    public class AccountService
    {
        private readonly string _connectionString;

        public AccountService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<AccountInfo>> GetAllAccountsAsync()
        {
            var accounts = new List<AccountInfo>();

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            // Join realmcharacters for char count, account_banned for active ban status.
            // last_login can be '0000-00-00 00:00:00' which MySql.Data can't parse as DateTime,
            // so we read it as string and parse manually.
            var sql = @"
                SELECT
                    a.id, a.username, a.gmlevel, a.online,
                    a.joindate,
                    CAST(a.last_login AS CHAR) AS last_login_str,
                    a.last_ip, a.failed_logins, a.locked,
                    COALESCE(rc.numchars, 0) AS numchars,
                    CASE WHEN ab.id IS NOT NULL THEN 1 ELSE 0 END AS is_banned
                FROM account a
                LEFT JOIN realmcharacters rc ON a.id = rc.acctid
                LEFT JOIN account_banned ab ON a.id = ab.id AND ab.active = 1
                ORDER BY a.id";

            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var lastLoginStr = reader.GetString(reader.GetOrdinal("last_login_str"));

                accounts.Add(new AccountInfo
                {
                    Id = reader.GetUInt32(reader.GetOrdinal("id")),
                    Username = reader.GetString(reader.GetOrdinal("username")),
                    GmLevel = reader.GetInt32(reader.GetOrdinal("gmlevel")),
                    Online = reader.GetBoolean(reader.GetOrdinal("online")),
                    JoinDate = reader.GetDateTime(reader.GetOrdinal("joindate")),
                    LastLogin = ParseMySqlTimestamp(lastLoginStr),
                    LastIp = reader.GetString(reader.GetOrdinal("last_ip")),
                    FailedLogins = reader.GetInt32(reader.GetOrdinal("failed_logins")),
                    Locked = reader.GetBoolean(reader.GetOrdinal("locked")),
                    NumCharacters = reader.GetInt32(reader.GetOrdinal("numchars")),
                    Banned = reader.GetBoolean(reader.GetOrdinal("is_banned")),
                });
            }

            return accounts;
        }

        /// <summary>
        /// Delete an account by ID. Direct SQL since MaNGOS has no .account delete SOAP command.
        /// Cleans up account, account_access, account_banned, and realmcharacters.
        /// </summary>
        public async Task DeleteAccountAsync(uint accountId)
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var tx = await conn.BeginTransactionAsync();
            try
            {
                await ExecAsync(conn, tx, "DELETE FROM realmcharacters WHERE acctid = @id", accountId);
                await ExecAsync(conn, tx, "DELETE FROM account_banned WHERE id = @id", accountId);
                await ExecAsync(conn, tx, "DELETE FROM account_access WHERE id = @id", accountId);
                await ExecAsync(conn, tx, "DELETE FROM account WHERE id = @id", accountId);
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

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

        private static async Task ExecAsync(MySqlConnection conn, MySqlTransaction tx, string sql, uint accountId)
        {
            await using var cmd = new MySqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@id", accountId);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// MariaDB returns '0000-00-00 00:00:00' for unset timestamps which .NET can't parse.
        /// </summary>
        private static DateTime ParseMySqlTimestamp(string value)
        {
            if (string.IsNullOrEmpty(value) || value.StartsWith("0000"))
                return DateTime.MinValue;
            return DateTime.TryParse(value, out var dt) ? dt : DateTime.MinValue;
        }
    }
}
