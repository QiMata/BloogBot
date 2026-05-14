using Communication;
using GameData.Core.Enums;

namespace WoWStateManagerUI.ViewModels
{
    /// <summary>
    /// View-model adapter over a single <see cref="WoWActivitySnapshot"/>. Decodes the
    /// fields that exist on the wire today (race/level/map/zone/action/health/position)
    /// and leaves placeholders for fields that arrive in Phase C of the UI refresh
    /// (class, zone name, stats, spells).
    /// </summary>
    public sealed class BotSnapshotViewModel
    {
        public WoWActivitySnapshot Raw { get; }
        public string AccountName { get; }
        public string CharacterName { get; }
        public string ScreenState { get; }
        public uint Level { get; }
        public string RaceDisplay { get; }
        public string ClassDisplay { get; }
        public uint MapId { get; }
        public uint ZoneId { get; }
        public string AreaDisplay { get; }
        public int Health { get; }
        public int MaxHealth { get; }
        public string ConnectionState { get; }
        public string CurrentAction { get; }
        public string Position { get; }

        public BotSnapshotViewModel(WoWActivitySnapshot snapshot)
        {
            Raw = snapshot;
            AccountName = snapshot.AccountName ?? "";
            CharacterName = snapshot.CharacterName ?? "";
            ScreenState = snapshot.ScreenState ?? "";

            var unit = snapshot.Player?.Unit;
            var baseObj = unit?.GameObject?.Base;

            Level = unit?.GameObject?.Level ?? 0;

            // Race is the low byte of UNIT_FIELD_BYTES_0; SnapshotBuilder serializes only that byte.
            var raceByte = unit != null ? (byte)(unit.Bytes0 & 0xFF) : (byte)0;
            RaceDisplay = raceByte == 0 ? "—" : ((Race)raceByte).ToString();

            // Class lives in byte 1 of UNIT_FIELD_BYTES_0 but isn't on the wire yet.
            // Phase C of the UI refresh extends the proto with an explicit class field.
            ClassDisplay = "—";

            MapId = baseObj?.MapId ?? 0;
            ZoneId = baseObj?.ZoneId ?? 0;
            AreaDisplay = ZoneId == 0 ? $"map {MapId}" : $"map {MapId} / zone {ZoneId}";

            Health = (int)(unit?.Health ?? 0);
            MaxHealth = (int)(unit?.MaxHealth ?? 0);
            ConnectionState = snapshot.ConnectionState.ToString();
            CurrentAction = snapshot.CurrentAction?.ActionType.ToString() ?? "";
            Position = baseObj?.Position != null
                ? $"({baseObj.Position.X:F0}, {baseObj.Position.Y:F0}, {baseObj.Position.Z:F0})"
                : "";
        }
    }
}
