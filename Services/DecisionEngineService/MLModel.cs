using Communication;

namespace DecisionEngineService;

public class MLModel
{
    private readonly List<float> _weights;

    public MLModel(List<float> initialWeights)
    {
        _weights = initialWeights ?? [];
    }

    public void LearnFromSnapshot(ActivitySnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        AdjustWeights(snapshot);
    }

    public static List<ActionMap> Predict(ActivitySnapshot snapshot)
    {
        return GenerateActionMap(snapshot);
    }

    public List<float> GetWeights()
    {
        return _weights;
    }

    private void AdjustWeights(ActivitySnapshot snapshot)
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

    private static List<ActionMap> GenerateActionMap(ActivitySnapshot snapshot)
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

        var hostileCount = snapshot.NearbyUnits?.Count(unit => unit.UnitFlags == 16 /* Hostile flag */) ?? 0;
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
