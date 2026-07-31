using System;

namespace AillGameplaySuite
{
    internal sealed class LegacySystem
    {
        private readonly SuiteSettings _settings;

        public LegacySystem(SuiteSettings settings)
        {
            _settings = settings;
        }

        public void Validate()
        {
            if (_settings.EnableFriendlyFire || _settings.EnableCutThrough || _settings.EnableDeadlyCombat || _settings.EnableHorseCharge)
            {
                throw new InvalidOperationException("Legacy combat patches are disabled until each patch passes compatibility validation.");
            }
        }

        public void DailyTick()
        {
            // Economy and training adapters will be registered independently. No combat patch may
            // mutate Agent.Team, suppress original damage logic, or activate merely because the
            // umbrella module loaded.
        }
    }
}
