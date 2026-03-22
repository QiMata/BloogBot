using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System.Runtime.InteropServices;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace ForegroundBotRunner.Statics
{
    public partial class ObjectManager
    {

        // LUA SCRIPTS — Auto-attack toggle
        //
        // CastSpellByName('Attack') toggles auto-attack via the action bar system — the same
        // code path as a player clicking the Attack button on their action bar. This generates
        // authentic CMSG_ATTACKSWING packets through the WoW client's normal spell-cast pipeline.
        //
        // Attack is in action bar slot 1 (button 1 on the main action bar).
        // IsCurrentAction(1) returns 1 when auto-attack is active (slot 1 is highlighted).
        // The guard prevents accidental toggle-off: CastSpellByName('Attack') is a toggle,
        // so calling it when already attacking would STOP the attack.
        //
        // Note: AttackTarget() is a lower-level API that bypasses the action bar system.
        // Using CastSpellByName('Attack') is preferred because it exercises the same client
        // code path as a real player, producing identical packet sequences.
        private const string AutoAttackLuaScript = "if IsCurrentAction(1) == nil then CastSpellByName('Attack') end";
        private const string WandLuaScript = "if IsCurrentAction(1) == nil then CastSpellByName('Shoot') end";
        private const string TurnOffAutoAttackLuaScript = "if IsCurrentAction(1) ~= nil then CastSpellByName('Attack') end";
        private const string TurnOffWandLuaScript = "if IsCurrentAction(1) ~= nil then CastSpellByName('Shoot') end";



        public IEnumerable<IWoWUnit> CasterAggressors =>
            Aggressors
                .Where(u => u.ManaPercent > 0);



        public IEnumerable<IWoWUnit> MeleeAggressors =>
            Aggressors
                .Where(u => u.ManaPercent <= 0);



        public IEnumerable<IWoWUnit> Aggressors =>
            Hostiles
                .Where(u => u.IsInCombat || u.IsFleeing);
        //.Where(u =>
        //    u.TargetGuid == Pet?.Guid || 
        //    u.IsFleeing ||
        //    PartyMembers.Any(x => u.TargetGuid == x.Guid));            

        //.Where(u =>
        //    u.TargetGuid == Pet?.Guid || 
        //    u.IsFleeing ||
        //    PartyMembers.Any(x => u.TargetGuid == x.Guid));            

        //.Where(u =>
        //    u.TargetGuid == Pet?.Guid || 
        //    u.IsFleeing ||
        //    PartyMembers.Any(x => u.TargetGuid == x.Guid));            

        //.Where(u =>
        //    u.TargetGuid == Pet?.Guid || 
        //    u.IsFleeing ||
        //    PartyMembers.Any(x => u.TargetGuid == x.Guid));            

        public IEnumerable<IWoWUnit> Hostiles =>
            Units
                .Where(u => u.Health > 0)
                .Where(u =>
                    u.UnitReaction == UnitReaction.Hated ||
                    u.UnitReaction == UnitReaction.Hostile ||
                    u.UnitReaction == UnitReaction.Unfriendly ||
                    u.UnitReaction == UnitReaction.Neutral);

        // https://vanilla-wow.fandom.com/wiki/API_GetTalentInfo
        // tab index is 1, 2 or 3
        // talentIndex is counter left to right, top to bottom, starting at 1


        // https://vanilla-wow.fandom.com/wiki/API_GetTalentInfo
        // tab index is 1, 2 or 3
        // talentIndex is counter left to right, top to bottom, starting at 1




        public void StartMeleeAttack()
        {
            // Skip if already attacking — avoids Lua call overhead on every behavior tree tick.
            // CastSpellByName('Attack') is a toggle; calling it when already active would STOP the attack.
            if (Player is LocalPlayer lp && lp._isAutoAttacking)
                return;

            if (!Player.IsCasting && (Player.Class == Class.Warlock || Player.Class == Class.Mage || Player.Class == Class.Priest))
            {
                MainThreadLuaCall(WandLuaScript);
            }
            else
            {
                // Toggle auto-attack ON via CastSpellByName('Attack') — action bar path.
                // Guarded by IsCurrentAction(1) to prevent accidental toggle-off.
                MainThreadLuaCall(AutoAttackLuaScript);
            }

            if (Player is LocalPlayer localPlayer)
                localPlayer._isAutoAttacking = true;
        }



        public uint GetManaCost(string spellName)
        {
            if (Player is not LocalPlayer lp) return 0;
            return (uint)lp.GetManaCost(spellName);
        }



        public void StartRangedAttack()
        {
            MainThreadLuaCall(AutoAttackLuaScript);
        }



        public void StopAttack()
        {
            MainThreadLuaCall(TurnOffAutoAttackLuaScript);
            if (Player is LocalPlayer lp)
                lp._isAutoAttacking = false;
        }



        public bool IsSpellReady(string spellName)
        {
            if (Player is not LocalPlayer lp) return false;
            return lp.IsSpellReady(spellName);
        }



        public void CastSpell(string spellName, int rank = -1, bool castOnSelf = false)
        {
            if (castOnSelf)
                MainThreadLuaCall($"SpellTargetUnit('player')");
            if (rank > 0)
                MainThreadLuaCall($"CastSpellByName('{spellName}(Rank {rank})')");
            else
                MainThreadLuaCall($"CastSpellByName('{spellName}')");
        }



        public void CastSpell(uint spellId, int rank = -1, bool castOnSelf = false)
        {
            // Foreground bot uses LuaCall; no direct spell-ID cast available.
            // Callers should prefer string overload.
        }



        public void StartWandAttack()
        {
            MainThreadLuaCall(WandLuaScript);
        }



        public void StopWandAttack()
        {
            MainThreadLuaCall(TurnOffWandLuaScript);
        }



        public void StopCasting()
        {
            MainThreadLuaCall("SpellStopCasting()");
        }



        public void CastSpell(int spellId, int rank = -1, bool castOnSelf = false)
        {
            // Look up spell name from the client spell DB and delegate to string overload.
            var spellName = GetSpellNameFromDb(spellId);
            if (string.IsNullOrEmpty(spellName))
            {
                Log.Warning("[CastSpell(int)] No spell name found for ID {SpellId} in client spell DB", spellId);
                return;
            }
            Log.Information("[CastSpell(int)] Resolved spell {SpellId} → '{SpellName}'", spellId, spellName);
            CastSpell(spellName, rank, castOnSelf);
        }



        public void CastSpellAtLocation(int spellId, float x, float y, float z)
        {
            // FG bot: Lua CastSpellByName handles location targeting natively —
            // the client calculates the bobber/AOE position from player facing.
            // Just resolve the spell name and cast it.
            var spellName = GetSpellNameFromDb(spellId);
            if (!string.IsNullOrEmpty(spellName))
            {
                Log.Information("[CastSpellAtLocation] Resolved spell {SpellId} → '{SpellName}', casting via Lua", spellId, spellName);
                CastSpell(spellName);
            }
            else
            {
                Log.Warning("[CastSpellAtLocation] No spell name found for ID {SpellId}", spellId);
            }
        }

        public void CastSpellOnGameObject(int spellId, ulong gameObjectGuid)
        {
            // FG bot: CGGameObject_C::OnRightClick sends both CMSG_GAMEOBJ_USE + CMSG_CAST_SPELL
            // automatically. InteractWithGameObject below triggers the right-click; this is a no-op
            // because the native handler handles spell casting as part of the interaction.
        }



        public bool CanCastSpell(int spellId, ulong targetGuid)
        {
            if (Player is not LocalPlayer lp) return false;
            return !Functions.IsSpellOnCooldown(spellId);
        }
    }
}
