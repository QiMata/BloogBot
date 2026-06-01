using System.Net;
using PromptHandlingService;
using PromptHandlingService.Api;
using PromptHandlingService.Storylines;
using WoWStateManager.Activities;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(
    builder.Configuration["StorylineApi:Urls"] ??
    builder.Configuration["Urls"] ??
    "http://127.0.0.1:5147");

builder.Services.AddPromptHandlingServices(builder.Configuration);
builder.Services.AddSingleton<IActivityCatalog, ActivityCatalog>();
builder.Services.AddSingleton<IStorylineActivityCatalog, ActivityCatalogStorylineAdapter>();
builder.Services.AddSingleton<IStorylineManagementService, StorylineManagementService>();
builder.Services.AddHostedService<StorylineFoundryDeploymentWorker>();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var remoteAddress = context.Connection.RemoteIpAddress;
    if (remoteAddress is not null && !IPAddress.IsLoopback(remoteAddress))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "storyline_api_local_only",
            message = "Storyline management API accepts loopback clients only."
        });
        return;
    }

    await next();
});

var api = app.MapGroup("/api/storylines/v1");

api.MapGet("/health", (IStorylineManagementService service, CancellationToken ct) =>
    service.GetHealthAsync(ct));

api.MapGet("/personas", (IStorylineManagementService service, CancellationToken ct) =>
    service.ListPersonasAsync(ct));

api.MapGet("/personas/{personaId}", async (string personaId, IStorylineManagementService service, CancellationToken ct) =>
{
    var persona = await service.GetPersonaAsync(personaId, ct).ConfigureAwait(false);
    return persona is null ? Results.NotFound() : Results.Ok(persona);
});

api.MapGet("/persona-versions", (string? personaId, IStorylineManagementService service, CancellationToken ct) =>
    service.ListPersonaVersionsAsync(personaId, ct));

api.MapGet("/graphs", (IStorylineManagementService service, CancellationToken ct) =>
    service.ListNarrativeGraphsAsync(ct));

api.MapGet("/graphs/{graphId}", async (string graphId, IStorylineManagementService service, CancellationToken ct) =>
{
    var graph = await service.GetNarrativeGraphAsync(graphId, ct).ConfigureAwait(false);
    return graph is null ? Results.NotFound() : Results.Ok(graph);
});

api.MapGet("/gameplay-arcs", (IStorylineManagementService service, CancellationToken ct) =>
    service.ListGameplayArcsAsync(ct));

api.MapGet("/characters", (IStorylineManagementService service, CancellationToken ct) =>
    service.ListCharacterBindingsAsync(ct));

api.MapGet("/characters/{characterId}", async (string characterId, IStorylineManagementService service, CancellationToken ct) =>
{
    var binding = await service.GetCharacterBindingAsync(characterId, ct).ConfigureAwait(false);
    return binding is null ? Results.NotFound() : Results.Ok(binding);
});

api.MapGet("/drafts", (string? kind, string? status, IStorylineManagementService service, CancellationToken ct) =>
    service.ListDraftsAsync(kind, status, ct));

api.MapGet("/drafts/{draftId}", async (string draftId, IStorylineManagementService service, CancellationToken ct) =>
{
    var draft = await service.GetDraftAsync(draftId, ct).ConfigureAwait(false);
    return draft is null ? Results.NotFound() : Results.Ok(draft);
});

api.MapPost("/drafts", (StorylineDraftDto draft, IStorylineManagementService service, CancellationToken ct) =>
    service.SaveDraftAsync(draft, ct));

api.MapPut("/drafts/{draftId}", (string draftId, StorylineDraftDto draft, IStorylineManagementService service, CancellationToken ct) =>
    service.SaveDraftAsync(draft with { DraftId = draftId }, ct));

