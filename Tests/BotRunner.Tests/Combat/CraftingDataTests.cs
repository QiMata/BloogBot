using BotRunner.Combat;

namespace BotRunner.Tests.Combat
{
    public class CraftingDataTests
    {
        // ======== Constants ========

        [Fact]
        public void FirstAidSkillId_Is129()
        {
            Assert.Equal(129u, CraftingData.FirstAidSkillId);
        }

        [Fact]
        public void CookingSkillId_Is185()
        {
            Assert.Equal(185u, CraftingData.CookingSkillId);
        }

        // ======== Cloth Constants ========

        [Theory]
        [InlineData(2589u, nameof(CraftingData.LinenCloth))]
        [InlineData(2592u, nameof(CraftingData.WoolCloth))]
        [InlineData(4306u, nameof(CraftingData.SilkCloth))]
        [InlineData(4338u, nameof(CraftingData.MageweaveCloth))]
        [InlineData(14047u, nameof(CraftingData.RuneclothItem))]
        public void ClothConstants_CorrectValues(uint expectedId, string constantName)
        {
            // Verify via reflection since we can't inline const fields in Theory
            var field = typeof(CraftingData).GetField(constantName);
            Assert.NotNull(field);
            Assert.Equal(expectedId, (uint)field!.GetValue(null)!);
        }

        // ======== Recipe Counts ========

        [Fact]
        public void FirstAidRecipes_Contains10Recipes()
        {
            Assert.Equal(10, CraftingData.FirstAidRecipes.Length);
        }

        [Fact]
        public void CookingRecipes_Contains9Recipes()
        {
            Assert.Equal(9, CraftingData.CookingRecipes.Length);
        }

        // ======== First Aid Recipe Data ========

        [Theory]
        [InlineData(0, "Linen Bandage", 1251u, 1, 50)]
        [InlineData(1, "Heavy Linen Bandage", 2581u, 40, 80)]
        [InlineData(2, "Wool Bandage", 3530u, 80, 150)]
        [InlineData(3, "Heavy Wool Bandage", 3531u, 115, 180)]
        [InlineData(4, "Silk Bandage", 6450u, 150, 210)]
        [InlineData(5, "Heavy Silk Bandage", 6451u, 180, 240)]
        [InlineData(6, "Mageweave Bandage", 8544u, 210, 270)]
        [InlineData(7, "Heavy Mageweave Bandage", 8545u, 240, 300)]
        [InlineData(8, "Runecloth Bandage", 14529u, 260, 300)]
        [InlineData(9, "Heavy Runecloth Bandage", 14530u, 290, 300)]
        public void FirstAidRecipes_CorrectData(int index, string name, uint resultItemId, int reqSkill, int greySkill)
        {
            var recipe = CraftingData.FirstAidRecipes[index];
            Assert.Equal(name, recipe.Name);
            Assert.Equal(resultItemId, recipe.ResultItemId);
            Assert.Equal(reqSkill, recipe.RequiredSkill);
            Assert.Equal(greySkill, recipe.GreySkill);
        }

        [Fact]
        public void FirstAidRecipes_SortedByRequiredSkill()
        {
            for (int i = 1; i < CraftingData.FirstAidRecipes.Length; i++)
            {
                Assert.True(CraftingData.FirstAidRecipes[i].RequiredSkill >=
                    CraftingData.FirstAidRecipes[i - 1].RequiredSkill,
                    $"Recipe at index {i} has lower RequiredSkill than {i - 1}");
            }
        }

        [Fact]
        public void FirstAidRecipes_AllHaveMaterials()
        {
            foreach (var recipe in CraftingData.FirstAidRecipes)
            {
                Assert.NotEmpty(recipe.Materials);
                foreach (var (itemId, count) in recipe.Materials)
                {
                    Assert.NotEqual(0u, itemId);
                    Assert.True(count > 0);
                }
            }
        }

        // ======== Cooking Recipe Data ========

        [Theory]
        [InlineData(0, "Charred Wolf Meat", 1)]
        [InlineData(1, "Roasted Boar Meat", 1)]
        [InlineData(2, "Spiced Wolf Meat", 10)]
        [InlineData(3, "Smoked Bear Meat", 40)]
        [InlineData(4, "Boiled Clams", 50)]
        [InlineData(5, "Crocolisk Steak", 80)]
        [InlineData(6, "Redridge Goulash", 100)]
        [InlineData(7, "Roast Raptor", 175)]
        [InlineData(8, "Spotted Yellowtail", 225)]
        public void CookingRecipes_CorrectNameAndSkill(int index, string name, int reqSkill)
        {
            var recipe = CraftingData.CookingRecipes[index];
            Assert.Equal(name, recipe.Name);
            Assert.Equal(reqSkill, recipe.RequiredSkill);
        }

        // ======== FindBestRecipeForSkillUp ========

        [Fact]
        public void FindBestRecipe_NoMaterials_ReturnsNull()
        {
            var inventory = new Dictionary<uint, int>();
            var result = CraftingData.FindBestRecipeForSkillUp(
                CraftingData.FirstAidRecipes, 50, inventory);
            Assert.Null(result);
        }

        [Fact]
        public void FindBestRecipe_SkillTooLow_ReturnsNull()
        {
            // Skill 0 can't craft anything (lowest recipe needs skill 1)
            var inventory = new Dictionary<uint, int> { [CraftingData.LinenCloth] = 100 };
            var result = CraftingData.FindBestRecipeForSkillUp(
                CraftingData.FirstAidRecipes, 0, inventory);
            Assert.Null(result);
        }

