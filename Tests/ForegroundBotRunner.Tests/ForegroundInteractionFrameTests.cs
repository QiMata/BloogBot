using ForegroundBotRunner.Frames;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace ForegroundBotRunner.Tests;

public sealed class ForegroundInteractionFrameTests
{
    [Fact]
    public void GossipFrame_UsesLuaVisibilityAndOptionCount()
    {
        var calls = new List<string>();
        var frame = new FgGossipFrame(
            lua => calls.Add(lua),
            lua =>
            {
                calls.Add(lua);
                if (lua.Contains("GetNumGossipOptions()"))
                    return ["3"];
                if (lua.Contains("GossipFrame:IsVisible()"))
                    return ["1"];

                return ["0"];
            },
            () => 0x42UL);

        Assert.True(frame.IsOpen);
        Assert.Equal(3, frame.Options.Count);
        Assert.Equal(0x42UL, frame.NPCGuid);

        frame.SelectGossipOption(2);

        Assert.Contains(calls, call => call.Contains("SelectGossipOption(2)"));
    }

    [Fact]
    public void QuestFrame_AcceptQuest_ClicksAcceptWhenQuestDialogIsOpen()
    {
        var calls = new List<string>();
        var frame = new FgQuestFrame(
            lua => calls.Add(lua),
            lua =>
            {
                calls.Add(lua);
                if (lua.Contains("QuestFrameAcceptButton"))
                    return ["1"];
                if (lua.Contains("QuestFrame and QuestFrame:IsVisible()"))
                    return ["1"];

                return ["0"];
            });

        Assert.True(frame.IsOpen);
        Assert.Equal(QuestFrameState.Accept, frame.State);

        frame.AcceptQuest();

        Assert.Contains(calls, call => call.Contains("QuestFrameAcceptButton:Click()"));
    }

    [Fact]
    public void QuestFrame_CompleteQuest_SelectsRequestedRewardBeforeTurnIn()
    {
        var calls = new List<string>();
        var frame = new FgQuestFrame(
            lua => calls.Add(lua),
            lua =>
            {
                calls.Add(lua);
                if (lua.Contains("GetNumQuestChoices()"))
                    return ["2"];
                if (lua.Contains("QuestFrame and QuestFrame:IsVisible()"))
                    return ["1"];

                return ["0"];
            });

        Assert.Equal(2, frame.RewardCount);

        frame.CompleteQuest(1);

        Assert.Contains(calls, call => call.Contains("QuestRewardItem2"));
        Assert.Contains(calls, call => call.Contains("QuestFrameCompleteQuestButton:Click()") || call.Contains("QuestFrameCompleteButton:Click()"));
    }

    [Fact]
    public void MerchantFrame_UsesLuaVisibilityAndRepairState()
    {
        var calls = new List<string>();
        var frame = new FgMerchantFrame(
            lua => calls.Add(lua),
            lua =>
            {
                calls.Add(lua);
                if (lua.Contains("CanMerchantRepair()"))
                    return ["1"];
                if (lua.Contains("GetRepairAllCost()"))
                    return ["321"];
                if (lua.Contains("GetMerchantNumItems()"))
                    return ["2"];
                if (lua.Contains("MerchantFrame:IsVisible()"))
                    return ["1"];

                return ["0"];
            },
            (_, _) => null,
            () => [],
            _ => new TestItem { MaxDurability = 100u, Durability = 50u },
            () => 0xABCDUL);

        Assert.True(frame.IsOpen);
        Assert.True(frame.CanRepair);
        Assert.Equal(321, frame.TotalRepairCost);
        Assert.Equal(2, frame.Items.Count);
        Assert.Equal(321, frame.RepairCost(EquipSlot.Head));

        frame.BuyItem(5, 2);
        frame.RepairAll();

        Assert.Contains(calls, call => call.Contains("BuyMerchantItem(5, 2)"));
        Assert.Contains(calls, call => call.Contains("RepairAllItems()"));
    }

    [Fact]
    public void TaxiFrame_UsesLuaNodeMetadataAndSelectsReachableNode()
    {
        var calls = new List<string>();
        var frame = new FgTaxiFrame(
            lua => calls.Add(lua),
            lua =>
            {
                calls.Add(lua);
                if (lua.Contains("NumTaxiNodes()"))
                    return ["2"];
                if (lua.Contains("TaxiNodeName(1)"))
                    return ["Orgrimmar", "0", "CURRENT"];
                if (lua.Contains("TaxiNodeName(2)"))
                    return ["Crossroads", "50", "REACHABLE"];
                if (lua.Contains("TaxiFrame:IsVisible()"))
                    return ["1"];

                return ["0"];
            });

        Assert.True(frame.IsOpen);
        Assert.Equal(2, frame.NodesAvailable);
        Assert.Equal("Orgrimmar", frame.CurrentNodeName);
        Assert.True(frame.HasNodeUnlocked(2));
        Assert.Equal("Crossroads", frame.Nodes[2].Name);
        Assert.Equal(50, frame.Nodes[2].Cost);

        frame.SelectNode(2);

        Assert.Contains(calls, call => call.Contains("TakeTaxiNode(2)"));
    }

