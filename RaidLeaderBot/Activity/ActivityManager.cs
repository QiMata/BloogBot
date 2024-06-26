﻿using System.Diagnostics;
using System.Collections.Generic;
using static RaidLeaderBot.WinImports;
using System;
using System.Linq;
using RaidMemberBot.Models.Dto;
using System.Net;
using System.Windows;
using static RaidMemberBot.Constants.Enums;
using Newtonsoft.Json;

namespace RaidLeaderBot.Activity
{
    public abstract class ActivityManager
    {
        protected ActivityType Activity { get; }
        protected CharacterState RaidLeader { set; get; }
        protected int RaidSize { get; }
        protected int MapId { get; }
        protected int HostMapId { get; }
        protected virtual bool ActivityRunCondition { get; }
        protected CommandSocketServer _commandSocketServer { get; }

        public readonly Dictionary<RaidMemberViewModel, CharacterState> PartyMembersToStates = new Dictionary<RaidMemberViewModel, CharacterState>();
        protected readonly Dictionary<int, InstanceCommand> NextCommand = new Dictionary<int, InstanceCommand>();

        public ActivityManager(ActivityType activityType, int portNumber, int mapId)
        {
            _commandSocketServer = new CommandSocketServer(portNumber, IPAddress.Parse(RaidLeaderBotSettings.Instance.ListenAddress));
            _commandSocketServer.Start();

            _commandSocketServer.InstanceUpdateObservable.Subscribe(OnInstanceUpdate);

            MapId = mapId;
            Activity = activityType;
        }
        ~ActivityManager()
        {
            _commandSocketServer?.Stop();
        }

