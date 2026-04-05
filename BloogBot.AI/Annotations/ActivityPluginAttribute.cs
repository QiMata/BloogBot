using BloogBot.AI.States;

namespace BloogBot.AI.Annotations;

/// <summary>
/// Marks a Semantic Kernel plugin class as relevant to one bot activity.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ActivityPluginAttribute : Attribute
{
    public ActivityPluginAttribute(BotActivity activity)
    {
        Activity = activity;
    }

    public BotActivity Activity { get; }
}
