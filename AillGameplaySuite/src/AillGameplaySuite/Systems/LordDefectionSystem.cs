using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace AillGameplaySuite
{
    internal sealed class LordDefectionSystem
    {
        private readonly SuiteSettings _settings;
        private readonly Dictionary<string, int> _cooldowns;
        private readonly Dictionary<string, string> _pendingOffers;

        public LordDefectionSystem(SuiteSettings settings, Dictionary<string, int> cooldowns, Dictionary<string, string> pendingOffers)
        {
            _settings = settings;
            _cooldowns = cooldowns;
            _pendingOffers = pendingOffers;
        }

        public void Validate()
        {
            if (_settings.LordDefectionMinimumRelation < -100 || _settings.LordDefectionMinimumRelation > 100)
                throw new InvalidOperationException("Defection relation threshold is outside the game range.");
            if (_settings.LordDefectionMaxCandidatesPerWeek < 1 || _settings.LordDefectionMaxCandidatesPerWeek > 50)
                throw new InvalidOperationException("Defection candidate budget is outside the safe range.");
        }

        public void WeeklyTick()
        {
            Clan playerClan = Clan.PlayerClan;
            Kingdom playerKingdom = playerClan?.Kingdom;
            if (playerClan == null || playerKingdom == null || playerKingdom.RulingClan != playerClan) return;

            List<Clan> candidates = Clan.All
                .Where(clan => IsEligible(clan, playerClan, playerKingdom))
                .Select(clan => new { Clan = clan, Score = Score(clan, playerKingdom) })
                .Where(item => item.Score >= 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Clan.StringId, StringComparer.Ordinal)
                .Take(_settings.LordDefectionMaxCandidatesPerWeek)
                .Select(item => item.Clan)
                .ToList();

            if (candidates.Count == 0) return;
            Clan selected = candidates[0];
            string key = "defection:" + selected.StringId;
            if (_pendingOffers.ContainsKey(key)) return;

            _pendingOffers[key] = "Contacted";
            _cooldowns[key] = _settings.LordDefectionOfferCooldownDays;
        }

        private bool IsEligible(Clan clan, Clan playerClan, Kingdom playerKingdom)
        {
            if (clan == null || clan == playerClan || clan.IsEliminated || clan.IsMinorFaction) return false;
            if (clan.Kingdom == null || clan.Kingdom == playerKingdom) return false;
            if (clan.Leader == null || !clan.Leader.IsAlive || clan.Leader.IsPrisoner) return false;
            if (clan.Fiefs.Count > 0) return false;
            if (clan.Leader.GetRelation(Hero.MainHero) < _settings.LordDefectionMinimumRelation) return false;
            if (_cooldowns.ContainsKey("defection:" + clan.StringId)) return false;
            return true;
        }

        private static int Score(Clan clan, Kingdom playerKingdom)
        {
            int relation = clan.Leader.GetRelation(Hero.MainHero);
            int tier = clan.Tier * 8;
            int culturePenalty = clan.Culture == playerKingdom.Culture ? 0 : 15;
            int rulingClanPenalty = clan.Kingdom?.RulingClan == clan ? 1000 : 0;
            return relation + tier - culturePenalty - rulingClanPenalty;
        }
    }
}
