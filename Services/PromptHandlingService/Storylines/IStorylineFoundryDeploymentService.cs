namespace PromptHandlingService.Storylines;

public interface IStorylineFoundryDeploymentService
{
    Task<FoundryDeploymentPreviewDto> PreviewDeploymentAsync(
        FoundryDeploymentTargetRequest request,
        CancellationToken cancellationToken);

    Task<FoundryDeploymentDto> QueueDeploymentAsync(
        FoundryDeploymentTargetRequest request,
        CancellationToken cancellationToken);

    Task<FoundryDeploymentDto?> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken);

    Task<FoundryDeploymentDto?> RunDeploymentAsync(string deploymentId, CancellationToken cancellationToken);

    Task<FoundryDeploymentDto?> PromoteDeploymentAsync(
        string deploymentId,
        PromoteFoundryDeploymentRequest request,
        CancellationToken cancellationToken);
}
