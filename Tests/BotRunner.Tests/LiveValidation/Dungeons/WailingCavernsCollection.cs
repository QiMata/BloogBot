using Xunit;

namespace BotRunner.Tests.LiveValidation.Dungeons;

[CollectionDefinition(Name)]
public class WailingCavernsCollection : ICollectionFixture<WailingCavernsFixture>
{
    public const string Name = "WailingCavernsValidation";
}
