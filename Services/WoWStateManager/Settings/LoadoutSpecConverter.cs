using Communication;

namespace WoWStateManager.Settings
{
    /// <summary>
    /// P3: Maps the JSON-friendly <see cref="LoadoutSpecSettings"/> POCO loaded
    /// from <c>CharacterSettings.Loadout</c> into the
    /// <see cref="Communication.LoadoutSpec"/> protobuf message that rides on
    /// <c>ActionMessage</c> for the single <c>ApplyLoadout</c> hand-off.
    ///
    /// Lives here instead of in the proto layer so the config-shape POCO can
    /// evolve independently from the wire format and the coordinator has one
    /// obvious place to call for the conversion.
    /// </summary>
    public static class LoadoutSpecConverter
    {
        public static LoadoutSpec ToProto(LoadoutSpecSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var spec = new LoadoutSpec
            {
                TargetLevel = settings.TargetLevel,
                HonorRank = settings.HonorRank,
                RidingSkill = settings.RidingSkill,
                MountSpellId = settings.MountSpellId,
                ArmorSetId = settings.ArmorSetId,
                TalentTemplate = settings.TalentTemplate ?? string.Empty,
            };

            if (settings.SpellIdsToLearn != null)
                spec.SpellIdsToLearn.AddRange(settings.SpellIdsToLearn);

            if (settings.SupplementalItemIds != null)
                spec.SupplementalItemIds.AddRange(settings.SupplementalItemIds);

            if (settings.ElixirItemIds != null)
                spec.ElixirItemIds.AddRange(settings.ElixirItemIds);

            if (settings.CompletedQuestIds != null)
                spec.CompletedQuestIds.AddRange(settings.CompletedQuestIds);

            if (settings.Skills != null)
            {
                foreach (var skill in settings.Skills)
                {
                    spec.Skills.Add(new LoadoutSkillValue
                    {
                        SkillId = skill.SkillId,
                        Value = skill.Value,
                        Max = skill.Max,
                    });
                }
            }

            if (settings.EquipItems != null)
            {
                foreach (var item in settings.EquipItems)
                {
                    spec.EquipItems.Add(new LoadoutEquipItem
                    {
                        ItemId = item.ItemId,
                        InventorySlot = item.InventorySlot,
                    });
                }
            }

            if (settings.FactionReps != null)
            {
                foreach (var rep in settings.FactionReps)
                {
                    spec.FactionReps.Add(new LoadoutFactionRep
                    {
                        FactionId = rep.FactionId,
                        Standing = rep.Standing,
                    });
                }
            }

            return spec;
        }

        /// <summary>
        /// Convenience wrapper: build a fully-populated <see cref="ActionMessage"/>
        /// of type <c>ApplyLoadout</c> directly from settings. Coordinators enqueue
        /// the returned message once per bot.
        /// </summary>
        public static ActionMessage BuildApplyLoadoutAction(LoadoutSpecSettings settings)
        {
            return new ActionMessage
            {
                ActionType = ActionType.ApplyLoadout,
                LoadoutSpec = ToProto(settings),
            };
        }
    }
}
