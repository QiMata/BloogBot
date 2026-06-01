#pragma warning disable OPENAI001

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure;
using Azure.AI.Projects;
using Azure.Core;
using Azure.Identity;
using PromptHandlingService.Foundry;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

internal static class Program
{
    private const string ManagementScope = "https://management.azure.com/.default";
    private const string FoundryScope = "https://ai.azure.com/.default";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = DeployOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(options.TimeoutMinutes));
            var context = DeploymentContext.Load(options);
            context.Metadata.NormalizeForDevDeployment(context);

            if (options.DryRun)
            {
                PrintPlan(context);
                return 0;
            }

            var credential = CreateCredential(options);

            await VerifyCredentialAsync(credential, timeout.Token).ConfigureAwait(false);

            var management = new FoundryArmClient(credential, context.SubscriptionId);
            FoundryAgentVersionInfo? createdVersion = null;

            if (!options.SmokeOnly && !options.ReadbacksOnly)
            {
                await management.EnsureModelDeploymentAsync(context, timeout.Token).ConfigureAwait(false);

                var provisioner = new FoundryAgentProvisioner(context.RuntimeOptions, credential);
                createdVersion = await provisioner.EnsurePromptAgentVersionAsync(
                    PersonaPromptAssembler.OutputContract,
                    timeout.Token).ConfigureAwait(false);
                Console.WriteLine($"prompt-agent version: {createdVersion.AgentName} v{createdVersion.Version} ({createdVersion.Id})");

                await management.EnsureApplicationAsync(context, createdVersion, timeout.Token).ConfigureAwait(false);
                var deployment = await management.EnsureApplicationDeploymentAsync(
                    context,
                    createdVersion,
                    timeout.Token).ConfigureAwait(false);
                await management.UpdateApplicationRoutingAsync(context, createdVersion, deployment, timeout.Token).ConfigureAwait(false);

                context.MetadataEnvironment.AgentVersion = createdVersion.Version;
                context.MetadataEnvironment.ApplicationName = context.ApplicationName;
                context.MetadataEnvironment.DeploymentName = context.DeploymentName;
                context.MetadataEnvironment.ApplicationEndpoint = context.ApplicationEndpoint;
                context.MetadataEnvironment.EvaluationSuites = MetadataEnvironment.CreateDevSeedSuites(context.AgentName);
                context.MetadataEnvironment.TestCases = null;
                context.MetadataEnvironment.TestSuites = null;
                context.Metadata.Save(context.MetadataPath);
                Console.WriteLine($"metadata updated: {Path.GetRelativePath(context.RepoRoot, context.MetadataPath)}");
            }

            var activeAgentVersion = createdVersion?.Version ?? context.MetadataEnvironment.AgentVersion;
            if (string.IsNullOrWhiteSpace(activeAgentVersion))
            {
                throw new InvalidOperationException("No prompt-agent version is available. Deploy first or set agentVersion in metadata.");
            }

            var readbacks = await Readbacks.CollectAsync(context, credential, management, activeAgentVersion, timeout.Token)
                .ConfigureAwait(false);
            readbacks.Print();

            if (!options.NoSmoke)
            {
                await SmokeRunner.RunProjectScopedSmokeAsync(context, credential, activeAgentVersion, timeout.Token)
                    .ConfigureAwait(false);
                await SmokeRunner.RunApplicationSmokeAsync(context, credential, timeout.Token).ConfigureAwait(false);
            }

            return 0;
        }
        catch (Exception ex) when (IsStaleAzureToken(ex))
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine("Refresh Azure auth for Foundry data-plane access:");
            Console.Error.WriteLine($"az login --tenant {ProgramDefaults.TenantId} --scope https://ai.azure.com/.default");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task VerifyCredentialAsync(TokenCredential credential, CancellationToken cancellationToken)
    {
        await credential.GetTokenAsync(new TokenRequestContext([FoundryScope]), cancellationToken).ConfigureAwait(false);
        await credential.GetTokenAsync(new TokenRequestContext([ManagementScope]), cancellationToken).ConfigureAwait(false);
    }

    private static TokenCredential CreateCredential(DeployOptions options) =>
        new ChainedTokenCredential(
            new EnvironmentCredential(),
            new AzureCliCredential(new AzureCliCredentialOptions { TenantId = options.TenantId }));

    private static bool IsStaleAzureToken(Exception ex)
    {
        return ex.ToString().Contains("AADSTS50173", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintPlan(DeploymentContext context)
    {
        Console.WriteLine("Foundry persona deployment dry run");
        Console.WriteLine($"repo:              {context.RepoRoot}");
        Console.WriteLine($"config:            {Path.GetRelativePath(context.RepoRoot, context.ConfigPath)}");
        Console.WriteLine($"metadata:          {Path.GetRelativePath(context.RepoRoot, context.MetadataPath)}");
        Console.WriteLine($"environment:       {context.EnvironmentName}");
        Console.WriteLine($"subscription:      {context.SubscriptionId}");
        Console.WriteLine($"resource group:    {context.ResourceGroup}");
        Console.WriteLine($"account/project:   {context.AccountName}/{context.ProjectName}");
        Console.WriteLine($"project endpoint:  {context.ProjectEndpoint}");
        Console.WriteLine($"model deployment:  {context.Model}");
        Console.WriteLine($"prompt agent:      {context.AgentName}");
        Console.WriteLine($"application:       {context.ApplicationName}");
        Console.WriteLine($"deployment:        {context.DeploymentName}");
        Console.WriteLine($"application route: {context.ApplicationResponsesEndpoint}");
        Console.WriteLine($"seed dataset:      {Path.GetRelativePath(context.RepoRoot, context.SeedDatasetPath)}");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            WWoW Foundry persona prompt-agent deployment tool

            Usage:
              dotnet run --project tools/FoundryPersonaDeploy/FoundryPersonaDeploy.csproj -- [options]

            Options:
              --dry-run                 Read config/metadata and print the deployment plan only.
              --smoke-only              Do not deploy; run readbacks and both smoke checks.
              --readbacks-only          Do not deploy or smoke; print Azure readbacks.
              --no-smoke                Deploy and read back, but skip smoke checks.
              --skip-model              Do not create a missing model deployment.
              --config <path>           Defaults to Config/foundry/persona-runtime.json.
              --metadata <path>         Defaults to Services/PromptHandlingService/.foundry/agent-metadata.yaml.
              --environment <name>      Defaults to the metadata defaultEnvironment.
              --subscription <id>       Defaults to AZURE_SUBSCRIPTION_ID or the known dev subscription.
              --resource-group <name>   Defaults to AZURE_RESOURCE_GROUP or rg-jrhodes-0775.
              --account <name>          Defaults to the account parsed from projectEndpoint.
              --project <name>          Defaults to the project parsed from projectEndpoint.
              --tenant <id>             Defaults to the known WWoW dev tenant.
              --timeout-minutes <n>     Defaults to 20.

            Before live deployment, refresh Foundry auth when needed:
              az login --tenant c6369020-2dbb-4c30-addb-61056e49fe27 --scope https://ai.azure.com/.default
            """);
    }
}

internal sealed record DeployOptions
{
    public string ConfigPath { get; init; } = "Config/foundry/persona-runtime.json";
    public string MetadataPath { get; init; } = "Services/PromptHandlingService/.foundry/agent-metadata.yaml";
    public string EnvironmentName { get; init; } = string.Empty;
    public string SubscriptionId { get; init; } =
        Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID") ?? ProgramDefaults.SubscriptionId;
    public string ResourceGroup { get; init; } =
        Environment.GetEnvironmentVariable("AZURE_RESOURCE_GROUP") ?? ProgramDefaults.ResourceGroup;
    public string AccountName { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string TenantId { get; init; } = ProgramDefaults.TenantId;
    public int TimeoutMinutes { get; init; } = 20;
    public bool DryRun { get; init; }
    public bool SmokeOnly { get; init; }
    public bool ReadbacksOnly { get; init; }
    public bool NoSmoke { get; init; }
    public bool SkipModel { get; init; }
    public bool ShowHelp { get; init; }

    public static DeployOptions Parse(string[] args)
    {
        var options = new DeployOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    options = options with { ShowHelp = true };
                    break;
                case "--dry-run":
                    options = options with { DryRun = true };
                    break;
                case "--smoke-only":
                    options = options with { SmokeOnly = true };
                    break;
                case "--readbacks-only":
                    options = options with { ReadbacksOnly = true, NoSmoke = true };
                    break;
                case "--no-smoke":
                    options = options with { NoSmoke = true };
                    break;
                case "--skip-model":
                    options = options with { SkipModel = true };
                    break;
                case "--config":
                    options = options with { ConfigPath = RequireValue(args, ref i, arg) };
                    break;
                case "--metadata":
                    options = options with { MetadataPath = RequireValue(args, ref i, arg) };
                    break;
                case "--environment":
                    options = options with { EnvironmentName = RequireValue(args, ref i, arg) };
                    break;
                case "--subscription":
                    options = options with { SubscriptionId = RequireValue(args, ref i, arg) };
                    break;
                case "--resource-group":
                    options = options with { ResourceGroup = RequireValue(args, ref i, arg) };
                    break;
                case "--account":
                    options = options with { AccountName = RequireValue(args, ref i, arg) };
                    break;
                case "--project":
                    options = options with { ProjectName = RequireValue(args, ref i, arg) };
                    break;
                case "--tenant":
                    options = options with { TenantId = RequireValue(args, ref i, arg) };
                    break;
                case "--timeout-minutes":
                    options = options with { TimeoutMinutes = int.Parse(RequireValue(args, ref i, arg)) };
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{arg}'. Use --help for usage.");
            }
        }

        if (options.TimeoutMinutes <= 0)
        {
            throw new ArgumentException("--timeout-minutes must be greater than zero.");
        }

        return options;
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

internal static class ProgramDefaults
{
    public const string SubscriptionId = "4513b073-3a04-4f5c-b272-bbcc329b2d49";
    public const string ResourceGroup = "rg-jrhodes-0775";
    public const string TenantId = "c6369020-2dbb-4c30-addb-61056e49fe27";
    public const string ManagementScope = "https://management.azure.com/.default";
    public const string FoundryScope = "https://ai.azure.com/.default";
}

internal sealed record DeploymentContext
{
    public required string RepoRoot { get; init; }
    public required string ConfigPath { get; init; }
    public required string MetadataPath { get; init; }
    public required AgentMetadata Metadata { get; init; }
    public required MetadataEnvironment MetadataEnvironment { get; init; }
    public required string EnvironmentName { get; init; }
    public required string SubscriptionId { get; init; }
    public required string ResourceGroup { get; init; }
    public required string AccountName { get; init; }
    public required string ProjectName { get; init; }
    public required string ProjectEndpoint { get; init; }
    public required string Model { get; init; }
    public required string AgentName { get; init; }
    public required string ApplicationName { get; init; }
    public required string DeploymentName { get; init; }
    public required string ApplicationEndpoint { get; init; }
    public required FoundryPersonaRuntimeOptions RuntimeOptions { get; init; }
    public required bool SkipModel { get; init; }

    public string ApplicationResponsesEndpoint => $"{ApplicationEndpoint.TrimEnd('/')}/responses";
    public string SeedDatasetPath => Path.Combine(
        Path.GetDirectoryName(MetadataPath)!,
        "datasets",
        $"{AgentName}-eval-seed-v1.jsonl");

    public static DeploymentContext Load(DeployOptions options)
    {
        var repoRoot = ResolveRepoRoot();
        var configPath = ResolvePath(repoRoot, options.ConfigPath);
        var metadataPath = ResolvePath(repoRoot, options.MetadataPath);

        var config = PersonaRuntimeConfig.Load(configPath);
        var runtimeOptions = new FoundryPersonaRuntimeOptions
        {
            ProjectEndpoint = config.ProjectEndpoint,
            Model = config.Model,
            AgentName = config.AgentName,
            TimeoutMs = config.TimeoutMs,
            MaxOutputTokens = config.MaxOutputTokens
        };
        runtimeOptions.Validate();

        var metadata = AgentMetadata.Load(metadataPath);
        var environmentName = string.IsNullOrWhiteSpace(options.EnvironmentName)
            ? metadata.DefaultEnvironment
            : options.EnvironmentName;
        if (!metadata.Environments.TryGetValue(environmentName, out var environment))
        {
            throw new InvalidOperationException($"Metadata environment '{environmentName}' was not found.");
        }

        var endpointParts = FoundryEndpointParts.Parse(config.ProjectEndpoint);
        var accountName = FirstNonBlank(options.AccountName, endpointParts.AccountName);
        var projectName = FirstNonBlank(options.ProjectName, endpointParts.ProjectName);
        var applicationName = FirstNonBlank(environment.ApplicationName, $"{config.AgentName}-app");
        var deploymentName = FirstNonBlank(environment.DeploymentName, $"{config.AgentName}-deployment");
        var applicationEndpoint = FirstNonBlank(
            environment.ApplicationEndpoint,
            $"{config.ProjectEndpoint.TrimEnd('/')}/applications/{applicationName}/protocols/openai");

        return new DeploymentContext
        {
            RepoRoot = repoRoot,
            ConfigPath = configPath,
            MetadataPath = metadataPath,
            Metadata = metadata,
            MetadataEnvironment = environment,
            EnvironmentName = environmentName,
            SubscriptionId = options.SubscriptionId,
            ResourceGroup = options.ResourceGroup,
            AccountName = accountName,
            ProjectName = projectName,
            ProjectEndpoint = config.ProjectEndpoint,
            Model = config.Model,
            AgentName = config.AgentName,
            ApplicationName = applicationName,
            DeploymentName = deploymentName,
            ApplicationEndpoint = applicationEndpoint,
            RuntimeOptions = runtimeOptions,
            SkipModel = options.SkipModel
        };
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WestworldOfWarcraft.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate WestworldOfWarcraft.sln from the current directory.");
    }

    private static string ResolvePath(string repoRoot, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(repoRoot, path));
    }

    private static string FirstNonBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new InvalidOperationException("Expected at least one non-empty value.");
    }
}

internal sealed record PersonaRuntimeConfig
{
    public string ProjectEndpoint { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string AgentName { get; init; } = string.Empty;
    public int TimeoutMs { get; init; } = 30_000;
    public int MaxOutputTokens { get; init; } = 512;

    public static PersonaRuntimeConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Foundry persona runtime config was not found.", path);
        }

        var config = JsonSerializer.Deserialize<PersonaRuntimeConfig>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (config is null)
        {
            throw new InvalidDataException($"Could not parse {path}.");
        }

        return config;
    }
}

internal sealed record FoundryEndpointParts(string AccountName, string ProjectName)
{
    public static FoundryEndpointParts Parse(string projectEndpoint)
    {
        var uri = new Uri(projectEndpoint);
        var accountName = uri.Host.Split('.')[0];
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var projectIndex = Array.FindIndex(segments, segment => string.Equals(segment, "projects", StringComparison.OrdinalIgnoreCase));
        if (projectIndex < 0 || projectIndex + 1 >= segments.Length)
        {
            throw new InvalidOperationException($"Could not parse project name from endpoint '{projectEndpoint}'.");
        }

        return new FoundryEndpointParts(accountName, segments[projectIndex + 1]);
    }
}

internal sealed class AgentMetadata
{
    public string DefaultEnvironment { get; set; } = "dev";
    public Dictionary<string, MetadataEnvironment> Environments { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static AgentMetadata Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Foundry agent metadata was not found.", path);
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var metadata = deserializer.Deserialize<AgentMetadata>(File.ReadAllText(path));
        if (metadata.Environments.Count == 0)
        {
            throw new InvalidDataException($"{path} does not define any environments.");
        }

        return metadata;
    }

    public void NormalizeForDevDeployment(DeploymentContext context)
    {
        if (string.IsNullOrWhiteSpace(DefaultEnvironment))
        {
            DefaultEnvironment = context.EnvironmentName;
        }

        context.MetadataEnvironment.ProjectEndpoint = context.ProjectEndpoint;
        context.MetadataEnvironment.AgentName = context.AgentName;
        context.MetadataEnvironment.ApplicationName = context.ApplicationName;
        context.MetadataEnvironment.DeploymentName = context.DeploymentName;
        context.MetadataEnvironment.ApplicationEndpoint = context.ApplicationEndpoint;

        if (context.MetadataEnvironment.EvaluationSuites.Count == 0)
        {
            context.MetadataEnvironment.EvaluationSuites = context.MetadataEnvironment.GetLegacySuitesOrDefault(context.AgentName);
        }
    }

    public void Save(string path)
    {
        var builder = new StringBuilder();
        builder.Append("defaultEnvironment: ").AppendLine(YamlScalar(DefaultEnvironment));
        builder.AppendLine("environments:");

        foreach (var (name, environment) in Environments.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append("  ").Append(YamlKey(name)).AppendLine(":");
            AppendScalar(builder, 4, "projectEndpoint", environment.ProjectEndpoint);
            AppendScalar(builder, 4, "agentName", environment.AgentName);
            AppendScalar(builder, 4, "agentVersion", environment.AgentVersion);
            AppendScalar(builder, 4, "applicationName", environment.ApplicationName);
            AppendScalar(builder, 4, "deploymentName", environment.DeploymentName);
            AppendScalar(builder, 4, "applicationEndpoint", environment.ApplicationEndpoint);
            builder.AppendLine("    evaluationSuites:");

            foreach (var suite in environment.EvaluationSuites)
            {
                builder.AppendLine("      - id: " + YamlScalar(suite.Id));
                builder.AppendLine("        tags:");
                foreach (var tag in suite.Tags.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    AppendScalar(builder, 10, tag.Key, tag.Value);
                }

                AppendScalar(builder, 8, "dataset", suite.Dataset);
                AppendScalar(builder, 8, "datasetVersion", suite.DatasetVersion);
                AppendScalar(builder, 8, "datasetFile", suite.DatasetFile);
                AppendScalar(builder, 8, "datasetUri", suite.DatasetUri);
                builder.AppendLine("        evaluators:");
                foreach (var evaluator in suite.Evaluators)
                {
                    builder.AppendLine("          - name: " + YamlScalar(evaluator.Name));
                    builder.Append("            threshold: ").AppendLine(evaluator.Threshold.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void AppendScalar(StringBuilder builder, int spaces, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.Append(' ', spaces).Append(YamlKey(key)).Append(": ").AppendLine(YamlScalar(value));
    }

    private static string YamlKey(string value) => value.Contains(':', StringComparison.Ordinal) ? YamlScalar(value) : value;

    private static string YamlScalar(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        var escaped = value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}

internal sealed class MetadataEnvironment
{
    public string ProjectEndpoint { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string ApplicationEndpoint { get; set; } = string.Empty;
    public List<EvaluationSuite> EvaluationSuites { get; set; } = [];
    public List<EvaluationSuite>? TestCases { get; set; }
    public List<EvaluationSuite>? TestSuites { get; set; }

    public List<EvaluationSuite> GetLegacySuitesOrDefault(string agentName)
    {
        var legacySuites = TestSuites is { Count: > 0 } ? TestSuites : TestCases;
        if (legacySuites is { Count: > 0 })
        {
            foreach (var suite in legacySuites)
            {
                suite.Tags ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(suite.Priority) &&
                    !suite.Tags.ContainsKey("tier") &&
                    TryMapPriority(suite.Priority, out var tier))
                {
                    suite.Tags["tier"] = tier;
                }

                suite.Priority = null;
            }

            return legacySuites;
        }

        return CreateDevSeedSuites(agentName);
    }

    public static List<EvaluationSuite> CreateDevSeedSuites(string agentName) =>
    [
        new EvaluationSuite
        {
            Id = "persona-runtime-smoke",
            Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["tier"] = "smoke",
                ["purpose"] = "baseline",
                ["stage"] = "seed"
            },
            Dataset = $"{agentName}-eval-seed",
            DatasetVersion = "v1",
            DatasetFile = $".foundry/datasets/{agentName}-eval-seed-v1.jsonl",
            DatasetUri = "pending",
            Evaluators =
            [
                new EvaluationEvaluator { Name = "relevance", Threshold = 4 },
                new EvaluationEvaluator { Name = "task_adherence", Threshold = 4 },
                new EvaluationEvaluator { Name = "intent_resolution", Threshold = 4 },
                new EvaluationEvaluator { Name = "indirect_attack", Threshold = 1 }
            ]
        }
    ];

    private static bool TryMapPriority(string priority, out string tier)
    {
        tier = priority.ToUpperInvariant() switch
        {
            "P0" => "smoke",
            "P1" => "regression",
            "P2" => "coverage",
            _ => string.Empty
        };
        return tier.Length > 0;
    }
}

internal sealed class EvaluationSuite
{
    public string Id { get; set; } = string.Empty;
    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Dataset { get; set; } = string.Empty;
    public string DatasetVersion { get; set; } = string.Empty;
    public string DatasetFile { get; set; } = string.Empty;
    public string DatasetUri { get; set; } = string.Empty;
    public List<EvaluationEvaluator> Evaluators { get; set; } = [];
    public string? Priority { get; set; }
}

internal sealed class EvaluationEvaluator
{
    public string Name { get; set; } = string.Empty;
    public double Threshold { get; set; }
}

internal sealed class FoundryArmClient
{
    private const string ManagementEndpoint = "https://management.azure.com";
    private const string ModelDeploymentApiVersion = "2024-10-01";
    private const string AgentApplicationApiVersion = "2026-01-15-preview";

    private readonly TokenCredential _credential;
    private readonly string _subscriptionId;
    private readonly HttpClient _httpClient = new();

    public FoundryArmClient(TokenCredential credential, string subscriptionId)
    {
        _credential = credential;
        _subscriptionId = subscriptionId;
    }

    public async Task EnsureModelDeploymentAsync(DeploymentContext context, CancellationToken cancellationToken)
    {
        var uri = ModelDeploymentUri(context);
        var existing = await SendJsonAsync(HttpMethod.Get, uri, null, allowNotFound: true, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            var provisioningState = ReadString(existing, "properties", "provisioningState");
            var state = ReadString(existing, "properties", "state");
            Console.WriteLine($"model deployment: {context.Model} provisioning={provisioningState} state={state}");
            if (!string.Equals(provisioningState, "Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Model deployment '{context.Model}' exists but provisioningState is '{provisioningState}'.");
            }

            return;
        }

        if (context.SkipModel)
        {
            throw new InvalidOperationException($"Model deployment '{context.Model}' is missing and --skip-model was set.");
        }

        var body = new JsonObject
        {
            ["sku"] = new JsonObject
            {
                ["name"] = "GlobalStandard",
                ["capacity"] = 50
            },
            ["properties"] = new JsonObject
            {
                ["model"] = new JsonObject
                {
                    ["format"] = "OpenAI",
                    ["name"] = context.Model,
                    ["version"] = "2025-08-07"
                },
                ["versionUpgradeOption"] = "OnceNewDefaultVersionAvailable",
                ["raiPolicyName"] = "Microsoft.DefaultV2"
            }
        };

        var created = await PutAndPollAsync(uri, body, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"model deployment created: {context.Model} provisioning={ReadString(created, "properties", "provisioningState")}");
    }

    public async Task EnsureApplicationAsync(
        DeploymentContext context,
        FoundryAgentVersionInfo agentVersion,
        CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["properties"] = new JsonObject
            {
                ["displayName"] = "WWoW Persona Runtime Dev",
                ["description"] = "WWoW persona dialogue advisory prompt-agent application.",
                ["isEnabled"] = true,
                ["agents"] = AgentReferences(agentVersion),
                ["tags"] = StandardTags(context)
            }
        };

        var result = await PutAndPollAsync(ApplicationUri(context), body, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"application: {context.ApplicationName} provisioning={ReadString(result, "properties", "provisioningState")}");
    }

    public async Task<ApplicationDeploymentReadback> EnsureApplicationDeploymentAsync(
        DeploymentContext context,
        FoundryAgentVersionInfo agentVersion,
        CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["properties"] = new JsonObject
            {
                ["displayName"] = "WWoW Persona Runtime Dev Deployment",
                ["description"] = "Managed deployment for the WWoW persona dialogue advisory prompt agent.",
                ["deploymentType"] = "Managed",
                ["state"] = "Running",
                ["tags"] = StandardTags(context),
                ["agents"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["agentId"] = agentVersion.Id,
                        ["agentName"] = agentVersion.AgentName,
                        ["agentVersion"] = agentVersion.Version
                    }
                },
                ["protocols"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["protocol"] = "Responses",
                        ["version"] = "1.0"
                    }
                }
            }
        };

        var result = await PutAndPollAsync(ApplicationDeploymentUri(context), body, cancellationToken).ConfigureAwait(false);
        var readback = ApplicationDeploymentReadback.FromJson(result);
        Console.WriteLine($"application deployment: {context.DeploymentName} state={readback.State} provisioning={readback.ProvisioningState}");
        return readback;
    }

    public async Task UpdateApplicationRoutingAsync(
        DeploymentContext context,
        FoundryAgentVersionInfo agentVersion,
        ApplicationDeploymentReadback deployment,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deployment.Id))
        {
            throw new InvalidOperationException("Application deployment readback did not include an ARM id.");
        }

        if (string.IsNullOrWhiteSpace(deployment.DeploymentId))
        {
            throw new InvalidOperationException("Application deployment readback did not include a deploymentId.");
        }

        var body = new JsonObject
        {
            ["properties"] = new JsonObject
            {
                ["displayName"] = "WWoW Persona Runtime Dev",
                ["description"] = "WWoW persona dialogue advisory prompt-agent application.",
                ["isEnabled"] = true,
                ["agents"] = AgentReferences(agentVersion),
                ["tags"] = StandardTags(context),
                ["trafficRoutingPolicy"] = new JsonObject
                {
                    ["protocol"] = "FixedRatio",
                    ["rules"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["ruleId"] = "persona-runtime-dev",
                            ["deploymentId"] = deployment.DeploymentId,
                            ["trafficPercentage"] = 100
                        }
                    }
                }
            }
        };

        var result = await PutAndPollAsync(ApplicationUri(context), body, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"application route updated: {context.ApplicationResponsesEndpoint} provisioning={ReadString(result, "properties", "provisioningState")}");
    }

    public async Task<JsonObject?> GetModelDeploymentAsync(DeploymentContext context, CancellationToken cancellationToken) =>
        await SendJsonAsync(HttpMethod.Get, ModelDeploymentUri(context), null, allowNotFound: true, cancellationToken).ConfigureAwait(false);

    public async Task<JsonObject?> GetApplicationAsync(DeploymentContext context, CancellationToken cancellationToken) =>
        await SendJsonAsync(HttpMethod.Get, ApplicationUri(context), null, allowNotFound: true, cancellationToken).ConfigureAwait(false);

    public async Task<JsonObject?> GetApplicationDeploymentAsync(DeploymentContext context, CancellationToken cancellationToken) =>
        await SendJsonAsync(HttpMethod.Get, ApplicationDeploymentUri(context), null, allowNotFound: true, cancellationToken).ConfigureAwait(false);

    private async Task<JsonObject> PutAndPollAsync(Uri uri, JsonObject body, CancellationToken cancellationToken)
    {
        var response = await SendRawAsync(HttpMethod.Put, uri, body, cancellationToken).ConfigureAwait(false);
        var initial = await ReadBodyAsync(response).ConfigureAwait(false);
        EnsureSuccess(response, initial);

        if (TryGetHeader(response, "Azure-AsyncOperation", out var operationUri) ||
            TryGetHeader(response, "Location", out operationUri))
        {
            await PollOperationAsync(new Uri(operationUri), cancellationToken).ConfigureAwait(false);
            var final = await SendJsonAsync(HttpMethod.Get, uri, null, allowNotFound: false, cancellationToken).ConfigureAwait(false);
            return final ?? throw new InvalidOperationException($"Resource was not returned after polling {uri}.");
        }

        return ParseObject(initial);
    }

    private async Task PollOperationAsync(Uri operationUri, CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            var result = await SendJsonAsync(HttpMethod.Get, operationUri, null, allowNotFound: false, cancellationToken)
                .ConfigureAwait(false);
            var status = ReadString(result!, "status") ?? ReadString(result!, "properties", "provisioningState");
            if (string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"ARM operation {operationUri} finished with status {status}: {result}");
            }
        }
    }

    private async Task<JsonObject?> SendJsonAsync(
        HttpMethod method,
        Uri uri,
        JsonObject? body,
        bool allowNotFound,
        CancellationToken cancellationToken)
    {
        using var response = await SendRawAsync(method, uri, body, cancellationToken).ConfigureAwait(false);
        var content = await ReadBodyAsync(response).ConfigureAwait(false);
        if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureSuccess(response, content);
        return ParseObject(content);
    }

    private async Task<HttpResponseMessage> SendRawAsync(
        HttpMethod method,
        Uri uri,
        JsonObject? body,
        CancellationToken cancellationToken)
    {
        var token = await _credential.GetTokenAsync(new TokenRequestContext([ProgramDefaults.ManagementScope]), cancellationToken)
            .ConfigureAwait(false);
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private Uri ModelDeploymentUri(DeploymentContext context) =>
        new($"{ManagementEndpoint}/subscriptions/{_subscriptionId}/resourceGroups/{Escape(context.ResourceGroup)}/providers/Microsoft.CognitiveServices/accounts/{Escape(context.AccountName)}/deployments/{Escape(context.Model)}?api-version={ModelDeploymentApiVersion}");

    private Uri ApplicationUri(DeploymentContext context) =>
        new($"{ManagementEndpoint}/subscriptions/{_subscriptionId}/resourceGroups/{Escape(context.ResourceGroup)}/providers/Microsoft.CognitiveServices/accounts/{Escape(context.AccountName)}/projects/{Escape(context.ProjectName)}/applications/{Escape(context.ApplicationName)}?api-version={AgentApplicationApiVersion}");

    private Uri ApplicationDeploymentUri(DeploymentContext context) =>
        new($"{ManagementEndpoint}/subscriptions/{_subscriptionId}/resourceGroups/{Escape(context.ResourceGroup)}/providers/Microsoft.CognitiveServices/accounts/{Escape(context.AccountName)}/projects/{Escape(context.ProjectName)}/applications/{Escape(context.ApplicationName)}/agentDeployments/{Escape(context.DeploymentName)}?api-version={AgentApplicationApiVersion}");

    private static JsonObject StandardTags(DeploymentContext context) => new()
    {
        ["wwow-purpose"] = "persona-dialogue-advisory",
        ["wwow-environment"] = context.EnvironmentName
    };

    private static JsonArray AgentReferences(FoundryAgentVersionInfo agentVersion) =>
    [
        new JsonObject
        {
            ["agentId"] = agentVersion.Id,
            ["agentName"] = agentVersion.AgentName
        }
    ];

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static bool TryGetHeader(HttpResponseMessage response, string name, out string value)
    {
        if (response.Headers.TryGetValues(name, out var values))
        {
            value = values.First();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response) =>
        response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync().ConfigureAwait(false);

    private static JsonObject ParseObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonNode.Parse(json)?.AsObject() ?? [];
    }

    private static void EnsureSuccess(HttpResponseMessage response, string content)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase}: {content}");
        }
    }

    public static string? ReadString(JsonObject json, params string[] path)
    {
        JsonNode? node = json;
        foreach (var segment in path)
        {
            node = node?[segment];
        }

        return node?.GetValue<string>();
    }
}

internal sealed record ApplicationDeploymentReadback(
    string Id,
    string Name,
    string DeploymentId,
    string ProvisioningState,
    string State)
{
    public static ApplicationDeploymentReadback FromJson(JsonObject json) => new(
        FoundryArmClient.ReadString(json, "id") ?? string.Empty,
        FoundryArmClient.ReadString(json, "name") ?? string.Empty,
        FoundryArmClient.ReadString(json, "properties", "deploymentId") ?? string.Empty,
        FoundryArmClient.ReadString(json, "properties", "provisioningState") ?? string.Empty,
        FoundryArmClient.ReadString(json, "properties", "state") ?? string.Empty);
}

internal sealed record Readbacks(
    string ModelProvisioningState,
    string ModelState,
    string AgentVersion,
    string AgentVersionId,
    string ApplicationProvisioningState,
    string DeploymentProvisioningState,
    string DeploymentState)
{
    public static async Task<Readbacks> CollectAsync(
        DeploymentContext context,
        TokenCredential credential,
        FoundryArmClient arm,
        string agentVersion,
        CancellationToken cancellationToken)
    {
        var model = await arm.GetModelDeploymentAsync(context, cancellationToken).ConfigureAwait(false);
        var application = await arm.GetApplicationAsync(context, cancellationToken).ConfigureAwait(false);
        var deployment = await arm.GetApplicationDeploymentAsync(context, cancellationToken).ConfigureAwait(false);

        var projectClient = new AIProjectClient(new Uri(context.ProjectEndpoint), credential);
        var version = await projectClient.AgentAdministrationClient
            .GetAgentVersionAsync(context.AgentName, agentVersion, cancellationToken)
            .ConfigureAwait(false);

        return new Readbacks(
            ModelProvisioningState: model is null ? "missing" : FoundryArmClient.ReadString(model, "properties", "provisioningState") ?? "",
            ModelState: model is null ? "missing" : FoundryArmClient.ReadString(model, "properties", "state") ?? "",
            AgentVersion: version.Value.Version,
            AgentVersionId: version.Value.Id,
            ApplicationProvisioningState: application is null ? "missing" : FoundryArmClient.ReadString(application, "properties", "provisioningState") ?? "",
            DeploymentProvisioningState: deployment is null ? "missing" : FoundryArmClient.ReadString(deployment, "properties", "provisioningState") ?? "",
            DeploymentState: deployment is null ? "missing" : FoundryArmClient.ReadString(deployment, "properties", "state") ?? "");
    }

    public void Print()
    {
        Console.WriteLine("readbacks:");
        Console.WriteLine($"  model deployment: provisioning={ModelProvisioningState} state={ModelState}");
        Console.WriteLine($"  prompt agent:      version={AgentVersion} id={AgentVersionId}");
        Console.WriteLine($"  application:       provisioning={ApplicationProvisioningState}");
        Console.WriteLine($"  app deployment:    provisioning={DeploymentProvisioningState} state={DeploymentState}");
    }
}

internal static class SmokeRunner
{
    public static async Task RunProjectScopedSmokeAsync(
        DeploymentContext context,
        TokenCredential credential,
        string agentVersion,
        CancellationToken cancellationToken)
    {
        var runtimeOptions = new FoundryPersonaRuntimeOptions
        {
            ProjectEndpoint = context.ProjectEndpoint,
            Model = context.Model,
            AgentName = context.AgentName,
            AgentVersion = agentVersion,
            TimeoutMs = context.RuntimeOptions.TimeoutMs,
            MaxOutputTokens = context.RuntimeOptions.MaxOutputTokens
        };
        var client = new FoundryProjectResponsesClient(runtimeOptions, credential);
        var runtime = new FoundryPersonaRuntime(runtimeOptions, new PersonaPromptAssembler(), client);

        var result = await runtime.GenerateAsync(SmokeRequest(), cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"project-scoped smoke: status=completed agent={result.FoundryAgentName} version={result.FoundryAgentVersion} model={result.Model}");
    }

    public static async Task RunApplicationSmokeAsync(
        DeploymentContext context,
        TokenCredential credential,
        CancellationToken cancellationToken)
    {
        var token = await credential.GetTokenAsync(new TokenRequestContext([ProgramDefaults.FoundryScope]), cancellationToken)
            .ConfigureAwait(false);
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{context.ApplicationResponsesEndpoint}?api-version=2025-11-15-preview");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var prompt = new PersonaPromptAssembler().Assemble(SmokeRequest());
        var body = new JsonObject
        {
            ["input"] = prompt,
            ["max_output_tokens"] = context.RuntimeOptions.MaxOutputTokens
        };
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase}: {content}");
        }

        var json = JsonNode.Parse(content)?.AsObject() ?? [];
        var status = json["status"]?.GetValue<string>() ?? "unknown";
        if (!string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Application smoke returned status '{status}': {content}");
        }

        Console.WriteLine($"application smoke:   status={status} endpoint={context.ApplicationResponsesEndpoint}");
    }

    private static PersonaPromptRequest SmokeRequest() => new(
        PersonaId: "durotar-razor-hill-guide",
        PersonaVersion: "v1",
        CharacterName: "Grask Trailwatch",
        Realm: "Westworld-Test",
        ActiveNarrativeNode: "arrival-greeting",
        CompactMemorySummary: "The visitor asked for local Razor Hill guidance.",
        CurrentMoodState: "steady and welcoming",
        InputText: "Can you point me to the inn?",
        PersonaDescription: "A grounded Durotar host who gives concise local directions.",
        PersonaPromptSummary: "Offer dialogue only. Do not choose world state, combat, travel, mail, trade, or group changes.");
}
