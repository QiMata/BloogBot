using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace WoWStateManagerUI.Models
{
    /// <summary>
    /// Which world-DB table the parameter searches against. <see cref="None"/>
    /// means free-form text (or a Choices dropdown if Choices is populated).
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SearchKind
    {
        None = 0,
        Quest = 1,
        Item = 2,
        Spell = 3,
    }

    /// <summary>
    /// Key/value parameter on an <see cref="ActivityModel"/>. Activities have
    /// wildly different parameter shapes (BGs care about bracket+faction,
    /// fishing cares about a zone, leveling cares about start/end) so the
    /// storage is generic. <see cref="Choices"/> turns the value cell into a
    /// dropdown when populated — pre-seeded with the valid value set for
    /// constrained parameters like Bracket / Faction / Skill / Method etc.
    /// </summary>
    public sealed class ActivityParameter : INotifyPropertyChanged
    {
        private string _key = string.Empty;
        private string _value = string.Empty;
        private string? _description;
        private bool _isRequired;

        [JsonProperty("Key")]
        public string Key { get => _key; set { _key = value; OnPropertyChanged(); } }

        [JsonProperty("Value")]
        public string Value { get => _value; set { _value = value; OnPropertyChanged(); } }

        [JsonProperty("Description", NullValueHandling = NullValueHandling.Ignore)]
        public string? Description { get => _description; set { _description = value; OnPropertyChanged(); } }

        [JsonProperty("IsRequired", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool IsRequired { get => _isRequired; set { _isRequired = value; OnPropertyChanged(); } }

        /// <summary>
        /// Valid value set. When non-empty the editor renders a ComboBox bound
        /// to this list instead of a free-form TextBox. Used by the Parameter
        /// value template selector.
        /// </summary>
        [JsonProperty("Choices", NullValueHandling = NullValueHandling.Ignore)]
        public ObservableCollection<string> Choices { get; set; } = [];

        /// <summary>True when <see cref="Choices"/> is populated. Bound by the
        /// template selector through INPC so cells re-render when choices
        /// arrive after construction.</summary>
        [JsonIgnore]
        public bool HasChoices => Choices.Count > 0;

        private SearchKind _searchKind;

        /// <summary>
        /// Which world-DB table the user searches against when picking a value.
        /// Drives the parameter-value template selector: Quest/Item render the
        /// search-picker template, otherwise the Choices/free-form template.
        /// </summary>
        [JsonProperty("SearchKind", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public SearchKind SearchKind
        {
            get => _searchKind;
            set
            {
                if (_searchKind == value) return;
                _searchKind = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSearch));
            }
        }

        [JsonIgnore]
        public bool HasSearch => _searchKind != SearchKind.None;

        public ActivityParameter()
        {
            Choices.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasChoices));
        }

        /// <summary>Initialize Choices from a list (used by the catalog seed).</summary>
        public void SetChoices(IEnumerable<string> values)
        {
            Choices.Clear();
            foreach (var v in values)
                Choices.Add(v);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
