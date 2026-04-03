using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using WoWSharpClient.Movement;

namespace WoWSharpClient.Tests.Movement;

public sealed class SceneDataClientTests
{
    [Fact]
    public void EnsureSceneDataAround_SuppressesImmediateRetryAfterFailure()
    {
        var now = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);
        var requestCount = 0;

        try
        {
            SceneDataClient.TestUtcNowOverride = () => now;
            SceneDataClient.TestSendRequestOverride = _ =>
            {
                requestCount++;
                throw new IOException("SceneDataService unavailable");
            };

            var client = new SceneDataClient(NullLogger.Instance);

            Assert.False(client.EnsureSceneDataAround(30, 100f, 200f));
            Assert.False(client.EnsureSceneDataAround(30, 100f, 200f));

            Assert.Equal(1, requestCount);
        }
        finally
        {
            SceneDataClient.TestSendRequestOverride = null;
            SceneDataClient.TestUtcNowOverride = null;
        }
    }

    [Fact]
    public void EnsureSceneDataAround_RetriesAfterBackoffExpires()
    {
        var now = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);
        var requestCount = 0;

        try
        {
            SceneDataClient.TestUtcNowOverride = () => now;
            SceneDataClient.TestSendRequestOverride = _ =>
            {
                requestCount++;
                throw new IOException("SceneDataService unavailable");
            };

            var client = new SceneDataClient(NullLogger.Instance);

            Assert.False(client.EnsureSceneDataAround(30, 100f, 200f));

            now = now.AddSeconds(3);

            Assert.False(client.EnsureSceneDataAround(30, 100f, 200f));

            Assert.Equal(2, requestCount);
        }
        finally
        {
            SceneDataClient.TestSendRequestOverride = null;
            SceneDataClient.TestUtcNowOverride = null;
        }
    }
}
