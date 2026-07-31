using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace AillGameplaySuite
{
    internal sealed class UnifiedCampaignBehavior : CampaignBehaviorBase
    {
        private Dictionary<string, int> _traitProgress = new Dictionary<string, int>(StringComparer.Ordinal);
        private Dictionary<string, int> _defectionCooldowns = new Dictionary<string, int>(StringComparer.Ordinal);

        public UnifiedCampaignBehavior()
        {
            GovernorTraitXpLedger.Bind(_traitProgress);
            LordDefectionRules.Bind(_defectionCooldowns);
        }

        public override void RegisterEvents()
        {
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("AillGameplaySuite.v1.GovernorTraitProgress", ref _traitProgress);
            dataStore.SyncData("AillGameplaySuite.v1.DefectionCooldowns", ref _defectionCooldowns);
            _traitProgress = _traitProgress ?? new Dictionary<string, int>(StringComparer.Ordinal);
            _defectionCooldowns = _defectionCooldowns ?? new Dictionary<string, int>(StringComparer.Ordinal);
            GovernorTraitXpLedger.Bind(_traitProgress);
            LordDefectionRules.Bind(_defectionCooldowns);
        }

        private void OnWeeklyTick()
        {
            Diagnostics.Guard("lord.weekly", LordDefectionRules.RunWeeklyOffer);
        }
    }

    internal static class GovernorTraitXpLedger
    {
        private static Dictionary<string, int> _progress = new Dictionary<string, int>(StringComparer.Ordinal);

        internal static void Bind(Dictionary<string, int> progress) => _progress = progress ?? new Dictionary<string, int>(StringComparer.Ordinal);

        internal static void Apply(Hero hero, TraitObject trait, int xp)
        {
            if (hero == null || trait == null || xp == 0 || Campaign.Current == null) return;
            var key = hero.StringId + "|" + trait.StringId;
            _progress.TryGetValue(key, out var stored);
            stored += xp;
            var level = hero.GetTraitLevel(trait);
            var model = Campaign.Current.Models.CharacterDevelopmentModel;

            while (stored > 0 && level < 2)
            {
                var cost = StepCost(model, trait, level, level + 1);
                if (stored < cost) break;
                stored -= cost;
                level++;
            }
            while (stored < 0 && level > -2)
            {
                var cost = StepCost(model, trait, level, level - 1);
                if (-stored < cost) break;
                stored += cost;
                level--;
            }
            hero.SetTraitLevel(trait, level);
            _progress[key] = stored;
        }

        private static int StepCost(CharacterDevelopmentModel model, TraitObject trait, int from, int to)
        {
            try
            {
                var a = model.GetTraitXpRequiredForTraitLevel(trait, from);
                var b = model.GetTraitXpRequiredForTraitLevel(trait, to);
                return Math.Max(1, Math.Abs(b - a));
            }
            catch { return 1000; }
        }
    }

    internal static class LordDefectionRules
    {
        private static Dictionary<string, int> _cooldowns = new Dictionary<string, int>(StringComparer.Ordinal);

        internal static void Bind(Dictionary<string, int> cooldowns) => _cooldowns = cooldowns ?? new Dictionary<string, int>(StringComparer.Ordinal);

        internal static bool CanRecruitConversationTarget(out string reason)
        {
            var clan = Hero.OneToOneConversationHero?.Clan;
            return Validate(clan, true, out reason);
        }

        internal static void RunWeeklyOffer()
        {
            if (!(LegacyBridge.ReadSetting("LordSurrender.LSSettings", "OpenLordSurrender", true) is bool open) || !open) return;
            var playerKingdom = Clan.PlayerClan?.Kingdom;
            if (playerKingdom == null || playerKingdom.RulingClan != Clan.PlayerClan) return;
            var now = CurrentDay;
            var candidates = Clan.All
                .Where(c => Validate(c, false, out _) && (!_cooldowns.TryGetValue(c.StringId, out var until) || until <= now))
                .Select(c => new { Clan = c, Relation = Hero.MainHero.GetRelation(c.Leader) })
                .OrderByDescending(x => x.Relation)
                .ThenByDescending(x => x.Clan.Tier)
                .ToList();
            if (candidates.Count == 0) return;
            var chosen = candidates[0];
            var chance = Math.Min(0.75f, 0.12f + Math.Max(0, chosen.Relation) / 180f);
            if (MBRandom.RandomFloat > chance) return;
            var price = RecruitmentPrice(chosen.Clan);
            var title = "A Secret Defection Offer";
            var text = chosen.Clan.Leader.Name + " of " + chosen.Clan.Name + " is landless and willing to abandon " + chosen.Clan.Kingdom.Name + ".\n\n" +
                       "Reason: relation " + chosen.Relation + ", no fiefs, and dissatisfaction with the current ruler.\n" +
                       "Requested settlement payment: " + price + " denars.";
            InformationManager.ShowInquiry(new InquiryData(title, text, true, true, "Accept", "Decline",
                () => TryRecruit(chosen.Clan, false),
                () => _cooldowns[chosen.Clan.StringId] = CurrentDay + 180,
                string.Empty, 0f, null, null, null), true, false);
            _cooldowns[chosen.Clan.StringId] = now + 30;
        }

        internal static bool TryRecruit(Clan clan, bool conversation)
        {
            if (!Validate(clan, conversation, out var reason))
            {
                Diagnostics.DisplayOnce("lord.invalid." + reason, reason, Colors.Red);
                return false;
            }
            var price = RecruitmentPrice(clan);
            if (Hero.MainHero.Gold < price)
            {
                Diagnostics.DisplayOnce("lord.gold." + clan.StringId, "Not enough denars for " + clan.Name + ": need " + price + ".", Colors.Red);
                _cooldowns[clan.StringId] = CurrentDay + 30;
                return false;
            }
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, clan.Leader, price, true);
            var compatibility = AccessTools.TypeByName("Bannerlord147Compat.Compatibility");
            var apply = AccessTools.Method(compatibility, "ApplyByJoinToKingdom");
            apply?.Invoke(null, new object[] { clan, Clan.PlayerClan.Kingdom, true });
            _cooldowns[clan.StringId] = CurrentDay + 365;
            Diagnostics.DisplayOnce("lord.joined." + clan.StringId, clan.Name + " joined your kingdom for " + price + " denars.", Colors.Green);
            return true;
        }

        private static bool Validate(Clan clan, bool allowIndependent, out string reason)
        {
            reason = string.Empty;
            var playerClan = Clan.PlayerClan;
            var playerKingdom = playerClan?.Kingdom;
            if (playerClan == null || playerKingdom == null || playerKingdom.RulingClan != playerClan) { reason = "You must rule a kingdom before recruiting a clan."; return false; }
            if (clan == null || clan.Leader == null || !clan.IsNoble || clan == playerClan) { reason = "This is not an eligible noble clan."; return false; }
            if (clan.Kingdom == playerKingdom) { reason = "This clan already belongs to your kingdom."; return false; }
            if (!allowIndependent && clan.Kingdom == null) { reason = "Weekly offers are reserved for clans serving another kingdom."; return false; }
            if (clan.Kingdom != null && clan.Kingdom.RulingClan == clan) { reason = "A ruling clan cannot defect through this system."; return false; }
            var requireLandless = LegacyBridge.ReadSetting("LordSurrender.LSSettings", "LordSurrenderRequireNoSettlement", true) is bool landless && landless;
            if (requireLandless && clan.Settlements.Count > 0) { reason = "This clan still controls settlements."; return false; }
            var minRelationObj = LegacyBridge.ReadSetting("LordSurrender.LSSettings", "LordSurrenderMinRelation", 0);
            var minRelation = minRelationObj is int value ? value : 0;
            if (Hero.MainHero.GetRelation(clan.Leader) < minRelation) { reason = "Relation with the clan leader is below the configured threshold."; return false; }
            return true;
        }

        private static int RecruitmentPrice(Clan clan) => Math.Max(10000, 10000 + clan.Tier * 15000);
        private static int CurrentDay => (int)CampaignTime.Now.ToDays;
    }
}
