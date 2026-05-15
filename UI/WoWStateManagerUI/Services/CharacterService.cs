using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace WoWStateManagerUI.Services
{
    /// <summary>
    /// One row from the VMaNGOS <c>characters</c> DB (port 3306, root:root,
    /// schema <c>characters</c>). Race / class / gender are stored as tinyint
    /// indexes; <see cref="RaceName"/> / <see cref="ClassName"/> resolve them.
    /// </summary>
    public sealed class CharacterInfo
    {
        public uint Guid { get; set; }
        public uint AccountId { get; set; }
        public string Name { get; set; } = "";
        public byte RaceId { get; set; }
        public byte ClassId { get; set; }
        public byte GenderId { get; set; }
        public byte Level { get; set; }
        public uint ZoneId { get; set; }
        public uint MapId { get; set; }

        public string RaceName => RaceNames.TryGetValue(RaceId, out var name) ? name : $"Race{RaceId}";
        public string ClassName => ClassNames.TryGetValue(ClassId, out var name) ? name : $"Class{ClassId}";
        public string GenderName => GenderId == 0 ? "Male" : "Female";

        public Models.Faction Faction => Models.FactionHelpers.FromRaceId(RaceId);

        private static readonly Dictionary<byte, string> RaceNames = new()
        {
            { 1, "Human" }, { 2, "Orc" }, { 3, "Dwarf" }, { 4, "NightElf" },
            { 5, "Undead" }, { 6, "Tauren" }, { 7, "Gnome" }, { 8, "Troll" }
        };

        private static readonly Dictionary<byte, string> ClassNames = new()
        {
            { 1, "Warrior" }, { 2, "Paladin" }, { 3, "Hunter" }, { 4, "Rogue" },
            { 5, "Priest" }, { 7, "Shaman" }, { 8, "Mage" }, { 9, "Warlock" },
            { 11, "Druid" }
        };
    }

    /// <summary>
    /// MySQL queries against the <c>characters</c> DB. Used by the Config Editor
    /// to validate that any Character assigned to an Activity actually exists in
    /// the server's character pool. If the DB is unpopulated, the picker is
    /// empty — that's the signal to seed accounts/characters before building
    /// configs.
    /// </summary>
    public sealed class CharacterService
    {
        private readonly string _connectionString;

        public CharacterService(string connectionString)
        {
            _connectionString = connectionString;
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

        public async Task<List<CharacterInfo>> GetAllCharactersAsync()
        {
            var characters = new List<CharacterInfo>();

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            const string sql = @"
                SELECT guid, account, name, race, class, gender, level, zone, map
                FROM characters
                ORDER BY account, name";

            await using var cmd = new MySqlCommand(sql, conn);
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
    }
}
