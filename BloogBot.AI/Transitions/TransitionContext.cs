using BloogBot.AI.Memory;
using BloogBot.AI.Observable;
using GameData.Core.Interfaces;

namespace BloogBot.AI.Transitions;

/// <summary>
/// Context provided to transition rule predicates for evaluation.
/// Contains all information needed to make context-aware transition decisions.
/// </summary>
public sealed record TransitionContext(
    StateChangeEvent CurrentState,
    IObjectManager ObjectManager,
    CharacterMemory? Memory);
