using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WoWSimulation.Tests.Core;

/// <summary>
/// Mock WoW server that simulates mangos server responses without requiring live injection
/// </summary>
public class MockMangosServer
{
    private readonly Dictionary<string, object> _gameState = new();
    private readonly List<MockGameObject> _gameObjects = new();
    private readonly List<MockPlayer> _players = new();
    private readonly List<SimulationEvent> _eventQueue = new();
    private readonly int _commandLatencyMs;

    public event EventHandler<SimulationEvent>? EventOccurred;

    public MockMangosServer(int commandLatencyMs = 10)
    {
        _commandLatencyMs = commandLatencyMs;
        InitializeGameWorld();
    }

    private void InitializeGameWorld()
    {
        // Initialize a basic game world for testing
        _gameState["WorldTime"] = 0;
        _gameState["Weather"] = "Clear";
        _gameState["PlayerCount"] = 0;

        // Add some basic NPCs and objects for testing
        _gameObjects.Add(new MockGameObject 
        { 
            Id = 1, 
            Name = "Training Dummy", 
            Type = GameObjectType.Unit,
            Position = new Position3D(100, 100, 0),
            Health = 1000,
            MaxHealth = 1000
        });

        _gameObjects.Add(new MockGameObject 
        { 
            Id = 2, 
            Name = "Herb Node", 
            Type = GameObjectType.GameObject,
            Position = new Position3D(150, 150, 0),
            IsInteractable = true
        });
    }

    public async Task<T> SendCommand<T>(string command, object? parameters = null)
    {
        if (_commandLatencyMs > 0)
            await Task.Delay(_commandLatencyMs); // Simulate network latency

        return command switch
        {
            "GetPlayerPosition" => (T)(object)GetPlayerPosition(),
            "GetNearbyObjects" => (T)(object)GetNearbyObjects((int)(parameters ?? 50)),
            "MoveToPosition" => (T)(object)MoveToPosition((Position3D)(parameters ?? new Position3D())),
            "InteractWithObject" => (T)(object)InteractWithObject((int)(parameters ?? 0)),
            "GetPlayerHealth" => (T)(object)GetPlayerHealth(),
            "CastSpell" => (T)(object)CastSpell((string)(parameters ?? "")),
            "KillPlayer" => (T)(object)KillPlayer(),
            "ResurrectPlayer" => (T)(object)ResurrectPlayer(),
            _ => throw new NotSupportedException($"Command {command} not supported")
        };
    }

    private Position3D GetPlayerPosition()
    {
        var player = GetCurrentPlayer();
        return player?.Position ?? new Position3D(0, 0, 0);
    }

    private List<MockGameObject> GetNearbyObjects(int range)
    {
        var playerPos = GetPlayerPosition();
        var nearbyObjects = new List<MockGameObject>();

        foreach (var obj in _gameObjects)
        {
            var distance = CalculateDistance(playerPos, obj.Position);
            if (distance <= range)
            {
                nearbyObjects.Add(obj);
            }
        }

        return nearbyObjects;
    }

    private bool MoveToPosition(Position3D targetPosition)
    {
        var player = GetCurrentPlayer();
        if (player == null) return false;

        // Simulate movement over time
        var currentPos = player.Position;
        var distance = CalculateDistance(currentPos, targetPosition);
        
        // Simple linear interpolation for movement simulation
        var moveTime = distance / 7.0; // Assume 7 units per second movement speed
        
        player.Position = targetPosition;
        
        // Trigger movement event
        var moveEvent = new SimulationEvent
        {
            Type = EventType.PlayerMoved,
            Data = new { From = currentPos, To = targetPosition, Duration = moveTime }
        };
        
        _eventQueue.Add(moveEvent);
        EventOccurred?.Invoke(this, moveEvent);

        return true;
    }

    private bool InteractWithObject(int objectId)
    {
        var obj = _gameObjects.Find(o => o.Id == objectId);
        if (obj == null || !obj.IsInteractable) return false;

        var interactionEvent = new SimulationEvent
        {
            Type = EventType.ObjectInteraction,
            Data = new { ObjectId = objectId, ObjectName = obj.Name }
        };

        _eventQueue.Add(interactionEvent);
        EventOccurred?.Invoke(this, interactionEvent);

        return true;
    }

    private int GetPlayerHealth()
    {
        var player = GetCurrentPlayer();
        return player?.Health ?? 0;
    }

    private bool CastSpell(string spellName)
    {
        var player = GetCurrentPlayer();
        if (player == null) return false;

        // Simulate spell casting
        var spellEvent = new SimulationEvent
        {
            Type = EventType.SpellCast,
            Data = new { SpellName = spellName, CasterId = player.Id }
        };

        _eventQueue.Add(spellEvent);
        EventOccurred?.Invoke(this, spellEvent);

        return true;
    }

    private bool KillPlayer()
    {
        var player = GetCurrentPlayer();
        if (player == null) return false;
        if (player.Health <= 0) return false; // Already dead

        player.Health = 0;

        var deathEvent = new SimulationEvent
        {
            Type = EventType.Death,
            Data = new { PlayerId = player.Id, Position = player.Position }
        };

        _eventQueue.Add(deathEvent);
        EventOccurred?.Invoke(this, deathEvent);

        return true;
    }

    private bool ResurrectPlayer()
    {
        var player = GetCurrentPlayer();
        if (player == null) return false;
        if (player.Health > 0) return false; // Not dead

        player.Health = player.MaxHealth;

        var resEvent = new SimulationEvent
        {
            Type = EventType.Resurrection,
            Data = new { PlayerId = player.Id, RestoredHealth = player.MaxHealth }
        };

        _eventQueue.Add(resEvent);
        EventOccurred?.Invoke(this, resEvent);

        return true;
    }

    private MockPlayer? GetCurrentPlayer()
    {
        return _players.Count > 0 ? _players[0] : null;
    }

    private double CalculateDistance(Position3D pos1, Position3D pos2)
    {
        var dx = pos1.X - pos2.X;
        var dy = pos1.Y - pos2.Y;
        var dz = pos1.Z - pos2.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public void AddPlayer(MockPlayer player)
    {
        _players.Add(player);
        _gameState["PlayerCount"] = _players.Count;
    }

    public List<SimulationEvent> GetEventHistory() => new(_eventQueue);

    public void ClearEventHistory() => _eventQueue.Clear();
}

public class MockPlayer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Position3D Position { get; set; } = new();
    public int Health { get; set; } = 100;
    public int MaxHealth { get; set; } = 100;
    public int Mana { get; set; } = 100;
    public int MaxMana { get; set; } = 100;
    public int Level { get; set; } = 1;
    public string Class { get; set; } = "Warrior";
}

public class MockGameObject
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public GameObjectType Type { get; set; }
    public Position3D Position { get; set; } = new();
    public bool IsInteractable { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
}

public struct Position3D(float x, float y, float z)
{
    public float X { get; set; } = x;
    public float Y { get; set; } = y;
    public float Z { get; set; } = z;
}

public enum GameObjectType
{
    Unit,
    GameObject,
    Player,
    NPC
}

public class SimulationEvent
{
    public EventType Type { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public object? Data { get; set; }
}

public enum EventType
{
    PlayerMoved,
    ObjectInteraction,
    SpellCast,
    Combat,
    Death,
    Resurrection,
    LevelUp,
    ItemLooted
}
