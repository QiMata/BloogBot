using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Communication;
using GameData.Core.Enums;

namespace WoWStateManagerUI.ViewModels
{
    /// <summary>
    /// View-model adapter over a single <see cref="WoWActivitySnapshot"/>. Mutable —
    /// the listener service updates the same VM instance per account so WPF bindings
    /// see PropertyChanged for the fields that actually changed and don't tear down
    /// the row (or detail panel) on every push.
    /// </summary>
    public sealed class BotSnapshotViewModel : INotifyPropertyChanged
    {
        private WoWActivitySnapshot _raw;
        private string _characterName = string.Empty;
        private string _screenState = string.Empty;
        private uint _level;
        private string _raceDisplay = "—";
        private string _classDisplay = "—";
        private uint _mapId;
        private uint _zoneId;
        private string _areaDisplay = string.Empty;
        private int _health;
        private int _maxHealth;
        private string _connectionState = string.Empty;
        private string _currentAction = string.Empty;
        private string _position = string.Empty;

        public WoWActivitySnapshot Raw => _raw;

        /// <summary>The bot's account name. Identity field; never changes after construction.</summary>
        public string AccountName { get; }

        public string CharacterName { get => _characterName; private set => SetField(ref _characterName, value); }
        public string ScreenState { get => _screenState; private set => SetField(ref _screenState, value); }
        public uint Level { get => _level; private set => SetField(ref _level, value); }
        public string RaceDisplay { get => _raceDisplay; private set => SetField(ref _raceDisplay, value); }
        public string ClassDisplay { get => _classDisplay; private set => SetField(ref _classDisplay, value); }
        public uint MapId { get => _mapId; private set => SetField(ref _mapId, value); }
        public uint ZoneId { get => _zoneId; private set => SetField(ref _zoneId, value); }
        public string AreaDisplay { get => _areaDisplay; private set => SetField(ref _areaDisplay, value); }
        public int Health { get => _health; private set => SetField(ref _health, value); }
        public int MaxHealth { get => _maxHealth; private set => SetField(ref _maxHealth, value); }
        public string ConnectionState { get => _connectionState; private set => SetField(ref _connectionState, value); }
        public string CurrentAction { get => _currentAction; private set => SetField(ref _currentAction, value); }
        public string Position { get => _position; private set => SetField(ref _position, value); }

        public BotSnapshotViewModel(WoWActivitySnapshot snapshot)
        {
            _raw = snapshot;
            AccountName = snapshot.AccountName ?? "";
            ApplyFields(snapshot);
        }

        /// <summary>
        /// Diff-and-set: each property setter only fires PropertyChanged when the
        /// incoming value differs from the cached one, so WPF bindings stay stable
        /// across no-op pushes.
        /// </summary>
        public void Update(WoWActivitySnapshot snapshot)
        {
            _raw = snapshot;
            ApplyFields(snapshot);
            // Raw is consumed by the detail panel; signal a refresh so derived
            // detail VMs (stats, spells, travel) re-read it.
            OnPropertyChanged(nameof(Raw));
        }

        private void ApplyFields(WoWActivitySnapshot snapshot)
        {
            var unit = snapshot.Player?.Unit;
            var baseObj = unit?.GameObject?.Base;

            CharacterName = snapshot.CharacterName ?? "";
            ScreenState = snapshot.ScreenState ?? "";
            Level = unit?.GameObject?.Level ?? 0;

            var raceByte = unit != null ? (byte)(unit.Bytes0 & 0xFF) : (byte)0;
            RaceDisplay = raceByte == 0 ? "—" : ((Race)raceByte).ToString();

            var classByte = unit != null ? (byte)(unit.UnitClass & 0xFF) : (byte)0;
            ClassDisplay = classByte == 0 || !Enum.IsDefined(typeof(Class), classByte)
                ? "—"
                : ((Class)classByte).ToString();

            MapId = baseObj?.MapId ?? 0;
            ZoneId = baseObj?.ZoneId ?? 0;
            var zoneName = baseObj?.ZoneName;
            AreaDisplay = !string.IsNullOrEmpty(zoneName)
                ? zoneName
                : ZoneId == 0 ? $"map {MapId}" : $"map {MapId} / zone {ZoneId}";

            Health = (int)(unit?.Health ?? 0);
            MaxHealth = (int)(unit?.MaxHealth ?? 0);
            ConnectionState = snapshot.ConnectionState.ToString();
            CurrentAction = snapshot.CurrentAction?.ActionType.ToString() ?? "";
            Position = baseObj?.Position != null
                ? $"({baseObj.Position.X:F0}, {baseObj.Position.Y:F0}, {baseObj.Position.Z:F0})"
                : "";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
