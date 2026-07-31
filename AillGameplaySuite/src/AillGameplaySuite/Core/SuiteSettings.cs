namespace AillGameplaySuite
{
    internal sealed class SuiteSettings
    {
        public bool EnableRelationshipSimulation = true;
        public int RelationshipDailyPairBudget = 20;
        public int RelationshipMajorEventCooldownDays = 60;
        public float FamiliarityGainPerSharedDay = 0.25f;

        public bool EnableGovernorIssues = true;
        public bool ResolveGovernorIssuesForAi = false;
        public int GovernorIssueAttemptCooldownDays = 14;
        public int GovernorMinimumSkill = 60;

        public bool EnableLordDefection = true;
        public int LordDefectionMinimumRelation = 40;
        public int LordDefectionOfferCooldownDays = 90;
        public int LordDefectionMaxCandidatesPerWeek = 8;

        public bool EnableLegacyEconomy = true;
        public bool EnableLegacyTraining = true;
        public bool EnableLegacyCombat = false;
        public bool EnableFriendlyFire = false;
        public bool EnableCutThrough = false;
        public bool EnableDeadlyCombat = false;
        public bool EnableHorseCharge = false;
    }
}
