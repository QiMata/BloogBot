using System.Reflection;
using System.Linq;
using BloogBot.AI.Annotations;
using BloogBot.AI.States;
using Microsoft.SemanticKernel;
using System.Threading;

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
            .Where(t => t.GetCustomAttributes<ActivityPluginAttribute>().Any());

        foreach (var t in types)
        {
            var plugin = KernelPluginFactory.CreateFromObject(t, t.Name);
            foreach (var attr in t.GetCustomAttributes<ActivityPluginAttribute>())
            {
                map.GetOrAdd(attr.Activity).Add(plugin);
            }
        }

        return map.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<KernelPlugin>)pair.Value.AsReadOnly());
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
