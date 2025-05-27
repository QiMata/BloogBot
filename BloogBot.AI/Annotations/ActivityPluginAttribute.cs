using BloogBot.AI.States;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ActivityPluginAttribute : Attribute
{
    public BotActivity Activity { get; }
    public ActivityPluginAttribute(BotActivity activity) => Activity = activity;
}