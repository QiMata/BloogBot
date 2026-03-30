using Xunit;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

[CollectionDefinition(Name)]
public class WarsongGulchCollection : ICollectionFixture<WarsongGulchFixture>
{
    public const string Name = "WarsongGulchValidation";
}
