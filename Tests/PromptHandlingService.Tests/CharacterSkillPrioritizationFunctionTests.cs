using GameData.Core.Enums;
using PromptHandlingService.Predefined.CharacterSkills;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptHandlingService.Tests
{
    public class CharacterSkillPrioritizationFunctionTests
    {
        [Fact]
        public async Task GetPrioritizedCharacterSkill_ValidCharacterDescription_ReturnsPrioritizedSkill()
        {
            // Arrange
            var promptRunner = new ScriptedPromptRunner(_ => "Mortal Strike");

            // Create a sample CharacterDescription object for testing
            var characterDescription = new CharacterSkillPrioritizationFunction.CharacterDescription
            {
                ClassName = Class.Warrior.ToString(),
                Race = Race.Orc.ToString(),
                Level = 60,
                Skills = ["Charge", "Heroic Strike", "Mortal Strike"]
            };

            // Act
            var result = await CharacterSkillPrioritizationFunction.GetPrioritizedCharacterSkill(promptRunner, characterDescription, CancellationToken.None);

            // Assert
            Assert.NotNull(result); // Ensure that a skill is returned
            Assert.Contains(result, characterDescription.Skills); // Check that the result is one of the character's spells
            var userPrompt = Assert.Single(promptRunner.Calls).Single(chat => chat.Key == "User").Value;
            Assert.Contains("Mortal Strike", userPrompt);
            Assert.DoesNotContain("{CharacterDescriptor}", userPrompt);
        }
    }

    [Trait("Category", "Integration")]
    public class CharacterSkillPrioritizationOllamaIntegrationTests
    {
        private readonly Uri _ollamaUri = new("http://localhost:11434");
        private const string ModelName = "deepseekr1:14b";

        [Fact]
        public async Task GetPrioritizedCharacterSkill_WithLocalOllama_ReturnsSkillName()
        {
            using var promptRunner = PromptRunnerFactory.GetOllamaPromptRunner(_ollamaUri, ModelName);
            var characterDescription = new CharacterSkillPrioritizationFunction.CharacterDescription
            {
                ClassName = Class.Warrior.ToString(),
                Race = Race.Orc.ToString(),
                Level = 60,
                Skills = ["Charge", "Heroic Strike", "Mortal Strike"]
            };

            var result = await CharacterSkillPrioritizationFunction.GetPrioritizedCharacterSkill(promptRunner, characterDescription, CancellationToken.None);

            Assert.False(string.IsNullOrWhiteSpace(result));
        }
    }
}
