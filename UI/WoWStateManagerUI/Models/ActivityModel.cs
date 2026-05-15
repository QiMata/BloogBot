using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace WoWStateManagerUI.Models
{
    /// <summary>
    /// A single Activity slot within a <see cref="ConfigModel"/>. Holds the
    /// catalog id this Activity was instantiated from, presentation metadata,
    /// game-side restrictions (level bracket, faction, attunements), the
    /// state-change goal that motivates the activity, arbitrary parameters,
    /// and the set of characters assigned to run it. Character count is
    /// capped at 80 (Alterac Valley is the largest WWoW instance).
    /// </summary>
    public sealed class ActivityModel : INotifyPropertyChanged
    {
        /// <summary>Hard cap on characters per activity. Pre-2.0 group caps:
        /// MC/BWL/AQ40/Naxx raid = 40, AV = 40 per side (you make one activity
        /// per faction if you want both sides of AV). Nothing in 1.12.1 needs
        /// more than 40 player slots per coordinator.</summary>
        public const int MaxCharactersPerActivity = 40;

        private string _activityId = string.Empty;
        private string _displayName = string.Empty;
        private string? _family;
        private string? _location;
        private int _minPlayers = 1;
        private int _maxPlayers = MaxCharactersPerActivity;
        private int _levelMin = 1;
        private int _levelMax = 60;
        private Faction _factionRestriction = Faction.Either;
        private string? _stateChangeGoal;
        private bool _repeat;
        private bool _resetStateOnStart;

        [JsonProperty("ActivityId")]
        public string ActivityId { get => _activityId; set { _activityId = value; OnPropertyChanged(); } }

        [JsonProperty("DisplayName")]
        public string DisplayName { get => _displayName; set { _displayName = value; OnPropertyChanged(); } }

        [JsonProperty("Family", NullValueHandling = NullValueHandling.Ignore)]
        public string? Family { get => _family; set { _family = value; OnPropertyChanged(); } }

        [JsonProperty("Location", NullValueHandling = NullValueHandling.Ignore)]
        public string? Location { get => _location; set { _location = value; OnPropertyChanged(); } }

        [JsonProperty("MinPlayers")]
        public int MinPlayers { get => _minPlayers; set { _minPlayers = value; OnPropertyChanged(); } }

        [JsonProperty("MaxPlayers")]
        public int MaxPlayers
        {
            get => _maxPlayers;
            set { _maxPlayers = Math.Min(value, MaxCharactersPerActivity); OnPropertyChanged(); }
        }

        [JsonProperty("LevelMin")]
        public int LevelMin { get => _levelMin; set { _levelMin = value; OnPropertyChanged(); } }

        [JsonProperty("LevelMax")]
        public int LevelMax { get => _levelMax; set { _levelMax = value; OnPropertyChanged(); } }

        [JsonProperty("FactionRestriction")]
        public Faction FactionRestriction
        {
            get => _factionRestriction;
            set { _factionRestriction = value; OnPropertyChanged(); }
        }

        [JsonProperty("StateChangeGoal", NullValueHandling = NullValueHandling.Ignore)]
        public string? StateChangeGoal
        {
            get => _stateChangeGoal;
            set { _stateChangeGoal = value; OnPropertyChanged(); }
        }

        /// <summary>If true, the StateManager loops this Activity after each
        /// conclusion; otherwise it runs once and the bot moves on.</summary>
        [JsonProperty("Repeat", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Repeat
        {
            get => _repeat;
            set { _repeat = value; OnPropertyChanged(); }
        }

        /// <summary>If true, the StateManager clears any in-flight Activity
        /// state (objective progress, marked targets, lockout flags) before
        /// kicking off; otherwise it resumes where it left off.</summary>
        [JsonProperty("ResetStateOnStart", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool ResetStateOnStart
        {
            get => _resetStateOnStart;
            set { _resetStateOnStart = value; OnPropertyChanged(); }
        }

        /// <summary>Quest/attunement IDs the character must hold to enter (e.g. "Seal of Ascension").</summary>
        [JsonProperty("AttunementRequirements")]
        public ObservableCollection<string> AttunementRequirements { get; set; } = [];

        [JsonProperty("Parameters")]
        public ObservableCollection<ActivityParameter> Parameters { get; set; } = [];

        [JsonProperty("Characters")]
        public ObservableCollection<CharacterSettingsModel> Characters { get; set; } = [];

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
