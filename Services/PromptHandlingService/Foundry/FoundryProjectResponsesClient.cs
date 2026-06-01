#pragma warning disable OPENAI001

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
            MaxOutputTokenCount = request.MaxOutputTokens,
            StoredOutputEnabled = true
        };

        if (string.IsNullOrWhiteSpace(request.AgentVersion))
        {
            responseOptions.Instructions = PersonaPromptAssembler.OutputContract;
            responseOptions.ReasoningOptions = new ResponseReasoningOptions
            {
                ReasoningEffortLevel = ResponseReasoningEffortLevel.Minimal
            };
        }

        var responsesClient = GetResponsesClient(request);
        var result = await responsesClient.CreateResponseAsync(responseOptions, cancellationToken).ConfigureAwait(false);
        var outputText = ExtractOutputText(result.Value, result.GetRawResponse().Content);
        return new FoundryResponseEnvelope(outputText, result.Value.Model, result.Value.Id);
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

    private static string ExtractOutputText(ResponseResult response, BinaryData rawResponseContent)
    {
        var outputText = response.GetOutputText();
        if (!string.IsNullOrWhiteSpace(outputText))
        {
            return outputText;
        }

        var builder = new StringBuilder();
        foreach (var outputItem in response.OutputItems)
        {
            AppendOutputFragments(builder, outputItem, depth: 0);
        }

        outputText = builder.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(outputText))
        {
            return outputText;
        }

        return ExtractOutputTextFromJson(rawResponseContent.ToString());
    }

    private static void AppendOutputFragments(StringBuilder builder, object? value, int depth)
    {
        if (value is null || depth > 5)
        {
            return;
        }

        if (value is string text)
        {
            AppendFragment(builder, text);
            return;
        }

        if (value is BinaryData data)
        {
            AppendFragment(builder, NormalizeBinaryData(data));
            return;
        }

        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                AppendOutputFragments(builder, item, depth + 1);
            }

            return;
        }

        foreach (var propertyName in new[] { "Output", "OutputText", "Text", "Content" })
        {
            var property = value.GetType().GetProperty(propertyName);
            if (property is null || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            AppendOutputFragments(builder, property.GetValue(value), depth + 1);
        }
    }

    private static string ExtractOutputTextFromJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return string.Empty;
        }

        try
        {
            var root = JsonNode.Parse(rawJson);
            if (root is null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            AppendOutputJsonFragments(builder, root);
            return builder.ToString().Trim();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static void AppendOutputJsonFragments(StringBuilder builder, JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (TryAppendJsonString(builder, obj, "output_text") ||
                TryAppendJsonString(builder, obj, "text"))
            {
                return;
            }

            if (obj["output"] is JsonObject structuredOutput)
            {
                AppendFragment(builder, structuredOutput.ToJsonString());
                return;
            }

            if (obj["content"] is JsonArray content)
            {
                AppendOutputJsonFragments(builder, content);
                return;
            }

            if (obj["output"] is JsonArray output)
            {
                AppendOutputJsonFragments(builder, output);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null)
                {
                    AppendOutputJsonFragments(builder, item);
                }
            }
        }
    }

    private static bool TryAppendJsonString(StringBuilder builder, JsonObject obj, string propertyName)
    {
        if (obj[propertyName] is not JsonValue value ||
            !value.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        AppendFragment(builder, text);
        return true;
    }

    private static string NormalizeBinaryData(BinaryData data)
    {
        var text = data.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        try
        {
            var node = JsonNode.Parse(text);
            return node switch
            {
                JsonValue value when value.TryGetValue<string>(out var stringValue) => stringValue,
                not null => node.ToJsonString(),
                _ => text
            };
        }
        catch (JsonException)
        {
            return text;
        }
    }

    private static void AppendFragment(StringBuilder builder, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(text.Trim());
    }
}