    [Fact]
    public void TrainerFrame_EnumeratesServicesAndUsesZeroBasedTrainIndex()
    {
        var calls = new List<string>();
        var frame = new FgTrainerFrame(
            lua => calls.Add(lua),
            lua =>
            {
                calls.Add(lua);
                if (lua.Contains("ClassTrainerFrame"))
                    return ["1"];
                if (lua.Contains("GetTrainerServiceInfo(1)"))
                    return ["Heroic Strike", "Rank 2", "available", "100"];
                if (lua.Contains("GetTrainerServiceInfo(2)"))
                    return ["Rend", string.Empty, "used", "250"];
                if (lua.Contains("GetNumTrainerServices()"))
                    return ["2"];

                return ["0"];
            });

        Assert.True(frame.IsOpen);

        var spells = frame.Spells.ToArray();
        Assert.Equal(2, spells.Length);
        Assert.Equal("Heroic Strike", spells[0].Name);
        Assert.Equal(2, spells[0].Rank);
        Assert.Equal(0, spells[0].Index);
        Assert.True(spells[0].CanLearn);
        Assert.Equal(100, spells[0].Cost);
        Assert.False(spells[1].CanLearn);

        frame.TrainSpell(1);

        Assert.Contains(calls, call => call.Contains("BuyTrainerService(2)"));
    }

    [Fact]
    public void TalentFrame_ReportsPointsBuildsTabsAndLearnsTalentBySpellId()
    {
        var calls = new List<string>();
        var frame = new FgTalentFrame(
            lua => calls.Add(lua),
            lua =>
            {
                calls.Add(lua);
                if (lua.Contains("UnitLevel('player')"))
                    return ["22"];
                if (lua.Contains("GetNumTalentTabs()"))
                    return ["2"];
                if (lua.Contains("GetTalentTabInfo(1)"))
                    return ["Arms", string.Empty, "5", string.Empty];
                if (lua.Contains("GetTalentTabInfo(2)"))
                    return ["Fury", string.Empty, "3", string.Empty];
                if (lua.Contains("GetNumTalents(1)"))
                    return ["2"];
                if (lua.Contains("GetNumTalents(2)"))
                    return ["1"];
                if (lua.Contains("GetTalentInfo(1, 1)"))
                    return ["Deflection", string.Empty, "1", "2", "2", "5"];
                if (lua.Contains("GetTalentInfo(1, 2)"))
                    return ["Improved Rend", string.Empty, "1", "3", "3", "3"];
                if (lua.Contains("GetTalentInfo(2, 1)"))
                    return ["Cruelty", string.Empty, "2", "1", "0", "5"];
                if (lua.Contains("TalentFrame:IsVisible()"))
                    return ["1"];

                return ["0"];
            },
            spellId => spellId == 16464 ? "Deflection" : null,
            spellName => spellName == "Deflection"
                ? new uint[] { 16462, 16463, 16464, 16465, 16466 }
                : []);

        Assert.True(frame.IsOpen);
        Assert.Equal(8, frame.TalentPointsSpent);
        Assert.Equal(13, frame.TalentPointsAll);
        Assert.Equal(5, frame.TalentPointsAvailable);

        var tabs = frame.Tabs.ToArray();
        Assert.Equal(2, tabs.Length);
        Assert.Equal("Arms", tabs[0].Name);
        Assert.Equal("Deflection", tabs[0].Rows[0].Talents[1].Name);
        Assert.Equal(2, tabs[0].Rows[0].Talents[1].Ranks);
        Assert.Equal(5, tabs[0].Rows[0].Talents[1].TotalRanksAvailable);
        Assert.Equal(16464, tabs[0].Rows[0].Talents[1].NextRankSpellId);

        frame.LearnTalent(16464);

        Assert.Contains(calls, call => call.Contains("LearnTalent(1, 1)"));
    }

    [Fact]
    public void CraftFrame_ChecksMaterialsAndUsesZeroBasedCraftIndex()
    {
        var calls = new List<string>();
        var frame = new FgCraftFrame(
            lua => calls.Add(lua),
            lua =>
            {
                calls.Add(lua);
                if (lua.Contains("GetCraftReagentInfo"))
                    return ["1"];
                if (lua.Contains("CraftFrame:IsVisible()"))
                    return ["1"];

                return ["0"];
            });

        Assert.True(frame.IsOpen);
        Assert.True(frame.HasMaterialsNeeded(2));

        frame.Craft(2);

        Assert.Contains(calls, call => call.Contains("DoCraft(3)"));
    }

    private sealed class TestItem : IWoWItem
    {
        public string Name { get; init; } = string.Empty;
        public uint ItemId { get; init; }
        public uint Quantity { get; init; }
        public uint StackCount => Quantity;
        public uint MaxDurability { get; init; }
        public uint RequiredLevel => 0;
        public uint Durability { get; init; }
        public uint Duration => 0;
        public uint[] SpellCharges => [];
        public uint[] Enchantments => [];
        public uint PropertySeed => 0;
        public uint RandomPropertiesId => 0;
        public uint ItemTextId => 0;
        public bool IsCoins => false;
        public HighGuid Owner => default;
        public HighGuid Contained => default;
        public HighGuid CreatedBy => default;
        public HighGuid GiftCreator => default;
        public ItemCacheInfo? Info => null;
        public ItemDynFlags ItemDynamicFlags { get; set; }
        public ItemQuality Quality { get; init; } = ItemQuality.Common;
        public uint DurabilityPercentage => MaxDurability == 0 ? 0u : (uint)((double)Durability / MaxDurability * 100);
        public HighGuid HighGuid => default;
        public ulong Guid { get; init; }
        public WoWObjectType ObjectType => WoWObjectType.Item;
        public uint Entry => ItemId;
        public float ScaleX => 1f;
        public Position? Position => null;
        public float Facing => 0f;
        public uint LastUpdated => 0u;

        public void Use()
        {
        }

        public void Loot()
        {
        }
    }
}
