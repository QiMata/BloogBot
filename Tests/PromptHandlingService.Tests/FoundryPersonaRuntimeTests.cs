using System.Text.Json;
using System.Text.RegularExpressions;
using PromptHandlingService.Foundry;

namespace PromptHandlingService.Tests;

public sealed class FoundryPersonaRuntimeOptionsTests
{
    [Fact]
    public void Validate_AcceptsDiscoveredProjectSettings()
    {
        var options = ValidOptions();

        options.Validate();

        Assert.Equal("gpt-5-mini", options.Model);
        Assert.Equal("wwow-persona-runtime-dev", options.AgentName);
    }

    [Fact]
    public void Validate_RejectsMissingEndpoint()
    {
        var options = ValidOptions().WithEndpoint("");

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("projectEndpoint", ex.Message);
    }

    [Fact]
    public void Validate_RejectsNonHttpsEndpoint()
    {
        var options = ValidOptions().WithEndpoint("http://example.test/api/projects/dev");

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("HTTPS", ex.Message);
    }

    [Fact]
    public void Validate_RejectsMissingModel()
    {
        var options = new FoundryPersonaRuntimeOptions
        {
            ProjectEndpoint = ValidOptions().ProjectEndpoint,
            Model = " ",
            AgentName = ValidOptions().AgentName
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("model", ex.Message);
    }

    [Fact]
    public void ConfigFile_DoesNotContainSecrets()
    {
        var configPath = FindRepoFile("Config/foundry/persona-runtime.json");
        var metadataPath = FindRepoFile("Services/PromptHandlingService/.foundry/agent-metadata.yaml");
        var combined = File.ReadAllText(configPath) + "\n" + File.ReadAllText(metadataPath);

        Assert.DoesNotMatch(new Regex("(api[_-]?key|secret|password|bearer|access[_-]?token)", RegexOptions.IgnoreCase), combined);
    }

    [Fact]
    public void OptionsSurface_DoesNotExposeApiKeyProperty()
    {
        var propertyNames = typeof(FoundryPersonaRuntimeOptions)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(propertyNames, name => name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("BearerToken", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("AccessToken", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("RefreshToken", StringComparison.OrdinalIgnoreCase));
    }

    private static FoundryPersonaRuntimeOptions ValidOptions() => new()
    {
        ProjectEndpoint = "https://atlsqlsattest-resource.services.ai.azure.com/api/projects/atlsqlsattest",
        Model = "gpt-5-mini",
        AgentName = "wwow-persona-runtime-dev",
        TimeoutMs = 10_000,
        MaxOutputTokens = 512
    };

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }
}

public sealed class FoundryPersonaPromptAssemblerTests
{
    [Fact]
    public void Assemble_OrdersSectionsDeterministically()
    {
        var assembler = new PersonaPromptAssembler();

        var prompt = assembler.Assemble(SampleRequest());

        Assert.True(prompt.IndexOf("task:", StringComparison.Ordinal) < prompt.IndexOf("persona:", StringComparison.Ordinal));
        Assert.True(prompt.IndexOf("persona:", StringComparison.Ordinal) < prompt.IndexOf("character:", StringComparison.Ordinal));
        Assert.True(prompt.IndexOf("character:", StringComparison.Ordinal) < prompt.IndexOf("narrative:", StringComparison.Ordinal));
        Assert.True(prompt.IndexOf("narrative:", StringComparison.Ordinal) < prompt.IndexOf("memory:", StringComparison.Ordinal));
        Assert.True(prompt.IndexOf("memory:", StringComparison.Ordinal) < prompt.IndexOf("state:", StringComparison.Ordinal));
        Assert.True(prompt.IndexOf("state:", StringComparison.Ordinal) < prompt.IndexOf("input:", StringComparison.Ordinal));
        Assert.True(prompt.IndexOf("input:", StringComparison.Ordinal) < prompt.IndexOf("output:", StringComparison.Ordinal));
    }

    [Fact]
    public void Assemble_RejectsMissingPersona()
    {
        var assembler = new PersonaPromptAssembler();
        var request = SampleRequest() with { PersonaId = "" };

        var ex = Assert.Throws<ArgumentException>(() => assembler.Assemble(request));
        Assert.Contains(nameof(PersonaPromptRequest.PersonaId), ex.Message);
    }

    [Fact]
    public void Assemble_RejectsMissingNarrativeNode()
    {
        var assembler = new PersonaPromptAssembler();
        var request = SampleRequest() with { ActiveNarrativeNode = " " };

        var ex = Assert.Throws<ArgumentException>(() => assembler.Assemble(request));
        Assert.Contains(nameof(PersonaPromptRequest.ActiveNarrativeNode), ex.Message);
    }

    [Fact]
    public void Assemble_IncludesAdvisoryBoundary()
    {
        var assembler = new PersonaPromptAssembler();

        var prompt = assembler.Assemble(SampleRequest());

        Assert.Contains("advisory text only", prompt);
        Assert.Contains("do not choose game-state transitions", prompt);
        Assert.Contains("Return only minified JSON", prompt);
    }

    internal static PersonaPromptRequest SampleRequest() => new(
        PersonaId: "orc-grunt-friendly",
        PersonaVersion: "v1",
        CharacterName: "Gorvak",
        Realm: "Westworld-Test",
        ActiveNarrativeNode: "durotar-after-boar-hunt",
        CompactMemorySummary: "Met the player at Razor Hill.",
        CurrentMoodState: "tired but helpful",
        InputText: "Need help finding the inn.");
}

public sealed class FoundryPersonaRuntimeTests
{
    [Fact]
    public async Task GenerateAsync_ParsesNormalJsonResponse()
    {
        FoundryResponseRequest? captured = null;
        var client = new FakeFoundryResponsesClient((request, _) =>
        {
            captured = request;
            return Task.FromResult(new FoundryResponseEnvelope(
                "{\"replyText\":\"Try the inn by the watch tower.\",\"intent\":\"answer\",\"memoryCandidates\":[\"player asked for inn\"],\"rationale\":\"Uses current node.\"}",
                "gpt-5-mini",
                "resp_1"));
        });
        var runtime = new FoundryPersonaRuntime(ValidOptions(), new PersonaPromptAssembler(), client);

        var result = await runtime.GenerateAsync(FoundryPersonaPromptAssemblerTests.SampleRequest(), CancellationToken.None);

        Assert.Equal("Try the inn by the watch tower.", result.ReplyText);
        Assert.Equal("answer", result.Intent);
        Assert.Equal("player asked for inn", Assert.Single(result.MemoryCandidates));
        Assert.Equal("wwow-persona-runtime-dev", result.FoundryAgentName);
        Assert.Equal("gpt-5-mini", result.Model);
        Assert.NotNull(captured);
        Assert.Equal("gpt-5-mini", captured!.Model);
        Assert.Equal("wwow-persona-runtime-dev", captured.AgentName);
        Assert.Null(captured.AgentVersion);
        Assert.Equal(512, captured.MaxOutputTokens);
        Assert.Contains("durotar-after-boar-hunt", captured.InputText);
    }

    [Fact]
    public async Task GenerateAsync_UsesRuntimeBinding_WhenProvided()
    {
        FoundryResponseRequest? captured = null;
        var client = new FakeFoundryResponsesClient((request, _) =>
        {
            captured = request;
            return Task.FromResult(new FoundryResponseEnvelope(
                "{\"replyText\":\"Binding-specific reply.\",\"intent\":\"answer\",\"memoryCandidates\":[],\"rationale\":\"Uses binding.\"}",
                "gpt-persona",
                "resp_binding"));
        });
        var runtime = new FoundryPersonaRuntime(ValidOptions(), new PersonaPromptAssembler(), client);
        var binding = new PersonaPromptRuntimeBinding("gpt-persona", "persona-agent", "agent-v7", 321);

        var result = await runtime.GenerateAsync(FoundryPersonaPromptAssemblerTests.SampleRequest(), binding, CancellationToken.None);

        Assert.Equal("Binding-specific reply.", result.ReplyText);
        Assert.Equal("persona-agent", result.FoundryAgentName);
        Assert.Equal("agent-v7", result.FoundryAgentVersion);
        Assert.Equal("gpt-persona", result.Model);
        Assert.NotNull(captured);
        Assert.Equal("gpt-persona", captured!.Model);
        Assert.Equal("persona-agent", captured.AgentName);
        Assert.Equal("agent-v7", captured.AgentVersion);
        Assert.Equal(321, captured.MaxOutputTokens);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsTimeout_WhenClientExceedsBudget()
    {
        var options = ValidOptions().WithTimeout(10);
        var client = new FakeFoundryResponsesClient(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return new FoundryResponseEnvelope("{}", "gpt-5-mini", null);
        });
        var runtime = new FoundryPersonaRuntime(options, new PersonaPromptAssembler(), client);

        await Assert.ThrowsAsync<TimeoutException>(
            () => runtime.GenerateAsync(FoundryPersonaPromptAssemblerTests.SampleRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_PropagatesAuthFailure()
    {
        var client = new FakeFoundryResponsesClient((_, _) => throw new UnauthorizedAccessException("DefaultAzureCredential failed."));
        var runtime = new FoundryPersonaRuntime(ValidOptions(), new PersonaPromptAssembler(), client);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => runtime.GenerateAsync(FoundryPersonaPromptAssemblerTests.SampleRequest(), CancellationToken.None));
        Assert.Contains("DefaultAzureCredential", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_RejectsInvalidJson()
    {
        var client = new FakeFoundryResponsesClient((_, _) =>
            Task.FromResult(new FoundryResponseEnvelope("not json", "gpt-5-mini", "resp_2")));
        var runtime = new FoundryPersonaRuntime(ValidOptions(), new PersonaPromptAssembler(), client);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => runtime.GenerateAsync(FoundryPersonaPromptAssemblerTests.SampleRequest(), CancellationToken.None));
        Assert.Contains("invalid JSON", ex.Message);
    }

    private static FoundryPersonaRuntimeOptions ValidOptions() => new()
    {
        ProjectEndpoint = "https://atlsqlsattest-resource.services.ai.azure.com/api/projects/atlsqlsattest",
        Model = "gpt-5-mini",
        AgentName = "wwow-persona-runtime-dev",
        TimeoutMs = 10_000,
        MaxOutputTokens = 512
    };

    private sealed class FakeFoundryResponsesClient(
        Func<FoundryResponseRequest, CancellationToken, Task<FoundryResponseEnvelope>> handler) : IFoundryResponsesClient
    {
        public Task<FoundryResponseEnvelope> CreateResponseAsync(
            FoundryResponseRequest request,
            CancellationToken cancellationToken) => handler(request, cancellationToken);
    }
}

public sealed class FoundryPersonaLiveSmokeTests
{
    [Fact(Skip = "Live Foundry smoke. Remove skip and set WWOW_FOUNDRY_LIVE=1 to run.")]
    public async Task DirectModelResponseSmoke_UsesConfiguredFoundryProject()
    {
        Assert.Equal("1", Environment.GetEnvironmentVariable("WWOW_FOUNDRY_LIVE"));
        var runtime = new FoundryPersonaRuntime(LiveOptions());

        var result = await runtime.GenerateAsync(FoundryPersonaPromptAssemblerTests.SampleRequest(), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.ReplyText));
    }

    [Fact(Skip = "Mutating Foundry smoke. Remove skip and set WWOW_FOUNDRY_MUTATING=1 to run.")]
    public async Task PromptAgentVersionCreationSmoke_UsesConfiguredFoundryProject()
    {
        Assert.Equal("1", Environment.GetEnvironmentVariable("WWOW_FOUNDRY_MUTATING"));
        var provisioner = new FoundryAgentProvisioner(LiveOptions());

        var version = await provisioner.EnsurePromptAgentVersionAsync(PersonaPromptAssembler.OutputContract, CancellationToken.None);

        Assert.Equal("wwow-persona-runtime-dev", version.AgentName);
        Assert.False(string.IsNullOrWhiteSpace(version.Version));
    }

    private static FoundryPersonaRuntimeOptions LiveOptions() => new()
    {
        ProjectEndpoint = "https://atlsqlsattest-resource.services.ai.azure.com/api/projects/atlsqlsattest",
        Model = "gpt-5-mini",
        AgentName = "wwow-persona-runtime-dev",
        TimeoutMs = 30_000,
        MaxOutputTokens = 512
    };
}

file static class FoundryPersonaRuntimeOptionsTestExtensions
{
    public static FoundryPersonaRuntimeOptions WithEndpoint(this FoundryPersonaRuntimeOptions options, string endpoint) => new()
    {
        ProjectEndpoint = endpoint,
        Model = options.Model,
        AgentName = options.AgentName,
        AgentVersion = options.AgentVersion,
        TimeoutMs = options.TimeoutMs,
        MaxOutputTokens = options.MaxOutputTokens
    };

    public static FoundryPersonaRuntimeOptions WithTimeout(this FoundryPersonaRuntimeOptions options, int timeoutMs) => new()
    {
        ProjectEndpoint = options.ProjectEndpoint,
        Model = options.Model,
        AgentName = options.AgentName,
        AgentVersion = options.AgentVersion,
        TimeoutMs = timeoutMs,
        MaxOutputTokens = options.MaxOutputTokens
    };
}
