using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace WoWStateManagerUI.Models
{
    /// <summary>
    /// Top-level StateManager configuration. A Config contains N Activities and
    /// a StateManager can run all of them in parallel (subject to performance).
    /// Each Activity then holds its own set of Characters that participate in it.
    /// </summary>
    public sealed class ConfigModel : INotifyPropertyChanged
    {
        private string _name = "New Config";
        private string? _description;

        [JsonProperty("Name")]
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        [JsonProperty("Description", NullValueHandling = NullValueHandling.Ignore)]
        public string? Description { get => _description; set { _description = value; OnPropertyChanged(); } }

        [JsonProperty("Activities")]
        public ObservableCollection<ActivityModel> Activities { get; set; } = [];

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
