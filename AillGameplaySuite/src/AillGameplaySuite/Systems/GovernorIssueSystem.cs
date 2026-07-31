using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace AillGameplaySuite
{
    internal sealed class GovernorIssueSystem
    {
        private readonly SuiteSettings _settings;
        private readonly Dictionary<string, int> _cooldowns;

        public GovernorIssueSystem(SuiteSettings settings, Dictionary<string, int> cooldowns)
        {
            _settings = settings;
            _cooldowns = cooldowns;
        }

        public void Validate()
        {
            if (_settings.GovernorIssueAttemptCooldownDays < 1)
                throw new InvalidOperationException("Governor issue cooldown must be positive.");
            if (_settings.GovernorMinimumSkill < 0 || _settings.GovernorMinimumSkill > 300)
                throw new InvalidOperationException("Governor skill threshold is outside the safe range.");
        }

        public void DailySettlementTick(Settlement settlement)
        {
            if (settlement == null || settlement.Town == null || settlement.Town.Governor == null) return;
            if (settlement.OwnerClan != Clan.PlayerClan && !_settings.ResolveGovernorIssuesForAi) return;
            if (settlement.Town.IsUnderSiege) return;

            foreach (IssueBase issue in IssueManager.GetIssuesInSettlement(settlement, true).Where(issue => issue != null))
            {
                string key = "issue:" + issue.GetType().FullName + ":" + issue.IssueOwner?.StringId;
                if (_cooldowns.ContainsKey(key)) continue;

                // No private consequence method is invoked before an issue-specific adapter has
                // declared its costs, outputs, rollback behavior and version compatibility.
                _cooldowns[key] = _settings.GovernorIssueAttemptCooldownDays;
            }
        }
    }
}
