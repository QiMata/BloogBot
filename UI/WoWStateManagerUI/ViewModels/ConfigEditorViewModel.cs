using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WoWStateManagerUI.Handlers;
using WoWStateManagerUI.Models;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.ViewModels
{
    public sealed class ConfigEditorViewModel : INotifyPropertyChanged
    {
        private string _settingsDirectory = string.Empty;
        private string? _selectedProfilePath;
        private CharacterSettingsModel? _selectedCharacter;
        private string _statusMessage = string.Empty;

        public ObservableCollection<string> ProfilePaths { get; } = [];
        public ObservableCollection<CharacterSettingsModel> Characters { get; } = [];

        public string SettingsDirectory
        {
            get => _settingsDirectory;
            set { _settingsDirectory = value; OnPropertyChanged(); RefreshProfiles(); }
        }

        public string? SelectedProfilePath
        {
            get => _selectedProfilePath;
            set { _selectedProfilePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedProfileName)); }
        }

        public string SelectedProfileName => _selectedProfilePath != null ? Path.GetFileName(_selectedProfilePath) : "(none)";

        public CharacterSettingsModel? SelectedCharacter
        {
            get => _selectedCharacter;
            set { _selectedCharacter = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        public ICommand BrowseDirectoryCommand { get; }
        public ICommand LoadProfileCommand { get; }
        public ICommand SaveProfileCommand { get; }
        public ICommand SaveAsProfileCommand { get; }
        public ICommand AddCharacterCommand { get; }
        public ICommand RemoveCharacterCommand { get; }
        public ICommand DuplicateCharacterCommand { get; }

        public string[] AvailableClasses { get; } =
            ["Warrior", "Paladin", "Hunter", "Rogue", "Priest", "Shaman", "Mage", "Warlock", "Druid"];

        public string[] AvailableRaces { get; } =
            ["Human", "Dwarf", "NightElf", "Gnome", "Orc", "Undead", "Tauren", "Troll"];

        public string[] AvailableGenders { get; } = ["Male", "Female"];

        public ConfigEditorViewModel()
        {
            BrowseDirectoryCommand = new CommandHandler(BrowseDirectory, true);
            LoadProfileCommand = new CommandHandler(LoadSelectedProfile, true);
            SaveProfileCommand = new CommandHandler(SaveCurrentProfile, true);
            SaveAsProfileCommand = new CommandHandler(SaveAsNewProfile, true);
            AddCharacterCommand = new CommandHandler(AddCharacter, true);
            RemoveCharacterCommand = new CommandHandler(RemoveCharacter, true);
            DuplicateCharacterCommand = new CommandHandler(DuplicateCharacter, true);
        }

        private void BrowseDirectory()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select a settings file"
            };

            if (dialog.ShowDialog() == true)
            {
                SettingsDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
                SelectedProfilePath = dialog.FileName;
                LoadSelectedProfile();
            }
        }

        private void RefreshProfiles()
        {
            ProfilePaths.Clear();
            if (string.IsNullOrEmpty(_settingsDirectory)) return;

            foreach (var path in SettingsFileService.FindProfiles(_settingsDirectory))
                ProfilePaths.Add(path);
        }

        private void LoadSelectedProfile()
        {
            if (string.IsNullOrEmpty(_selectedProfilePath)) return;

            try
            {
                var loaded = SettingsFileService.LoadSettings(_selectedProfilePath);
                Characters.Clear();
                foreach (var c in loaded)
                    Characters.Add(c);

                SelectedCharacter = Characters.FirstOrDefault();
                StatusMessage = $"Loaded {Characters.Count} characters from {Path.GetFileName(_selectedProfilePath)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load failed: {ex.Message}";
            }
        }

        private void SaveCurrentProfile()
        {
            if (string.IsNullOrEmpty(_selectedProfilePath)) return;

            try
            {
                SettingsFileService.SaveSettings(_selectedProfilePath, Characters);
                StatusMessage = $"Saved {Characters.Count} characters to {Path.GetFileName(_selectedProfilePath)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save failed: {ex.Message}";
            }
        }

        private void SaveAsNewProfile()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                Title = "Save settings as",
                InitialDirectory = _settingsDirectory,
                FileName = "StateManagerSettings.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    SettingsFileService.SaveSettings(dialog.FileName, Characters);
                    SelectedProfilePath = dialog.FileName;
                    SettingsDirectory = Path.GetDirectoryName(dialog.FileName) ?? _settingsDirectory;
                    StatusMessage = $"Saved {Characters.Count} characters to {Path.GetFileName(dialog.FileName)}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Save As failed: {ex.Message}";
                }
            }
        }

        private void AddCharacter()
        {
            var newChar = new CharacterSettingsModel
            {
                AccountName = $"BOT{Characters.Count + 1}",
                RunnerType = BotRunnerType.Background,
                ShouldRun = true,
                GmLevel = 6,
                CharacterClass = "Warrior",
                CharacterRace = "Orc",
                CharacterGender = "Male",
            };
            Characters.Add(newChar);
            SelectedCharacter = newChar;
            StatusMessage = $"Added character {newChar.AccountName}";
        }

        private void RemoveCharacter()
        {
            if (_selectedCharacter == null) return;
            var name = _selectedCharacter.AccountName;
            Characters.Remove(_selectedCharacter);
            SelectedCharacter = Characters.FirstOrDefault();
            StatusMessage = $"Removed {name}";
        }

        private void DuplicateCharacter()
        {
            if (_selectedCharacter == null) return;
            var clone = _selectedCharacter.Clone();
            clone.AccountName = $"{clone.AccountName}_COPY";
            Characters.Add(clone);
            SelectedCharacter = clone;
            StatusMessage = $"Duplicated as {clone.AccountName}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