        [Fact]
        public void FindBestRecipe_SkillAtGrey_SkipsGreyRecipe()
        {
            // Linen Bandage is grey at 50, but we have skill 50
            // Heavy Linen requires 40 and goes grey at 80, so it should be picked
            var inventory = new Dictionary<uint, int> { [CraftingData.LinenCloth] = 100 };
            var result = CraftingData.FindBestRecipeForSkillUp(
                CraftingData.FirstAidRecipes, 50, inventory);
            Assert.NotNull(result);
            Assert.Equal("Heavy Linen Bandage", result!.Name);
        }

        [Fact]
        public void FindBestRecipe_PicksHighestSkillRecipe()
        {
            // At skill 80, can craft Wool Bandage (80-150) or Heavy Linen Bandage (40-80 grey)
            // Heavy Linen is grey at 80 so should be skipped
            // Wool Bandage requires skill 80 — should be picked
            var inventory = new Dictionary<uint, int>
            {
                [CraftingData.LinenCloth] = 100,
                [CraftingData.WoolCloth] = 100
            };
            var result = CraftingData.FindBestRecipeForSkillUp(
                CraftingData.FirstAidRecipes, 80, inventory);
            Assert.NotNull(result);
            Assert.Equal("Wool Bandage", result!.Name);
        }

        [Fact]
        public void FindBestRecipe_SkillAtMax_ReturnsNull()
        {
            // All recipes are grey at 300, skill 300 should return null
            var inventory = new Dictionary<uint, int>
            {
                [CraftingData.LinenCloth] = 100,
                [CraftingData.WoolCloth] = 100,
                [CraftingData.SilkCloth] = 100,
                [CraftingData.MageweaveCloth] = 100,
                [CraftingData.RuneclothItem] = 100
            };
            var result = CraftingData.FindBestRecipeForSkillUp(
                CraftingData.FirstAidRecipes, 300, inventory);
            Assert.Null(result);
        }

        [Fact]
        public void FindBestRecipe_FallsBackToLowerRecipe_WhenNoHigherMats()
        {
            // At skill 150, could use Silk Bandage (150-210) but no silk in inventory
            // Should fall back to Heavy Wool Bandage (115-180) if wool available
            var inventory = new Dictionary<uint, int>
            {
                [CraftingData.WoolCloth] = 100
            };
            var result = CraftingData.FindBestRecipeForSkillUp(
                CraftingData.FirstAidRecipes, 150, inventory);
            Assert.NotNull(result);
            Assert.Equal("Heavy Wool Bandage", result!.Name);
        }

        [Fact]
        public void FindBestRecipe_EmptyRecipeArray_ReturnsNull()
        {
            var inventory = new Dictionary<uint, int> { [CraftingData.LinenCloth] = 100 };
            var result = CraftingData.FindBestRecipeForSkillUp(
                [], 50, inventory);
            Assert.Null(result);
        }

        // ======== MaxCraftCount ========

        [Fact]
        public void MaxCraftCount_EmptyInventory_ReturnsZero()
        {
            var recipe = CraftingData.FirstAidRecipes[0]; // Linen Bandage (1 linen)
            var inventory = new Dictionary<uint, int>();
            Assert.Equal(0, CraftingData.MaxCraftCount(recipe, inventory));
        }

        [Fact]
        public void MaxCraftCount_ExactMaterials_ReturnsOne()
        {
            var recipe = CraftingData.FirstAidRecipes[0]; // Linen Bandage (1 linen)
            var inventory = new Dictionary<uint, int> { [CraftingData.LinenCloth] = 1 };
            Assert.Equal(1, CraftingData.MaxCraftCount(recipe, inventory));
        }

        [Fact]
        public void MaxCraftCount_MultipleCrafts()
        {
            var recipe = CraftingData.FirstAidRecipes[0]; // Linen Bandage (1 linen each)
            var inventory = new Dictionary<uint, int> { [CraftingData.LinenCloth] = 10 };
            Assert.Equal(10, CraftingData.MaxCraftCount(recipe, inventory));
        }

        [Fact]
        public void MaxCraftCount_MultipleMaterials_LimitedByScarcest()
        {
            // Spiced Wolf Meat needs (2672, 1) and (2678, 1) — mild spices
            var recipe = CraftingData.CookingRecipes[2]; // Spiced Wolf Meat
            var inventory = new Dictionary<uint, int>
            {
                [2672u] = 20,  // 20 wolf meat
                [2678u] = 5    // 5 mild spices — limiting factor
            };
            Assert.Equal(5, CraftingData.MaxCraftCount(recipe, inventory));
        }

        [Fact]
        public void MaxCraftCount_HeavyBandage_TwoClothEach()
        {
            var recipe = CraftingData.FirstAidRecipes[1]; // Heavy Linen Bandage (2 linen each)
            var inventory = new Dictionary<uint, int> { [CraftingData.LinenCloth] = 10 };
            Assert.Equal(5, CraftingData.MaxCraftCount(recipe, inventory));
        }

        [Fact]
        public void MaxCraftCount_MissingOneMaterial_ReturnsZero()
        {
            // Spiced Wolf Meat needs both wolf meat and mild spices
            var recipe = CraftingData.CookingRecipes[2];
            var inventory = new Dictionary<uint, int>
            {
                [2672u] = 20  // wolf meat only, no spices
            };
            Assert.Equal(0, CraftingData.MaxCraftCount(recipe, inventory));
        }

        [Fact]
        public void MaxCraftCount_NoMaterialsInRecipe_ReturnsZero()
        {
            // Synthetic recipe with empty materials array
            var recipe = new CraftingData.Recipe { Materials = [] };
            var inventory = new Dictionary<uint, int> { [1u] = 100 };
            Assert.Equal(0, CraftingData.MaxCraftCount(recipe, inventory));
        }
    }
}
