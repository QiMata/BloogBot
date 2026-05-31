using System.Text.Json;

namespace PromptHandlingService.Foundry;

public sealed class FoundryPersonaRuntime : IFoundryPersonaRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly FoundryPersonaRuntimeOptions _options;
    private readonly PersonaPromptAssembler _assembler;
    private readonly IFoundryResponsesClient _responsesClient;

    public FoundryPersonaRuntime(FoundryPersonaRuntimeOptions options)
        : this(options, new PersonaPromptAssembler(), new FoundryProjectResponsesClient(options))
    {
    }

    public FoundryPersonaRuntime(
        FoundryPersonaRuntimeOptions options,
        PersonaPromptAssembler assembler,
        IFoundryResponsesClient responsesClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _assembler = assembler ?? throw new ArgumentNullException(nameof(assembler));
        _responsesClient = responsesClient ?? throw new ArgumentNullException(nameof(responsesClient));
        _options.Validate();
    }

    public async Task<PersonaPromptResult> GenerateAsync(PersonaPromptRequest request, CancellationToken cancellationToken)
    {
        return await GenerateAsync(request, PersonaPromptRuntimeBinding.FromOptions(_options), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PersonaPromptResult> GenerateAsync(
        PersonaPromptRequest request,
        PersonaPromptRuntimeBinding binding,
        CancellationToken cancellationToken)
    {
        var assembledPrompt = _assembler.Assemble(request);
        ArgumentNullException.ThrowIfNull(binding);
        binding.Validate();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(_options.TimeoutMs));

        try
        {
            var response = await _responsesClient.CreateResponseAsync(
                new FoundryResponseRequest(
                    assembledPrompt,
                    binding.Model,
                    binding.MaxOutputTokens,
                    binding.AgentName,
                    binding.AgentVersion),
                timeout.Token).ConfigureAwait(false);

            return ParseResult(response, binding);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new TimeoutException($"Foundry persona runtime timed out after {_options.TimeoutMs} ms.", ex);
        }
    }

    private PersonaPromptResult ParseResult(FoundryResponseEnvelope response, PersonaPromptRuntimeBinding binding)
    {
        if (string.IsNullOrWhiteSpace(response.OutputText))
        {
            throw new InvalidDataException("Foundry persona runtime returned an empty response.");
        }

        FoundryPersonaPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<FoundryPersonaPayload>(response.OutputText, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Foundry persona runtime returned invalid JSON.", ex);
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.ReplyText))
        {
            throw new InvalidDataException("Foundry persona runtime response is missing replyText.");
        }

        return new PersonaPromptResult(
            payload.ReplyText,
            payload.Intent ?? string.Empty,
            payload.MemoryCandidates ?? Array.Empty<string>(),
            payload.Rationale ?? string.Empty,
            binding.AgentName,
            binding.AgentVersion ?? string.Empty,
            string.IsNullOrWhiteSpace(response.Model) ? binding.Model : response.Model);
    }

    private sealed class FoundryPersonaPayload
    {
        public string? ReplyText { get; init; }
        public string? Intent { get; init; }
        public string[]? MemoryCandidates { get; init; }
        public string? Rationale { get; init; }
    }
}
