using WWoW.AI.Memory;
using WWoW.AI.Observable;
using GameData.Core.Interfaces;

namespace WWoW.AI.Transitions;

/// <summary>
/// Context provided to transition rule predicates for evaluation.
/// Contains all information needed to make context-aware transition decisions.
/// </summary>
public sealed record TransitionContext(
    StateChangeEvent CurrentState,
    IObjectManager ObjectManager,
    CharacterMemory? Memory);
