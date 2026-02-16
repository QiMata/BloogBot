using Communication;
using System.Collections.Generic;
using System.Linq;

namespace DecisionEngineService;

public class MLModel(List<float> initialWeights)
{
    private readonly List<float> _weights = initialWeights ?? [];

    public void LearnFromSnapshot(WoWActivitySnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        AdjustWeights(snapshot);
    }

    public static List<ActionMap> Predict(WoWActivitySnapshot snapshot)
    {
        return GenerateActionMap(snapshot);
    }

    public List<float> GetWeights()
    {
        return _weights;
    }

    private void AdjustWeights(WoWActivitySnapshot snapshot)
    {
        if (snapshot.CurrentAction == null)
        {
            return;
        }

        var actionIndex = (int)snapshot.CurrentAction.ActionType;
        if (actionIndex < 0)
        {
            return;
        }

        EnsureCapacity(actionIndex);

        if (snapshot.CurrentAction.ActionResult == ResponseResult.Success)
        {
            _weights[actionIndex]++;
        }
        else if (snapshot.CurrentAction.ActionResult == ResponseResult.Failure)
        {
            _weights[actionIndex]--;
        }
    }

    private void EnsureCapacity(int index)
    {
        while (_weights.Count <= index)
        {
            _weights.Add(0f);
        }
    }

    private static List<ActionMap> GenerateActionMap(WoWActivitySnapshot snapshot)
    {
        List<ActionMap> actionMaps = [];

        if (snapshot.Player?.Unit is { } unit && unit.Health < unit.MaxHealth * 0.5)
        {
            actionMaps.Add(new ActionMap
            {
                Actions =
                {
                    new ActionMessage
                    {
                        ActionType = ActionType.CastSpell,
                        Parameters =
                        {
                            new RequestParameter { IntParam = 12345 }
                        }
                    }
                }
            });
        }

        // Fix: RepeatedField<T> does not support Count(predicate), so use LINQ ToList().Count
        var hostileCount = snapshot.NearbyUnits == null
            ? 0
            : snapshot.NearbyUnits.Where(unit => unit.UnitFlags == 16 /* Hostile flag */).ToList().Count;
        if (hostileCount > 2)
        {
            actionMaps.Add(new ActionMap
            {
                Actions =
                {
                    new ActionMessage
                    {
                        ActionType = ActionType.CastSpell,
                        Parameters =
                        {
                            new RequestParameter { IntParam = 6789 }
                        }
                    }
                }
            });
        }

        if (snapshot.Player?.Unit?.GameObject?.Base?.Position is { } position)
        {
            actionMaps.Add(new ActionMap
            {
                Actions =
                {
                    new ActionMessage
                    {
                        ActionType = ActionType.Goto,
                        Parameters =
                        {
                            new RequestParameter { FloatParam = position.X },
                            new RequestParameter { FloatParam = position.Y },
                            new RequestParameter { FloatParam = position.Z }
                        }
                    }
                }
            });
        }

        return actionMaps;
    }
}
