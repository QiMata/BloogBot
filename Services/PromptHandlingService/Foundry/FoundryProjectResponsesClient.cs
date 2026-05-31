#pragma warning disable OPENAI001

using System.Collections.Concurrent;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using OpenAI.Responses;

namespace PromptHandlingService.Foundry;

public sealed class FoundryProjectResponsesClient : IFoundryResponsesClient
{
    private readonly AIProjectClient _projectClient;
    private readonly ConcurrentDictionary<string, ProjectResponsesClient> _responsesClients = new();

    public FoundryProjectResponsesClient(FoundryPersonaRuntimeOptions options)
        : this(options, new DefaultAzureCredential())
    {
    }

    public FoundryProjectResponsesClient(FoundryPersonaRuntimeOptions options, Azure.Core.TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credential);
        options.Validate();

        _projectClient = new AIProjectClient(new Uri(options.ProjectEndpoint), credential);
    }

    public async Task<FoundryResponseEnvelope> CreateResponseAsync(FoundryResponseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var inputItems = new[]
        {
            ResponseItem.CreateUserMessageItem(request.InputText)
        };

        var responseOptions = new CreateResponseOptions(request.Model, inputItems)
        {
            Instructions = PersonaPromptAssembler.OutputContract,
            MaxOutputTokenCount = request.MaxOutputTokens,
            ReasoningOptions = new ResponseReasoningOptions
            {
                ReasoningEffortLevel = ResponseReasoningEffortLevel.Minimal
            },
            StoredOutputEnabled = true
        };

        var responsesClient = GetResponsesClient(request);
        var result = await responsesClient.CreateResponseAsync(responseOptions, cancellationToken).ConfigureAwait(false);
        return new FoundryResponseEnvelope(result.Value.GetOutputText(), result.Value.Model, result.Value.Id);
    }

    private ProjectResponsesClient GetResponsesClient(FoundryResponseRequest request)
    {
        var key = string.IsNullOrWhiteSpace(request.AgentVersion)
            ? $"model:{request.Model}"
            : $"agent:{request.AgentName}:{request.AgentVersion}:model:{request.Model}";

        return _responsesClients.GetOrAdd(key, _ =>
        {
            if (string.IsNullOrWhiteSpace(request.AgentVersion))
            {
                return _projectClient.ProjectOpenAIClient.GetProjectResponsesClientForModel(request.Model);
            }

            return _projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgent(
                new AgentReference(request.AgentName, request.AgentVersion));
        });
    }
}
