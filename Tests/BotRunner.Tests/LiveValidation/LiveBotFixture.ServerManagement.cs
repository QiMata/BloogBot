using BotRunner.Clients;
using BotRunner.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Communication;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

internal readonly record struct AccountCharacterRecord(string Name, byte RaceId, byte ClassId, byte GenderId);
internal readonly record struct FishingPoolSpawnSite(int PoolId, float X, float Y, float Z, float Distance2D);
internal readonly record struct FishingPoolGameObjectSite(int PoolId, uint Guid, uint Entry, float X, float Y, float Z);
internal readonly record struct GameObjectSelectResult(
    bool HasSelection,
    bool NoGameObjectsFound,
    uint Guid,
    int? PoolId,
    uint Entry,
    float X,
    float Y,
    float Z,
    float DistanceFromExpected,
    string? RawLine);
internal readonly record struct GameObjectTargetResult(
    bool Found,
    uint Guid,
    uint Entry,
    float X,
    float Y,
    float Z,
    string? RawLine);
internal readonly record struct PoolInfoChildPoolResult(
    bool Parsed,
    uint PoolId,
    bool Active,
    string? RawLine);

public partial class LiveBotFixture
{
    private static readonly BigInteger SrpPrime = new(
        Convert.FromHexString("894B645E89E1535BBDAD5B8B290650530801B18EBFBF5E8FAB3C82872A3E9BB7"),
        isUnsigned: true,
        isBigEndian: true);

    private static readonly BigInteger SrpGenerator = new(7);

    private static readonly Regex GameObjectSelectPattern = new(
        @"(?<guid>\d+)(?:\s+\(Pool\s+(?<pool>\d+)\))?,\s+Entry\s+(?<entry>\d+).*?\[(?<name>.+?)\s+X:(?<x>[+-]?\d+(?:\.\d+)?)\s+Y:(?<y>[+-]?\d+(?:\.\d+)?)\s+Z:(?<z>[+-]?\d+(?:\.\d+)?)\s+MapId:(?<map>\d+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex GameObjectTargetPattern = new(
        @"Selected object:\s*\|cffffffff\|Hgameobject:(?<guid>\d+)\|h\[(?<name>.+?)\]\|h\|r\s+GUID:\s*(?<detailGuid>\d+)\s+ID:\s*(?<entry>\d+)\s+X:\s*(?<x>[+-]?\d+(?:\.\d+)?)\s+Y:\s*(?<y>[+-]?\d+(?:\.\d+)?)\s+Z:\s*(?<z>[+-]?\d+(?:\.\d+)?)\s+MapId:\s*(?<map>\d+)\s+Orientation:\s*(?<orientation>[+-]?\d+(?:\.\d+)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex PoolInfoChildPoolPattern = new(
        @"(?<pool>\d+)\s+-\s+\|cffffffff\|Hpool:(?<poolLink>\d+)\|h\[(?<name>.+?)\]\|h\|r\s+AutoSpawn:\s*(?<auto>\d+)\s+MaxLimit:\s*(?<max>\d+)\s+Creatures:\s*(?<creatures>\d+)\s+GameObjecs:\s*(?<gos>\d+)\s+Pools\s+(?<pools>\d+)\s*(?<active>\[active\])?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private const float FishingDesiredPoolDistance = 24f;
    private const float FishingPoolDistanceTolerance = 14f;
    private const float FishingMinCastingDistance = FishingDesiredPoolDistance - FishingPoolDistanceTolerance;
    private const float FishingMaxCastingDistance = FishingDesiredPoolDistance + FishingPoolDistanceTolerance;
    private const float FishingIdealCastingDistanceFromPool = 18f;
    private const float FishingPathfindingPierLayerZTolerance = 3f;
    private const float VisiblePoolMatchTolerance = 6f;

    // ---- MySQL direct helpers (bypass disabled GM commands in some repacks) ----

    private string MangosWorldDbConnectionString
        => $"Server=127.0.0.1;Port={Config.MySqlPort};Uid={Config.MySqlUser};Pwd={Config.MySqlPassword};Database=mangos;Connection Timeout=5;";


    private string MangosCharDbConnectionString
        => $"Server=127.0.0.1;Port={Config.MySqlPort};Uid={Config.MySqlUser};Pwd={Config.MySqlPassword};Database=characters;Connection Timeout=5;";


    private string MangosRealmDbConnectionString
        => $"Server=127.0.0.1;Port={Config.MySqlPort};Uid={Config.MySqlUser};Pwd={Config.MySqlPassword};Database=realmd;Connection Timeout=5;";

    private Task<bool> EnsureSoapAdminAccountAsync()
        => EnsureRealmAccountAsync(
            username: (Config.SoapUsername ?? string.Empty).Trim().ToUpperInvariant(),
            password: (Config.SoapPassword ?? string.Empty).Trim().ToUpperInvariant(),
            logPrefix: "SOAP-BOOTSTRAP");

    /// <summary>
    /// Ensures the Shodan GM admin account exists in the realm DB with GM level 6 and
    /// a known password. Creates the SRP verifier/salt pair if the account is missing;
    /// otherwise rotates the password when the existing verifier doesn't match. The
    /// StateManager will log in and auto-create the character on first launch based on
    /// the race/class/gender in Fishing.config.json.
    /// </summary>
    public Task<bool> EnsureShodanAccountAsync()
        => EnsureRealmAccountAsync(
            username: ShodanAccount,
            password: "PASSWORD",
            logPrefix: "SHODAN-BOOTSTRAP");

    private async Task<bool> EnsureRealmAccountAsync(string username, string password, string logPrefix)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("[{LogPrefix}] Skipped because username/password were empty.", logPrefix);
                return false;
            }

            using var realmConn = new MySql.Data.MySqlClient.MySqlConnection(MangosRealmDbConnectionString);
            await realmConn.OpenAsync();

            uint accountId;
            bool resetPassword = false;

