using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System;
using System.Collections.Generic;

namespace WoWSharpClient
{
    /// <summary>
    /// Partial class that delegates spell casting, melee/ranged attack, target selection,
    /// and cooldown tracking to <see cref="SpellcastingManager"/>.
    /// All public IObjectManager method signatures are preserved; implementation lives in the extracted class.
    /// </summary>
    public partial class WoWSharpObjectManager
    {
        // ---- Cooldown checker wiring ----

        public void SetSpellCooldownChecker(Func<uint, bool> checker) => _spellcasting.SetSpellCooldownChecker(checker);

        // ---- Melee rejection tracking (internal — called by SpellHandler, WorldClient) ----

        internal void NoteMeleeRangeRejected() => _spellcasting.NoteMeleeRangeRejected();
        internal void NoteMeleeFacingRejected() => _spellcasting.NoteMeleeFacingRejected();
        internal void ClearRecentMeleeRejections(ulong targetGuid = 0) => _spellcasting.ClearRecentMeleeRejections(targetGuid);
        public bool HadRecentMeleeRangeRejection(ulong targetGuid) => _spellcasting.HadRecentMeleeRangeRejection(targetGuid);
        public bool HadRecentMeleeFacingRejection(ulong targetGuid) => _spellcasting.HadRecentMeleeFacingRejection(targetGuid);
        internal void NotePendingMeleeAttackStart(ulong targetGuid) => _spellcasting.NotePendingMeleeAttackStart(targetGuid);
        internal bool HasPendingMeleeAttackStart(ulong targetGuid) => _spellcasting.HasPendingMeleeAttackStart(targetGuid);
        internal void ClearPendingMeleeAttackStart(ulong targetGuid = 0) => _spellcasting.ClearPendingMeleeAttackStart(targetGuid);
        internal void ConfirmMeleeAttackStarted(ulong targetGuid = 0) => _spellcasting.ConfirmMeleeAttackStarted(targetGuid);

        // ---- Spell readiness ----

        public bool IsSpellReady(string spellName) => _spellcasting.IsSpellReady(spellName);

        // ---- Casting ----

        public void StopCasting() => _spellcasting.StopCasting();
        public void CastSpell(string spellName, int rank = -1, bool castOnSelf = false) => _spellcasting.CastSpell(spellName, rank, castOnSelf);
        public void CastSpell(int spellId, int rank = -1, bool castOnSelf = false) => _spellcasting.CastSpell(spellId, rank, castOnSelf);
        public void CastSpellAtLocation(int spellId, float x, float y, float z) => _spellcasting.CastSpellAtLocation(spellId, x, y, z);
        public void CastSpellOnGameObject(int spellId, ulong gameObjectGuid) => _spellcasting.CastSpellOnGameObject(spellId, gameObjectGuid);
        public bool CanCastSpell(int spellId, ulong targetGuid) => _spellcasting.CanCastSpell(spellId, targetGuid);
        public IReadOnlyCollection<uint> KnownSpellIds => _spellcasting.KnownSpellIds;
        public void CancelAura(uint spellId) => _spellcasting.CancelAura(spellId);

        // ---- Wand ----

        public void StartWandAttack() => _spellcasting.StartWandAttack();
        public void StopWandAttack() => _spellcasting.StopWandAttack();

        // ---- Target ----

        public void SetTarget(ulong guid) => _spellcasting.SetTarget(guid);

        // ---- Attack ----

        public void StopAttack() => _spellcasting.StopAttack();
        public void StartMeleeAttack() => _spellcasting.StartMeleeAttack();
        public void StartRangedAttack() => _spellcasting.StartRangedAttack();

        // ---- Talent / Mana ----

        public sbyte GetTalentRank(uint tabIndex, uint talentIndex) => _spellcasting.GetTalentRank(tabIndex, talentIndex);
        public uint GetManaCost(string spellName) => _spellcasting.GetManaCost(spellName);

        // ---- ObjectUpdateOperation enum stays here (used by Network.cs ProcessUpdatesAsync) ----

        public enum ObjectUpdateOperation
        {
            Add,
            Update,
            Remove,
        }
    }
}
