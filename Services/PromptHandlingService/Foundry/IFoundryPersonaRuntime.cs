namespace PromptHandlingService.Foundry;

public interface IFoundryPersonaRuntime
{
    Task<PersonaPromptResult> GenerateAsync(PersonaPromptRequest request, CancellationToken cancellationToken);

    Task<PersonaPromptResult> GenerateAsync(
        PersonaPromptRequest request,
        PersonaPromptRuntimeBinding binding,
        CancellationToken cancellationToken);
}
