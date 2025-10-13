namespace WWoW.RecordedTests.Shared;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WWoW.RecordedTests.Shared.Abstractions.I;

public sealed class LocalMangosDockerTrueNasAppsClient : IMangosAppsClient
{
    private readonly IReadOnlyDictionary<string, LocalMangosDockerConfiguration> _configurations;
    private readonly IDockerCli _docker;
    private bool _disposed;

    public LocalMangosDockerTrueNasAppsClient(IEnumerable<LocalMangosDockerConfiguration> configurations, IDockerCli? dockerCli = null)
    {
        ArgumentNullException.ThrowIfNull(configurations);

        var configList = configurations.ToList();
        if (configList.Count == 0)
        {
            throw new ArgumentException("At least one docker configuration must be provided.", nameof(configurations));
        }

        _configurations = configList.ToDictionary(cfg => cfg.ReleaseName, StringComparer.OrdinalIgnoreCase);
        _docker = dockerCli ?? new DockerCli();
    }

    public async Task<TrueNasAppRelease?> GetReleaseAsync(string releaseName, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(releaseName))
        {
            throw new ArgumentException("Release name is required.", nameof(releaseName));
        }

        if (!_configurations.TryGetValue(releaseName, out var configuration))
        {
            return null;
        }

        var containerName = configuration.EffectiveContainerName;
        var state = await InspectContainerAsync(containerName, cancellationToken).ConfigureAwait(false);
        var isRunning = state?.IsRunning ?? false;

        return new TrueNasAppRelease(
            configuration.ReleaseName,
            isRunning,
            IsCheckedOut: false,
            Host: configuration.Host,
            Port: configuration.HostPort,
            Realm: configuration.Realm);
    }

    public async Task StartReleaseAsync(string releaseName, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(releaseName))
        {
            throw new ArgumentException("Release name is required.", nameof(releaseName));
        }

        if (!_configurations.TryGetValue(releaseName, out var configuration))
        {
            throw new ArgumentException($"No docker configuration registered for release '{releaseName}'.", nameof(releaseName));
        }

        var containerName = configuration.EffectiveContainerName;
        var state = await InspectContainerAsync(containerName, cancellationToken).ConfigureAwait(false);

        if (state is { IsRunning: true })
        {
            return;
        }

        if (state is not null)
        {
            await RunDockerAsync(new[] { "start", containerName }, cancellationToken).ConfigureAwait(false);
            return;
        }

        var args = BuildRunArguments(configuration, containerName);
        await RunDockerAsync(args, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private async Task<DockerContainerState?> InspectContainerAsync(string containerName, CancellationToken cancellationToken)
    {
        var result = await _docker.RunAsync(new[] { "container", "inspect", containerName }, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            if (result.StandardError.IndexOf("No such object", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return null;
            }

            throw new InvalidOperationException($"Failed to inspect container '{containerName}': {result.StandardError.Trim()}");
        }

        using var document = JsonDocument.Parse(result.StandardOutput);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        var container = document.RootElement[0];
        if (!container.TryGetProperty("State", out var stateElement))
        {
            return null;
        }

        var isRunning = stateElement.TryGetProperty("Running", out var running) && running.GetBoolean();
        var status = stateElement.TryGetProperty("Status", out var statusElement)
            ? statusElement.GetString() ?? string.Empty
            : string.Empty;

        return new DockerContainerState(isRunning, status);
    }

    private async Task RunDockerAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var result = await _docker.RunAsync(arguments, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Docker command '{FormatArguments(arguments)}' failed with exit code {result.ExitCode}: {result.StandardError.Trim()}");
        }
    }

    private static IReadOnlyList<string> BuildRunArguments(LocalMangosDockerConfiguration configuration, string containerName)
    {
        var args = new List<string>
        {
            "run",
            "--detach",
            "--name",
            containerName,
            "--pull",
            "missing",
            "--publish",
            FormattableString.Invariant($"{configuration.HostPort}:{configuration.ContainerPort}")
        };

        if (configuration.Environment.Count > 0)
        {
            foreach (var kvp in configuration.Environment)
            {
                args.Add("--env");
                args.Add(FormattableString.Invariant($"{kvp.Key}={kvp.Value}"));
            }
        }

        if (configuration.VolumeMappings.Count > 0)
        {
            foreach (var volume in configuration.VolumeMappings)
            {
                args.Add("--volume");
                args.Add(volume);
            }
        }

        if (configuration.AdditionalArguments.Count > 0)
        {
            args.AddRange(configuration.AdditionalArguments);
        }

        args.Add(configuration.Image);

        if (configuration.Command.Count > 0)
        {
            args.AddRange(configuration.Command);
        }

        return args;
    }

    private static string FormatArguments(IEnumerable<string> args)
    {
        return string.Join(' ', args.Select(a => a.Any(char.IsWhiteSpace) ? $"\"{a}\"" : a));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LocalMangosDockerTrueNasAppsClient));
        }
    }

    private sealed record DockerContainerState(bool IsRunning, string Status);

    public interface IDockerCli
    {
        Task<DockerCliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken);
    }

    public sealed record DockerCliResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class DockerCli : IDockerCli
    {
        public async Task<DockerCliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = new Process { StartInfo = startInfo };

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("Failed to start docker process.");
                }
            }
            catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
            {
                throw new InvalidOperationException("Docker CLI is not available on the current machine.", ex);
            }

            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var stdOut = await stdOutTask.ConfigureAwait(false);
            var stdErr = await stdErrTask.ConfigureAwait(false);

            return new DockerCliResult(process.ExitCode, stdOut, stdErr);
        }
    }
}

public sealed class LocalMangosDockerConfiguration
{
    public LocalMangosDockerConfiguration(
        string releaseName,
        string image,
        int hostPort,
        int containerPort,
        string? containerName = null,
        string host = "127.0.0.1",
        string? realm = null,
        IReadOnlyDictionary<string, string>? environment = null,
        IReadOnlyList<string>? volumeMappings = null,
        IReadOnlyList<string>? additionalArguments = null,
        IReadOnlyList<string>? command = null)
    {
        if (string.IsNullOrWhiteSpace(releaseName))
        {
            throw new ArgumentException("Release name is required.", nameof(releaseName));
        }

        if (string.IsNullOrWhiteSpace(image))
        {
            throw new ArgumentException("Docker image is required.", nameof(image));
        }

        if (hostPort <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hostPort), "Host port must be greater than zero.");
        }

        if (containerPort <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(containerPort), "Container port must be greater than zero.");
        }

        ReleaseName = releaseName;
        Image = image;
        HostPort = hostPort;
        ContainerPort = containerPort;
        ContainerName = containerName;
        Host = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host;
        Realm = realm;
        Environment = environment ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        VolumeMappings = volumeMappings ?? Array.Empty<string>();
        AdditionalArguments = additionalArguments ?? Array.Empty<string>();
        Command = command ?? Array.Empty<string>();
    }

    public string ReleaseName { get; }

    public string Image { get; }

    public int HostPort { get; }

    public int ContainerPort { get; }

    public string? ContainerName { get; }

    public string Host { get; }

    public string? Realm { get; }

    public IReadOnlyDictionary<string, string> Environment { get; }

    public IReadOnlyList<string> VolumeMappings { get; }

    public IReadOnlyList<string> AdditionalArguments { get; }

    public IReadOnlyList<string> Command { get; }

    public string EffectiveContainerName => string.IsNullOrWhiteSpace(ContainerName)
        ? $"mangos-{ReleaseName.Replace(' ', '-').ToLowerInvariant()}"
        : ContainerName;
}
