using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace AillGameplaySuite
{
    internal sealed class SuiteCampaignBehavior : CampaignBehaviorBase
    {
        private int _saveVersion = 1;
        private Dictionary<string, float> _familiarity = new Dictionary<string, float>();
        private Dictionary<string, int> _cooldowns = new Dictionary<string, int>();
        private Dictionary<string, string> _pendingOffers = new Dictionary<string, string>();

        private readonly SuiteSettings _settings = new SuiteSettings();
        private RelationshipSystem _relationships;
        private GovernorIssueSystem _governance;
        private LordDefectionSystem _defection;
        private LegacySystem _legacy;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("AillGameplaySuite.SaveVersion", ref _saveVersion);
            dataStore.SyncData("AillGameplaySuite.Familiarity.v1", ref _familiarity);
            dataStore.SyncData("AillGameplaySuite.Cooldowns.v1", ref _cooldowns);
            dataStore.SyncData("AillGameplaySuite.PendingOffers.v1", ref _pendingOffers);

            _familiarity = _familiarity ?? new Dictionary<string, float>();
            _cooldowns = _cooldowns ?? new Dictionary<string, int>();
            _pendingOffers = _pendingOffers ?? new Dictionary<string, string>();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            _relationships = new RelationshipSystem(_settings, _familiarity, _cooldowns);
            _governance = new GovernorIssueSystem(_settings, _cooldowns);
            _defection = new LordDefectionSystem(_settings, _cooldowns, _pendingOffers);
            _legacy = new LegacySystem(_settings);

            SuiteRuntime.RunGuarded("Relationships", _relationships.Validate);
            SuiteRuntime.RunGuarded("Governance", _governance.Validate);
            SuiteRuntime.RunGuarded("Defection", _defection.Validate);
            SuiteRuntime.RunGuarded("Legacy", _legacy.Validate);
            SuiteRuntime.ShowStartupStatus();
        }

        private void OnDailyTick()
        {
            TickCooldowns();
            if (_settings.EnableRelationshipSimulation && _relationships != null)
            {
                SuiteRuntime.RunGuarded("Relationships", _relationships.DailyTick);
            }
            if (_legacy != null)
            {
                SuiteRuntime.RunGuarded("Legacy", _legacy.DailyTick);
            }
        }

        private void OnDailyTickSettlement(Settlement settlement)
        {
            if (_settings.EnableGovernorIssues && _governance != null)
            {
                SuiteRuntime.RunGuarded("Governance", () => _governance.DailySettlementTick(settlement));
            }
        }

        private void OnWeeklyTick()
        {
            if (_settings.EnableLordDefection && _defection != null)
            {
                SuiteRuntime.RunGuarded("Defection", _defection.WeeklyTick);
            }
        }

        private void TickCooldowns()
        {
            if (_cooldowns.Count == 0) return;
            List<string> keys = new List<string>(_cooldowns.Keys);
            foreach (string key in keys)
            {
                int remaining = _cooldowns[key] - 1;
                if (remaining <= 0) _cooldowns.Remove(key);
                else _cooldowns[key] = remaining;
            }
        }
    }
}
