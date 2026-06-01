using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using WoWStateManagerUI.Handlers;
using WoWStateManagerUI.Models;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.ViewModels
{
    /// <summary>
    /// Hierarchical Config editor: Config → Activities → Characters.
    /// Each Activity has free-form parameters and a catalog-side Objective →
    /// Task → Action hierarchy that the user can inspect, validate, and
    /// trace from the Hierarchy tab in the Activity Detail panel.
    /// Characters can only be added if they exist in the <c>characters</c> DB
    /// so configs don't reference ghosts.
    /// </summary>
    public sealed class ConfigEditorViewModel : INotifyPropertyChanged
    {
        private readonly ActivityCatalogService _catalog = new();
        private readonly ActivityValidator _validator = new();
        private readonly CharacterService _characterService = new(UIConstants.CharactersConnectionString);

        private string _settingsDirectory = string.Empty;
        private string? _selectedConfigPath;
        private ConfigModel _config = new();
        private ActivityModel? _selectedActivity;
        private CharacterInfo? _characterPickerSelection;
        private ActivityTemplate? _templatePickerSelection;
        private CharacterSettingsModel? _selectedCharacter;
        private string _statusMessage = string.Empty;
        private string _hierarchySummary = "(no activity selected)";
        private bool _hierarchyValid;

        public ObservableCollection<string> ConfigPaths { get; } = [];
        public ObservableCollection<CharacterInfo> AvailableCharacters { get; } = [];

        /// <summary>
        /// AvailableCharacters narrowed to whatever Faction the selected
        /// activity is restricted to. Bound by the inline ComboBox in each
        /// character row, so users only see picks that match the activity's
        /// faction. <see cref="Faction.Either"/> shows all rows.
        /// </summary>
        public ObservableCollection<CharacterInfo> AvailableCharactersForActivity { get; } = [];

        /// <summary>Objectives for the currently selected activity (from its catalog template).</summary>
        public ObservableCollection<ObjectiveDefinition> SelectedActivityObjectives { get; } = [];

        /// <summary>Flat trace of the selected activity's hierarchy (Activity → Objective → Task → Action).</summary>
        public ObservableCollection<TraceLine> HierarchyTrace { get; } = [];

        /// <summary>Issues reported by <see cref="ActivityValidator"/> for the selected activity.</summary>
        public ObservableCollection<ValidationIssue> HierarchyIssues { get; } = [];

        public string HierarchySummary
        {
            get => _hierarchySummary;
            private set { _hierarchySummary = value; OnPropertyChanged(); }
        }

        public bool HierarchyValid
        {
            get => _hierarchyValid;
            private set { _hierarchyValid = value; OnPropertyChanged(); }
        }

        public IReadOnlyList<ActivityTemplate> ActivityTemplates => _catalog.Templates;

        private IReadOnlyList<ActivityFamilyGroup>? _activityFamiliesCache;

        /// <summary>
        /// Family-grouped catalog (labelled "Type" in the UI) for the two-stage
        /// Type → Instance picker. Cached so the ItemsSource and SelectedItem
        /// resolve to the same instances — otherwise records-by-value-equality
        /// still left the SelectedItem path showing blank.
        /// </summary>
        public IReadOnlyList<ActivityFamilyGroup> ActivityFamilies
            => _activityFamiliesCache ??= _catalog.GetFamilies();

        /// <summary>The selected family in the header dropdown; drives the
        /// instance dropdown's contents. Setting this also re-syncs the
        /// active instance to the family's first member if the current
        /// instance no longer belongs to this family.</summary>
        /// <summary>True when the selected activity belongs to a family with exactly one
        /// instance — used to hide the (redundant) Instance dropdown for those families
        /// (Leveling, Reputation, Earn Gold, ProfessionLeveling, Questing).</summary>
        public bool IsMultiInstanceFamily
        {
            get
            {
                var fam = SelectedActivityFamily;
                return fam != null && fam.Instances.Count > 1;
            }
        }

        public ActivityFamilyGroup? SelectedActivityFamily
        {
            get
            {
                if (_selectedActivity == null) return null;
                var familyName = _selectedActivity.Family;
                foreach (var fam in ActivityFamilies)
                    if (fam.FamilyName == familyName)
                        return fam;
                return null;
            }
            set
            {
                if (_selectedActivity == null || value == null) return;
                if (value.FamilyName == _selectedActivity.Family) return;
                // Switching family — morph to the family's first instance.
                if (value.Instances.Count > 0)
                {
                    MorphActivityToTemplate(_selectedActivity, value.Instances[0]);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ActiveActivityTemplate));
                    OnPropertyChanged(nameof(IsMultiInstanceFamily));
                    RefreshHierarchyView();
                    StatusMessage = $"Changed family to '{value.FamilyName}' → '{value.Instances[0].DisplayName}'";
                }
            }
        }

        /// <summary>
        /// Faction picker shown at the top of the Activity Detail panel.
        /// <see cref="Faction.Either"/> is intentionally excluded — activity
        /// slots are run by ONE faction at a time (cross-faction grouping
        /// doesn't exist in 1.12.1). Catalog templates that declare
        /// <c>Either</c> are normalized to <see cref="Faction.Alliance"/> on
        /// instantiate / morph; the user flips to Horde via the dropdown.
        /// </summary>
        public IReadOnlyList<Faction> FactionOptions { get; } = [Faction.Alliance, Faction.Horde];

        public string SettingsDirectory
        {
            get => _settingsDirectory;
            set { _settingsDirectory = value; OnPropertyChanged(); RefreshConfigPaths(); }
        }

        public string? SelectedConfigPath
        {
            get => _selectedConfigPath;
            set
            {
                if (_selectedConfigPath == value) return;
                _selectedConfigPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedConfigName));
                RefreshCanExecute();
                if (!string.IsNullOrEmpty(value))
                    LoadSelectedConfig();
            }
        }

        public string SelectedConfigName => _selectedConfigPath != null ? Path.GetFileName(_selectedConfigPath) : "(none)";

        public ConfigModel Config
        {
            get => _config;
            private set { _config = value; OnPropertyChanged(); }
        }

        public ActivityModel? SelectedActivity
        {
            get => _selectedActivity;
            set
            {
                if (_selectedActivity != null)
                    _selectedActivity.PropertyChanged -= OnSelectedActivityPropertyChanged;
                _selectedActivity = value;
                if (_selectedActivity != null)
                    _selectedActivity.PropertyChanged += OnSelectedActivityPropertyChanged;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedActivity));
                OnPropertyChanged(nameof(ActiveActivityTemplate));
                OnPropertyChanged(nameof(SelectedActivityFamily));
                OnPropertyChanged(nameof(IsMultiInstanceFamily));
                _selectedCharacter = null;
                OnPropertyChanged(nameof(SelectedCharacter));
                RebuildFactionFilteredCharacters();
                RefreshHierarchyView();
                RefreshCanExecute();
            }
        }

        public bool HasSelectedActivity => _selectedActivity != null;

        public CharacterSettingsModel? SelectedCharacter
        {
            get => _selectedCharacter;
            set { _selectedCharacter = value; OnPropertyChanged(); RefreshCanExecute(); }
        }

        public CharacterInfo? CharacterPickerSelection
        {
            get => _characterPickerSelection;
            set { _characterPickerSelection = value; OnPropertyChanged(); RefreshCanExecute(); }
        }

        public ActivityTemplate? TemplatePickerSelection
        {
            get => _templatePickerSelection;
            set { _templatePickerSelection = value; OnPropertyChanged(); RefreshCanExecute(); }
        }

        /// <summary>
        /// Template the currently-selected activity was instantiated from.
        /// Setting this property MORPHS the activity to the new template:
        /// catalog metadata (id/family/level/faction/etc.) and parameters are
        /// re-seeded, but the Characters list is preserved (and re-validated
        /// against the new restrictions on subsequent adds).
        /// </summary>
        public ActivityTemplate? ActiveActivityTemplate
        {
            get => _selectedActivity == null ? null : _catalog.FindById(_selectedActivity.ActivityId);
            set
            {
                if (_selectedActivity == null || value == null) return;
                if (_selectedActivity.ActivityId == value.Id) return;
                MorphActivityToTemplate(_selectedActivity, value);
                OnPropertyChanged();
                RefreshHierarchyView();
                StatusMessage = $"Changed activity to '{value.DisplayName}'";
            }
        }

        /// <summary>Re-seed an activity in place from a new template. Characters survive the swap.
        /// If the template's faction is Either, preserve the slot's existing Alliance/Horde pick
        /// (or fall back to Alliance if the slot was previously Either).</summary>
        private void MorphActivityToTemplate(ActivityModel activity, ActivityTemplate template)
        {
            activity.ActivityId = template.Id;
            activity.DisplayName = template.DisplayName;
            activity.Family = template.Family;
            activity.Location = template.Location;
            activity.MinPlayers = template.MinPlayers;
            activity.MaxPlayers = template.MaxPlayers;
            activity.LevelMin = template.LevelMin;
            activity.LevelMax = template.LevelMax;
            if (template.FactionRestriction != Faction.Either)
                activity.FactionRestriction = template.FactionRestriction;
            else if (activity.FactionRestriction == Faction.Either)
                activity.FactionRestriction = Faction.Alliance;
            activity.StateChangeGoal = template.StateChangeGoal;

            activity.AttunementRequirements.Clear();
            foreach (var att in template.AttunementRequirements)
                activity.AttunementRequirements.Add(att);

            activity.Parameters.Clear();
            foreach (var p in template.DefaultParameters)
            {
                var copy = new ActivityParameter
                {
                    Key = p.Key,
                    Value = p.Value,
                    Description = p.Description,
                    IsRequired = p.IsRequired,
                    SearchKind = p.SearchKind,
                };
                if (p.Choices.Count > 0)
                    copy.SetChoices(p.Choices);
                activity.Parameters.Add(copy);
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        public ICommand BrowseDirectoryCommand { get; }
        public ICommand LoadConfigCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand SaveAsConfigCommand { get; }
        public ICommand NewConfigCommand { get; }
        public ICommand AddActivityCommand { get; }
        public ICommand RemoveActivityCommand { get; }
        public ICommand AddParameterCommand { get; }
        public ICommand AddCharacterCommand { get; }
        public ICommand RemoveCharacterCommand { get; }
        public ICommand RefreshAvailableCharactersCommand { get; }
        public ICommand ValidateHierarchyCommand { get; }
        public ICommand FillToMinCommand { get; }
        public ICommand FillToMaxCommand { get; }

        public ConfigEditorViewModel()
        {
            BrowseDirectoryCommand = new CommandHandler(BrowseDirectory, true);
            LoadConfigCommand = new CommandHandler(LoadSelectedConfig, () => !string.IsNullOrEmpty(_selectedConfigPath));
            SaveConfigCommand = new CommandHandler(SaveCurrentConfig, () => !string.IsNullOrEmpty(_selectedConfigPath));
            SaveAsConfigCommand = new CommandHandler(SaveAsNewConfig, true);
            NewConfigCommand = new CommandHandler(StartNewConfig, true);
            AddActivityCommand = new CommandHandler(AddActivity, () => _catalog.Templates.Count > 0);
            RemoveActivityCommand = new CommandHandler(RemoveActivity, () => _selectedActivity != null);
            AddParameterCommand = new CommandHandler(AddParameter, () => _selectedActivity != null);
            AddCharacterCommand = new CommandHandler(AddEmptyCharacterRow,
                () => _selectedActivity != null);
            RemoveCharacterCommand = new CommandHandler(RemoveCharacterFromActivity,
                () => _selectedActivity != null && _selectedCharacter != null);
            RefreshAvailableCharactersCommand = new AsyncCommandHandler(RefreshAvailableCharactersAsync);
            ValidateHierarchyCommand = new CommandHandler(RefreshHierarchyView, () => _selectedActivity != null);
            FillToMinCommand = new CommandHandler(() => FillCharactersTo(target: false),
                () => _selectedActivity != null);
            FillToMaxCommand = new CommandHandler(() => FillCharactersTo(target: true),
                () => _selectedActivity != null);

            if (Directory.Exists(UIConstants.ConfigsDirectory))
            {
                SettingsDirectory = UIConstants.ConfigsDirectory;
                var defaultPath = Path.Combine(UIConstants.ConfigsDirectory, UIConstants.DefaultConfigFileName);
                if (File.Exists(defaultPath))
                {
                    SelectedConfigPath = defaultPath;
                    LoadSelectedConfig();
                }
            }

            _ = RefreshAvailableCharactersAsync();
        }

        private async Task RefreshAvailableCharactersAsync()
        {
            try
            {
                var list = await _characterService.GetAllCharactersAsync();
                AvailableCharacters.Clear();
                foreach (var c in list)
                    AvailableCharacters.Add(c);
                RebuildFactionFilteredCharacters();
                RehydrateSelectedCharacterInfoOnAllRows();
                StatusMessage = $"Loaded {AvailableCharacters.Count} characters from the characters DB";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Character DB unavailable: {ex.Message} — populate the DB before assigning characters to activities";
            }
        }

        /// <summary>
        /// Re-populate <see cref="AvailableCharactersForActivity"/> filtered by
        /// the selected activity's <see cref="ActivityModel.FactionRestriction"/>.
        /// Called on activity selection change, faction-restriction change, and
        /// after the DB list is refreshed.
        /// </summary>
        private void RebuildFactionFilteredCharacters()
        {
            AvailableCharactersForActivity.Clear();
            var fac = _selectedActivity?.FactionRestriction ?? Faction.Either;
            foreach (var c in AvailableCharacters)
            {
                if (fac == Faction.Either || c.Faction == fac)
                    AvailableCharactersForActivity.Add(c);
            }
        }

        /// <summary>For every Character row across all activities, look up the
        /// matching DB row by AccountName so the inline ComboBox shows the
        /// correct selection after a config load.</summary>
        private void RehydrateSelectedCharacterInfoOnAllRows()
        {
            foreach (var act in Config.Activities)
                foreach (var ch in act.Characters)
                {
                    if (ch.SelectedCharacterInfo != null) continue;
                    foreach (var info in AvailableCharacters)
                        if (string.Equals(info.Name, ch.AccountName, StringComparison.OrdinalIgnoreCase))
                        {
                            ch.SelectedCharacterInfo = info;
                            break;
                        }
                }
        }

        private void BrowseDirectory()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select a config file",
                InitialDirectory = Directory.Exists(UIConstants.ConfigsDirectory) ? UIConstants.ConfigsDirectory : null
            };

            if (dialog.ShowDialog() == true)
            {
                SettingsDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
                SelectedConfigPath = dialog.FileName;
                LoadSelectedConfig();
            }
        }

        private void RefreshConfigPaths()
        {
            ConfigPaths.Clear();
            if (string.IsNullOrEmpty(_settingsDirectory)) return;
            foreach (var path in ConfigFileService.FindConfigs(_settingsDirectory))
                ConfigPaths.Add(path);
        }

        private void LoadSelectedConfig()
        {
            if (string.IsNullOrEmpty(_selectedConfigPath)) return;
            try
            {
                Config = ConfigFileService.Load(_selectedConfigPath);

                // Normalize any persisted Either→Alliance so the slot dropdown
                // (which doesn't list Either) has a valid selection.
                foreach (var act in Config.Activities)
                    if (act.FactionRestriction == Faction.Either)
                        act.FactionRestriction = Faction.Alliance;

                RehydrateSelectedCharacterInfoOnAllRows();
                SelectedActivity = Config.Activities.FirstOrDefault();

                // Always re-fetch the characters DB on config load so the
                // inline ComboBoxes in each row are fresh — covers tab-click
                // and "different config picked from dropdown" cases.
                _ = RefreshAvailableCharactersAsync();

                var charCount = Config.Activities.Sum(a => a.Characters.Count);
                StatusMessage = $"Loaded {Config.Activities.Count} activities, {charCount} characters from {Path.GetFileName(_selectedConfigPath)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load failed: {ex.Message}";
            }
        }

        private void SaveCurrentConfig()
        {
            if (string.IsNullOrEmpty(_selectedConfigPath)) return;
            try
            {
                ConfigFileService.Save(_selectedConfigPath, Config);
                StatusMessage = $"Saved to {Path.GetFileName(_selectedConfigPath)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save failed: {ex.Message}";
            }
        }

        private void SaveAsNewConfig()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                Title = "Save config as",
                InitialDirectory = _settingsDirectory,
                FileName = $"{(string.IsNullOrEmpty(Config.Name) ? "NewConfig" : Config.Name)}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ConfigFileService.Save(dialog.FileName, Config);
                    SelectedConfigPath = dialog.FileName;
                    SettingsDirectory = Path.GetDirectoryName(dialog.FileName) ?? _settingsDirectory;
                    StatusMessage = $"Saved to {Path.GetFileName(dialog.FileName)}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Save As failed: {ex.Message}";
                }
            }
        }

        private void StartNewConfig()
        {
            Config = new ConfigModel { Name = "New Config" };
            SelectedActivity = null;
            SelectedConfigPath = null;
            StatusMessage = "New empty config. Add an Activity to begin.";
        }

        private void AddActivity()
        {
            // Add a placeholder activity from the first catalog template. User
            // changes the template in the detail panel via ActiveActivityTemplate.
            var template = _templatePickerSelection ?? _catalog.Templates[0];
            var activity = _catalog.Instantiate(template);
            Config.Activities.Add(activity);
            SelectedActivity = activity;
            StatusMessage = $"Added activity '{activity.DisplayName}' — change via the dropdown at the top of the detail panel";
        }

        private void RemoveActivity()
        {
            if (_selectedActivity == null) return;
            var name = _selectedActivity.DisplayName;
            Config.Activities.Remove(_selectedActivity);
            SelectedActivity = Config.Activities.FirstOrDefault();
            StatusMessage = $"Removed activity '{name}'";
        }

        private void AddParameter()
        {
            if (_selectedActivity == null) return;
            _selectedActivity.Parameters.Add(new ActivityParameter
            {
                Key = "NewParam",
                Value = string.Empty,
            });
        }

        /// <summary>
        /// Add empty character rows up to the activity's MinPlayers (target=false)
        /// or MaxPlayers (target=true). Easy way to fill a 5-man dungeon or
        /// 40-man raid roster; user picks each character via the inline dropdown.
        /// </summary>
        private void FillCharactersTo(bool target)
        {
            if (_selectedActivity == null) return;
            var goal = target ? _selectedActivity.MaxPlayers : _selectedActivity.MinPlayers;
            var existing = _selectedActivity.Characters.Count;
            if (existing >= goal)
            {
                StatusMessage = $"Already at {existing}/{goal} characters — nothing to add.";
                return;
            }
            var toAdd = goal - existing;
            for (var i = 0; i < toAdd; i++)
            {
                _selectedActivity.Characters.Add(new CharacterSettingsModel
                {
                    AccountName = string.Empty,
                    RunnerType = BotRunnerType.Background,
                    ShouldRun = true,
                });
            }
            SelectedCharacter = _selectedActivity.Characters.LastOrDefault();
            var label = target ? "Max" : "Min";
            StatusMessage = $"Filled to {label} ({goal}) — added {toAdd} empty rows; pick characters via the Account column dropdown.";
        }

        /// <summary>
        /// Add a fresh empty character row. The user picks the actual DB
        /// character via the inline ComboBox in the Account column; setting
        /// that ComboBox triggers the metadata copy via
        /// <see cref="CharacterSettingsModel.SelectedCharacterInfo"/>.
        /// Game-restriction enforcement (level / faction) happens lazily.
        /// </summary>
        private void AddEmptyCharacterRow()
        {
            if (_selectedActivity == null) return;
            if (_selectedActivity.Characters.Count >= _selectedActivity.MaxPlayers)
            {
                StatusMessage = $"BLOCKED: '{_selectedActivity.DisplayName}' is at MaxPlayers ({_selectedActivity.MaxPlayers}).";
                return;
            }
            var c = new CharacterSettingsModel
            {
                AccountName = string.Empty,
                RunnerType = BotRunnerType.Background,
                ShouldRun = true,
            };
            _selectedActivity.Characters.Add(c);
            SelectedCharacter = c;
            StatusMessage = "Empty row added — pick a character from the Account column dropdown.";
        }

        /// <summary>
        /// Legacy validating add (used by automated tests). Kept for callers
        /// that still pre-select a CharacterInfo before calling Add. New row
        /// addition goes through <see cref="AddEmptyCharacterRow"/>.
        /// </summary>
        private void AddCharacterToActivity()
        {
            if (_selectedActivity == null || _characterPickerSelection == null) return;

            // Game-restriction enforcement: max players, level bracket, faction.
            if (_selectedActivity.Characters.Count >= _selectedActivity.MaxPlayers)
            {
                StatusMessage = $"BLOCKED: '{_selectedActivity.DisplayName}' is at MaxPlayers ({_selectedActivity.MaxPlayers}).";
                return;
            }
            if (_selectedActivity.Characters.Any(c => c.AccountName == _characterPickerSelection.Name))
            {
                StatusMessage = $"BLOCKED: '{_characterPickerSelection.Name}' is already on this activity.";
                return;
            }

            var pick = _characterPickerSelection;
            var charLevel = pick.Level;
            var (effectiveMin, effectiveMax) = GetEffectiveLevelRange(_selectedActivity);
            if (charLevel < effectiveMin || charLevel > effectiveMax)
            {
                StatusMessage = $"BLOCKED: '{pick.Name}' is level {charLevel}; '{_selectedActivity.DisplayName}' " +
                                $"requires {effectiveMin}-{effectiveMax}.";
                return;
            }

            var charFaction = pick.Faction;
            if (!FactionHelpers.Allows(_selectedActivity.FactionRestriction, charFaction))
            {
                StatusMessage = $"BLOCKED: '{pick.Name}' is {charFaction}; '{_selectedActivity.DisplayName}' " +
                                $"is {_selectedActivity.FactionRestriction}-only.";
                return;
            }

            // BG-specific: warn if the chosen bracket parameter conflicts with the picked Faction.
            var factionParam = _selectedActivity.Parameters.FirstOrDefault(p => p.Key == "Faction");
            if (factionParam != null && Enum.TryParse<Faction>(factionParam.Value, true, out var paramFaction)
                && paramFaction != Faction.Either && paramFaction != charFaction)
            {
                StatusMessage = $"BLOCKED: activity Faction parameter is '{paramFaction}' but '{pick.Name}' is {charFaction}.";
                return;
            }

            var c = new CharacterSettingsModel
            {
                AccountName = pick.Name,
                CharacterClass = pick.ClassName,
                CharacterRace = pick.RaceName,
                CharacterGender = pick.GenderName,
                RunnerType = BotRunnerType.Background,
                ShouldRun = true,
            };
            _selectedActivity.Characters.Add(c);
            SelectedCharacter = c;

            var attWarning = _selectedActivity.AttunementRequirements.Count > 0
                ? $" (warning: {_selectedActivity.AttunementRequirements.Count} attunement(s) required: " +
                  string.Join(", ", _selectedActivity.AttunementRequirements) + " — verify before run)"
                : string.Empty;
            StatusMessage = $"Added '{c.AccountName}' (level {charLevel} {charFaction}) to '{_selectedActivity.DisplayName}'{attWarning}";
        }

        private void RemoveCharacterFromActivity()
        {
            if (_selectedActivity == null || _selectedCharacter == null) return;
            var name = _selectedCharacter.AccountName;
            _selectedActivity.Characters.Remove(_selectedCharacter);
            SelectedCharacter = _selectedActivity.Characters.FirstOrDefault();
            StatusMessage = $"Removed '{name}' from '{_selectedActivity.DisplayName}'";
        }

        /// <summary>
        /// Compute the effective level range for an activity. If a "Bracket"
        /// parameter is present (BGs use this — "10-19", "20-29", etc.), parse
        /// it and use that as the gate. Otherwise fall back to the activity's
        /// LevelMin/LevelMax. This lets condensed BG templates (one row per BG
        /// instead of one per bracket) still enforce per-bracket level checks.
        /// </summary>
        private static (int min, int max) GetEffectiveLevelRange(ActivityModel a)
        {
            var bracketParam = a.Parameters.FirstOrDefault(p => p.Key == "Bracket");
            if (bracketParam != null && !string.IsNullOrWhiteSpace(bracketParam.Value))
            {
                var parts = bracketParam.Value.Split('-');
                if (parts.Length == 2
                    && int.TryParse(parts[0], out var bMin)
                    && int.TryParse(parts[1], out var bMax))
                {
                    return (bMin, bMax);
                }
            }
            return (a.LevelMin, a.LevelMax);
        }

        /// <summary>
        /// Populate Objectives / Trace / Issues collections from the selected
        /// activity's catalog template. Looked up by ActivityId so a saved
        /// config can be reloaded without losing the hierarchy view.
        /// </summary>
        private void RefreshHierarchyView()
        {
            SelectedActivityObjectives.Clear();
            HierarchyTrace.Clear();
            HierarchyIssues.Clear();

            if (_selectedActivity == null)
            {
                HierarchySummary = "(no activity selected)";
                HierarchyValid = false;
                return;
            }

            var template = _catalog.FindById(_selectedActivity.ActivityId);
            if (template == null)
            {
                HierarchySummary = $"No catalog template for ActivityId '{_selectedActivity.ActivityId}' — hierarchy unavailable.";
                HierarchyValid = false;
                return;
            }

            foreach (var obj in template.Objectives)
                SelectedActivityObjectives.Add(obj);

            var result = _validator.Validate(template);
            foreach (var line in result.Trace)
                HierarchyTrace.Add(line);
            foreach (var issue in result.Issues)
                HierarchyIssues.Add(issue);

            HierarchyValid = result.IsValid;
            HierarchySummary = $"{result.ObjectiveCount} objectives · {result.TaskCount} tasks · {result.ActionCount} actions · " +
                               (result.IsValid ? "VALID" : $"{result.Issues.Count} issues");
        }

        private void RefreshCanExecute()
        {
            (LoadConfigCommand as CommandHandler)?.RaiseCanExecuteChanged();
            (SaveConfigCommand as CommandHandler)?.RaiseCanExecuteChanged();
            (AddActivityCommand as CommandHandler)?.RaiseCanExecuteChanged();
            (RemoveActivityCommand as CommandHandler)?.RaiseCanExecuteChanged();
            (AddParameterCommand as CommandHandler)?.RaiseCanExecuteChanged();
            (AddCharacterCommand as CommandHandler)?.RaiseCanExecuteChanged();
            (RemoveCharacterCommand as CommandHandler)?.RaiseCanExecuteChanged();
            (ValidateHierarchyCommand as CommandHandler)?.RaiseCanExecuteChanged();
            (FillToMinCommand as CommandHandler)?.RaiseCanExecuteChanged();
            (FillToMaxCommand as CommandHandler)?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Listen for FactionRestriction edits on the selected activity so the
        /// faction-filtered character dropdown narrows live when the user
        /// flips Alliance/Horde/Either at the top of the panel.
        /// </summary>
        private void OnSelectedActivityPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ActivityModel.FactionRestriction))
                RebuildFactionFilteredCharacters();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
