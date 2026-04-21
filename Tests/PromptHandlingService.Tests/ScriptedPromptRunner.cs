using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptHandlingService.Tests;

internal sealed class ScriptedPromptRunner(Func<IReadOnlyList<KeyValuePair<string, string?>>, string?> responseFactory) : IPromptRunner
{
    private readonly List<IReadOnlyList<KeyValuePair<string, string?>>> _calls = [];

    public IReadOnlyList<IReadOnlyList<KeyValuePair<string, string?>>> Calls => _calls;

    public int MaxConcurrent => 100;

    public Task<string?> RunChatAsync(IEnumerable<KeyValuePair<string, string?>> chatHistory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = chatHistory.ToList();
        _calls.Add(snapshot);
        return Task.FromResult(responseFactory(snapshot));
    }

    public void Dispose()
    {
    }
}