            using (var lookup = realmConn.CreateCommand())
            {
                lookup.CommandText = "SELECT id, s, v FROM account WHERE username = @username LIMIT 1";
                lookup.Parameters.AddWithValue("@username", username);

                using var reader = await lookup.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    accountId = 0;
                }
                else
                {
                    accountId = Convert.ToUInt32(reader.GetValue(0));
                    var saltHex = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    var verifierHex = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    resetPassword = !VerifierMatchesPassword(username, password, saltHex, verifierHex);
                }
            }

            if (accountId == 0)
            {
                var (saltHex, verifierHex) = GenerateSaltAndVerifier(username, password);
                using var insert = realmConn.CreateCommand();
                insert.CommandText = @"
                    INSERT INTO account (username, gmlevel, v, s, token_key, joindate)
                    VALUES (@username, 6, @v, @s, '', NOW())";
                insert.Parameters.AddWithValue("@username", username);
                insert.Parameters.AddWithValue("@v", verifierHex);
                insert.Parameters.AddWithValue("@s", saltHex);
                await insert.ExecuteNonQueryAsync();
                accountId = Convert.ToUInt32(insert.LastInsertedId);
                _logger.LogInformation("[{LogPrefix}] Created missing account '{Account}' (id={Id}).", logPrefix, username, accountId);
            }
            else if (resetPassword)
            {
                var (saltHex, verifierHex) = GenerateSaltAndVerifier(username, password);
                using var updatePassword = realmConn.CreateCommand();
                updatePassword.CommandText = "UPDATE account SET v = @v, s = @s WHERE id = @id";
                updatePassword.Parameters.AddWithValue("@v", verifierHex);
                updatePassword.Parameters.AddWithValue("@s", saltHex);
                updatePassword.Parameters.AddWithValue("@id", accountId);
                await updatePassword.ExecuteNonQueryAsync();
                _logger.LogInformation("[{LogPrefix}] Reset SRP verifier for account '{Account}' (id={Id}).", logPrefix, username, accountId);
            }

            using (var gmUpdate = realmConn.CreateCommand())
            {
                gmUpdate.CommandText = "UPDATE account SET gmlevel = 6 WHERE id = @id";
                gmUpdate.Parameters.AddWithValue("@id", accountId);
                await gmUpdate.ExecuteNonQueryAsync();
            }

            foreach (var realmId in new[] { 1, -1 })
            {
                using var access = realmConn.CreateCommand();
                access.CommandText = @"
                    INSERT INTO account_access (id, gmlevel, RealmID)
                    VALUES (@id, 6, @realm)
                    ON DUPLICATE KEY UPDATE gmlevel = VALUES(gmlevel)";
                access.Parameters.AddWithValue("@id", accountId);
                access.Parameters.AddWithValue("@realm", realmId);
                await access.ExecuteNonQueryAsync();
            }

            try
            {
                using var realmChars = realmConn.CreateCommand();
                realmChars.CommandText = @"
                    REPLACE INTO realmcharacters (realmid, acctid, numchars)
                    SELECT id, @acctid, 0 FROM realmlist";
                realmChars.Parameters.AddWithValue("@acctid", accountId);
                await realmChars.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                // Not fatal for SOAP auth; some local schemas omit/shape this table differently.
                _logger.LogDebug("[{LogPrefix}] realmcharacters sync skipped: {Error}", logPrefix, ex.Message);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[{LogPrefix}] Failed to create/repair account '{Account}': {Error}",
                logPrefix, username, ex.Message);
            return false;
        }
    }

    private static bool VerifierMatchesPassword(string username, string password, string saltHex, string storedVerifierHex)
    {
        if (string.IsNullOrWhiteSpace(saltHex) || string.IsNullOrWhiteSpace(storedVerifierHex))
            return false;

        try
        {
            var computed = ComputeVerifierHex(username, password, saltHex);
            return string.Equals(computed, storedVerifierHex.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static (string SaltHex, string VerifierHex) GenerateSaltAndVerifier(string username, string password)
    {
        Span<byte> saltBigEndian = stackalloc byte[32];
        RandomNumberGenerator.Fill(saltBigEndian);
        saltBigEndian[0] |= 0x80;
        var saltHex = Convert.ToHexString(saltBigEndian);
        var verifierHex = ComputeVerifierHex(username, password, saltHex);
        return (saltHex, verifierHex);
    }

    private static string ComputeVerifierHex(string username, string password, string saltHex)
    {
        var normalizedUser = (username ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedPassword = (password ?? string.Empty).Trim().ToUpperInvariant();

        var shaPass = SHA1.HashData(Encoding.ASCII.GetBytes($"{normalizedUser}:{normalizedPassword}"));
        var salt = new BigInteger(Convert.FromHexString(saltHex), isUnsigned: true, isBigEndian: true);
        var saltLittleEndian = ToLittleEndianMinimal(salt);

        var verifierSeed = new byte[saltLittleEndian.Length + shaPass.Length];
        Buffer.BlockCopy(saltLittleEndian, 0, verifierSeed, 0, saltLittleEndian.Length);
        Buffer.BlockCopy(shaPass, 0, verifierSeed, saltLittleEndian.Length, shaPass.Length);

        var xDigest = SHA1.HashData(verifierSeed);
        var x = new BigInteger(xDigest, isUnsigned: true, isBigEndian: false);
        var verifier = BigInteger.ModPow(SrpGenerator, x, SrpPrime);
        return verifier.ToString("X", CultureInfo.InvariantCulture);
    }

    private static byte[] ToLittleEndianMinimal(BigInteger value)
    {
        if (value.IsZero)
            return [];

        return value.ToByteArray(isUnsigned: true, isBigEndian: false);
    }


    /// <summary>
    /// Populates <c>_knownCharacterNamesByAccount</c> from the characters DB for every
    /// configured account. Safe to re-invoke — missing rows are skipped, and any later
    /// row is remembered. Derived fixtures call this during a <c>WaitForExactBotCountAsync</c>
    /// retry so freshly-created BG characters (whose in-bot <c>ObjectManager.Player.Name</c>
    /// can lag the server-side row) still pass the hydration gate via
    /// <see cref="NormalizeSnapshotCharacterName"/>.
    /// </summary>
    protected internal async Task SeedExpectedCharacterNamesFromDatabaseAsync()
    {
        var accountsToResolve = new HashSet<string>(KnownAccountNamesForCharacterResolution, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(BgAccountName))
            accountsToResolve.Add(BgAccountName);
        if (!string.IsNullOrWhiteSpace(FgAccountName))
            accountsToResolve.Add(FgAccountName);
        if (!string.IsNullOrWhiteSpace(CombatTestAccountName))
            accountsToResolve.Add(CombatTestAccountName);
        if (!string.IsNullOrWhiteSpace(ShodanAccountName))
            accountsToResolve.Add(ShodanAccountName);

        foreach (var accountName in accountsToResolve)
        {
            var characterName = await ResolvePrimaryCharacterNameAsync(accountName);
            RememberKnownCharacterName(accountName, characterName);

            if (string.Equals(accountName, BgAccountName, StringComparison.OrdinalIgnoreCase))
                BgCharacterName ??= characterName;
            if (string.Equals(accountName, FgAccountName, StringComparison.OrdinalIgnoreCase))
                FgCharacterName ??= characterName;
            if (string.Equals(accountName, CombatTestAccountName, StringComparison.OrdinalIgnoreCase))
                CombatTestCharacterName ??= characterName;
            if (string.Equals(accountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
                ShodanCharacterName ??= characterName;
        }
    }


    private async Task<string?> ResolvePrimaryCharacterNameAsync(string? accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
            return null;

        try
        {
            using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosCharDbConnectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT c.name
                FROM characters c
                INNER JOIN realmd.account a ON a.id = c.account
                WHERE a.username = @username
                ORDER BY c.guid
                LIMIT 1";
            cmd.Parameters.AddWithValue("@username", accountName);

            var result = await cmd.ExecuteScalarAsync();
            if (result is string characterName && !string.IsNullOrWhiteSpace(characterName))
            {
                _logger.LogInformation("[MySQL] Resolved character '{Character}' for account '{Account}'",
                    characterName, accountName);
                return characterName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[MySQL] Failed to resolve character name for account '{Account}': {Error}",
                accountName, ex.Message);
        }

        return null;
    }

    private protected async Task<bool> AccountExistsAsync(string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
            throw new ArgumentException("Account name is required.", nameof(accountName));

        using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosRealmDbConnectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT 1
            FROM account
            WHERE username = @username
            LIMIT 1";
        cmd.Parameters.AddWithValue("@username", accountName);

        return await cmd.ExecuteScalarAsync() != null;
    }

    private protected async Task<IReadOnlyList<AccountCharacterRecord>> QueryCharactersForAccountAsync(string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
            throw new ArgumentException("Account name is required.", nameof(accountName));

        var results = new List<AccountCharacterRecord>();

        using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosCharDbConnectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT c.name, c.race, c.class, c.gender
            FROM characters c
            INNER JOIN realmd.account a ON a.id = c.account
            WHERE a.username = @username
            ORDER BY c.guid";
        cmd.Parameters.AddWithValue("@username", accountName);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new AccountCharacterRecord(
                Name: reader.GetString(0),
                RaceId: Convert.ToByte(reader.GetValue(1)),
                ClassId: Convert.ToByte(reader.GetValue(2)),
                GenderId: Convert.ToByte(reader.GetValue(3))));
        }

        return results;
    }

    private protected async Task<bool> CharacterNameExistsAsync(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
            throw new ArgumentException("Character name is required.", nameof(characterName));

        using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosCharDbConnectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT 1
            FROM characters
            WHERE name = @characterName
            LIMIT 1";
        cmd.Parameters.AddWithValue("@characterName", characterName);

        return await cmd.ExecuteScalarAsync() != null;
    }

    /// <summary>
    /// Ensure live battleground accounts meet honor-rank requirements before launch.
    /// This must run while characters are offline so rank fields hydrate on next login.
    /// </summary>
    protected async Task<int> EnsureHonorRankForAccountsAsync(
        IReadOnlyCollection<string> accountNames,
        int honorRank,
        float honorRankPoints = 65000f)
    {
        if (accountNames == null || accountNames.Count == 0)
            return 0;
        if (honorRank < 0)
            throw new ArgumentOutOfRangeException(nameof(honorRank), honorRank, "Honor rank must be non-negative.");

        var normalizedAccounts = accountNames
            .Where(account => !string.IsNullOrWhiteSpace(account))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedAccounts.Length == 0)
            return 0;

        using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosCharDbConnectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var parameterNames = new List<string>(normalizedAccounts.Length);
        for (var index = 0; index < normalizedAccounts.Length; index++)
        {
            var parameterName = $"@account{index}";
            parameterNames.Add(parameterName);
            cmd.Parameters.AddWithValue(parameterName, normalizedAccounts[index]);
        }

        cmd.Parameters.AddWithValue("@rank", honorRank);
        cmd.Parameters.AddWithValue("@rankPoints", honorRankPoints);
        cmd.CommandText = $@"
            UPDATE characters c
            INNER JOIN realmd.account a ON a.id = c.account
            SET
                c.honor_highest_rank = GREATEST(c.honor_highest_rank, @rank),
                c.honor_rank_points = GREATEST(c.honor_rank_points, @rankPoints),
                c.honor_standing = CASE WHEN c.honor_standing = 0 THEN 1 ELSE c.honor_standing END,
                c.honor_last_week_hk = GREATEST(c.honor_last_week_hk, 500),
                c.honor_last_week_cp = GREATEST(c.honor_last_week_cp, @rankPoints),
                c.honor_stored_hk = GREATEST(c.honor_stored_hk, 500)
            WHERE a.username IN ({string.Join(", ", parameterNames)})";

        var updatedRows = await cmd.ExecuteNonQueryAsync();
        _logger.LogInformation(
            "[MySQL] Ensured honor rank {Rank} (points>={RankPoints}) for {Rows} character row(s) across {Accounts} account(s).",
            honorRank, honorRankPoints, updatedRows, normalizedAccounts.Length);

        return updatedRows;
    }


    private async Task<int> RestoreCommandTableBaselineAsync(MySql.Data.MySqlClient.MySqlConnection conn)
    {
        // Backup current state once per run for local recovery.
        using (var backupCmd = conn.CreateCommand())
        {
            backupCmd.CommandText = "CREATE TABLE IF NOT EXISTS command_backup_fixture AS SELECT * FROM command";
            await backupCmd.ExecuteNonQueryAsync();
        }

        using (var truncateCmd = conn.CreateCommand())
        {
            truncateCmd.CommandText = "TRUNCATE TABLE command";
            await truncateCmd.ExecuteNonQueryAsync();
        }

        int inserted = 0;
        foreach (var row in CommandTableBaselineRows)
        {
            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO command (name, security, help, flags)
                VALUES (@name, @security, @help, @flags)";
            insertCmd.Parameters.AddWithValue("@name", row.Name);
            insertCmd.Parameters.AddWithValue("@security", row.Security);
            insertCmd.Parameters.AddWithValue("@help", row.Help);
            insertCmd.Parameters.AddWithValue("@flags", row.Flags);
            inserted += await insertCmd.ExecuteNonQueryAsync();
        }

        return inserted;
    }

    /// <summary>
    /// Resolve a deterministic self-kill command for the current server build.
    /// Prefers .kill when present; otherwise falls back to .die if available.
    /// </summary>


    /// <summary>
    /// Ensure test accounts have GM level and sanitize stale fixture-injected rows from the MaNGOS
    /// command table. We avoid inserting/overwriting command definitions because that can drift from
    /// the server's compiled 1.12.1 command hierarchy and produce misleading runtime warnings.
    /// </summary>
    public async Task EnsureGmCommandsEnabledAsync()
    {
        // Step 1: Ensure test + SOAP accounts have GM level 6 in both account and account_access tables.
        try
        {
            using var realmConn = new MySql.Data.MySqlClient.MySqlConnection(MangosRealmDbConnectionString);
            await realmConn.OpenAsync();

            var gmAccounts = new[] { "ADMINISTRATOR", "TESTBOT1", "TESTBOT2", "COMBATTEST", "SHODAN" };
            var updatedAccounts = 0;
            foreach (var accountName in gmAccounts)
            {
                using var gmCmd = realmConn.CreateCommand();
                gmCmd.CommandText = "UPDATE account SET gmlevel = 6 WHERE username = @username";
                gmCmd.Parameters.AddWithValue("@username", accountName);
                updatedAccounts += await gmCmd.ExecuteNonQueryAsync();

                using var accessCmd = realmConn.CreateCommand();
                accessCmd.CommandText = @"
                    INSERT INTO account_access (id, gmlevel, RealmID)
                    SELECT id, 6, 1 FROM account WHERE username = @username
                    ON DUPLICATE KEY UPDATE gmlevel = VALUES(gmlevel)";
                accessCmd.Parameters.AddWithValue("@username", accountName);
                _ = await accessCmd.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("[MySQL] Ensured GM level 6 for test accounts (account rows updated: {Rows})", updatedAccounts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[MySQL] Failed to enforce account GM levels: {Error}", ex.Message);
        }

        // Step 2: sanitize stale command-table rows created by older fixture behavior.
        try
        {
            using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosWorldDbConnectionString);
            await conn.OpenAsync();

            var useBaselineRestore = string.Equals(
                Environment.GetEnvironmentVariable("WWOW_TEST_RESTORE_COMMAND_TABLE"),
                "1",
                StringComparison.OrdinalIgnoreCase);

            int removedRows = 0;
            var cleanupCommands = new[]
            {
                "DELETE FROM command WHERE name = '' OR name IS NULL",
                // Only remove rows explicitly marked as fixture-owned; never delete core server commands.
                "DELETE FROM command WHERE help LIKE '%Enabled by test fixture%' AND name NOT IN ('die', 'revive')"
            };

            foreach (var sql in cleanupCommands)
            {
                using var cleanupCmd = conn.CreateCommand();
                cleanupCmd.CommandText = sql;
                removedRows += await cleanupCmd.ExecuteNonQueryAsync();
            }

            // Normalize stale fixture help text for commands we still rely on in live tests.
            var normalizeHelpCommands = new[]
            {
                ("die", "Syntax: .die [name]"),
                ("revive", "Syntax: .revive [name]")
            };

            var normalizedRows = 0;
            foreach (var (name, helpText) in normalizeHelpCommands)
            {
                using var normalizeCmd = conn.CreateCommand();
                normalizeCmd.CommandText = @"
                    UPDATE command
                    SET help = @help
                    WHERE name = @name
                      AND (help LIKE '%Enabled by test fixture%' OR help = '' OR help IS NULL)";
                normalizeCmd.Parameters.AddWithValue("@help", helpText);
                normalizeCmd.Parameters.AddWithValue("@name", name);
                normalizedRows += await normalizeCmd.ExecuteNonQueryAsync();
            }

            if (useBaselineRestore)
            {
                var insertedRows = await RestoreCommandTableBaselineAsync(conn);
                _logger.LogInformation("[MySQL] Command table baseline restore enabled (WWOW_TEST_RESTORE_COMMAND_TABLE=1). Inserted {Rows} row(s).",
                    insertedRows);
            }

            using var countCmd = conn.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM command";
            var remaining = Convert.ToInt32(await countCmd.ExecuteScalarAsync() ?? 0);

            _logger.LogInformation("[MySQL] Command table sanitized: removed {Removed} stale row(s), remaining rows={Remaining}",
                removedRows, remaining);
            _logger.LogInformation("[MySQL] Command table help normalization: updated {Rows} row(s) for die/revive", normalizedRows);

            // Log key command rows used by live test setup to keep command behavior debuggable.
            using var inspectCmd = conn.CreateCommand();
            inspectCmd.CommandText = @"
                SELECT name, security, help
                FROM command
                WHERE name IN ('kill', 'die', 'damage', 'aoedamage', 'revive', 'select', 'select player')
                ORDER BY name";
            using var reader = await inspectCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.IsDBNull(0) ? "(null)" : reader.GetString(0);
                var security = reader.IsDBNull(1) ? 0U : Convert.ToUInt32(reader.GetValue(1));
                var help = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                _logger.LogInformation("[MySQL] Command row: name='{Name}', security={Security}, help='{Help}'",
                    name, security, help);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[MySQL] Failed to sanitize command table: {Error}", ex.Message);
        }

        // Step 3: Reload command table so cleanup takes effect without server restart.
        try
        {
            var result = await ExecuteGMCommandAsync(".reload command");
            _logger.LogInformation("[MySQL] Reload command result: {Result}", result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[MySQL] Failed to reload command table: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Teach a spell directly via MySQL character_spell table + SOAP trigger.
    /// Fallback when .learn is disabled in the repack's command table.
    /// </summary>


    /// <summary>
    /// Teach a spell directly via MySQL character_spell table + SOAP trigger.
    /// Fallback when .learn is disabled in the repack's command table.
    /// </summary>
    public async Task<bool> DirectLearnSpellAsync(string characterName, uint spellId)
    {
        try
        {
            using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosCharDbConnectionString);
            await conn.OpenAsync();

            // Get character GUID
            using var guidCmd = conn.CreateCommand();
            guidCmd.CommandText = "SELECT guid FROM characters WHERE name = @name";
            guidCmd.Parameters.AddWithValue("@name", characterName);
            var guidObj = await guidCmd.ExecuteScalarAsync();
            if (guidObj == null)
            {
                _logger.LogWarning("[MySQL] Character '{Name}' not found in characters table", characterName);
                return false;
            }
            var charGuid = Convert.ToUInt32(guidObj);

            // Insert spell (ignore if already exists)
            using var spellCmd = conn.CreateCommand();
            spellCmd.CommandText = "INSERT IGNORE INTO character_spell (guid, spell, active, disabled) VALUES (@guid, @spell, 1, 0)";
            spellCmd.Parameters.AddWithValue("@guid", charGuid);
            spellCmd.Parameters.AddWithValue("@spell", spellId);
            var rows = await spellCmd.ExecuteNonQueryAsync();
            _logger.LogInformation("[MySQL] DirectLearnSpell: char={Name} (guid={Guid}) spell={Spell} rows={Rows}",
                characterName, charGuid, spellId, rows);
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[MySQL] DirectLearnSpell failed: {Error}", ex.Message);
            return false;
        }
    }

    // ---- Dual-bot helpers ----

    /// <summary>Execute a GM command for a specific character, with retry.</summary>


    // ---- MaNGOS Server Management ----

    /// <summary>
    /// Restart MaNGOS world server via SOAP and wait for it to come back online.
    /// This cleans up stale in-memory state (despawned objects, stuck NPCs, etc.).
    /// Note: manually-added gameobjects via .gobject add persist in the DB across restarts.
    /// </summary>
    public async Task RestartMangosdAsync(int timeoutSeconds = 120)
    {
        _logger.LogInformation("[RESTART] Sending .server restart 5 via SOAP...");
        await ExecuteGMCommandAsync(".server restart 5");

        // Wait for the server to actually go down
        await Task.Delay(10_000);

        // Poll until world server is back
        var health = new ServiceHealthChecker();
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            var isUp = await health.IsMangosdAvailableAsync(Config);
            if (isUp)
            {
                _logger.LogInformation("[RESTART] MaNGOS back online after {Elapsed:F0}s", sw.Elapsed.TotalSeconds);
                await Task.Delay(5000); // Extra settle time for full initialization
                return;
            }
            await Task.Delay(3000);
        }
        throw new TimeoutException($"MaNGOS did not restart within {timeoutSeconds}s");
    }

    /// <summary>
    /// Clean up zombie gameobjects created by previous .gobject add test runs.
    /// Cleans known test areas (Orgrimmar, Valley of Trials, Durotar) where nodes
    /// were manually spawned during earlier test development.
    /// </summary>


    /// <summary>
    /// Clean up zombie gameobjects created by previous .gobject add test runs.
    /// Cleans known test areas (Orgrimmar, Valley of Trials, Durotar) where nodes
    /// were manually spawned during earlier test development.
    /// </summary>
    private async Task CleanupZombieGameObjectsAsync()
    {
        // Known ore/herb entries used in tests
        var nodeEntries = new uint[] { 1731, 1732, 1617, 1618, 1619 }; // Copper, Tin, Peacebloom, Silverleaf, Earthroot

        // Known test locations where .gobject add was used in earlier sessions
        var testLocations = new (int map, float x, float y)[] {
            (1, 1629f, -4373f),   // Orgrimmar (GM setup area)
            (1, -600f, -4200f),   // Valley of Trials
            (1, -900f, -4500f),   // Durotar coast
        };

        int totalCleaned = 0;
        foreach (var entry in nodeEntries)
        {
            foreach (var (map, x, y) in testLocations)
            {
                totalCleaned += await CleanupGameObjectsNearAsync(entry, map, x, y, radius: 50);
            }
        }
        if (totalCleaned > 0)
            _logger.LogInformation("[CLEANUP] Total zombie gameobjects removed: {Count}", totalCleaned);
    }

    /// <summary>
    /// Delete manually-added gameobjects near a specific location.
    /// Used to clean up zombie nodes from previous test runs (.gobject add creates permanent DB entries).
    /// </summary>


    /// <summary>
    /// Delete manually-added gameobjects near a specific location.
    /// Used to clean up zombie nodes from previous test runs (.gobject add creates permanent DB entries).
    /// </summary>
    public async Task<int> CleanupGameObjectsNearAsync(uint entry, int map, float x, float y, float radius = 10)
    {
        try
        {
            using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosWorldDbConnectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"DELETE FROM gameobject
                WHERE id = @entry AND map = @map
                AND SQRT(POW(position_x - @x, 2) + POW(position_y - @y, 2)) < @radius";
            cmd.Parameters.AddWithValue("@entry", entry);
            cmd.Parameters.AddWithValue("@map", map);
            cmd.Parameters.AddWithValue("@x", x);
            cmd.Parameters.AddWithValue("@y", y);
            cmd.Parameters.AddWithValue("@radius", radius);
            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows > 0)
                _logger.LogInformation("[CLEANUP] Deleted {Rows} gameobjects (entry={Entry}) near ({X:F0},{Y:F0})", rows, entry, x, y);
            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[CLEANUP] Failed to clean up gameobjects: {Error}", ex.Message);
            return 0;
        }
    }

    // ---- World Database Queries ----

    private async Task<List<FishingPoolSpawnSite>> QueryMasterPoolSpawnSitesAsync(
        int masterPoolId,
        int mapId,
        float centerX,
        float centerY,
        float radius,
        int? limit = null)
    {
        using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosWorldDbConnectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var sql = new StringBuilder(@"
            SELECT
                pp.pool_id,
                go.position_x,
                go.position_y,
                go.position_z
            FROM pool_pool pp
            INNER JOIN (
                SELECT pool_entry, MIN(guid) AS guid
                FROM pool_gameobject
                GROUP BY pool_entry
            ) anchor ON anchor.pool_entry = pp.pool_id
            INNER JOIN gameobject go ON go.guid = anchor.guid
            WHERE pp.mother_pool = @masterPoolId
              AND go.map = @map
              AND POW(go.position_x - @cx, 2) + POW(go.position_y - @cy, 2) <= POW(@radius, 2)
            ORDER BY POW(go.position_x - @cx, 2) + POW(go.position_y - @cy, 2) ASC");
        if (limit.HasValue)
            sql.AppendLine(" LIMIT @limit");

        cmd.CommandText = sql.ToString();
        cmd.Parameters.AddWithValue("@masterPoolId", masterPoolId);
        cmd.Parameters.AddWithValue("@map", mapId);
        cmd.Parameters.AddWithValue("@cx", centerX);
        cmd.Parameters.AddWithValue("@cy", centerY);
        cmd.Parameters.AddWithValue("@radius", radius);
        if (limit.HasValue)
            cmd.Parameters.AddWithValue("@limit", Math.Max(1, limit.Value));

        var spawns = new List<FishingPoolSpawnSite>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var poolId = reader.GetInt32(0);
            var x = reader.GetFloat(1);
            var y = reader.GetFloat(2);
            var z = reader.GetFloat(3);
            spawns.Add(new FishingPoolSpawnSite(poolId, x, y, z, Distance2D(centerX, centerY, x, y)));
        }

        return spawns;
    }

    private async Task<List<FishingPoolGameObjectSite>> QueryMasterPoolGameObjectSitesAsync(int masterPoolId, int mapId)
    {
        using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosWorldDbConnectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                pp.pool_id,
                pg.guid,
                go.id,
                go.position_x,
                go.position_y,
                go.position_z
            FROM pool_pool pp
            INNER JOIN pool_gameobject pg ON pg.pool_entry = pp.pool_id
            INNER JOIN gameobject go ON go.guid = pg.guid
            WHERE pp.mother_pool = @masterPoolId
              AND go.map = @map
            ORDER BY pp.pool_id, pg.guid";
        cmd.Parameters.AddWithValue("@masterPoolId", masterPoolId);
        cmd.Parameters.AddWithValue("@map", mapId);

        var sites = new List<FishingPoolGameObjectSite>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sites.Add(new FishingPoolGameObjectSite(
                PoolId: reader.GetInt32(0),
                Guid: Convert.ToUInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                Entry: Convert.ToUInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                X: reader.GetFloat(3),
                Y: reader.GetFloat(4),
                Z: reader.GetFloat(5)));
        }

        return sites;
    }

    private async Task<int> RestoreBarrensFishingPoolBaselineAsync(string accountName, int mapId)
    {
        const int masterPoolId = 2628;
        List<FishingPoolGameObjectSite> gameObjects;
        try
        {
            gameObjects = await QueryMasterPoolGameObjectSitesAsync(masterPoolId, mapId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[FISHING-BASELINE] Failed to query master-pool child gameobjects: {Error}", ex.Message);
            return 0;
        }

        if (gameObjects.Count == 0)
            return 0;

        static string PositionKey(FishingPoolGameObjectSite site)
            => string.Create(
                CultureInfo.InvariantCulture,
                $"{MathF.Round(site.X, 2):F2}|{MathF.Round(site.Y, 2):F2}|{MathF.Round(site.Z, 2):F2}");

        var positionCounts = gameObjects
            .GroupBy(PositionKey)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var repaired = 0;
        foreach (var poolGroup in gameObjects.GroupBy(site => site.PoolId).OrderBy(group => group.Key))
        {
            var uniquePositionCount = poolGroup
                .Select(PositionKey)
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (uniquePositionCount <= 1)
                continue;

            var anchor = poolGroup
                .OrderBy(site => positionCounts[PositionKey(site)])
                .ThenBy(site => site.Guid)
                .First();

            foreach (var site in poolGroup)
            {
                if (site.Guid == anchor.Guid)
                    continue;

                var delta = Distance2D(site.X, site.Y, anchor.X, anchor.Y);
                if (delta <= 0.25f && MathF.Abs(site.Z - anchor.Z) <= 0.25f)
                    continue;

                var moveCommand = string.Create(
                    CultureInfo.InvariantCulture,
                    $".gobject move {site.Guid} {anchor.X:F2} {anchor.Y:F2} {anchor.Z:F2}");
                if (!await SendGmChatCommandAndAwaitServerAckAsync(accountName, moveCommand, timeoutMs: 8000))
                {
                    _logger.LogWarning(
                        "[FISHING-BASELINE] Failed to restore guid={Guid} entry={Entry} for pool {PoolId} onto ({X:F1},{Y:F1},{Z:F1}).",
                        site.Guid,
                        site.Entry,
                        site.PoolId,
                        anchor.X,
                        anchor.Y,
                        anchor.Z);
                    continue;
                }

                repaired++;
                _logger.LogInformation(
                    "[FISHING-BASELINE] Restored guid={Guid} entry={Entry} for pool {PoolId} from ({FromX:F1},{FromY:F1},{FromZ:F1}) to ({ToX:F1},{ToY:F1},{ToZ:F1}) using anchor guid={AnchorGuid}.",
                    site.Guid,
                    site.Entry,
                    site.PoolId,
                    site.X,
                    site.Y,
                    site.Z,
                    anchor.X,
                    anchor.Y,
                    anchor.Z,
                    anchor.Guid);
            }
        }

        return repaired;
    }

    private static bool TryParseGameObjectSelectLine(
        string response,
        float expectedX,
        float expectedY,
        out GameObjectSelectResult result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(response))
            return false;

        var match = GameObjectSelectPattern.Match(response);
        if (!match.Success)
            return false;

        if (!uint.TryParse(match.Groups["entry"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var entry)
            || !float.TryParse(match.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !float.TryParse(match.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !float.TryParse(match.Groups["z"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
        {
            return false;
        }

        int? poolId = null;
        if (int.TryParse(match.Groups["pool"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPoolId))
            poolId = parsedPoolId;

        result = new GameObjectSelectResult(
            HasSelection: true,
            NoGameObjectsFound: false,
            Guid: uint.TryParse(match.Groups["guid"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var guid)
                ? guid
                : 0u,
            PoolId: poolId,
            Entry: entry,
            X: x,
            Y: y,
            Z: z,
            DistanceFromExpected: Distance2D(expectedX, expectedY, x, y),
            RawLine: response);
        return true;
    }

    internal static bool TryParseGameObjectTargetLine(string response, out GameObjectTargetResult result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(response))
            return false;

        var match = GameObjectTargetPattern.Match(response);
        if (!match.Success)
            return false;

        if (!uint.TryParse(match.Groups["guid"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var guid)
            || !uint.TryParse(match.Groups["entry"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var entry)
            || !float.TryParse(match.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !float.TryParse(match.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !float.TryParse(match.Groups["z"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
        {
            return false;
        }

        result = new GameObjectTargetResult(
            Found: true,
            Guid: guid,
            Entry: entry,
            X: x,
            Y: y,
            Z: z,
            RawLine: response);
        return true;
    }

    internal static bool TryParsePoolInfoChildPoolLine(string response, out PoolInfoChildPoolResult result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(response))
            return false;

        var match = PoolInfoChildPoolPattern.Match(response);
        if (!match.Success)
            return false;

        if (!uint.TryParse(match.Groups["pool"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var poolId))
            return false;

        result = new PoolInfoChildPoolResult(
            Parsed: true,
            PoolId: poolId,
            Active: response.Contains("[active]", StringComparison.OrdinalIgnoreCase),
            RawLine: response);
        return true;
    }

    private static IEnumerable<string> GetObservedResponseLines(
        IReadOnlyList<string> baseline,
        int baselineCount,
        IReadOnlyList<string> current)
    {
        if (current.Count > baselineCount)
        {
            foreach (var message in current.Skip(baselineCount))
            {
                if (!string.IsNullOrWhiteSpace(message))
                    yield return message;
            }

            yield break;
        }

        foreach (var message in GetDeltaMessages(baseline, current))
        {
            if (!string.IsNullOrWhiteSpace(message))
                yield return message;
        }
    }

    private async Task<string[]> SendGmChatCommandAndCollectResponseLinesAsync(
        string accountName,
        string command,
        int timeoutMs = 5000,
        int settleMs = 700,
        int pollIntervalMs = 100)
    {
        if (_stateManagerClient == null)
            return Array.Empty<string>();

        var baseline = await GetSnapshotAsync(accountName);
        var baselineChats = baseline?.RecentChatMessages.ToArray() ?? Array.Empty<string>();
        var baselineErrors = baseline?.RecentErrors.ToArray() ?? Array.Empty<string>();
        var baselineChatCount = baseline?.RecentChatMessages.Count ?? 0;
        var baselineErrorCount = baseline?.RecentErrors.Count ?? 0;

        var correlationId = $"shodan-collect:{accountName}:{Interlocked.Increment(ref _testCorrelationSequence).ToString(CultureInfo.InvariantCulture)}";
        var action = new ActionMessage
        {
            ActionType = ActionType.SendChat,
            CorrelationId = correlationId,
            Parameters = { new RequestParameter { StringParam = command } }
        };

        var dispatchResult = await SendActionAsync(accountName, action, emitOutput: false);
        if (dispatchResult != ResponseResult.Success)
            return Array.Empty<string>();

        var deadline = DateTime.UtcNow.AddMilliseconds(GetTrackedChatCommandDelayMs(command, timeoutMs));
        var lastResponseSeenUtc = DateTime.MinValue;
        var actionSeen = false;
        var observed = Array.Empty<string>();
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(pollIntervalMs);
            await RefreshSnapshotsAsync();

            var snapshot = await GetSnapshotAsync(accountName);
            if (snapshot == null)
                continue;

            if (!actionSeen)
            {
                var ack = FindLatestMatchingAck(snapshot, correlationId);
                if ((ack != null && ack.Status != CommandAckEvent.Types.AckStatus.Pending)
                    || (snapshot.PreviousAction != null
                        && string.Equals(snapshot.PreviousAction.CorrelationId, correlationId, StringComparison.Ordinal)))
                {
                    actionSeen = true;
                    var postActionDeadline = DateTime.UtcNow.AddMilliseconds(GetTrackedChatCommandPostActionTailMs(command));
                    if (postActionDeadline > deadline)
                        deadline = postActionDeadline;
                }
            }

            var lines = GetObservedResponseLines(baselineChats, baselineChatCount, snapshot.RecentChatMessages)
                .Concat(GetObservedResponseLines(baselineErrors, baselineErrorCount, snapshot.RecentErrors))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (lines.Length > 0)
            {
                observed = lines;
                lastResponseSeenUtc = DateTime.UtcNow;
            }

            if (lastResponseSeenUtc != DateTime.MinValue
                && DateTime.UtcNow - lastResponseSeenUtc >= TimeSpan.FromMilliseconds(Math.Max(
                    settleMs,
                    GetTrackedChatCommandResponseSettleMs(command))))
            {
                break;
            }
        }

        return observed;
    }

    private static string FormatCommandEvidence(IReadOnlyCollection<string> responses, string emptyDetail)
    {
        if (responses.Count == 0)
            return emptyDetail;

        const int maxResponses = 4;
        var formatted = responses
            .Take(maxResponses)
            .Select(response => string.Join(" ", response.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .ToArray();
        var suffix = responses.Count > maxResponses ? $" || ... ({responses.Count - maxResponses} more)" : string.Empty;
        return string.Join(" || ", formatted) + suffix;
    }

    private async Task<bool> TeleportToFishingPoolProbeAsync(
        string accountName,
        int mapId,
        FishingPoolSpawnSite spawn,
        string logPrefix,
        float? probeX = null,
        float? probeY = null)
    {
        var targetX = probeX ?? spawn.X;
        var targetY = probeY ?? spawn.Y;
        var teleportZ = spawn.Z + 2f;
        var teleported = await SendGmChatCommandAndAwaitServerAckAsync(
            accountName,
            string.Create(CultureInfo.InvariantCulture, $".go xyz {targetX:F2} {targetY:F2} {teleportZ:F2} {mapId}"));
        if (!teleported)
        {
            _logger.LogWarning(
                "[{LogPrefix}] Teleport ack failed for pool {PoolId} at probe ({ProbeX:F1},{ProbeY:F1},{Z:F1}) targeting spawn ({X:F1},{Y:F1}).",
                logPrefix, spawn.PoolId, targetX, targetY, teleportZ, spawn.X, spawn.Y);
            return false;
        }

        var settled = await WaitForTeleportSettledAsync(
            accountName,
            targetX,
            targetY,
            timeoutMs: 6000,
            progressLabel: $"{logPrefix} pool {spawn.PoolId}",
            xyToleranceYards: 10f);
        if (!settled)
        {
            _logger.LogWarning(
                "[{LogPrefix}] Teleport did not settle for pool {PoolId} at probe ({ProbeX:F1},{ProbeY:F1}) targeting spawn ({X:F1},{Y:F1}).",
                logPrefix, spawn.PoolId, targetX, targetY, spawn.X, spawn.Y);
        }

        return true;
    }

    private async Task<GameObjectSelectResult> SelectGameObjectAtCurrentLocationAsync(
        string accountName,
        FishingPoolSpawnSite spawn,
        string logPrefix)
    {
        var responseLines = await SendGmChatCommandAndCollectResponseLinesAsync(
            accountName,
            ".gobject select",
            timeoutMs: 4000,
            settleMs: 900);

        GameObjectSelectResult? bestMatch = null;
        var sawNoGameObjects = false;
        foreach (var response in responseLines)
        {
            if (response.Contains("No gameobjects found", StringComparison.OrdinalIgnoreCase))
            {
                sawNoGameObjects = true;
                continue;
            }

            if (!TryParseGameObjectSelectLine(response, spawn.X, spawn.Y, out var parsed))
                continue;

            if (bestMatch == null || parsed.DistanceFromExpected < bestMatch.Value.DistanceFromExpected)
                bestMatch = parsed;
        }

        if (bestMatch != null)
        {
            _logger.LogInformation(
                "[{LogPrefix}] pool {PoolId} selected entry={Entry} at ({X:F1},{Y:F1},{Z:F1}) delta={Delta:F1} raw='{Raw}'",
                logPrefix,
                spawn.PoolId,
                bestMatch.Value.Entry,
                bestMatch.Value.X,
                bestMatch.Value.Y,
                bestMatch.Value.Z,
                bestMatch.Value.DistanceFromExpected,
                bestMatch.Value.RawLine);
            return bestMatch.Value;
        }

        if (sawNoGameObjects)
        {
            _logger.LogInformation(
                "[{LogPrefix}] pool {PoolId} had no selectable gameobject near ({X:F1},{Y:F1}).",
                logPrefix, spawn.PoolId, spawn.X, spawn.Y);
            return new GameObjectSelectResult(
                HasSelection: false,
                NoGameObjectsFound: true,
                Guid: 0u,
                PoolId: spawn.PoolId,
                Entry: 0,
                X: spawn.X,
                Y: spawn.Y,
                Z: spawn.Z,
                DistanceFromExpected: float.MaxValue,
                RawLine: "No gameobjects found!");
        }

        _logger.LogInformation(
            "[{LogPrefix}] pool {PoolId} select produced no parseable responses. raw=[{Responses}]",
            logPrefix,
            spawn.PoolId,
            responseLines.Length == 0 ? "none" : string.Join(" || ", responseLines));
        return new GameObjectSelectResult(
            HasSelection: false,
            NoGameObjectsFound: false,
            Guid: 0u,
            PoolId: spawn.PoolId,
            Entry: 0,
            X: spawn.X,
            Y: spawn.Y,
            Z: spawn.Z,
            DistanceFromExpected: float.MaxValue,
            RawLine: null);
    }

    private async Task<GameObjectSelectResult> SelectGameObjectNearSpawnAsync(
        string accountName,
        int mapId,
        FishingPoolSpawnSite spawn,
        string logPrefix,
        float? probeX = null,
        float? probeY = null)
    {
        var teleported = await TeleportToFishingPoolProbeAsync(accountName, mapId, spawn, logPrefix, probeX, probeY);
        if (!teleported)
            return default;

        return await SelectGameObjectAtCurrentLocationAsync(accountName, spawn, logPrefix);
    }

    private async Task<GameObjectSelectResult> RespawnAndSelectGameObjectNearSpawnAsync(
        string accountName,
        int mapId,
        FishingPoolSpawnSite spawn,
        string logPrefix,
        float? probeX = null,
        float? probeY = null)
    {
        var teleported = await TeleportToFishingPoolProbeAsync(accountName, mapId, spawn, logPrefix, probeX, probeY);
        if (!teleported)
            return default;

        var respawned = await SendGmChatCommandAndAwaitServerAckAsync(accountName, ".respawn");
        if (!respawned)
        {
            _logger.LogInformation(
                "[{LogPrefix}] pool {PoolId} generic .respawn ack did not arrive at ({X:F1},{Y:F1}).",
                logPrefix,
                spawn.PoolId,
                probeX ?? spawn.X,
                probeY ?? spawn.Y);
        }
        else
        {
            _logger.LogInformation(
                "[{LogPrefix}] pool {PoolId} issued generic .respawn at ({X:F1},{Y:F1}).",
                logPrefix,
                spawn.PoolId,
                probeX ?? spawn.X,
                probeY ?? spawn.Y);
            await Task.Delay(250);
        }

        return await SelectGameObjectAtCurrentLocationAsync(accountName, spawn, logPrefix);
    }

    private static bool IsSelectableFishingPool(GameObjectSelectResult selected)
        => selected.HasSelection
           && BarrensFishingPoolEntries.Contains(selected.Entry)
           && selected.DistanceFromExpected <= 8f;

    private static IEnumerable<(float X, float Y, string Label)> EnumerateWakeProbePoints(
        FishingPoolSpawnSite spawn,
        float stagingX,
        float stagingY)
    {
        yield return (spawn.X, spawn.Y, "exact");

        var dx = stagingX - spawn.X;
        var dy = stagingY - spawn.Y;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        if (distance < 0.1f)
            yield break;

        var offset = MathF.Min(8f, MathF.Max(3f, distance - 1f));
        yield return (
            spawn.X + ((dx / distance) * offset),
            spawn.Y + ((dy / distance) * offset),
            "toward-staging");
    }

    private float GetClosestVisibleFishingPoolDistance(WoWActivitySnapshot? snapshot, float centerX, float centerY)
    {
        var distances = new List<float>();

        if (snapshot?.NearbyObjects != null)
        {
            foreach (var gameObject in snapshot.NearbyObjects)
            {
                if (!BarrensFishingPoolEntries.Contains(gameObject.Entry) || gameObject.Base?.Position == null)
                    continue;

                distances.Add(Distance2D(centerX, centerY, gameObject.Base.Position.X, gameObject.Base.Position.Y));
            }
        }

        if (snapshot?.MovementData?.NearbyGameObjects != null)
        {
            foreach (var gameObject in snapshot.MovementData.NearbyGameObjects)
            {
                if (!BarrensFishingPoolEntries.Contains(gameObject.Entry) || gameObject.Position == null)
                    continue;

                distances.Add(Distance2D(centerX, centerY, gameObject.Position.X, gameObject.Position.Y));
            }
        }

        return distances.Count == 0 ? float.MaxValue : distances.Min();
    }

    private float GetClosestVisibleFishingPoolDistance(
        WoWActivitySnapshot? snapshot,
        float centerX,
        float centerY,
        IReadOnlyList<FishingPoolSpawnSite> candidateSites)
    {
        if (candidateSites.Count == 0)
            return float.MaxValue;

        bool MatchesCandidate(float x, float y)
            => candidateSites.Any(site => Distance2D(site.X, site.Y, x, y) <= VisiblePoolMatchTolerance);

        var distances = new List<float>();

        if (snapshot?.NearbyObjects != null)
        {
            foreach (var gameObject in snapshot.NearbyObjects)
            {
                if (!BarrensFishingPoolEntries.Contains(gameObject.Entry) || gameObject.Base?.Position == null)
                    continue;

                var pos = gameObject.Base.Position;
                if (!MatchesCandidate(pos.X, pos.Y))
                    continue;

                distances.Add(Distance2D(centerX, centerY, pos.X, pos.Y));
            }
        }

        if (snapshot?.MovementData?.NearbyGameObjects != null)
        {
            foreach (var gameObject in snapshot.MovementData.NearbyGameObjects)
            {
                if (!BarrensFishingPoolEntries.Contains(gameObject.Entry) || gameObject.Position == null)
                    continue;

                var pos = gameObject.Position;
                if (!MatchesCandidate(pos.X, pos.Y))
                    continue;

                distances.Add(Distance2D(centerX, centerY, pos.X, pos.Y));
            }
        }

        return distances.Count == 0 ? float.MaxValue : distances.Min();
    }

    private async Task<float> GetClosestSelectableFishingPoolDistanceAsync(
        string accountName,
        int mapId,
        IReadOnlyList<FishingPoolSpawnSite> candidateSites,
        string logPrefix,
        int iteration)
    {
        var best = float.MaxValue;

        foreach (var site in candidateSites)
        {
            var selected = await SelectGameObjectNearSpawnAsync(accountName, mapId, site, logPrefix);
            if (!selected.HasSelection
                || !BarrensFishingPoolEntries.Contains(selected.Entry)
                || selected.DistanceFromExpected > 8f)
            {
                continue;
            }

            best = Math.Min(best, site.Distance2D);
            _logger.LogInformation(
                "[{LogPrefix}] iter={Iter} selectable close pool={PoolId} dist={Dist:F1}y entry={Entry} delta={Delta:F1}",
                logPrefix,
                iteration,
                site.PoolId,
                site.Distance2D,
                selected.Entry,
                selected.DistanceFromExpected);
        }

        return best;
    }

    private async Task<float> WakeAndGetClosestSelectableFishingPoolDistanceAsync(
        string accountName,
        int mapId,
        IReadOnlyList<FishingPoolSpawnSite> candidateSites,
        float stagingX,
        float stagingY,
        float stagingZ,
        string logPrefix,
        int iteration)
    {
        if (candidateSites.Count == 0)
            return float.MaxValue;

        var best = float.MaxValue;
        foreach (var site in candidateSites)
        {
            GameObjectSelectResult selected = default;
            var confirmed = false;
            foreach (var (probeX, probeY, label) in EnumerateWakeProbePoints(site, stagingX, stagingY))
            {
                selected = await RespawnAndSelectGameObjectNearSpawnAsync(
                    accountName,
                    mapId,
                    site,
                    $"{logPrefix}-{label}",
                    probeX,
                    probeY);
                if (!IsSelectableFishingPool(selected))
                    continue;

                confirmed = true;
                break;
            }

            if (!confirmed)
            {
                _logger.LogInformation(
                    "[{LogPrefix}] iter={Iter} pool={PoolId} dist={Dist:F1}y did not surface a selectable fishing pool after probe-local generic .respawn.",
                    logPrefix,
                    iteration,
                    site.PoolId,
                    site.Distance2D);
            }

            if (!confirmed || !IsSelectableFishingPool(selected))
                continue;

            await SendGmChatCommandAndAwaitServerAckAsync(accountName, ".gobject respawn");
            best = Math.Min(best, site.Distance2D);
            _logger.LogInformation(
                "[{LogPrefix}] iter={Iter} selectable close pool confirmed via direct select: pool={PoolId} dist={Dist:F1}y entry={Entry}",
                logPrefix,
                iteration,
                site.PoolId,
                site.Distance2D,
                selected.Entry);
        }

        await SendGmChatCommandAndAwaitServerAckAsync(accountName, string.Create(CultureInfo.InvariantCulture,
            $".go xyz {stagingX:F2} {stagingY:F2} {stagingZ:F2} {mapId}"));
        await WaitForTeleportSettledAsync(
            accountName,
            stagingX,
            stagingY,
            timeoutMs: 6000,
            progressLabel: $"{logPrefix} return",
            xyToleranceYards: 10f);

        return best;
    }

    /// <summary>
    /// Forces the fishing pool gameobjects nearest to the given center to respawn
    /// immediately, regardless of their scheduled respawn timers.
    ///
    /// Why this helper exists: <c>.pool update &lt;id&gt;</c> only schedules new pool members
    /// with a fresh respawn delay (see <c>PoolManager::Spawn1Object</c> at
    /// <c>instantly=false</c>), so it doesn't produce visible pools for a test that just
    /// looted them. The generic <c>.respawn</c> command walks nearby world objects and
    /// calls <c>Respawn()</c> on each one, including GameObjects on the loaded grid. We
    /// teleport the specified bot on top of each known pool spawn location so the active
    /// pooled child loads nearby, issue <c>.respawn</c> to wake it if it was waiting on a
    /// respawn timer, then use <c>.gobject select</c> to confirm a fishing pool actually
    /// surfaced before returning the bot to the staging point.
    ///
    /// <paramref name="maxLocations"/> caps how many pool spawn points are force-respawned.
    /// Keeping the count small matters in practice because every <c>.gobject *</c> chat
    /// dispatch is serialized through the bot's outbound action queue (~2 seconds each).
    /// The bot is typically already in a fishing activity while this runs, so finishing
    /// before the FishingTask's <c>PoolAcquireTimeoutMs</c> expires is what keeps the
    /// closest pool visible when the task starts its acquisition pass.
    ///
    /// Returns the number of pool locations the helper teleported to and attempted to
    /// respawn (not necessarily the number of GameObjects that actually changed state —
    /// already-visible pools are safe no-ops).
    /// </summary>
    public async Task<int> RespawnFishingPoolsNearAsync(
        string accountName,
        int mapId,
        float centerX,
        float centerY,
        float radius,
        float stagingZ,
        float? stagingX = null,
        float? stagingY = null,
        int maxLocations = 2)
    {
        const int masterPoolId = 2628;
        List<FishingPoolSpawnSite> spawns;
        try
        {
            spawns = await QueryMasterPoolSpawnSitesAsync(
                masterPoolId,
                mapId,
                centerX,
                centerY,
                radius,
                limit: maxLocations);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[FISHING-RESPAWN] DB query for master pool sites failed: {Error}", ex.Message);
            return 0;
        }

        if (spawns.Count == 0)
        {
            _logger.LogInformation("[FISHING-RESPAWN] No master-pool fishing sites found near ({X:F1},{Y:F1}) r={R}",
                centerX, centerY, radius);
            return 0;
        }

        _logger.LogInformation("[FISHING-RESPAWN] Force-respawning {Count} nearest master-pool sites via {Bot}",
            spawns.Count, accountName);

        var processed = 0;
        foreach (var spawn in spawns)
        {
            var selected = await RespawnAndSelectGameObjectNearSpawnAsync(accountName, mapId, spawn, "FISHING-RESPAWN");
            var confirmedFishingPool = selected.HasSelection
                && BarrensFishingPoolEntries.Contains(selected.Entry)
                && selected.DistanceFromExpected <= 8f;

            if (confirmedFishingPool)
            {
                processed++;
                continue;
            }

            _logger.LogInformation(
                "[FISHING-RESPAWN] pool {PoolId} had no selectable fishing gameobject near ({X:F1},{Y:F1}) after probe-local generic .respawn.",
                spawn.PoolId,
                spawn.X,
                spawn.Y);
        }

        // Return the bot to a known safe staging point (typically the pier landing) so
        // the FishingTask's next acquisition tick sees the bot on the pier, not in the
        // middle of the water. Callers that want to skip this can pass the center coords.
        var returnX = stagingX ?? centerX;
        var returnY = stagingY ?? centerY;
        await SendGmChatCommandAndAwaitServerAckAsync(accountName, string.Create(CultureInfo.InvariantCulture,
            $".go xyz {returnX:F2} {returnY:F2} {stagingZ:F2} {mapId}"));
        await WaitForTeleportSettledAsync(
            accountName,
            returnX,
            returnY,
            timeoutMs: 6000,
            progressLabel: "FISHING-RESPAWN return",
            xyToleranceYards: 10f);

        _logger.LogInformation("[FISHING-RESPAWN] Respawned {Count} pool spawn locations near ({X:F1},{Y:F1})",
            processed, centerX, centerY);

        return processed;
    }

    /// <summary>
    /// Forces VMaNGOS master pool 2628 (and any other fishing master pool in the
    /// supplied radius) to re-roll which children are active, so the fishing bot
    /// has a chance to see a pool close to the pier instead of whatever random
    /// 8-of-N the server happens to be holding.
    ///
    /// How master pools rotate on VMaNGOS: each child pool is either in the
    /// "active" set (spawned or waiting on a respawn timer) or completely absent
    /// from memory. When an active child is despawned via <c>.gobject despawn</c>,
    /// <c>PoolManager::UpdatePool</c> immediately rolls a NEW child from the
    /// pool to fill the vacated slot (see <c>PoolGroup&lt;GameObject&gt;::SpawnObject</c>
    /// with <c>triggerFrom = despawned GUID</c>). That new child enters "waiting
    /// on a fresh respawn timer" state. Calling <c>.gobject respawn</c> on it
    /// then wakes it immediately.
    ///
    /// This routine: (1) for every known fishing-pool spawn XY within the radius,
    /// teleport Shodan there and issue <c>.gobject select</c> + <c>.gobject
    /// despawn</c> — this kicks the rotation for any currently-active children
    /// at that XY; (2) pass the resulting per-XY sequence to the existing
    /// <see cref="RespawnFishingPoolsNearAsync"/> to immediately wake whatever
    /// children were just rolled into the active set.
    ///
    /// Returns the number of pool spawn locations that were processed.
    /// </summary>
    private async Task<int> RotateFishingPoolsNearAsync(
        string accountName,
        int mapId,
        float centerX,
        float centerY,
        float radius,
        float stagingZ,
        IReadOnlyList<FishingPoolSpawnSite>? targetSites = null,
        int iteration = 0,
        float acceptDistance = 55f)
    {
        const int masterPoolId = 2628;
        List<FishingPoolSpawnSite> spawns;
        try
        {
            // Rotation has to touch the full Barrens-coast master pool, not just the
            // local Ratchet subset. If the current active 8-of-21 excludes the local
            // children entirely, a local-only despawn pass can never force a close
            // child into the active set.
            spawns = await QueryMasterPoolSpawnSitesAsync(masterPoolId, mapId, centerX, centerY, 99999f);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[FISHING-ROTATE] DB query for master pool sites failed: {Error}", ex.Message);
            return 0;
        }

        if (spawns.Count == 0)
        {
            _logger.LogInformation("[FISHING-ROTATE] No master-pool fishing sites found near ({X:F1},{Y:F1}) r={R}",
                centerX, centerY, radius);
            return 0;
        }

        _logger.LogInformation("[FISHING-ROTATE] Rotating {Count} pool spawn locations via {Bot} to force master re-roll",
            spawns.Count, accountName);

        var targetPoolIds = new HashSet<int>(
            targetSites?.Select(site => site.PoolId) ?? Enumerable.Empty<int>());
        var processed = 0;
        foreach (var spawn in spawns)
        {
            var selected = await SelectGameObjectNearSpawnAsync(accountName, mapId, spawn, "FISHING-ROTATE");
            if (!selected.HasSelection
                || !BarrensFishingPoolEntries.Contains(selected.Entry)
                || selected.DistanceFromExpected > 8f)
            {
                continue;
            }

            if (targetPoolIds.Contains(spawn.PoolId))
            {
                _logger.LogInformation(
                    "[FISHING-ROTATE] pool {PoolId} is already a target-site fishing pool at {Dist:F1}y; stopping rotation before despawn.",
                    spawn.PoolId,
                    spawn.Distance2D);
                break;
            }

            // `.gobject despawn` is a no-op when nothing valid was selected. We only
            // fire it after a parsed fishing-pool selection so the rotation pass never
            // targets dock decorations or unrelated nearby game objects.
            if (await SendGmChatCommandAndAwaitServerAckAsync(accountName, ".gobject despawn"))
            {
                processed++;
                if (targetSites is { Count: > 0 })
                {
                    var closestActiveTarget = await GetClosestActivePoolDistanceAsync(
                        accountName,
                        targetSites,
                        "FISHING-ACTIVE-DURING-ROTATE",
                        iteration);
                    _logger.LogInformation(
                        "[FISHING-ROTATE] after despawning pool {PoolId}, closest active target pool = {Dist:F1}y (accept={Accept:F0}y)",
                        spawn.PoolId,
                        closestActiveTarget,
                        acceptDistance);
                    if (closestActiveTarget <= acceptDistance)
                        break;
                }
            }
        }

        await SendGmChatCommandAndAwaitServerAckAsync(accountName, string.Create(CultureInfo.InvariantCulture,
            $".go xyz {centerX:F2} {centerY:F2} {stagingZ:F2} {mapId}"));
        await WaitForTeleportSettledAsync(
            accountName,
            centerX,
            centerY,
            timeoutMs: 6000,
            progressLabel: "FISHING-ROTATE return",
            xyToleranceYards: 10f);

        _logger.LogInformation("[FISHING-ROTATE] Completed rotation pass across {Count} pool XYs near ({X:F1},{Y:F1})",
            processed, centerX, centerY);

        return processed;
    }

    private async Task<bool> RelocateNearestActiveFishingPoolToTargetSiteAsync(
        string accountName,
        int mapId,
        float centerX,
        float centerY,
        float stagingZ,
        IReadOnlyList<FishingPoolSpawnSite> targetSites)
    {
        if (targetSites.Count == 0)
            return false;

        const int masterPoolId = 2628;
        List<FishingPoolSpawnSite> spawns;
        try
        {
            spawns = await QueryMasterPoolSpawnSitesAsync(masterPoolId, mapId, centerX, centerY, 99999f);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[FISHING-RELOCATE] DB query for master-pool sites failed: {Error}", ex.Message);
            return false;
        }

        var targetSite = targetSites
            // Empirically prefer the south-pier pool when relocating. The
            // 2620 landing-adjacent spot is easy to make selectable, but it
            // has been much more prone to FG loot-window timeouts after a
            // forced move. 2627 still stays within the accepted pier envelope
            // and gives the cast resolver a roomier water approach.
            .OrderBy(site => site.PoolId == 2627 ? 0 : 1)
            .ThenBy(site => site.Distance2D)
            .First();
        var targetPoolIds = new HashSet<int>(targetSites.Select(site => site.PoolId));
        var relocated = false;

        _logger.LogInformation(
            "[FISHING-RELOCATE] Targeting pool {PoolId} at ({X:F1},{Y:F1},{Z:F1}) dist={Dist:F1}y for relocation fallback.",
            targetSite.PoolId,
            targetSite.X,
            targetSite.Y,
            targetSite.Z,
            targetSite.Distance2D);

        foreach (var spawn in spawns)
        {
            var selected = await SelectGameObjectNearSpawnAsync(accountName, mapId, spawn, "FISHING-RELOCATE");
            if (!selected.HasSelection
                || selected.Guid == 0u
                || !BarrensFishingPoolEntries.Contains(selected.Entry)
                || selected.DistanceFromExpected > 8f)
            {
                continue;
            }

            if (targetPoolIds.Contains(spawn.PoolId))
            {
                _logger.LogInformation(
                    "[FISHING-RELOCATE] target-site pool {PoolId} is already selectable via guid={Guid}; relocation not needed.",
                    spawn.PoolId,
                    selected.Guid);
                relocated = true;
                break;
            }

            var moveCommand = string.Create(
                CultureInfo.InvariantCulture,
                $".gobject move {selected.Guid} {targetSite.X:F2} {targetSite.Y:F2} {targetSite.Z:F2}");
            if (!await SendGmChatCommandAndAwaitServerAckAsync(accountName, moveCommand, timeoutMs: 8000))
            {
                _logger.LogWarning(
                    "[FISHING-RELOCATE] Move ack failed for guid={Guid} entry={Entry} from pool {PoolId} to target pool {TargetPoolId}.",
                    selected.Guid,
                    selected.Entry,
                    spawn.PoolId,
                    targetSite.PoolId);
                continue;
            }

            _logger.LogInformation(
                "[FISHING-RELOCATE] moved guid={Guid} entry={Entry} from pool {PoolId} ({X:F1},{Y:F1},{Z:F1}) to target pool {TargetPoolId} ({TargetX:F1},{TargetY:F1},{TargetZ:F1}).",
                selected.Guid,
                selected.Entry,
                spawn.PoolId,
                selected.X,
                selected.Y,
                selected.Z,
                targetSite.PoolId,
                targetSite.X,
                targetSite.Y,
                targetSite.Z);
            relocated = true;
            break;
        }

        await SendGmChatCommandAndAwaitServerAckAsync(
            accountName,
            string.Create(CultureInfo.InvariantCulture, $".go xyz {centerX:F2} {centerY:F2} {stagingZ:F2} {mapId}"));
        await WaitForTeleportSettledAsync(
            accountName,
            centerX,
            centerY,
            timeoutMs: 6000,
            progressLabel: "FISHING-RELOCATE return",
            xyToleranceYards: 10f);

        return relocated;
    }

    /// <summary>
    /// Fishing-hole game object template entry IDs spawned as children of master pool 2628
    /// (the Barrens coast fishing pools). Used to recognize pools inside snapshot.NearbyObjects.
    /// 180582 = Floating Wreckage; 180655 = Schools of Tastyfish / Oily Blackmouth School.
    /// </summary>
    private static readonly HashSet<uint> BarrensFishingPoolEntries = new() { 180582u, 180655u };

    /// <summary>
    /// Returns the closest fishing-pool distance currently visible to the bot from the
    /// supplied center, based on snapshot nearby-object streams. Polls briefly because
    /// nearby GO visibility can lag a tick or two after the landing teleport.
    /// </summary>
    public async Task<float> GetClosestVisibleFishingPoolDistanceAsync(
        string accountName,
        float centerX,
        float centerY,
        int timeoutMs = 4000)
        => await GetClosestVisibleFishingPoolDistanceAsync(
            accountName,
            centerX,
            centerY,
            timeoutMs,
            candidateSites: null);

    private async Task<float> GetClosestVisibleFishingPoolDistanceAsync(
        string accountName,
        float centerX,
        float centerY,
        int timeoutMs,
        IReadOnlyList<FishingPoolSpawnSite>? candidateSites)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var best = float.MaxValue;

        while (DateTime.UtcNow < deadline)
        {
            await RefreshSnapshotsAsync();
            var snapshot = GetTrackedSnapshotForAccount(accountName) ?? await GetSnapshotAsync(accountName);
            best = Math.Min(
                best,
                candidateSites == null
                    ? GetClosestVisibleFishingPoolDistance(snapshot, centerX, centerY)
                    : GetClosestVisibleFishingPoolDistance(snapshot, centerX, centerY, candidateSites));
            if (best < float.MaxValue)
                return best;

            await Task.Delay(250);
        }

        return best;
    }

    private IReadOnlyList<FishingPoolSpawnSite> GetFgPierReachableCloseSites(
        int mapId,
        float centerX,
        float centerY,
        float centerZ,
        IReadOnlyList<FishingPoolSpawnSite> closeSites)
    {
        if (closeSites.Count == 0)
            return closeSites;

        var playerPosition = new Position(centerX, centerY, centerZ);
        var client = new PathfindingClient("127.0.0.1", 5001, _loggerFactory.CreateLogger<PathfindingClient>());
        var reachable = new List<FishingPoolSpawnSite>();
        var diagnostics = new List<string>();

        foreach (var site in closeSites)
        {
            var poolPosition = new Position(site.X, site.Y, site.Z);
            var castPosition = TryResolveFishingCastViaPathfinding(client, (uint)mapId, playerPosition, poolPosition);
            diagnostics.Add(
                castPosition == null
                    ? $"pool={site.PoolId} dist={site.Distance2D:F1} fgReachable=False"
                    : $"pool={site.PoolId} dist={site.Distance2D:F1} fgReachable=True cast=({castPosition.Value.Position.X:F1},{castPosition.Value.Position.Y:F1},{castPosition.Value.Position.Z:F1}) edge={castPosition.Value.EdgeDistance:F1}");

            if (castPosition != null)
                reachable.Add(site);
        }

        _logger.LogInformation(
            "[FISHING-ENSURE] FG reachability filter: {Diagnostics}",
            string.Join(" | ", diagnostics));

        return reachable;
    }

    private static FishingCastPosition? TryResolveFishingCastViaPathfinding(
        PathfindingClient client,
        uint mapId,
        Position playerPosition,
        Position poolPosition)
    {
        if (!client.IsAvailable)
            return null;

        Position[] path;
        try
        {
            path = client.GetPath(mapId, playerPosition, poolPosition, smoothPath: false);
        }
        catch
        {
            return null;
        }

        if (path == null || path.Length == 0)
            return null;

        bool IsOnPierLayer(Position p) => MathF.Abs(p.Z - playerPosition.Z) <= FishingPathfindingPierLayerZTolerance;

        for (var i = path.Length - 1; i > 0; i--)
        {
            var nearPool = path[i];
            var nearPlayer = path[i - 1];
            var distNearPool = nearPool.DistanceTo(poolPosition);
            var distNearPlayer = nearPlayer.DistanceTo(poolPosition);

            var minDist = MathF.Min(distNearPool, distNearPlayer);
            var maxDist = MathF.Max(distNearPool, distNearPlayer);
            if (FishingIdealCastingDistanceFromPool < minDist || FishingIdealCastingDistanceFromPool > maxDist)
                continue;

            var segmentLen = MathF.Max(distNearPlayer - distNearPool, 0.0001f);
            var t = (FishingIdealCastingDistanceFromPool - distNearPool) / segmentLen;
            t = Math.Clamp(t, 0f, 1f);
            var interpolated = new Position(
                nearPool.X + ((nearPlayer.X - nearPool.X) * t),
                nearPool.Y + ((nearPlayer.Y - nearPool.Y) * t),
                nearPool.Z + ((nearPlayer.Z - nearPool.Z) * t));

            if (!IsOnPierLayer(interpolated))
                continue;

            return BuildFishingCastPosition(interpolated, poolPosition, FishingIdealCastingDistanceFromPool);
        }

        Position? bestNode = null;
        var bestScore = float.MaxValue;
        foreach (var node in path)
        {
            if (!IsOnPierLayer(node))
                continue;

            var distToPool = node.DistanceTo(poolPosition);
            if (distToPool < FishingMinCastingDistance || distToPool > FishingMaxCastingDistance)
                continue;

            var score = MathF.Abs(distToPool - FishingIdealCastingDistanceFromPool);
            if (score >= bestScore)
                continue;

            bestNode = node;
            bestScore = score;
        }

        if (bestNode != null)
            return BuildFishingCastPosition(bestNode, poolPosition, bestNode.DistanceTo(poolPosition));

        var endpoint = path[path.Length - 1];
        var endpointDist = endpoint.DistanceTo(poolPosition);
        if (endpointDist > FishingMaxCastingDistance + 10f || !IsOnPierLayer(endpoint))
            return null;

        return BuildFishingCastPosition(endpoint, poolPosition, endpointDist);
    }

    private static FishingCastPosition BuildFishingCastPosition(Position standoff, Position poolPosition, float distToPool)
    {
        var facing = MathF.Atan2(poolPosition.Y - standoff.Y, poolPosition.X - standoff.X);
        if (facing < 0f)
            facing += MathF.PI * 2f;

        return new FishingCastPosition(standoff, facing, distToPool, HasLineOfSight: true);
    }

    private async Task<(FishingPoolActivationState State, IReadOnlyList<string> EvidenceResponses)> QueryPoolSpawnStateAsync(
        string accountName,
        FishingPoolSpawnSite spawn,
        string logPrefix,
        int iteration)
    {
        var spawnResponses = await SendGmChatCommandAndCollectResponseLinesAsync(
            accountName,
            string.Create(CultureInfo.InvariantCulture, $".pool spawns {spawn.PoolId}"),
            timeoutMs: 5000,
            settleMs: 1000);

        var evidenceResponses = spawnResponses
            .Where(response => !string.IsNullOrWhiteSpace(response))
            .ToArray();
        var poolEntry = Convert.ToUInt32(spawn.PoolId, CultureInfo.InvariantCulture);
        var state = FishingPoolActivationAnalyzer.ClassifyPoolSpawnStateResponses(poolEntry, evidenceResponses);

        _logger.LogInformation(
            "[{LogPrefix}] iter={Iter} pool={PoolId} dist={Dist:F1}y .pool spawns => {SpawnEvidence} => {State}",
            logPrefix,
            iteration,
            spawn.PoolId,
            spawn.Distance2D,
            FormatCommandEvidence(spawnResponses, "no active spawns reported"),
            state);

        return (state, evidenceResponses);
    }

    private async Task<float> GetClosestActivePoolDistanceAsync(
        string accountName,
        IReadOnlyList<FishingPoolSpawnSite> candidateSites,
        string logPrefix,
        int iteration)
    {
        if (candidateSites.Count == 0)
            return float.MaxValue;

        var activeSites = new List<FishingPoolSpawnSite>();
        foreach (var site in candidateSites.OrderBy(site => site.Distance2D))
        {
            var (state, _) = await QueryPoolSpawnStateAsync(accountName, site, logPrefix, iteration);
            if (state == FishingPoolActivationState.Spawned)
                activeSites.Add(site);
        }

        _logger.LogInformation(
            "[{LogPrefix}] iter={Iter} active target pools => {ActivePools}",
            logPrefix,
            iteration,
            activeSites.Count == 0
                ? "none"
                : string.Join(", ", activeSites.Select(site => $"{site.PoolId}@{site.Distance2D:F1}y")));

        if (activeSites.Count == 0)
            return float.MaxValue;

        foreach (var site in activeSites)
        {
            _logger.LogInformation(
                "[{LogPrefix}] iter={Iter} close active pool confirmed via .pool spawns: pool={PoolId} dist={Dist:F1}y",
                logPrefix,
                iteration,
                site.PoolId,
                site.Distance2D);
        }

        return activeSites[0].Distance2D;
    }

    /// <summary>
    /// Iterative, verification-driven pool setup: loop check -> rotate -> respawn -> check
    /// until Shodan confirms a fishing pool is visible within
    /// <paramref name="acceptDistance"/> of the pier landing, or until
    /// <paramref name="maxIterations"/> rounds are exhausted.
    ///
    /// Each iteration:
    ///   1. Teleport Shodan to the pier landing and refresh her snapshot.
    ///   2. If a fishing-hole game object appears in NearbyObjects within the accept
    ///      distance, return success immediately — the fishing task can now acquire it.
    ///   3. Otherwise, despawn at every fishing-hole spawn XY within <paramref name="rotateRadius"/>
    ///      (forces VMaNGOS master pool 2628 to re-roll every active slot it holds in
    ///      this area), then respawn at the closest <paramref name="respawnLimit"/> XYs
    ///      to wake whatever sub-pool children just got rolled in.
    ///
    /// Returns true if a close pool is confirmed visible; false on timeout.
    /// </summary>
    public async Task<bool> EnsureCloseFishingPoolActiveNearAsync(
        string accountName,
        int mapId,
        float centerX,
        float centerY,
        float stagingZ,
        float acceptDistance = 55f,
        float rotateRadius = 200f,
        int respawnLimit = 5,
        int maxIterations = 5)
    {
        const int masterPoolId = 2628;
        var repairedBaseline = await RestoreBarrensFishingPoolBaselineAsync(accountName, mapId);
        if (repairedBaseline > 0)
        {
            _logger.LogInformation(
                "[FISHING-ENSURE] Restored {Count} relocated Barrens pool child gameobject(s) before staging.",
                repairedBaseline);
        }

        IReadOnlyList<FishingPoolSpawnSite> closeSites;
        IReadOnlyList<FishingPoolSpawnSite> fgReachableCloseSites;
        try
        {
            closeSites = await QueryMasterPoolSpawnSitesAsync(masterPoolId, mapId, centerX, centerY, acceptDistance);
            fgReachableCloseSites = GetFgPierReachableCloseSites(mapId, centerX, centerY, stagingZ - 2f, closeSites);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[FISHING-ENSURE] Failed to query master-pool sites: {Error}", ex.Message);
            return false;
        }

        _logger.LogInformation(
            "[FISHING-ENSURE] Close-site plan: {Sites}",
            closeSites.Count == 0
                ? "none"
                : string.Join(" | ", closeSites.Select(site => $"pool={site.PoolId} dist={site.Distance2D:F1} pos=({site.X:F1},{site.Y:F1},{site.Z:F1})")));
        _logger.LogInformation(
            "[FISHING-ENSURE] FG-reachable close-site plan: {Sites}",
            fgReachableCloseSites.Count == 0
                ? "none"
                : string.Join(" | ", fgReachableCloseSites.Select(site => $"pool={site.PoolId} dist={site.Distance2D:F1} pos=({site.X:F1},{site.Y:F1},{site.Z:F1})")));

        for (var iteration = 1; iteration <= maxIterations; iteration++)
        {
            await SendGmChatCommandAndAwaitServerAckAsync(accountName, string.Create(CultureInfo.InvariantCulture,
                $".go xyz {centerX:F2} {centerY:F2} {stagingZ:F2} {mapId}"));
            await WaitForTeleportSettledAsync(
                accountName,
                centerX,
                centerY,
                timeoutMs: 6000,
                progressLabel: $"FISHING-ENSURE landing iter {iteration}",
                xyToleranceYards: 10f);

            var visibilitySites = fgReachableCloseSites.Count > 0 ? fgReachableCloseSites : closeSites;
            var closestVisible = await GetClosestVisibleFishingPoolDistanceAsync(accountName, centerX, centerY, 4000, visibilitySites);
            _logger.LogInformation("[FISHING-ENSURE] iter={Iter} closest FG-reachable visible pool = {Dist:F1}y (accept={Accept:F0}y)",
                iteration, closestVisible, acceptDistance);

            if (closestVisible <= acceptDistance)
            {
                _logger.LogInformation("[FISHING-ENSURE] FG-reachable visible pool confirmed at {Dist:F1}y on iteration {Iter}.",
                    closestVisible, iteration);
                return true;
            }

            var closestSelectable = await GetClosestSelectableFishingPoolDistanceAsync(
                accountName,
                mapId,
                visibilitySites,
                "FISHING-SELECT",
                iteration);
            _logger.LogInformation(
                "[FISHING-ENSURE] iter={Iter} closest selectable FG-reachable close pool = {Dist:F1}y (accept={Accept:F0}y)",
                iteration,
                closestSelectable,
                acceptDistance);
            if (closestSelectable <= acceptDistance)
            {
                _logger.LogInformation(
                    "[FISHING-ENSURE] FG-reachable selectable pool confirmed at {Dist:F1}y on iteration {Iter}.",
                    closestSelectable,
                    iteration);
                return true;
            }

            await SendGmChatCommandAndAwaitServerAckAsync(accountName, string.Create(CultureInfo.InvariantCulture,
                $".go xyz {centerX:F2} {centerY:F2} {stagingZ:F2} {mapId}"));
            await WaitForTeleportSettledAsync(
                accountName,
                centerX,
                centerY,
                timeoutMs: 6000,
                progressLabel: $"FISHING-ENSURE re-stage iter {iteration}",
                xyToleranceYards: 10f);

            var closestActive = await GetClosestActivePoolDistanceAsync(
                accountName,
                visibilitySites,
                "FISHING-ACTIVE",
                iteration);
            _logger.LogInformation(
                "[FISHING-ENSURE] iter={Iter} closest active close pool = {Dist:F1}y (accept={Accept:F0}y)",
                iteration,
                closestActive,
                acceptDistance);

            // No close visible pool yet. Force a re-roll across the whole Barrens coast
            // (every master-pool child site in range), then wake the nearest children.
            _ = await RotateFishingPoolsNearAsync(
                accountName,
                mapId,
                centerX,
                centerY,
                rotateRadius,
                stagingZ,
                targetSites: visibilitySites,
                iteration: iteration,
                acceptDistance: acceptDistance);

            var closestActiveAfterRotate = await GetClosestActivePoolDistanceAsync(
                accountName,
                visibilitySites,
                "FISHING-ACTIVE-POST-ROTATE",
                iteration);
            _logger.LogInformation(
                "[FISHING-ENSURE] iter={Iter} closest active close pool after rotate = {Dist:F1}y (accept={Accept:F0}y)",
                iteration,
                closestActiveAfterRotate,
                acceptDistance);

            _ = await RespawnFishingPoolsNearAsync(
                accountName, mapId, centerX, centerY, acceptDistance,
                stagingZ, stagingX: centerX, stagingY: centerY,
                maxLocations: Math.Max(1, Math.Min(respawnLimit, closeSites.Count)));

            var closestSelectableAfterRespawn = await GetClosestSelectableFishingPoolDistanceAsync(
                accountName,
                mapId,
                visibilitySites,
                "FISHING-SELECT-POST-RESPAWN",
                iteration);
            _logger.LogInformation(
                "[FISHING-ENSURE] iter={Iter} closest selectable FG-reachable close pool after respawn = {Dist:F1}y (accept={Accept:F0}y)",
                iteration,
                closestSelectableAfterRespawn,
                acceptDistance);
            if (closestSelectableAfterRespawn <= acceptDistance)
            {
                _logger.LogInformation(
                    "[FISHING-ENSURE] FG-reachable selectable pool confirmed at {Dist:F1}y immediately after respawn on iteration {Iter}.",
                    closestSelectableAfterRespawn,
                    iteration);
                return true;
            }

            var closestVisibleAfterRespawn = await GetClosestVisibleFishingPoolDistanceAsync(accountName, centerX, centerY, 4000, visibilitySites);
            _logger.LogInformation(
                "[FISHING-ENSURE] iter={Iter} closest FG-reachable visible pool after respawn = {Dist:F1}y (accept={Accept:F0}y)",
                iteration,
                closestVisibleAfterRespawn,
                acceptDistance);
            if (closestVisibleAfterRespawn <= acceptDistance)
            {
                _logger.LogInformation(
                    "[FISHING-ENSURE] FG-reachable visible pool confirmed at {Dist:F1}y immediately after respawn on iteration {Iter}.",
                    closestVisibleAfterRespawn,
                    iteration);
                return true;
            }

            var closestSelectableAfterWake = await WakeAndGetClosestSelectableFishingPoolDistanceAsync(
                accountName,
                mapId,
                visibilitySites,
                centerX,
                centerY,
                stagingZ,
                "FISHING-WAKE",
                iteration);
            _logger.LogInformation(
                "[FISHING-ENSURE] iter={Iter} closest selectable FG-reachable close pool after wake = {Dist:F1}y (accept={Accept:F0}y)",
                iteration,
                closestSelectableAfterWake,
                acceptDistance);
            if (closestSelectableAfterWake <= acceptDistance)
            {
                _logger.LogInformation(
                    "[FISHING-ENSURE] FG-reachable selectable pool confirmed at {Dist:F1}y via targeted wake-up on iteration {Iter}.",
                    closestSelectableAfterWake,
                    iteration);
                return true;
            }

            var closestVisibleAfterWake = await GetClosestVisibleFishingPoolDistanceAsync(accountName, centerX, centerY, 4000, visibilitySites);
            _logger.LogInformation(
                "[FISHING-ENSURE] iter={Iter} closest FG-reachable visible pool after wake = {Dist:F1}y (accept={Accept:F0}y)",
                iteration,
                closestVisibleAfterWake,
                acceptDistance);
            if (closestVisibleAfterWake <= acceptDistance)
            {
                _logger.LogInformation(
                    "[FISHING-ENSURE] FG-reachable visible pool confirmed at {Dist:F1}y after targeted wake-up on iteration {Iter}.",
                    closestVisibleAfterWake,
                    iteration);
                return true;
            }

            var relocated = await RelocateNearestActiveFishingPoolToTargetSiteAsync(
                accountName,
                mapId,
                centerX,
                centerY,
                stagingZ,
                visibilitySites);
            if (!relocated)
                continue;

            var closestSelectableAfterRelocate = await GetClosestSelectableFishingPoolDistanceAsync(
                accountName,
                mapId,
                visibilitySites,
                "FISHING-SELECT-POST-RELOCATE",
                iteration);
            _logger.LogInformation(
                "[FISHING-ENSURE] iter={Iter} closest selectable FG-reachable close pool after relocate = {Dist:F1}y (accept={Accept:F0}y)",
                iteration,
                closestSelectableAfterRelocate,
                acceptDistance);
            if (closestSelectableAfterRelocate <= acceptDistance)
            {
                _logger.LogInformation(
                    "[FISHING-ENSURE] FG-reachable selectable pool confirmed at {Dist:F1}y after relocation fallback on iteration {Iter}.",
                    closestSelectableAfterRelocate,
                    iteration);
                return true;
            }

            var closestVisibleAfterRelocate = await GetClosestVisibleFishingPoolDistanceAsync(accountName, centerX, centerY, 4000, visibilitySites);
            _logger.LogInformation(
                "[FISHING-ENSURE] iter={Iter} closest FG-reachable visible pool after relocate = {Dist:F1}y (accept={Accept:F0}y)",
                iteration,
                closestVisibleAfterRelocate,
                acceptDistance);
            if (closestVisibleAfterRelocate <= acceptDistance)
            {
                _logger.LogInformation(
                    "[FISHING-ENSURE] FG-reachable visible pool confirmed at {Dist:F1}y after relocation fallback on iteration {Iter}.",
                    closestVisibleAfterRelocate,
                    iteration);
                return true;
            }
        }

        var finalClosestSelectable = await WakeAndGetClosestSelectableFishingPoolDistanceAsync(
            accountName,
            mapId,
            fgReachableCloseSites.Count > 0 ? fgReachableCloseSites : closeSites,
            centerX,
            centerY,
            stagingZ,
            "FISHING-WAKE",
            maxIterations + 1);
        if (finalClosestSelectable <= acceptDistance)
        {
            _logger.LogInformation(
                "[FISHING-ENSURE] Final close selectable pool confirmed at {Dist:F1}y after {Max} rotation rounds.",
                finalClosestSelectable,
                maxIterations);
            return true;
        }

        _logger.LogWarning("[FISHING-ENSURE] Exhausted {Max} iterations without confirming a close active pool.",
            maxIterations);
        return false;
    }
}