api.MapPost("/drafts/{draftId}/publish", async (
    string draftId,
    PublishDraftRequest request,
    IStorylineManagementService service,
    CancellationToken ct) =>
{
    var result = await service.PublishDraftAsync(draftId, request, ct).ConfigureAwait(false);
    return result.Published ? Results.Ok(result) : Results.Json(result, statusCode: StatusCodes.Status400BadRequest);
});

api.MapPost("/foundry/deployments/preview", (
    FoundryDeploymentTargetRequest request,
    IStorylineFoundryDeploymentService service,
    CancellationToken ct) =>
        service.PreviewDeploymentAsync(request, ct));

api.MapPost("/foundry/deployments", async (
    FoundryDeploymentTargetRequest request,
    IStorylineFoundryDeploymentService service,
    CancellationToken ct) =>
{
    try
    {
        var deployment = await service.QueueDeploymentAsync(request, ct).ConfigureAwait(false);
        return Results.Accepted($"/api/storylines/v1/foundry/deployments/{deployment.DeploymentId}", deployment);
    }
    catch (StorylineFoundryDeploymentValidationException ex)
    {
        return Results.Json(ex.Preview, statusCode: StatusCodes.Status400BadRequest);
    }
});

api.MapGet("/foundry/deployments/{deploymentId}", async (
    string deploymentId,
    IStorylineFoundryDeploymentService service,
    CancellationToken ct) =>
{
    var deployment = await service.GetDeploymentAsync(deploymentId, ct).ConfigureAwait(false);
    return deployment is null ? Results.NotFound() : Results.Ok(deployment);
});

api.MapPost("/foundry/deployments/{deploymentId}/promote", async (
    string deploymentId,
    PromoteFoundryDeploymentRequest request,
    IStorylineFoundryDeploymentService service,
    CancellationToken ct) =>
{
    try
    {
        var deployment = await service.PromoteDeploymentAsync(deploymentId, request, ct).ConfigureAwait(false);
        return deployment is null ? Results.NotFound() : Results.Ok(deployment);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new
        {
            errors = new[] { new ValidationErrorDto("deploymentId", "invalid_status", ex.Message) }
        });
    }
});

api.MapGet("/memory-candidates", async (string characterId, string? status, IStorylineManagementService service, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(characterId))
    {
        return Results.BadRequest(new
        {
            errors = new[] { new ValidationErrorDto("characterId", "required", "characterId is required.") }
        });
    }

    var candidates = await service.ListMemoryCandidatesAsync(
        characterId,
        string.IsNullOrWhiteSpace(status) ? StorylineMemoryStatus.Pending : status,
        ct).ConfigureAwait(false);
    return Results.Ok(candidates);
});

api.MapPost("/memory-candidates/{candidateId}/review", async (
    string candidateId,
    MemoryCandidateReviewDto review,
    IStorylineManagementService service,
    CancellationToken ct) =>
{
    try
    {
        var candidate = await service.ReviewMemoryCandidateAsync(
            candidateId,
            review with { CandidateId = candidateId },
            ct).ConfigureAwait(false);
        return candidate is null ? Results.NotFound() : Results.Ok(candidate);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new
        {
            errors = new[] { new ValidationErrorDto("status", "invalid_status", ex.Message) }
        });
    }
});

api.MapGet("/activity-catalog", (IStorylineManagementService service, CancellationToken ct) =>
    service.ListActivityCatalogAsync(ct));

api.MapGet("/graph-layouts/{graphId}", async (string graphId, IStorylineManagementService service, CancellationToken ct) =>
{
    var layout = await service.GetGraphLayoutAsync(graphId, ct).ConfigureAwait(false);
    return layout is null ? Results.NotFound() : Results.Ok(layout);
});

api.MapPut("/graph-layouts/{graphId}", (string graphId, GraphLayoutDto layout, IStorylineManagementService service, CancellationToken ct) =>
    service.SaveGraphLayoutAsync(layout with { GraphId = graphId }, ct));

app.Run();

public partial class Program;
