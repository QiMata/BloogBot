using System.ComponentModel;
using System.Runtime.CompilerServices;
using WoWStateManagerUI.Models;

namespace WoWStateManagerUI.ViewModels
{
    /// <summary>
    /// Dashboard row that joins a configured <see cref="CharacterSettingsModel"/>
    /// with any matching live <see cref="BotSnapshotViewModel"/> pushed by the
    /// StateManager. Both inputs are mutated in place; setting
    /// <see cref="LiveSnapshot"/> rebroadcasts the derived display properties.
    /// </summary>
    public sealed class DashboardCharacterViewModel : INotifyPropertyChanged
    {
        private BotSnapshotViewModel? _liveSnapshot;

        public CharacterSettingsModel Character { get; }

        public string AccountName => Character.AccountName;
        public string Race => Character.CharacterRace ?? "—";
        public string Class => Character.CharacterClass ?? "—";

        public bool IsLive => _liveSnapshot != null;
        public string LiveStatus => _liveSnapshot == null ? "offline" : "live";
        public string LiveCharacterName => _liveSnapshot?.CharacterName ?? "";
        public uint LiveLevel => _liveSnapshot?.Level ?? 0;
        public string LiveAreaDisplay => _liveSnapshot?.AreaDisplay ?? "";
        public int LiveHealth => _liveSnapshot?.Health ?? 0;
        public int LiveMaxHealth => _liveSnapshot?.MaxHealth ?? 0;
        public string LiveAction => _liveSnapshot?.CurrentAction ?? "";

        public BotSnapshotViewModel? LiveSnapshot
        {
            get => _liveSnapshot;
            set
            {
                if (ReferenceEquals(_liveSnapshot, value)) return;

                if (_liveSnapshot != null)
                    _liveSnapshot.PropertyChanged -= OnSnapshotPropertyChanged;
                _liveSnapshot = value;
                if (_liveSnapshot != null)
                    _liveSnapshot.PropertyChanged += OnSnapshotPropertyChanged;

                RaiseDerivedChanged();
            }
        }

        public DashboardCharacterViewModel(CharacterSettingsModel character)
        {
            Character = character;
        }

        private void OnSnapshotPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RaiseDerivedChanged();
        }

        private void RaiseDerivedChanged()
        {
            OnPropertyChanged(nameof(IsLive));
            OnPropertyChanged(nameof(LiveStatus));
            OnPropertyChanged(nameof(LiveCharacterName));
            OnPropertyChanged(nameof(LiveLevel));
            OnPropertyChanged(nameof(LiveAreaDisplay));
            OnPropertyChanged(nameof(LiveHealth));
            OnPropertyChanged(nameof(LiveMaxHealth));
            OnPropertyChanged(nameof(LiveAction));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
