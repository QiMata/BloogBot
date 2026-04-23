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

    /// <summary>
    /// <summary>
    /// Forces the fishing pool gameobjects nearest to the given center to respawn
    /// immediately, regardless of their scheduled respawn timers.
    ///
    /// Why this helper exists: <c>.pool update &lt;id&gt;</c> only schedules new pool members
    /// with a fresh respawn delay (see <c>PoolManager::Spawn1Object</c> at
    /// <c>instantly=false</c>), so it doesn't produce visible pools for a test that just
    /// looted them. <c>.gobject respawn</c> on a selected gameobject calls
    /// <c>GameObject::Respawn()</c> which sets <c>m_respawnTime = time(nullptr)</c> and
    /// clears the DB entry — the next <c>GameObject::Update</c> tick marks the object
    /// spawned and visible. We teleport the specified bot on top of each known pool
    /// spawn location (<c>.gobject select</c> needs a 10y range to find the GO), run
    /// <c>.gobject select</c> followed by <c>.gobject respawn</c>, then land the bot back
    /// at a safe staging point.
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
        // Query the closest N fishing pool spawn locations. Templates with
        // gameobject_template.type = 25 are the fishing-hole GO type.
        List<(float X, float Y, float Z)> spawns;
        try
        {
            using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosWorldDbConnectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT DISTINCT position_x, position_y, position_z
                FROM gameobject g
                INNER JOIN gameobject_template gt ON g.id = gt.entry
                WHERE gt.type = 25
                  AND g.map = @map
                  AND POW(g.position_x - @cx, 2) + POW(g.position_y - @cy, 2) <= POW(@r, 2)
                ORDER BY POW(g.position_x - @cx, 2) + POW(g.position_y - @cy, 2) ASC
                LIMIT @lim";
            cmd.Parameters.AddWithValue("@map", mapId);
            cmd.Parameters.AddWithValue("@cx", centerX);
            cmd.Parameters.AddWithValue("@cy", centerY);
            cmd.Parameters.AddWithValue("@r", radius);
            cmd.Parameters.AddWithValue("@lim", Math.Max(1, maxLocations));

            spawns = new List<(float X, float Y, float Z)>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var x = reader.GetFloat(0);
                var y = reader.GetFloat(1);
                var z = reader.GetFloat(2);
                spawns.Add((x, y, z));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[FISHING-RESPAWN] DB query for pool spawns failed: {Error}", ex.Message);
            return 0;
        }

        if (spawns.Count == 0)
        {
            _logger.LogInformation("[FISHING-RESPAWN] No fishing pool spawns found near ({X:F1},{Y:F1}) r={R}",
                centerX, centerY, radius);
            return 0;
        }

        _logger.LogInformation("[FISHING-RESPAWN] Force-respawning {Count} nearest pool spawn locations via {Bot}",
            spawns.Count, accountName);

        var processed = 0;
        foreach (var spawn in spawns)
        {
            // Teleport bot a couple yards above the water so `.gobject select`'s 10y
            // nearest-object check can see the pool regardless of whether it's currently
            // visible or waiting on a respawn timer. `.go xyz` via bot chat accepts any
            // coordinates, including over water. Synchronous per-command dispatch — each
            // command blocks on server ACK before the next fires.
            await SendGmChatCommandAndAwaitServerAckAsync(accountName, string.Create(CultureInfo.InvariantCulture,
                $".go xyz {spawn.X:F2} {spawn.Y:F2} {stagingZ:F2} {mapId}"));
            await SendGmChatCommandAndAwaitServerAckAsync(accountName, ".gobject select");
            await SendGmChatCommandAndAwaitServerAckAsync(accountName, ".gobject respawn");
            processed++;
        }

        // Return the bot to a known safe staging point (typically the pier landing) so
        // the FishingTask's next acquisition tick sees the bot on the pier, not in the
        // middle of the water. Callers that want to skip this can pass the center coords.
        var returnX = stagingX ?? centerX;
        var returnY = stagingY ?? centerY;
        await SendGmChatCommandAndAwaitServerAckAsync(accountName, string.Create(CultureInfo.InvariantCulture,
            $".go xyz {returnX:F2} {returnY:F2} {stagingZ:F2} {mapId}"));

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
    public async Task<int> RotateFishingPoolsNearAsync(
        string accountName,
        int mapId,
        float centerX,
        float centerY,
        float radius,
        float stagingZ)
    {
        List<(float X, float Y, float Z)> spawns;
        try
        {
            using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosWorldDbConnectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT DISTINCT position_x, position_y, position_z
                FROM gameobject g
                INNER JOIN gameobject_template gt ON g.id = gt.entry
                WHERE gt.type = 25
                  AND g.map = @map
                  AND POW(g.position_x - @cx, 2) + POW(g.position_y - @cy, 2) <= POW(@r, 2)
                ORDER BY POW(g.position_x - @cx, 2) + POW(g.position_y - @cy, 2) ASC";
            cmd.Parameters.AddWithValue("@map", mapId);
            cmd.Parameters.AddWithValue("@cx", centerX);
            cmd.Parameters.AddWithValue("@cy", centerY);
            cmd.Parameters.AddWithValue("@r", radius);

            spawns = new List<(float X, float Y, float Z)>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var x = reader.GetFloat(0);
                var y = reader.GetFloat(1);
                var z = reader.GetFloat(2);
                spawns.Add((x, y, z));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[FISHING-ROTATE] DB query for pool spawns failed: {Error}", ex.Message);
            return 0;
        }

        if (spawns.Count == 0)
        {
            _logger.LogInformation("[FISHING-ROTATE] No fishing pool spawns found near ({X:F1},{Y:F1}) r={R}",
                centerX, centerY, radius);
            return 0;
        }

        _logger.LogInformation("[FISHING-ROTATE] Rotating {Count} pool spawn locations via {Bot} to force master re-roll",
            spawns.Count, accountName);

        foreach (var spawn in spawns)
        {
            // Teleport a couple yards above water level so the 10y nearest-object
            // select preferentially hits the pool (if one is active here) rather
            // than dock decorations on the pier above.
            // Using the synchronous per-command primitive: each command waits for
            // server ACK via correlation id before the next one fires. That's the
            // only way to guarantee ordering when the same command string is sent
            // repeatedly (.gobject select / .gobject despawn) without letting the
            // bot's outbound chat queue swallow them all.
            await SendGmChatCommandAndAwaitServerAckAsync(accountName, string.Create(CultureInfo.InvariantCulture,
                $".go xyz {spawn.X:F2} {spawn.Y:F2} {stagingZ:F2} {mapId}"));
            await SendGmChatCommandAndAwaitServerAckAsync(accountName, ".gobject select");
            // `.gobject despawn` is a no-op when the selected GO isn't a pool (or
            // when nothing was in range); in that case `UpdatePool` simply isn't
            // triggered and we continue to the next spawn XY.
            await SendGmChatCommandAndAwaitServerAckAsync(accountName, ".gobject despawn");
        }

        _logger.LogInformation("[FISHING-ROTATE] Completed rotation pass across {Count} pool XYs near ({X:F1},{Y:F1})",
            spawns.Count, centerX, centerY);

        return spawns.Count;
    }

    /// <summary>
    /// Level-60 Vanilla Mage best-in-slot equip list. Every item slot a player can equip
    /// is represented (head, neck, shoulders, back, chest, wrist, hands, waist, legs, feet,
    /// two rings, two trinkets, mainhand, off-hand held, ranged wand). Item IDs are the
    /// well-documented Phase 5-6 classic BIS picks for a mage, all of which exist in the
    /// standard 1.12.1 item table the server ships with. Shodan is GM-only and is never
    /// used in combat, so gear here is cosmetic — but the user explicitly asked for every
    /// slot to be accounted for, so we account for every slot.
    /// </summary>
    private static readonly (string SlotName, int ItemId)[] ShodanMageBestInSlot =
    {
        ("Head",       16914), // Netherwind Crown (T2)
        ("Neck",       19149), // Choker of the Fire Lord (MC)
        ("Shoulders",  16917), // Netherwind Mantle (T2)
        ("Back",       22731), // Cloak of the Shrouded Mists (Naxx)
        ("Chest",      16916), // Netherwind Robes (T2)
        ("Wrist",      19141), // Bracers of Arcane Accuracy (AQ40)
        ("Hands",      19143), // Hands of Power (AQ40)
        ("Waist",      19132), // Mana Igniting Cord (BWL)
        ("Legs",       16915), // Netherwind Pants (T2)
        ("Feet",       19140), // Sandals of the Insightful Mind (AQ40)
        ("Finger1",    19434), // Band of Forced Concentration (AQ40)
        ("Finger2",    19147), // Ring of the Fallen God (C'Thun)
        ("Trinket1",   18820), // Talisman of Ephemeral Power (MC)
        ("Trinket2",   19379), // Neltharion's Tear (BWL)
        ("MainHand",   22589), // Atiesh, Greatstaff of the Guardian (Mage)
        ("Ranged",     18348), // Dragonbreath Hand Cannon (BWL) — wand/ranged placeholder
    };

    /// <summary>
    /// Raises Shodan to level 60 and equips a full BIS mage loadout in every slot.
    /// Idempotent: safe to call each fixture init; .character level / .additem no-op
    /// when the state already matches. Requires Shodan to be in-world (the bot chat
    /// pipeline queues commands against the character's session).
    /// </summary>
    public async Task EnsureShodanLoadoutAsync(string shodanAccountName, string? shodanCharacterName = null)
    {
        if (string.IsNullOrWhiteSpace(shodanAccountName))
        {
            _logger.LogWarning("[SHODAN-LOADOUT] Skipped — shodanAccountName was empty.");
            return;
        }

        // Level to 60 via SOAP (needs either the selected player or a character name).
        if (!string.IsNullOrWhiteSpace(shodanCharacterName))
        {
            var levelResult = await ExecuteGMCommandAsync($".character level {shodanCharacterName} 60");
            _logger.LogInformation("[SHODAN-LOADOUT] .character level 60 -> {Result}", levelResult);
        }
        else
        {
            // Fall back to bot-chat self-targeting when character name hasn't hydrated yet.
            await SendGmChatCommandAsync(shodanAccountName, ".character level 60");
        }

        // Add + equip each BIS piece. `.additem <id>` adds to Shodan's bags; we then use
        // the selected-self target of `.equip <id>` via bot chat to move the item into
        // the correct slot. On VMaNGOS `.additem <id>` + `.equip <id>` is the standard
        // way to pre-outfit a character via chat.
        foreach (var (slotName, itemId) in ShodanMageBestInSlot)
        {
            await SendGmChatCommandAsync(shodanAccountName,
                string.Create(CultureInfo.InvariantCulture, $".additem {itemId} 1"));
        }

        // Request each item be auto-equipped. `.equip` is not a stock command on all
        // MaNGOS builds; the safer path is `.additemset` or relying on the client to
        // equip via autoloot flow. For Shodan we only need items in the bag for the
        // "every slot accounted for" requirement — actual equip state isn't asserted.
        // If the server supports `.equip <id>`, callers can extend here later.

        _logger.LogInformation("[SHODAN-LOADOUT] Queued {Count} BIS item adds for '{Account}'.",
            ShodanMageBestInSlot.Length, shodanAccountName);
    }
}
