using Communication;
using PromptHandlingService.Predefined.IntentParser;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptHandlingService.Tests
{
    public class IntentionParserFunctionTests
    {
        [Fact]
        public async Task ParsePromptIntent_GMCommand_ReturnsCorrectHandOffString()
        {
            // Arrange
            var promptRunner = new ScriptedPromptRunner(_ => "Send to GMCommandRunner: I want to run Molten Core");
            var testRequest = new IntentionParserFunction.UserRequest
            {
                Request = "I want to run Molten Core.",
                ActivitySnapshot = new WoWActivitySnapshot()
                {
                    Player = new Game.WoWPlayer()
                    {
                        Unit = new Game.WoWUnit()
                        {
                            GameObject = new Game.WoWGameObject()
                            {
                                Base = new Game.WoWObject()
                                {
                                    Guid = 150,
                                    MapId = 0,
                                    Position = new Game.Position()
                                    {
                                        X = 0,
                                        Y = 0,
                                        Z = 0
                                    }
                                }
                            }
                        }
                    }
                }
            };

            // Act
            var result = await IntentionParserFunction.ParsePromptIntent(promptRunner, testRequest, CancellationToken.None);

            // Assert
            Assert.Equal("Send to GMCommandRunner: I want to run Molten Core", result);
            var userPrompt = Assert.Single(promptRunner.Calls).Single(chat => chat.Key == "User").Value;
            Assert.Contains("I want to run Molten Core.", userPrompt);
        }

        [Fact]
        public async Task ParsePromptIntent_MechanicsExplanation_ReturnsCorrectHandOffString()
        {
            // Arrange
            var promptRunner = new ScriptedPromptRunner(_ => "Send to MechanicsExplainerRunner: Explain how threat works in World of Warcraft");
            var testRequest = new IntentionParserFunction.UserRequest
            {
                Request = "Explain how threat works in World of Warcraft"
            };

            // Act
            var result = await IntentionParserFunction.ParsePromptIntent(promptRunner, testRequest, CancellationToken.None);

            // Assert
            Assert.Equal("Send to MechanicsExplainerRunner: Explain how threat works in World of Warcraft", result);
        }

        [Fact]
        public async Task ParsePromptIntent_DataQuery_ReturnsCorrectHandOffString()
        {
            // Arrange
            var promptRunner = new ScriptedPromptRunner(_ => "Send to DataQueryRunner: Fetch stats for item 12345");
            var testRequest = new IntentionParserFunction.UserRequest
            {
                Request = "Can you teleport me to Orgrimmar?"
            };

            // Act
            var result = await IntentionParserFunction.ParsePromptIntent(promptRunner, testRequest, CancellationToken.None);

            // Assert
            Assert.Equal("Send to DataQueryRunner: Fetch stats for item 12345", result);
        }

        [Fact]
        public async Task ParsePromptIntent_Miscellaneous_ReturnsCorrectHandOffString()
        {
            // Arrange
            var promptRunner = new ScriptedPromptRunner(_ => "Send to MiscellaneousRequestRunner: How do I organize a guild event?");
            var testRequest = new IntentionParserFunction.UserRequest
            {
                Request = "How do I organize a guild event?"
            };

            // Act
            var result = await IntentionParserFunction.ParsePromptIntent(promptRunner, testRequest, CancellationToken.None);

            // Assert
            Assert.Equal("Send to MiscellaneousRequestRunner: How do I organize a guild event?", result);
        }
    }

    [Trait("Category", "Integration")]
    public class IntentionParserOllamaIntegrationTests
    {
        private readonly Uri _ollamaUri = new("http://localhost:11434");
        private const string ModelName = "deepseek-r1";

        [Fact]
        public async Task ParsePromptIntent_WithLocalOllama_ReturnsNonEmptyHandOff()
        {
            using var ollamaPromptRunner = PromptRunnerFactory.GetOllamaPromptRunner(_ollamaUri, ModelName);
            var testRequest = new IntentionParserFunction.UserRequest
            {
                Request = "I want to run Molten Core."
            };

            var result = await IntentionParserFunction.ParsePromptIntent(ollamaPromptRunner, testRequest, CancellationToken.None);

            Assert.False(string.IsNullOrWhiteSpace(result));
        }
    }
}
