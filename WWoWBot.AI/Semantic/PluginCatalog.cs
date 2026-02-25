using System.Reflection;
using BloogBot.AI.Annotations;
using BloogBot.AI.States;
using Microsoft.SemanticKernel;

namespace BloogBot.AI.Semantic;

public sealed class PluginCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<BotActivity, IReadOnlyList<KernelPlugin>>> _cachedCatalog =
        new(BuildCatalog, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly IReadOnlyDictionary<BotActivity, IReadOnlyList<KernelPlugin>> _byActivity;

    public PluginCatalog()
    {
        _byActivity = _cachedCatalog.Value;
    }

    public IReadOnlyList<KernelPlugin> For(BotActivity activity) =>
        _byActivity.TryGetValue(activity, out var list) ? list : Array.Empty<KernelPlugin>();

    private static IReadOnlyDictionary<BotActivity, IReadOnlyList<KernelPlugin>> BuildCatalog()
    {
        var map = new Dictionary<BotActivity, List<KernelPlugin>>();

        var types = AppDomain
            .CurrentDomain.GetAssemblies()
            .SelectMany(a => SafeGetTypes(a))
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.GetCustomAttributes<ActivityPluginAttribute>().Any())
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

        foreach (var t in types)
        {
            var plugin = TryCreatePlugin(t);
            if (plugin is null)
            {
                continue;
            }

            foreach (var attr in t.GetCustomAttributes<ActivityPluginAttribute>())
            {
                GetOrCreateList(map, attr.Activity).Add(plugin);
            }
        }

        return map.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<KernelPlugin>)pair.Value.AsReadOnly());
    }

    private static KernelPlugin? TryCreatePlugin(Type pluginType)
    {
        var constructor = pluginType.GetConstructor(Type.EmptyTypes);
        if (constructor is null)
        {
            return null;
        }

        var instance = Activator.CreateInstance(pluginType);
        return instance is null ? null : KernelPluginFactory.CreateFromObject(instance, pluginType.Name);
    }

    private static List<KernelPlugin> GetOrCreateList(
        IDictionary<BotActivity, List<KernelPlugin>> map,
        BotActivity activity)
    {
        if (!map.TryGetValue(activity, out var list))
        {
            list = new List<KernelPlugin>();
            map[activity] = list;
        }

        return list;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }
}
