namespace WWoW.RecordedTests.Shared;

using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public sealed class TrueNasAppsClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly bool _ownsClient;

    public TrueNasAppsClient(string baseAddress, string apiKey, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            throw new ArgumentException("Base address is required.", nameof(baseAddress));
        }

        if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var baseUri))
        {
            throw new ArgumentException("Base address must be an absolute URI.", nameof(baseAddress));
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key is required.", nameof(apiKey));
        }

        _apiKey = apiKey;

        if (httpClient is null)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = baseUri,
                Timeout = TimeSpan.FromSeconds(30)
            };
            _ownsClient = true;
        }
        else
        {
            _httpClient = httpClient;
            if (_httpClient.BaseAddress is null)
            {
                _httpClient.BaseAddress = baseUri;
            }
        }

        if (_httpClient.DefaultRequestHeaders.Accept.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }

    public async Task<TrueNasAppRelease?> GetReleaseAsync(string releaseName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(releaseName))
        {
            throw new ArgumentException("Release name is required.", nameof(releaseName));
        }

        using var response = await SendAsync(HttpMethod.Get, $"api/v2.0/chart/release/{Uri.EscapeDataString(releaseName)}", cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;

        var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? releaseName : releaseName;

        var state = TryGetString(root,
            "status.state",
            "app_state.state",
            "state");
        var isRunning = IsActiveState(state);

        var isCheckedOut = TryGetBoolean(root,
            "checkedout",
            "checked_out",
            "checkedOut",
            "config.checkedout",
            "config.checked_out",
            "config.checkedOut",
            "user_values.checkedout",
            "user_values.checked_out",
            "values.checkedout",
            "values.checked_out",
            "metadata.checkedout") ?? false;

        var host = TryGetString(root,
            "server_host",
            "game_host",
            "config.server_host",
            "user_values.server_host",
            "values.server_host",
            "metadata.server_host",
            "chart_metadata.server_host");

        var port = TryGetInt32(root,
            "server_port",
            "game_port",
            "config.server_port",
            "user_values.server_port",
            "values.server_port",
            "metadata.server_port",
            "chart_metadata.server_port");

        var realm = TryGetString(root,
            "server_realm",
            "config.server_realm",
            "user_values.server_realm",
            "values.server_realm",
            "metadata.server_realm",
            "chart_metadata.server_realm",
            "realm");

        return new TrueNasAppRelease(name, isRunning, isCheckedOut, host, port, realm);
    }

    public async Task StartReleaseAsync(string releaseName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(releaseName))
        {
            throw new ArgumentException("Release name is required.", nameof(releaseName));
        }

        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await SendAsync(HttpMethod.Post, $"api/v2.0/chart/release/{Uri.EscapeDataString(releaseName)}/start", cancellationToken, content).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativePath, CancellationToken cancellationToken, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, relativePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        if (content != null)
        {
            request.Content = content;
        }

        return _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static string? TryGetString(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryResolveProperty(root, propertyName, out var value))
            {
                return value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number => value.ToString(),
                    JsonValueKind.True => bool.TrueString,
                    JsonValueKind.False => bool.FalseString,
                    _ => null
                };
            }
        }

        return null;
    }

    private static int? TryGetInt32(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryResolveProperty(root, propertyName, out var value))
            {
                switch (value.ValueKind)
                {
                    case JsonValueKind.Number:
                        if (value.TryGetInt32(out var numeric))
                        {
                            return numeric;
                        }

                        if (value.TryGetInt64(out var large))
                        {
                            return Convert.ToInt32(large);
                        }

                        break;
                    case JsonValueKind.String:
                        if (int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                        {
                            return parsed;
                        }

                        break;
                }
            }
        }

        return null;
    }

    private static bool? TryGetBoolean(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryResolveProperty(root, propertyName, out var value))
            {
                switch (value.ValueKind)
                {
                    case JsonValueKind.True:
                        return true;
                    case JsonValueKind.False:
                        return false;
                    case JsonValueKind.Number:
                        if (value.TryGetInt32(out var numeric))
                        {
                            return numeric != 0;
                        }

                        break;
                    case JsonValueKind.String:
                        var text = value.GetString();
                        if (bool.TryParse(text, out var boolValue))
                        {
                            return boolValue;
                        }

                        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                        {
                            return parsed != 0;
                        }

                        break;
                }
            }
        }

        return null;
    }

    private static bool TryResolveProperty(JsonElement root, string propertyPath, out JsonElement value)
    {
        var segments = propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var current = root;

        foreach (var segment in segments)
        {
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(segment, out current))
                {
                    value = default;
                    return false;
                }
            }
            else
            {
                value = default;
                return false;
            }
        }

        value = current;
        return true;
    }

    private static bool IsActiveState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        return state.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase)
            || state.Equals("RUNNING", StringComparison.OrdinalIgnoreCase)
            || state.Equals("STARTED", StringComparison.OrdinalIgnoreCase)
            || state.Equals("DEPLOYED", StringComparison.OrdinalIgnoreCase);
    }

    public sealed record TrueNasAppRelease(string Name, bool IsRunning, bool IsCheckedOut, string? Host, int? Port, string? Realm)
    {
        public bool HasConnectionInfo => !string.IsNullOrWhiteSpace(Host) && Port.HasValue;
    }
}
