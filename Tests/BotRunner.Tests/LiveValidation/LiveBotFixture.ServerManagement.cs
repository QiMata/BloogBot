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
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Communication;
using Microsoft.Extensions.Logging;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

internal readonly record struct AccountCharacterRecord(string Name, byte RaceId, byte ClassId, byte GenderId);

public partial class LiveBotFixture
{
    private static readonly BigInteger SrpPrime = new(
        Convert.FromHexString("894B645E89E1535BBDAD5B8B290650530801B18EBFBF5E8FAB3C82872A3E9BB7"),
        isUnsigned: true,
        isBigEndian: true);

    private static readonly BigInteger SrpGenerator = new(7);

    // ---- MySQL direct helpers (bypass disabled GM commands in some repacks) ----

    private string MangosWorldDbConnectionString
        => $"Server=127.0.0.1;Port={Config.MySqlPort};Uid={Config.MySqlUser};Pwd={Config.MySqlPassword};Database=mangos;Connection Timeout=5;";


    private string MangosCharDbConnectionString
        => $"Server=127.0.0.1;Port={Config.MySqlPort};Uid={Config.MySqlUser};Pwd={Config.MySqlPassword};Database=characters;Connection Timeout=5;";


    private string MangosRealmDbConnectionString
        => $"Server=127.0.0.1;Port={Config.MySqlPort};Uid={Config.MySqlUser};Pwd={Config.MySqlPassword};Database=realmd;Connection Timeout=5;";

    private async Task<bool> EnsureSoapAdminAccountAsync()
    {
        try
        {
            var username = (Config.SoapUsername ?? string.Empty).Trim().ToUpperInvariant();
            var password = (Config.SoapPassword ?? string.Empty).Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("[SOAP-BOOTSTRAP] Skipped because SOAP username/password were empty.");
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
                _logger.LogInformation("[SOAP-BOOTSTRAP] Created missing SOAP account '{Account}' (id={Id}).", username, accountId);
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
                _logger.LogInformation("[SOAP-BOOTSTRAP] Reset SRP verifier for SOAP account '{Account}' (id={Id}).", username, accountId);
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
                _logger.LogDebug("[SOAP-BOOTSTRAP] realmcharacters sync skipped: {Error}", ex.Message);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[SOAP-BOOTSTRAP] Failed to create/repair SOAP admin account: {Error}", ex.Message);
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


    private async Task SeedExpectedCharacterNamesFromDatabaseAsync()
    {
        BgCharacterName ??= await ResolvePrimaryCharacterNameAsync(BgAccountName);
        FgCharacterName ??= await ResolvePrimaryCharacterNameAsync(FgAccountName);
        CombatTestCharacterName ??= await ResolvePrimaryCharacterNameAsync(CombatTestAccountName);
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

            var gmAccounts = new[] { "ADMINISTRATOR", "TESTBOT1", "TESTBOT2", "COMBATTEST" };
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

    /// <summary>
    /// Query the MaNGOS gameobject table for existing spawn locations of a given template entry.
    /// Returns up to <paramref name="limit"/> (map, x, y, z) tuples.
    /// </summary>


    /// <summary>
    /// Clears persisted respawn timers for fishing pool gameobjects near a given position.
    /// This only removes rows from characters.gameobject_respawn; callers still need to
    /// trigger an appropriate in-world refresh (for example, a nearby .respawn) if they
    /// want loaded objects to become interactable again.
    /// </summary>
    public async Task<int> ClearFishingPoolRespawnTimersAsync(int mapId, float centerX, float centerY, float radius)
    {
        try
        {
            using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosCharDbConnectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                DELETE gr FROM gameobject_respawn gr
                INNER JOIN mangos.gameobject g ON gr.guid = g.guid
                INNER JOIN mangos.gameobject_template gt ON g.id = gt.entry
                WHERE gt.type = 25
                  AND gr.map = @map
                  AND g.position_x BETWEEN @minX AND @maxX
                  AND g.position_y BETWEEN @minY AND @maxY";
            cmd.Parameters.AddWithValue("@map", mapId);
            cmd.Parameters.AddWithValue("@minX", centerX - radius);
            cmd.Parameters.AddWithValue("@maxX", centerX + radius);
            cmd.Parameters.AddWithValue("@minY", centerY - radius);
            cmd.Parameters.AddWithValue("@maxY", centerY + radius);

            var deleted = await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("[MySQL] Cleared {Count} fishing pool respawn timers near ({X:F0},{Y:F0}) radius={Radius}",
                deleted, centerX, centerY, radius);

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[MySQL] ClearFishingPoolRespawnTimers failed: {Error}", ex.Message);
            return 0;
        }
    }
}
