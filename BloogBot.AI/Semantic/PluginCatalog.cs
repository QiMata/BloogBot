using System.Collections.Concurrent;
using System.Reflection;
using BloogBot.AI.States;

public sealed class PluginCatalog
{
    private readonly ConcurrentDictionary<BotActivity, List<KernelPlugin>> _byActivity = new();

    public PluginCatalog()
    {
        foreach (var type in AppDomain.CurrentDomain
                     .GetAssemblies()
                     .SelectMany(a => a.GetTypes())
                     .Where(t => t.GetCustomAttributes<ActivityPluginAttribute>().Any()))
        {
            var plugin = KernelPluginFactory.CreateFromObject(type, type.Name);

            foreach (var attr in type.GetCustomAttributes<ActivityPluginAttribute>())
                _byActivity.GetOrAdd(attr.Activity, new List<KernelPlugin>()).Add(plugin);
        }
    }

    public IReadOnlyList<KernelPlugin> For(BotActivity a) => _byActivity.GetValueOrDefault(a) ?? [];
}

public class KernelPlugin
{
    // Placeholder for the actual KernelPlugin implementation
}

public static class KernelPluginFactory
{
    public static KernelPlugin CreateFromObject(Type type, string typeName)
    {
        throw new NotImplementedException();
    }
}

