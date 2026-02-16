using Communication;
using WoWStateManagerUI.Handlers;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;

namespace WoWStateManagerUI.Views;

public sealed class StateManagerViewModel : INotifyPropertyChanged, IDisposable
{
    private Timer? _statusPollTimer;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(10);

    private readonly Dictionary<CharacterDefinition, CharacterDefinition> _characterStates = [];
    private CharacterDefinition[]? _characterCache;

    public ICommand LocalStateManagerLoadCommand { get; }

    public ICommand StateManagerConnectCommand { get; }

    public ICommand StateManagerDisconnectCommand { get; }

    public StateManagerViewModel()
    {
        LocalStateManagerLoadCommand = new CommandHandler(StartStatusTimer, true);
        StateManagerConnectCommand = new CommandHandler(() =>
        {
            StartStatusTimer();
            _ = CheckServerStatusAsync();
        }, true);
        StateManagerDisconnectCommand = new CommandHandler(StopStatusTimer, true);

        OnPropertyChanged(nameof(SelectCharacterIndex));
    }

    private void StartStatusTimer()
    {
        if (_statusPollTimer != null)
        {
            return;
        }

        _statusPollTimer = new Timer(async _ => await PollServerStatusAsync(), null, TimeSpan.Zero, _pollInterval);
    }

    private void StopStatusTimer()
    {
        _statusPollTimer?.Dispose();
        _statusPollTimer = null;
    }

    private async Task PollServerStatusAsync()
    {
        try
        {
            RealmState = await CheckPortStatus(3724) ? "UP" : "DOWN";
            WorldState = await CheckPortStatus(8085) ? "UP" : "DOWN";

            TotalPopulation = RealmState == "UP" && WorldState == "UP" ? "3000" : "0";

            OnPropertyChanged(nameof(RealmState));
            OnPropertyChanged(nameof(WorldState));
            OnPropertyChanged(nameof(TotalPopulation));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to poll server status: {ex}");

            RealmState = "UNKNOWN";
            WorldState = "UNKNOWN";
            TotalPopulation = "--";

            OnPropertyChanged(nameof(RealmState));
            OnPropertyChanged(nameof(WorldState));
            OnPropertyChanged(nameof(TotalPopulation));
        }
    }

    private static async Task<bool> CheckPortStatus(int port, int timeoutMs = 1000)
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync("127.0.0.1", port);
            var completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
            return ReferenceEquals(completed, task) && client.Connected;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Port {port} check failed: {ex.Message}");
            return false;
        }
    }

    public async Task CheckServerStatusAsync()
    {
        var is3724 = await CheckPortStatus(3724);
        var is7878 = await CheckPortStatus(7878);
        var is8085 = await CheckPortStatus(8085);

        IsConnected = is3724 && is7878 && is8085;
    }

    public string RealmState { get; set; } = "UNKNOWN";
    public string WorldState { get; set; } = "UNKNOWN";
    public string TotalPopulation { get; set; } = "0";

    public string StateManagerUrl { get; set; } = "http://localhost:8085";
    public string MangosUrl { get; set; } = "http://localhost:7878";
    public string AdminUsername { get; set; } = "ADMINISTRATOR";
    public string AdminPassword { get; set; } = "PASSWORD";

    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public int SelectCharacterIndex => _selectedCharacterIndex;
    public int CurrentPageIndex => _currentPageIndex;

    private int _currentPageIndex { get; set; } = -1;
    private int _selectedCharacterIndex { get; set; } = -1;

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (_isConnected != value)
            {
                _isConnected = value;
                OnPropertyChanged();
            }
        }
    }

    public float OpennessValue
    {
        get => GetSelectedCharacter()?.Openness ?? 0f;
        set
        {
            if (TryGetSelectedCharacter(out var character))
            {
                character.Openness = value;
                OnPropertyChanged();
            }
        }
    }

    public float ConscientiousnessValue
    {
        get => GetSelectedCharacter()?.Conscientiousness ?? 0f;
        set
        {
            if (TryGetSelectedCharacter(out var character))
            {
                character.Conscientiousness = value;
                OnPropertyChanged();
            }
        }
    }

    public float ExtraversionValue
    {
        get => GetSelectedCharacter()?.Extraversion ?? 0f;
        set
        {
            if (TryGetSelectedCharacter(out var character))
            {
                character.Extraversion = value;
                OnPropertyChanged();
            }
        }
    }

    public float AgreeablenessValue
    {
        get => GetSelectedCharacter()?.Agreeableness ?? 0f;
        set
        {
            if (TryGetSelectedCharacter(out var character))
            {
                character.Agreeableness = value;
                OnPropertyChanged();
            }
        }
    }

    public float NeuroticismValue
    {
        get => GetSelectedCharacter()?.Neuroticism ?? 0f;
        set
        {
            if (TryGetSelectedCharacter(out var character))
            {
                character.Neuroticism = value;
                OnPropertyChanged();
            }
        }
    }

    private CharacterDefinition? GetSelectedCharacter() => TryGetSelectedCharacter(out var character) ? character : null;

    private bool TryGetSelectedCharacter(out CharacterDefinition character)
    {
        character = default!;

        if (_characterStates.Count == 0)
        {
            return false;
        }

        EnsureCharacterCache();
        var index = 20 * _currentPageIndex + _selectedCharacterIndex;

        if (index < 0 || _characterCache == null || index >= _characterCache.Length)
        {
            return false;
        }

        character = _characterCache[index];
        return true;
    }

    private void EnsureCharacterCache()
    {
        if (_characterCache == null || _characterCache.Length != _characterStates.Count)
        {
            _characterCache = _characterStates.Keys.ToArray();
        }
    }

    public void Dispose()
    {
        StopStatusTimer();
    }
}
