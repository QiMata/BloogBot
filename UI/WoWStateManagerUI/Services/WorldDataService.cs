using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace WoWStateManagerUI.Services
{
    /// <summary>One row returned by the world-DB search dialog.</summary>
    public sealed class WorldSearchResult
    {
        public uint Id { get; set; }
        public string Name { get; set; } = "";
        public string Extra { get; set; } = "";
    }

    /// <summary>
    /// MySQL queries against the VMaNGOS world DB (<c>mangos</c> schema) for
    /// search-driven parameter pickers. <see cref="SearchQuestsAsync"/> queries
    /// <c>quest_template(entry, Title, MinLevel)</c>;
    /// <see cref="SearchItemsAsync"/> queries <c>item_template(entry, name)</c>.
    /// Both fan out a LIKE search and limit the result set so the UI can render
    /// a tight result grid.
    /// </summary>
    public sealed class WorldDataService
    {
        private readonly string _connectionString;

        public WorldDataService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<WorldSearchResult>> SearchQuestsAsync(string query, int limit = 100)
        {
            var results = new List<WorldSearchResult>();

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            // Match by Title OR exact entry-id (so typing "5482" finds Q5482).
            // Method >= 2 filters out disabled (0) and deprecated (1) quest rows;
            // Title <> '' drops the handful of unnamed test rows.
            const string sql = @"
                SELECT entry, Title, MinLevel
                FROM quest_template
                WHERE (Title LIKE @q OR entry = @id)
                  AND Method >= 2
                  AND Title <> ''
                  AND Title NOT LIKE '%(test)%'
                  AND Title NOT LIKE '%TEST%'
                ORDER BY MinLevel, Title
                LIMIT @lim";

            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@q", "%" + (query ?? "") + "%");
            cmd.Parameters.AddWithValue("@id", uint.TryParse(query, out var id) ? id : 0u);
            cmd.Parameters.AddWithValue("@lim", limit);

            await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new WorldSearchResult
                {
                    Id = reader.GetUInt32("entry"),
                    Name = reader.GetString("Title"),
                    Extra = $"lv {reader.GetInt32("MinLevel")}+",
                });
            }
            return results;
        }

        /// <summary>
        /// Live spell search against <c>mangos.spell_template</c> (35k+ rows
        /// with English names). Class-name queries (Warrior / Paladin / Hunter
        /// / Rogue / Priest / Shaman / Mage / Warlock / Druid) join through
        /// <c>skill_line_ability.class_mask</c> and return every spell flagged
        /// for that class. Free-text queries match by name LIKE.
        /// </summary>
        public async Task<List<WorldSearchResult>> SearchSpellsAsync(string query, int limit = 25)
        {
            var results = new List<WorldSearchResult>();
            var classMask = ClassMaskFor(query);

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            // Spell-noise patterns in vmangos: 'zzOLD' prefixes from deprecated
            // alternate ranks, 'test' / 'TEST' fragments, plus all-caps debug
            // names like 'GGOODMANTEST' / 'LeCraft Test Spell'.
            const string spellNoiseFilter = @"
                  AND name NOT LIKE 'zzOLD%'
                  AND name NOT LIKE '%test%'
                  AND name NOT LIKE '%Test%'
                  AND name NOT LIKE '%TEST%'";

            string sql;
            MySqlCommand cmd;
            if (classMask > 0)
            {
                sql = $@"
                    SELECT DISTINCT st.entry, st.name, st.spellLevel
                    FROM spell_template st
                    JOIN skill_line_ability sla ON sla.spell_id = st.entry
                    WHERE sla.class_mask & @cm <> 0 AND st.name <> ''
                      AND st.name NOT LIKE 'zzOLD%'
                      AND st.name NOT LIKE '%test%'
                      AND st.name NOT LIKE '%Test%'
                      AND st.name NOT LIKE '%TEST%'
                    ORDER BY st.spellLevel, st.name
                    LIMIT @lim";
                cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@cm", classMask);
            }
            else
            {
                sql = $@"
                    SELECT entry, name, spellLevel
                    FROM spell_template
                    WHERE (name LIKE @q OR entry = @id) AND name <> ''
                          {spellNoiseFilter}
                    ORDER BY spellLevel, name
                    LIMIT @lim";
                cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@q", "%" + (query ?? "") + "%");
                cmd.Parameters.AddWithValue("@id", uint.TryParse(query, out var id) ? id : 0u);
            }
            cmd.Parameters.AddWithValue("@lim", limit);

            await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var level = reader.GetInt32("spellLevel");
                results.Add(new WorldSearchResult
                {
                    Id = reader.GetUInt32("entry"),
                    Name = reader.GetString("name"),
                    Extra = level > 0 ? $"lv {level}" : "passive/talent",
                });
            }
            cmd.Dispose();
            return results;
        }

        /// <summary>Map class display name → 1.12.1 ChrClasses.dbc bitmask.
        /// 0 = not a recognized class name (free-text search instead).</summary>
        private static int ClassMaskFor(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return 0;
            return query.Trim().ToLowerInvariant() switch
            {
                "warrior" => 1,
                "paladin" => 2,
                "hunter" => 4,
                "rogue" => 8,
                "priest" => 16,
                "shaman" => 64,
                "mage" => 128,
                "warlock" => 256,
                "druid" => 1024,
                _ => 0,
            };
        }

        public async Task<List<WorldSearchResult>> SearchItemsAsync(string query, int limit = 100)
        {
            var results = new List<WorldSearchResult>();

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            // Filter out deprecated / test rows that pollute the result set.
            // 'Deprecated ' / 'zzOLD' prefixes + '(test)' / 'TEST' fragments
            // are the common noise patterns in vmangos item_template.
            const string sql = @"
                SELECT entry, name, quality, item_level
                FROM item_template
                WHERE (name LIKE @q OR entry = @id)
                  AND name <> ''
                  AND name NOT LIKE 'Deprecated %'
                  AND name NOT LIKE 'zzOLD%'
                  AND name NOT LIKE '%(test)%'
                  AND name NOT LIKE '%(Test)%'
                  AND name NOT LIKE '%TEST%'
                  AND name NOT LIKE 'Monster -%'
                ORDER BY item_level DESC, name
                LIMIT @lim";

            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@q", "%" + (query ?? "") + "%");
            cmd.Parameters.AddWithValue("@id", uint.TryParse(query, out var id) ? id : 0u);
            cmd.Parameters.AddWithValue("@lim", limit);

            await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var quality = reader.GetInt32("quality");
                results.Add(new WorldSearchResult
                {
                    Id = reader.GetUInt32("entry"),
                    Name = reader.GetString("name"),
                    Extra = $"q{quality} · iLv {reader.GetInt32("item_level")}",
                });
            }
            return results;
        }
    }
}
