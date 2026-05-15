using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using WoWStateManagerUI.Handlers;
using WoWStateManagerUI.Models;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.ViewModels
{
    /// <summary>
    /// View-model behind <c>SearchPickerDialog</c>. Drives a search box + result
    /// grid against the world DB. <see cref="SearchKind"/> selects which table
    /// (Quest / Item) the query targets.
    /// </summary>
    public sealed class SearchPickerViewModel : INotifyPropertyChanged
    {
        private readonly WorldDataService _world;
        private readonly SearchKind _kind;

        private string _query = string.Empty;
        private WorldSearchResult? _selectedResult;
        private string _statusMessage = string.Empty;

        public ObservableCollection<WorldSearchResult> Results { get; } = [];

        public string Query
        {
            get => _query;
            set { _query = value; OnPropertyChanged(); }
        }

        public WorldSearchResult? SelectedResult
        {
            get => _selectedResult;
            set { _selectedResult = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        public string Title => _kind switch
        {
            SearchKind.Quest => "Find Quest",
            SearchKind.Item => "Find Item",
            _ => "Search"
        };

        public ICommand SearchCommand { get; }

        public SearchPickerViewModel(SearchKind kind, string initialQuery)
        {
            _world = new WorldDataService(UIConstants.MangosConnectionString);
            _kind = kind;
            _query = initialQuery ?? string.Empty;
            SearchCommand = new AsyncCommandHandler(RunSearchAsync);

            // Run an initial query so the user sees results immediately.
            _ = RunSearchAsync();
        }

        private async Task RunSearchAsync()
        {
            try
            {
                var rows = _kind switch
                {
                    SearchKind.Quest => await _world.SearchQuestsAsync(_query),
                    SearchKind.Item => await _world.SearchItemsAsync(_query),
                    SearchKind.Spell => await _world.SearchSpellsAsync(_query),
                    _ => new System.Collections.Generic.List<WorldSearchResult>()
                };

                Results.Clear();
                foreach (var r in rows)
                    Results.Add(r);
                StatusMessage = $"{Results.Count} matches";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Search failed: {ex.Message}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
