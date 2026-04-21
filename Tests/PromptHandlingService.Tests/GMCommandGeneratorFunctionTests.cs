using PromptHandlingService.Predefined.GMCommands;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptHandlingService.Tests
{
    public class GMCommandGeneratorFunctionTests
    {
        [Fact]
        public async Task GetGMCommands_LevelUpCommand_ReturnsCorrectGMCommand()
        {
            // Create a sample CharacterDescription object for testing
            var promptRunner = new ScriptedPromptRunner(_ => ".character level [$playername] 60");
            var gmRequest = new GMCommandConstructionFunction.GMCommandContext
            {
                Command = "Set my level to 60."
            };

            // Act
            var result = await GMCommandConstructionFunction.GetGMCommand(promptRunner, gmRequest, CancellationToken.None);

            // Assert
            Assert.Equal(".character level [$playername] 60", result);
        }

        [Fact]
        public async Task GetGMCommands_SetCharacterMoney_ReturnsMoneyCommand()
        {
            // Arrange
            var promptRunner = new ScriptedPromptRunner(_ => ".modify money 1000000");
            var gmRequest = new GMCommandConstructionFunction.GMCommandContext
            {
                Command = "Give me 1 million gold."
            };

            // Act
            var result = await GMCommandConstructionFunction.GetGMCommand(promptRunner, gmRequest, CancellationToken.None);

            // Assert
            Assert.Equal(".modify money 1000000", result);
        }

        [Fact]
        public async Task GetGMCommands_InvalidCommand_ReturnsErrorMessage()
        {
            // Arrange
            var promptRunner = new ScriptedPromptRunner(_ => "Error: Invalid GM Command");
            var gmRequest = new GMCommandConstructionFunction.GMCommandContext
            {
                Command = "Turn me into a dragon!"
            };

            // Act
            var result = await GMCommandConstructionFunction.GetGMCommand(promptRunner, gmRequest, CancellationToken.None);

            // Assert
            Assert.Equal("Error: Invalid GM Command", result);
        }

        [Fact]
        public async Task GetGMCommands_LearnAllSpells_ReturnsLearnAllMyClassCommand()
        {
            // Arrange
            var promptRunner = new ScriptedPromptRunner(_ => ".learn all_myclass");
            var gmRequest = new GMCommandConstructionFunction.GMCommandContext
            {
                Command = "Teach me all my class spells."
            };

            // Act
            var result = await GMCommandConstructionFunction.GetGMCommand(promptRunner, gmRequest, CancellationToken.None);

            // Assert
            Assert.Equal(".learn all_myclass", result);
        }

        [Fact]
        public async Task GetGMCommands_ResetTalents_ReturnsResetTalentsCommand()
        {
            // Arrange
            var promptRunner = new ScriptedPromptRunner(_ => ".reset talents [Playername]");
            var gmRequest = new GMCommandConstructionFunction.GMCommandContext
            {
                Command = "Reset my talents."
            };

            // Act
            var result = await GMCommandConstructionFunction.GetGMCommand(promptRunner, gmRequest, CancellationToken.None);

            // Assert
            Assert.Equal(".reset talents [Playername]", result);
        }

        [Fact]
        public async Task GetGMCommands_ModifyFactionReputation_ReturnsModifyFactionCommand()
        {
            // Arrange
            var promptRunner = new ScriptedPromptRunner(_ => ".modify rep 530 exalted");
            var gmRequest = new GMCommandConstructionFunction.GMCommandContext
            {
                Command = "Make me exalted with the Darnassus faction."
            };

            // Act
            var result = await GMCommandConstructionFunction.GetGMCommand(promptRunner, gmRequest, CancellationToken.None);

            // Assert
            Assert.Equal(".modify rep 530 exalted", result);
        }

        [Fact]
        public async Task GetGMCommands_GoToCoordinates_ReturnsGoCommand()
        {
            // Arrange
            var promptRunner = new ScriptedPromptRunner(_ => ".go xyz 1000 2000 1");
            var gmRequest = new GMCommandConstructionFunction.GMCommandContext
            {
                Command = "Teleport me to coordinates 1000, 2000, 3000 on map 1."
            };

            // Act
            var result = await GMCommandConstructionFunction.GetGMCommand(promptRunner, gmRequest, CancellationToken.None);

            // Assert
            Assert.Equal(".go xyz 1000 2000 1", result);
        }
    }

    [Trait("Category", "Integration")]
    public class GMCommandGeneratorOllamaIntegrationTests
    {
        private readonly Uri _ollamaUri = new("http://localhost:11434");
        private const string ModelName = "llama3";

        [Fact]
        public async Task GetGMCommand_WithLocalOllama_ReturnsNonEmptyCommand()
        {
            using var promptRunner = PromptRunnerFactory.GetOllamaPromptRunner(_ollamaUri, ModelName);
            var gmRequest = new GMCommandConstructionFunction.GMCommandContext
            {
                Command = "Reset my talents."
            };

            var result = await GMCommandConstructionFunction.GetGMCommand(promptRunner, gmRequest, CancellationToken.None);

            Assert.False(string.IsNullOrWhiteSpace(result));
        }
    }
}
