using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace AillGameplaySuite
{
    internal sealed class RelationshipSystem
    {
        private readonly SuiteSettings _settings;
        private readonly Dictionary<string, float> _familiarity;
        private readonly Dictionary<string, int> _cooldowns;

        public RelationshipSystem(SuiteSettings settings, Dictionary<string, float> familiarity, Dictionary<string, int> cooldowns)
        {
            _settings = settings;
            _familiarity = familiarity;
            _cooldowns = cooldowns;
        }

        public void Validate()
        {
            if (_settings.RelationshipDailyPairBudget < 0 || _settings.RelationshipDailyPairBudget > 200)
                throw new InvalidOperationException("Relationship pair budget is outside the safe range.");
            if (_settings.FamiliarityGainPerSharedDay < 0f || _settings.FamiliarityGainPerSharedDay > 5f)
                throw new InvalidOperationException("Familiarity gain is outside the safe range.");
        }

        public void DailyTick()
        {
            List<Hero> heroes = Hero.AllAliveHeroes
                .Where(IsEligible)
                .OrderBy(hero => hero.StringId, StringComparer.Ordinal)
                .ToList();

            int processed = 0;
            foreach (IGrouping<string, Hero> group in heroes.GroupBy(hero => hero.CurrentSettlement.StringId))
            {
                Hero[] local = group.ToArray();
                for (int first = 0; first < local.Length && processed < _settings.RelationshipDailyPairBudget; first++)
                {
                    for (int second = first + 1; second < local.Length && processed < _settings.RelationshipDailyPairBudget; second++)
                    {
                        UpdatePair(local[first], local[second]);
                        processed++;
                    }
                }
                if (processed >= _settings.RelationshipDailyPairBudget) break;
            }
        }

        private static bool IsEligible(Hero hero)
        {
            return hero != null && hero.IsAlive && !hero.IsChild && hero.CurrentSettlement != null && hero.Clan != null;
        }

        private void UpdatePair(Hero first, Hero second)
        {
            string key = PairKey(first, second);
            float value = _familiarity.TryGetValue(key, out float current) ? current : 0f;
            value = Math.Min(100f, value + _settings.FamiliarityGainPerSharedDay);
            _familiarity[key] = value;
        }

        private static string PairKey(Hero first, Hero second)
        {
            string left = first.StringId;
            string right = second.StringId;
            return string.CompareOrdinal(left, right) <= 0 ? "rel:" + left + ":" + right : "rel:" + right + ":" + left;
        }
    }
}