        protected void OnInstanceUpdate(CharacterState state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RaidMemberViewModel raidMemberViewModel = UpdateCharacterState(state);
                if (raidMemberViewModel.ShouldRun)
                {
                    if (BotPreparedToStart(raidMemberViewModel, state))
                        CheckForCommand(raidMemberViewModel, state);
                }
                else
                    NextCommand[state.ProcessId] = new InstanceCommand()
                    {
                        CommandAction = CommandAction.FullStop
                    };

                SendCommandToProcess(state.ProcessId, NextCommand[state.ProcessId]);
            });
        }

        private RaidMemberViewModel UpdateCharacterState(CharacterState characterState)
        {
            if (PartyMembersToStates.Values.Any(x => x.ProcessId == characterState.ProcessId))
            {
                for (int i = 0; i < PartyMembersToStates.Values.Count; i++)
                {
                    if (PartyMembersToStates.Values.ElementAt(i).ProcessId == characterState.ProcessId)
                    {
                        PartyMembersToStates[PartyMembersToStates.Keys.ElementAt(i)] = characterState;
                        if (RaidLeader != null && characterState.RaidLeaderGuid == characterState.Guid)
                        {
                            RaidLeader = characterState;
                        }

                        if (!characterState.IsConnected)
                        {
                            characterState = new CharacterState();
                        }
                        else
                        {
                            SetWindowText(Process.GetProcessById(characterState.ProcessId).MainWindowHandle, $"WoW - Player {i + 1}");
                        }

                        return PartyMembersToStates.Keys.ElementAt(i);
                    }
                }
            }
            else
            {
                for (int i = 0; i < PartyMembersToStates.Count; i++)
                {
                    if (PartyMembersToStates.Values.ElementAt(i).ProcessId == 0)
                    {
                        PartyMembersToStates[PartyMembersToStates.Keys.ElementAt(i)] = characterState;
                        NextCommand.Add(characterState.ProcessId, new InstanceCommand());
                        return PartyMembersToStates.Keys.ElementAt(i);
                    }
                }
            }
            return null;
        }
        protected abstract void CheckForCommand(RaidMemberViewModel raidMemberViewModel, CharacterState newCharacterState);
        protected bool BotPreparedToStart(RaidMemberViewModel raidMemberViewModel, CharacterState newCharacterState)
        {
            if (newCharacterState.ProcessId > 0)
            {
               if (!newCharacterState.IsReset)
                {
                    if (newCharacterState.AccountName != raidMemberViewModel.AccountName || newCharacterState.BotProfileName != raidMemberViewModel.BotProfileName)
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.SetAccountInfo,
                            CommandParam1 = raidMemberViewModel.AccountName,
                            CommandParam2 = raidMemberViewModel.BotProfileName,
                        };
                    }
                    else if (newCharacterState.Zone != "GM Island")
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.TeleTo,
                            CommandParam1 = "16226",
                            CommandParam2 = "16257",
                            CommandParam3 = "13",
                            CommandParam4 = "1",
                        };
                    }
                    else if (string.IsNullOrEmpty(newCharacterState.CurrentActivity))
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.SetActivity,
                            CommandParam1 = Activity.ToString(),
                            CommandParam2 = MapId.ToString(),
                        };
                    }
                    else if (RaidLeader == null)
                    {
                        if (raidMemberViewModel.RaidMemberPreset.IsMainTank && !string.IsNullOrEmpty(newCharacterState.CharacterName))
                        {
                            RaidLeader = newCharacterState;

                            NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                            {
                                CommandAction = CommandAction.SetRaidLeader,
                                CommandParam1 = RaidLeader.CharacterName,
                                CommandParam2 = RaidLeader.Guid.ToString(),
                            };
                        }
                    }
                    else
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.ResetCharacterState,
                        };
                    }
                }
                else if (!newCharacterState.IsReadyToStart)
                {
                    if (newCharacterState.Level < raidMemberViewModel.Level)
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.SetLevel,
                            CommandParam1 = raidMemberViewModel.Level.ToString()
                        };
                    }
                    else if (FindMissingSkills(raidMemberViewModel.Skills, newCharacterState, out int skillSpellId))
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.AddSpell,
                            CommandParam1 = skillSpellId.ToString()
                        };
                    }
                    else if (FindMissingSpells(raidMemberViewModel.Spells, newCharacterState, out int spellId))
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.AddSpell,
                            CommandParam1 = spellId.ToString()
                        };
                    }
                    else if (FindMissingEquipment(raidMemberViewModel, newCharacterState, out int itemId, out EquipSlot equipSlot))
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.AddEquipment,
                            CommandParam1 = itemId.ToString(),
                            CommandParam2 = ((int)equipSlot).ToString()
                        };
                    }
                    else if (FindMissingTalents(raidMemberViewModel, newCharacterState, out int preTalentSpellId))
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.AddTalent,
                            CommandParam1 = preTalentSpellId.ToString(),
                        };
                    }
                    else if (raidMemberViewModel.RaidMemberPreset.IsRole1 && !newCharacterState.IsRole1)
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.AddRole,
                            CommandParam1 = "1",
                        };
                    }
                    else if (raidMemberViewModel.RaidMemberPreset.IsRole2 && !newCharacterState.IsRole2)
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.AddRole,
                            CommandParam1 = "2",
                        };
                    }
                    else if (raidMemberViewModel.RaidMemberPreset.IsRole3 && !newCharacterState.IsRole3)
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.AddRole,
                            CommandParam1 = "3",
                        };
                    }
                    else if (raidMemberViewModel.RaidMemberPreset.IsRole4 && !newCharacterState.IsRole4)
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.AddRole,
                            CommandParam1 = "4",
                        };
                    }
                    else if (raidMemberViewModel.RaidMemberPreset.IsRole5 && !newCharacterState.IsRole5)
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.AddRole,
                            CommandParam1 = "5",
                        };
                    }
                    else if (raidMemberViewModel.RaidMemberPreset.IsRole6 && !newCharacterState.IsRole6)
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.AddRole,
                            CommandParam1 = "6",
                        };
                    }
                    else if (raidMemberViewModel.RaidMemberPreset.IsMainTank && !newCharacterState.IsMainTank)
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.AddRole,
                            CommandParam1 = "7",
                        };
                    }
                    else if (raidMemberViewModel.RaidMemberPreset.IsMainHealer && !newCharacterState.IsMainHealer)
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.AddRole,
                            CommandParam1 = "8",
                        };
                    }
                    else if (raidMemberViewModel.RaidMemberPreset.IsOffTank && !newCharacterState.IsOffTank)
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.AddRole,
                            CommandParam1 = "9",
                        };
                    }
                    else if (raidMemberViewModel.RaidMemberPreset.IsOffHealer && !newCharacterState.IsOffHealer)
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.AddRole,
                            CommandParam1 = "10",
                        };
                    }
                    else if (raidMemberViewModel.RaidMemberPreset.ShouldCleanse && !newCharacterState.ShouldCleanse)
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.AddRole,
                            CommandParam1 = "11",
                        };
                    }
                    else if (raidMemberViewModel.RaidMemberPreset.ShouldRebuff && !newCharacterState.ShouldRebuff)
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.AddRole,
                            CommandParam1 = "12",
                        };
                    }
                    else if (RaidLeader.Guid != newCharacterState.RaidLeaderGuid)
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.SetRaidLeader,
                            CommandParam1 = RaidLeader.CharacterName,
                            CommandParam2 = RaidLeader.Guid.ToString()
                        };
                    }
                    else if (!newCharacterState.IsReadyToStart)
                    {
                        NextCommand[newCharacterState.ProcessId] = new InstanceCommand()
                        {
                            CommandAction = CommandAction.SetReadyState,
                            CommandParam1 = true.ToString()
                        };
                    }
                }
                else if (RaidLeader.Guid != newCharacterState.Guid && !(newCharacterState.InParty || newCharacterState.InRaid))
                {
                    NextCommand[RaidLeader.ProcessId] = new InstanceCommand()
                    {
                        CommandAction = CommandAction.AddPartyMember,
                        CommandParam1 = newCharacterState.CharacterName,
                    };
                    return false;
                }
                else
                    return true;
            }
            return false;
        }

        protected bool FindMissingTalents(RaidMemberViewModel raidMemberViewModel, CharacterState newCharacterState, out int talentSpellId)
        {
            talentSpellId = 0;
            if (raidMemberViewModel.Talents.Count > 0 && newCharacterState.Level > 9)
            {
                int max = Math.Min(newCharacterState.Level - 9, raidMemberViewModel.Talents.Count);
                for (int i = 0; i < max; i++)
                {
                    if (!newCharacterState.Talents.Contains(raidMemberViewModel.Talents[i]))
                    {
                        talentSpellId = raidMemberViewModel.Talents[i];
                        return true;
                    }
                }
            }
            return false;
        }

        protected bool FindMissingEquipment(RaidMemberViewModel raidMemberViewModel, CharacterState newCharacterState, out int itemId, out EquipSlot inventorySlot)
        {
            itemId = 0;
            inventorySlot = EquipSlot.Ammo;

            if (newCharacterState.HeadItem != raidMemberViewModel.HeadItem.Entry)
            {
                inventorySlot = EquipSlot.Head;
                itemId = raidMemberViewModel.HeadItem.Entry;
                return true;
            }
            else if (newCharacterState.NeckItem != raidMemberViewModel.NeckItem.Entry)
            {
                inventorySlot = EquipSlot.Neck;
                itemId = raidMemberViewModel.NeckItem.Entry;
                return true;
            }
            else if (newCharacterState.ShoulderItem != raidMemberViewModel.ShoulderItem.Entry)
            {
                inventorySlot = EquipSlot.Shoulders;
                itemId = raidMemberViewModel.ShoulderItem.Entry;
                return true;
            }
            else if (newCharacterState.BackItem != raidMemberViewModel.BackItem.Entry)
            {
                inventorySlot = EquipSlot.Back;
                itemId = raidMemberViewModel.BackItem.Entry;
                return true;
            }
            else if (newCharacterState.ChestItem != raidMemberViewModel.ChestItem.Entry)
            {
                inventorySlot = EquipSlot.Chest;
                itemId = raidMemberViewModel.ChestItem.Entry;
                return true;
            }
            else if (newCharacterState.ShirtItem != raidMemberViewModel.ShirtItem.Entry)
            {
                inventorySlot = EquipSlot.Shirt;
                itemId = raidMemberViewModel.ShirtItem.Entry;
                return true;
            }
            else if (newCharacterState.Tabardtem != raidMemberViewModel.TabardItem.Entry)
            {
                inventorySlot = EquipSlot.Tabard;
                itemId = raidMemberViewModel.TabardItem.Entry;
                return true;
            }
            else if (newCharacterState.WristsItem != raidMemberViewModel.WristsItem.Entry)
            {
                inventorySlot = EquipSlot.Wrist;
                itemId = raidMemberViewModel.WristsItem.Entry;
                return true;
            }
            else if (newCharacterState.HandsItem != raidMemberViewModel.HandsItem.Entry)
            {
                inventorySlot = EquipSlot.Hands;
                itemId = raidMemberViewModel.HandsItem.Entry;
                return true;
            }
            else if (newCharacterState.WaistItem != raidMemberViewModel.WaistItem.Entry)
            {
                inventorySlot = EquipSlot.Waist;
                itemId = raidMemberViewModel.WaistItem.Entry;
                return true;
            }
            else if (newCharacterState.LegsItem != raidMemberViewModel.LegsItem.Entry)
            {
                inventorySlot = EquipSlot.Legs;
                itemId = raidMemberViewModel.LegsItem.Entry;
                return true;
            }
            else if (newCharacterState.FeetItem != raidMemberViewModel.FeetItem.Entry)
            {
                inventorySlot = EquipSlot.Feet;
                itemId = raidMemberViewModel.FeetItem.Entry;
                return true;
            }
            else if (newCharacterState.Finger1Item != raidMemberViewModel.Finger1Item.Entry)
            {
                inventorySlot = EquipSlot.Finger1;
                itemId = raidMemberViewModel.Finger1Item.Entry;
                return true;
            }
            else if (newCharacterState.Finger2Item != raidMemberViewModel.Finger2Item.Entry)
            {
                inventorySlot = EquipSlot.Finger2;
                itemId = raidMemberViewModel.Finger2Item.Entry;
                return true;
            }
            else if (newCharacterState.Trinket1Item != raidMemberViewModel.Trinket1Item.Entry)
            {
                inventorySlot = EquipSlot.Trinket1;
                itemId = raidMemberViewModel.Trinket1Item.Entry;
                return true;
            }
            else if (newCharacterState.Trinket2Item != raidMemberViewModel.Trinket2Item.Entry)
            {
                inventorySlot = EquipSlot.Trinket2;
                itemId = raidMemberViewModel.Trinket2Item.Entry;
                return true;
            }
            else if (newCharacterState.MainHandItem != raidMemberViewModel.MainHandItem.Entry)
            {
                inventorySlot = EquipSlot.MainHand;
                itemId = raidMemberViewModel.MainHandItem.Entry;
                return true;
            }
            else if (newCharacterState.OffHandItem != raidMemberViewModel.OffHandItem.Entry)
            {
                inventorySlot = EquipSlot.OffHand;
                itemId = raidMemberViewModel.OffHandItem.Entry;
                return true;
            }
            else if (newCharacterState.RangedItem != raidMemberViewModel.RangedItem.Entry)
            {
                inventorySlot = EquipSlot.Ranged;
                itemId = raidMemberViewModel.RangedItem.Entry;
                return true;
            }
            return false;
        }

        protected bool FindMissingSkills(List<int> skills, CharacterState newCharacterState, out int skillSpellId)
        {
            skillSpellId = 0;
            for (int i = 0; i < skills.Count; i++)
            {
                if (!newCharacterState.Skills.Contains(skills[i]))
                {
                    skillSpellId = SkillsToSpellsList[skills[i]];
                    return true;
                }
            }
            return false;
        }

        protected bool FindMissingSpells(List<int> spellList, CharacterState newCharacterState, out int spellId)
        {
            spellId = 0;

            for (int i = 0; i < spellList.Count; i++)
            {
                if (!newCharacterState.Spells.Contains(spellList[i]))
                {
                    spellId = spellList[i];
                    return true;
                }
            }
            return false;
        }

        public void QueueCommandToProcess(int processId, InstanceCommand command)
        {
            NextCommand[processId] = command;
        }
        protected void SendCommandToProcess(int processId, InstanceCommand command)
        {
            _commandSocketServer.SendCommandToProcess(processId, command);
            NextCommand[processId] = new InstanceCommand();
        }
        public void AddRaidMember(RaidMemberViewModel raidMemberViewModel)
        {
            PartyMembersToStates.Add(raidMemberViewModel, new CharacterState());
        }
        public void RemoveRaidMember(RaidMemberViewModel raidMemberViewModel)
        {
            PartyMembersToStates.Remove(raidMemberViewModel);
        }

        protected Dictionary<int, int> SkillsToSpellsList = new Dictionary<int, int>()
        {
            { 40, 2842 },
            { 43, 201 },
            { 44, 196 },
            { 45, 264 },
            { 46, 266 },
            { 54, 198 },
            { 55, 202 },
            { 118, 674 },
            { 129, 3273 },
            { 136, 227 },
            { 160, 199 },
            //{ 164, 2 },
            //{ 165, 2 },
            //{ 171, 2 },
            { 172, 197 },
            { 173, 1180 },
            { 176, 2567 },
            //{ 182, 2 },
            //{ 185, 2 },
            //{ 186, 2 },
            //{ 197, 2 },
            //{ 202, 2 },
            { 226, 5011 },
            { 228, 5009 },
            { 229, 200 },
            //{ 293, 2 },
            //{ 333, 2 },
            //{ 356, 2 },
            //{ 393, 2 },
            { 413, 8737 },
            { 414, 9077 },
            { 415, 9078 },
            { 433, 9116 },
            //{ 473, 15590 },
            { 633, 1804 },
        };
    }
}
