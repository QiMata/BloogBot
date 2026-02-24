using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;

namespace RecordedTests.Shared.Tests;

public class TrueNasAppsClientTests
{
    private readonly HttpMessageHandler _messageHandler;
    private readonly HttpClient _httpClient;

    public TrueNasAppsClientTests()
    {
        _messageHandler = Substitute.ForPartsOf<FakeHttpMessageHandler>();
        _httpClient = new HttpClient(_messageHandler)
        {
            BaseAddress = new Uri("https://truenas.local/")
        };
    }

    [Fact]
    public async Task GetReleaseAsync_404Response_ReturnsNull()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.NotFound, "");

        var client = new TrueNasAppsClient("https://truenas.local/", "test-api-key", _httpClient);

        // Act
        var result = await client.GetReleaseAsync("nonexistent-release", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("ACTIVE", true)]
    [InlineData("RUNNING", true)]
    [InlineData("STARTED", true)]
    [InlineData("DEPLOYED", true)]
    [InlineData("STOPPED", false)]
    [InlineData("FAILED", false)]
    [InlineData("PENDING", false)]
    public async Task GetReleaseAsync_StateMapping_MapsToIsRunning(string state, bool expectedIsRunning)
    {
        // Arrange
        var responseJson = $$"""
        {
            "name": "mangosd-dev",
            "status": "{{state}}"
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new TrueNasAppsClient("https://truenas.local/", "test-api-key", _httpClient);

        // Act
        var result = await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsRunning.Should().Be(expectedIsRunning);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("\"True\"", true)]
    [InlineData("\"False\"", false)]
    public async Task GetReleaseAsync_CheckedOutParsing_HandlesBoolAndIntAndString(string checkedOutValue, bool expected)
    {
        // Arrange
        var responseJson = $$"""
        {
            "name": "mangosd-dev",
            "status": "ACTIVE",
            "config": {
                "iscsi_target": {
                    "checkedout": {{checkedOutValue}}
                }
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new TrueNasAppsClient("https://truenas.local/", "test-api-key", _httpClient);

        // Act
        var result = await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsCheckedOut.Should().Be(expected);
    }

    [Theory]
    [InlineData("config.iscsi_target.checkedout")]
    [InlineData("chart_metadata.checkedout")]
    [InlineData("resources.checkedout")]
    public async Task GetReleaseAsync_CheckedOutFromNestedPaths_FindsValue(string path)
    {
        // Arrange
        var pathParts = path.Split('.');
        var jsonBuilder = new StringBuilder();
        jsonBuilder.AppendLine("{");
        jsonBuilder.AppendLine("  \"name\": \"mangosd-dev\",");
        jsonBuilder.AppendLine("  \"status\": \"ACTIVE\",");

        for (int i = 0; i < pathParts.Length - 1; i++)
        {
            jsonBuilder.AppendLine($"  \"{pathParts[i]}\": {{");
        }

        jsonBuilder.AppendLine($"    \"{pathParts[^1]}\": true");

        for (int i = 0; i < pathParts.Length - 1; i++)
        {
            jsonBuilder.AppendLine("  }");
        }

        jsonBuilder.AppendLine("}");

        SetupHttpResponse(HttpStatusCode.OK, jsonBuilder.ToString());

        var client = new TrueNasAppsClient("https://truenas.local/", "test-api-key", _httpClient);

        // Act
        var result = await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsCheckedOut.Should().BeTrue();
    }

    [Theory]
    [InlineData("host", "port", "realm")]
    [InlineData("mangos_host", "mangos_port", "mangos_realm")]
    [InlineData("server_host", "server_port", "server_realm")]
    public async Task GetReleaseAsync_HostPortRealmKeyVariants_FindsValues(
        string hostKey,
        string portKey,
        string realmKey)
    {
        // Arrange
        var responseJson = $$"""
        {
            "name": "mangosd-dev",
            "status": "ACTIVE",
            "config": {
                "{{hostKey}}": "192.168.1.100",
                "{{portKey}}": 3724,
                "{{realmKey}}": "Development"
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new TrueNasAppsClient("https://truenas.local/", "test-api-key", _httpClient);

        // Act
        var result = await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Host.Should().Be("192.168.1.100");
        result.Port.Should().Be(3724);
        result.Realm.Should().Be("Development");
    }

    [Fact]
    public async Task GetReleaseAsync_HostPortRealmInNestedConfig_FindsValues()
    {
        // Arrange
        var responseJson = """
        {
            "name": "mangosd-dev",
            "status": "ACTIVE",
            "config": {
                "mangosd": {
                    "host": "192.168.1.100",
                    "port": 3724,
                    "realm": "Development"
                }
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new TrueNasAppsClient("https://truenas.local/", "test-api-key", _httpClient);

        // Act
        var result = await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Host.Should().Be("192.168.1.100");
        result.Port.Should().Be(3724);
        result.Realm.Should().Be("Development");
    }

    [Fact]
    public async Task StartReleaseAsync_ConflictResponse_DoesNotThrow()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.Conflict, "Already running");

        var client = new TrueNasAppsClient("https://truenas.local/", "test-api-key", _httpClient);

        // Act
        var act = async () => await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task StartReleaseAsync_NonSuccessNonConflict_ThrowsHttpRequestException(HttpStatusCode statusCode)
    {
        // Arrange
        SetupHttpResponse(statusCode, "Error");

        var client = new TrueNasAppsClient("https://truenas.local/", "test-api-key", _httpClient);

        // Act
        var act = async () => await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task TrueNasAppRelease_HasConnectionInfo_ReturnsTrueWhenBothPresent()
    {
        // Arrange
        var release = new TrueNasAppRelease(
            "test",
            IsRunning: true,
            IsCheckedOut: false,
            Host: "localhost",
            Port: 3724,
            Realm: null
        );

        // Act & Assert
        release.HasConnectionInfo.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, 3724)]
    [InlineData("", 3724)]
    [InlineData("   ", 3724)]
    [InlineData("localhost", null)]
    [InlineData(null, null)]
    public async Task TrueNasAppRelease_HasConnectionInfo_ReturnsFalseWhenMissing(
        string? host,
        int? port)
    {
        // Arrange
        var release = new TrueNasAppRelease(
            "test",
            IsRunning: true,
            IsCheckedOut: false,
            Host: host,
            Port: port,
            Realm: null
        );

        // Act & Assert
        release.HasConnectionInfo.Should().BeFalse();
    }

    [Fact]
    public void Constructor_InvalidBaseAddress_ThrowsArgumentException()
    {
        // Act
        var act = () => new TrueNasAppsClient("not-a-url", "test-api-key");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_RelativeBaseAddress_ThrowsArgumentException()
    {
        // Act
        var act = () => new TrueNasAppsClient("/relative/path", "test-api-key");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_MissingApiKey_ThrowsArgumentException(string? apiKey)
    {
        // Act
        var act = () => new TrueNasAppsClient("https://truenas.local/", apiKey!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task GetReleaseAsync_PortAsString_ConvertsToInt()
    {
        // Arrange
        var responseJson = """
        {
            "name": "mangosd-dev",
            "status": "ACTIVE",
            "config": {
                "port": "3724"
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new TrueNasAppsClient("https://truenas.local/", "test-api-key", _httpClient);

        // Act
        var result = await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Port.Should().Be(3724);
    }

    [Fact]
    public async Task GetReleaseAsync_MissingAllConnectionInfo_ReturnsNullValues()
    {
        // Arrange
        var responseJson = """
        {
            "name": "mangosd-dev",
            "status": "ACTIVE"
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new TrueNasAppsClient("https://truenas.local/", "test-api-key", _httpClient);

        // Act
        var result = await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Host.Should().BeNull();
        result.Port.Should().BeNull();
        result.Realm.Should().BeNull();
        result.HasConnectionInfo.Should().BeFalse();
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

        ((FakeHttpMessageHandler)_messageHandler).SetResponse(response);
    }

    // Fake HttpMessageHandler for testing
    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        private HttpResponseMessage? _response;

        public void SetResponse(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_response == null)
            {
                throw new InvalidOperationException("No response configured");
            }

            return Task.FromResult(_response);
        }
    }
}
