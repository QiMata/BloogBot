using System.Collections.Generic;
using System.Threading.Tasks;
using BotCommLayer;
using MySql.Data.MySqlClient;

namespace WoWStateManagerUI.Services
{
    public sealed class RealmInfo
    {
        public uint Id { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public int Port { get; set; }
        public float Population { get; set; }
        public int CharactersOnRealm { get; set; }
    }

    /// <summary>
    /// Per-account aggregation: realms the account has accessed (one row per
    /// realm via <c>realmcharacters</c>) and the characters the account owns
    /// (from the <c>characters</c> DB). Used by the Accounts panel to render
    /// a detail-pane view for the currently selected account.
    /// </summary>
    public sealed class AccountDetailService
    {
        private readonly string _realmdConnection;
        private readonly string _charactersConnection;

        public AccountDetailService(string realmdConnection, string charactersConnection)
        {
            _realmdConnection = realmdConnection;
            _charactersConnection = charactersConnection;
        }

        /// <summary>Realms in <c>realmd.realmlist</c> with char count for one account.</summary>
        public async Task<List<RealmInfo>> GetRealmsForAccountAsync(uint accountId)
        {
            var realms = new List<RealmInfo>();

            await using var conn = new MySqlConnection(_realmdConnection);
            await conn.OpenAsync();

            const string sql = @"
                SELECT r.id, r.name, r.address, r.port, r.population,
                       COALESCE(rc.numchars, 0) AS numchars
                FROM realmlist r
                LEFT JOIN realmcharacters rc ON rc.realmid = r.id AND rc.acctid = @acct
                ORDER BY r.id";

            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@acct", accountId);
            await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                realms.Add(new RealmInfo
                {
                    Id = reader.GetUInt32("id"),
                    Name = reader.GetString("name"),
                    Address = reader.GetString("address"),
                    Port = reader.GetInt32("port"),
                    Population = reader.GetFloat("population"),
                    CharactersOnRealm = reader.GetInt32("numchars"),
                });
            }

            return realms;
        }

        /// <summary>Characters owned by one account (across realms — the
        /// <c>characters</c> DB is single-realm, so this is realm-scoped already).</summary>
        public async Task<List<CharacterInfo>> GetCharactersForAccountAsync(uint accountId)
        {
            var characters = new List<CharacterInfo>();

            await using var conn = new MySqlConnection(_charactersConnection);
            await conn.OpenAsync();

            const string sql = @"
                SELECT guid, account, name, race, class, gender, level, zone, map
                FROM characters
                WHERE account = @acct
                ORDER BY name";

            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@acct", accountId);
            await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                characters.Add(new CharacterInfo
                {
                    Guid = reader.GetUInt32("guid"),
                    AccountId = reader.GetUInt32("account"),
                    Name = reader.GetString("name"),
                    RaceId = reader.GetByte("race"),
                    ClassId = reader.GetByte("class"),
                    GenderId = reader.GetByte("gender"),
                    Level = reader.GetByte("level"),
                    ZoneId = reader.GetUInt32("zone"),
                    MapId = reader.GetUInt32("map"),
                });
            }

            return characters;
        }

        /// <summary>
        /// Delete a character via SOAP <c>.character erase &lt;name&gt;</c>. The MaNGOS
        /// GM command path can erase by name without a logged-in client. Create
        /// is not symmetrical — character creation requires CMSG_CHAR_CREATE from
        /// a logged-in account, which is the BG-client packet path the user
        /// described. Phase 2 will spawn a transient BG client to issue creates.
        /// </summary>
        public async Task<string> EraseCharacterAsync(MangosSOAPClient soap, string characterName)
        {
            var result = await soap.ExecuteGMCommandAsync($".character erase {characterName}");
            return string.IsNullOrEmpty(result) ? "(no response)" : result;
        }
    }
}
