using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using WoWStateManagerUI.Models;

namespace WoWStateManagerUI.ViewModels
{
    /// <summary>
    /// Dashboard-side wrapper around an <see cref="ActivityModel"/> from the
    /// loaded config. Owns its own observable list of
    /// <see cref="DashboardCharacterViewModel"/> rows (one per configured
    /// character) and exposes live aggregate stats (how many are pushing
    /// snapshots right now vs configured total).
    /// </summary>
    public sealed class DashboardActivityViewModel : INotifyPropertyChanged
    {
        public ActivityModel Activity { get; }

        public ObservableCollection<DashboardCharacterViewModel> Characters { get; } = [];

        public string DisplayName => Activity.DisplayName;
        public string Family => Activity.Family ?? "—";
        public string Location => Activity.Location ?? "—";
        public int ConfiguredCount => Characters.Count;
        public int LiveCount => Characters.Count(c => c.IsLive);
        public string Summary => $"{LiveCount}/{ConfiguredCount} live · {Family} · {Location}";

        public DashboardActivityViewModel(ActivityModel activity)
        {
            Activity = activity;
            foreach (var c in activity.Characters)
                Characters.Add(new DashboardCharacterViewModel(c));

            Characters.CollectionChanged += OnCharactersChanged;
        }

        private void OnCharactersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ConfiguredCount));
            OnPropertyChanged(nameof(LiveCount));
            OnPropertyChanged(nameof(Summary));
        }

        /// <summary>Tell the activity bucket that a character's live state changed.</summary>
        public void RaiseLiveChanged()
        {
            OnPropertyChanged(nameof(LiveCount));
            OnPropertyChanged(nameof(Summary));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
