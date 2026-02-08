using RecordedTests.Shared.Abstractions.I;

namespace RecordedTests.Shared.Factories;

/// <summary>
/// Helper methods for creating IBotRunnerFactory instances from delegates or types.
/// </summary>
public static class BotRunnerFactoryHelpers
{
    /// <summary>
    /// Creates a factory from a delegate that produces IBotRunner instances.
    /// </summary>
    /// <param name="factoryDelegate">Delegate that creates bot runner instances.</param>
    /// <returns>An IBotRunnerFactory that wraps the delegate.</returns>
    public static IBotRunnerFactory FromDelegate(Func<IBotRunner> factoryDelegate)
    {
        ArgumentNullException.ThrowIfNull(factoryDelegate);
        return new DelegateBotRunnerFactory(factoryDelegate);
    }

    /// <summary>
    /// Creates a factory that constructs instances of the specified type using the parameterless constructor.
    /// </summary>
    /// <typeparam name="TRunner">The concrete type of IBotRunner to create.</typeparam>
    /// <returns>An IBotRunnerFactory that creates instances of TRunner.</returns>
    public static IBotRunnerFactory FromType<TRunner>() where TRunner : IBotRunner, new()
    {
        return new TypedBotRunnerFactory<TRunner>();
    }

    private sealed class DelegateBotRunnerFactory : IBotRunnerFactory
    {
        private readonly Func<IBotRunner> _factoryDelegate;

        public DelegateBotRunnerFactory(Func<IBotRunner> factoryDelegate)
        {
            _factoryDelegate = factoryDelegate;
        }

        public IBotRunner Create() => _factoryDelegate();
    }

    private sealed class TypedBotRunnerFactory<TRunner> : IBotRunnerFactory
        where TRunner : IBotRunner, new()
    {
        public IBotRunner Create() => new TRunner();
    }
}
