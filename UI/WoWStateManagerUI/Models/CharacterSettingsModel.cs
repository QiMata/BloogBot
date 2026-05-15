using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum BotRunnerType
    {
        Foreground = 0,
        Background = 1
    }

    /// <summary>
    /// UI-friendly mirror of WoWStateManager.Settings.CharacterSettings with INotifyPropertyChanged.
    /// Serializes to the same JSON format so files are interchangeable.
    /// </summary>
    public class CharacterSettingsModel : INotifyPropertyChanged
    {
        private string _accountName = string.Empty;
        private float _openness = 1.0f;
        private float _conscientiousness = 1.0f;
        private float _extraversion = 1.0f;
        private float _agreeableness = 1.0f;
        private float _neuroticism = 1.0f;
        private bool _shouldRun = true;
        private BotRunnerType _runnerType = BotRunnerType.Foreground;
        private int? _targetProcessId;
        private string? _characterClass;
        private string? _characterRace;
        private string? _characterGender;
        private int? _characterNameAttemptOffset;
        private string? _startingState;
        private string? _endState;
        private CharacterInfo? _selectedCharacterInfo;

        [JsonProperty("AccountName")]
        public string AccountName { get => _accountName; set { _accountName = value; OnPropertyChanged(); } }

        [JsonProperty("Openness")]
        public float Openness { get => _openness; set { _openness = value; OnPropertyChanged(); } }

        [JsonProperty("Conscientiousness")]
        public float Conscientiousness { get => _conscientiousness; set { _conscientiousness = value; OnPropertyChanged(); } }

        [JsonProperty("Extraversion")]
        public float Extraversion { get => _extraversion; set { _extraversion = value; OnPropertyChanged(); } }

        [JsonProperty("Agreeableness")]
        public float Agreeableness { get => _agreeableness; set { _agreeableness = value; OnPropertyChanged(); } }

        [JsonProperty("Neuroticism")]
        public float Neuroticism { get => _neuroticism; set { _neuroticism = value; OnPropertyChanged(); } }

        [JsonProperty("ShouldRun")]
        public bool ShouldRun { get => _shouldRun; set { _shouldRun = value; OnPropertyChanged(); } }

        [JsonProperty("RunnerType")]
        public BotRunnerType RunnerType { get => _runnerType; set { _runnerType = value; OnPropertyChanged(); } }

        [JsonProperty("TargetProcessId", NullValueHandling = NullValueHandling.Ignore)]
        public int? TargetProcessId { get => _targetProcessId; set { _targetProcessId = value; OnPropertyChanged(); } }

        [JsonProperty("CharacterClass", NullValueHandling = NullValueHandling.Ignore)]
        public string? CharacterClass { get => _characterClass; set { _characterClass = value; OnPropertyChanged(); } }

        [JsonProperty("CharacterRace", NullValueHandling = NullValueHandling.Ignore)]
        public string? CharacterRace { get => _characterRace; set { _characterRace = value; OnPropertyChanged(); } }

        [JsonProperty("CharacterGender", NullValueHandling = NullValueHandling.Ignore)]
        public string? CharacterGender { get => _characterGender; set { _characterGender = value; OnPropertyChanged(); } }

        [JsonProperty("CharacterNameAttemptOffset", NullValueHandling = NullValueHandling.Ignore)]
        public int? CharacterNameAttemptOffset { get => _characterNameAttemptOffset; set { _characterNameAttemptOffset = value; OnPropertyChanged(); } }

        /// <summary>Per-character starting state for this Activity (free-form for now; e.g. "level 30, in Hillsbrad, 12g").
        /// Drives the runtime decision of where to teleport / what gear to set before the activity kicks off.</summary>
        [JsonProperty("StartingState", NullValueHandling = NullValueHandling.Ignore)]
        public string? StartingState { get => _startingState; set { _startingState = value; OnPropertyChanged(); } }

        /// <summary>Per-character desired end state — what character-state change this Activity should produce
        /// (e.g. "level 40", "Argent Dawn Honored", "Mining 225").</summary>
        [JsonProperty("EndState", NullValueHandling = NullValueHandling.Ignore)]
        public string? EndState { get => _endState; set { _endState = value; OnPropertyChanged(); } }

        /// <summary>
        /// The DB <see cref="CharacterInfo"/> this row is pointing at. Driven by
        /// the inline ComboBox in the Characters grid — picking from the
        /// dropdown copies AccountName / class / race / gender from the picked
        /// row. Not serialized; rehydrated on load by name match against the
        /// characters DB list.
        /// </summary>
        [JsonIgnore]
        public CharacterInfo? SelectedCharacterInfo
        {
            get => _selectedCharacterInfo;
            set
            {
                if (ReferenceEquals(_selectedCharacterInfo, value)) return;
                _selectedCharacterInfo = value;
                OnPropertyChanged();
                if (value != null)
                {
                    AccountName = value.Name;
                    CharacterClass = value.ClassName;
                    CharacterRace = value.RaceName;
                    CharacterGender = value.GenderName;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public CharacterSettingsModel Clone()
        {
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<CharacterSettingsModel>(json)!;
        }
    }
}
