using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace AillGameplaySuite
{
    internal enum ModuleHealth
    {
        Active,
        Degraded,
        Disabled
    }

    internal static class SuiteRuntime
    {
        private static readonly Dictionary<string, ModuleHealth> Health = new Dictionary<string, ModuleHealth>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> LastErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Color Accent = Color.ConvertStringToColor("#7CC7FFFF");

        public static void RunGuarded(string module, Action action)
        {
            try
            {
                action();
                Health[module] = ModuleHealth.Active;
                LastErrors.Remove(module);
            }
            catch (Exception exception)
            {
                Health[module] = ModuleHealth.Degraded;
                LastErrors[module] = exception.GetType().Name + ": " + exception.Message;
                Debug.Print("[AillGameplaySuite][" + module + "] " + exception, 0, Debug.DebugColor.Red);
            }
        }

        public static ModuleHealth GetHealth(string module)
        {
            return Health.TryGetValue(module, out ModuleHealth health) ? health : ModuleHealth.Disabled;
        }

        public static void ShowStartupStatus()
        {
            int active = 0;
            int degraded = 0;
            foreach (ModuleHealth value in Health.Values)
            {
                if (value == ModuleHealth.Active) active++;
                if (value == ModuleHealth.Degraded) degraded++;
            }

            InformationManager.DisplayMessage(new InformationMessage(
                "Aill Gameplay Suite loaded: " + active + " active, " + degraded + " degraded.", Accent));

            foreach (KeyValuePair<string, string> error in LastErrors)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Aill Gameplay Suite disabled unsafe behavior in " + error.Key + ": " + error.Value,
                    Colors.Red));
            }
        }

        public static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int index = 0; index < value.Length; index++)
                {
                    hash = hash * 31 + value[index];
                }
                return hash;
            }
        }
    }
}
