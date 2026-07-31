using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace AillGameplaySuite
{
    [HarmonyPatch]
    internal static class CompatibilityTraitXpPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("Bannerlord147Compat.Compatibility"), "AddGovernorTraitXp");

        private static bool Prefix(object[] __args)
        {
            if (__args != null && __args.Length >= 3 && __args[0] is Hero hero && __args[1] is TraitObject trait)
            {
                GovernorTraitXpLedger.Apply(hero, trait, Convert.ToInt32(__args[2]));
            }
            return false;
        }
    }

    [HarmonyPatch]
    internal static class SafeSettlementDailyTickPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("Bannerlord147Compat.Compatibility"), "ShouldRunSettlementDailyTick");

        private static bool Prefix(object[] __args, ref bool __result)
        {
            try
            {
                var settings = __args != null && __args.Length > 0 ? __args[0] : null;
                var settlement = __args != null && __args.Length > 1 ? __args[1] as Settlement : null;
                if (settings == null || settlement == null) { __result = true; return false; }
                if (!ReadBool(settings, "enableQuestResolutionMode", true)) { __result = true; return false; }
                if ((!settlement.IsTown && !settlement.IsVillage) || settlement.OwnerClan == null) { __result = true; return false; }
                var resolveAi = ReadBool(settings, "resolveIssuesForAI", false);
                if (settlement.OwnerClan != Clan.PlayerClan && !resolveAi) { __result = true; return false; }
                var governor = settlement.IsTown ? settlement.Town?.Governor : settlement.Village?.Bound?.Town?.Governor;
                if (governor == null) { __result = true; return false; }
                var frequency = Math.Max(0f, Math.Min(1f, ReadFloat(settings, "issueFrequencyPercent", 0.4f)));
                __result = MBRandom.RandomFloat <= frequency;
                return false;
            }
            catch
            {
                __result = true;
                return false;
            }
        }

        private static bool ReadBool(object target, string name, bool fallback)
        {
            var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return property?.GetValue(target, null) is bool value ? value : fallback;
        }

        private static float ReadFloat(object target, string name, float fallback)
        {
            var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return property?.GetValue(target, null) is float value ? value : fallback;
        }
    }

    [HarmonyPatch]
    internal static class GhiSettlementFinalizerPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("GovernorsHandleIssues.GovernorsHandleIssuesCampaignBehavior"), "HandleSettlementIssues");

        private static Exception Finalizer(object __instance, Exception __exception)
        {
            Clear(__instance, "<CurrentIssue>k__BackingField", null);
            Clear(__instance, "<CurrentGovernor>k__BackingField", null);
            Clear(__instance, "<SolvingForAI>k__BackingField", false);
            Clear(__instance, "<CallingSuccessConsequence>k__BackingField", false);
            Clear(__instance, "<CallingFailureConsequence>k__BackingField", false);
            if (__exception != null) Diagnostics.Report("Governor issue resolution", __exception, true);
            return null;
        }

        private static void Clear(object instance, string fieldName, object value)
        {
            try { AccessTools.Field(instance?.GetType(), fieldName)?.SetValue(instance, value); }
            catch { }
        }
    }

    [HarmonyPatch]
    internal static class LordConversationConditionPatch
    {
        private static MethodBase TargetMethod() => LegacyBridge.FindCompilerMethod("LordSurrender.LSBehavior", "<OnSessionLaunched_c1>b__1_0");

        private static bool Prefix(ref bool __result)
        {
            __result = LordDefectionRules.CanRecruitConversationTarget(out _);
            return false;
        }
    }

    [HarmonyPatch]
    internal static class LordConversationConsequencePatch
    {
        private static MethodBase TargetMethod() => LegacyBridge.FindCompilerMethod("LordSurrender.LSBehavior", "<OnSessionLaunched_c1>b__1_4");

        private static bool Prefix()
        {
            if (!LordDefectionRules.CanRecruitConversationTarget(out var reason))
            {
                Diagnostics.DisplayOnce("lord.conversation.invalid." + reason, reason, Colors.Red);
                return false;
            }
            LordDefectionRules.TryRecruit(Hero.OneToOneConversationHero?.Clan, true);
            return false;
        }
    }

    [HarmonyPatch]
    internal static class LordWeeklyTickSuppressPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("LordSurrender.LSBehavior"), "WeeklyTick");
        private static bool Prefix() => false;
    }

    internal static class SafetyPatchInstaller
    {
        internal static void InstallFriendlyFire(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(Mission), "CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase");
            var oldPrefix = AccessTools.Method(AccessTools.TypeByName("XorberaxLegacy.Patches.FriendlyFirePatch"), "Prefix");
            if (target == null) return;
            if (oldPrefix != null) harmony.Unpatch(target, oldPrefix);
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(SafeFriendlyFirePatch), nameof(SafeFriendlyFirePatch.Prefix)));
        }

        internal static void InstallEmotionalDailyFinalizers(Harmony harmony)
        {
            var finalizer = new HarmonyMethod(typeof(EmotionalServiceFinalizer), nameof(EmotionalServiceFinalizer.Finalizer));
            foreach (var type in LegacyBridge.GetTypesSafe())
            {
                if (type?.Namespace == null || !type.Namespace.StartsWith("EmotionalLife", StringComparison.Ordinal)) continue;
                if (!type.Name.EndsWith("Service", StringComparison.Ordinal)) continue;
                var method = AccessTools.Method(type, "OnDailyTick");
                if (method != null) harmony.Patch(method, finalizer: finalizer);
            }
        }
    }

    internal static class SafeFriendlyFirePatch
    {
        public static bool Prefix(Mission __instance, ref bool __result, Agent __0, Agent __1)
        {
            try
            {
                if (!(LegacyBridge.ReadSetting("XorberaxLegacy.Settings.Settings", "EnableFriendlyFireMod", false) is bool enabled) || !enabled) return true;
                if (__instance == null || (__instance.SceneName?.Contains("training") ?? false)) return true;
                var attacker = __0;
                var victim = __1;
                if (attacker == null || victim == null || !victim.IsHuman) return true;
                var mode = (int)__instance.Mode;
                if (mode == 2 && !Read("EnableFriendlyFireBattles", true)) return true;
                if (mode == 3 && !Read("EnableFriendlyFireHideoutDuels", false)) return true;
                if (mode == 7 && !Read("EnableFriendlyFireTournaments", false)) return true;
                if (mode == 0 && !Read("EnableFriendlyFireTowns", false)) return true;
                if (Read("EnableFriendlyFireRangedOnly", false) && !attacker.IsMount && attacker.HorseCreationKey == null)
                {
                    var weapon = attacker.GetWieldedWeaponInfo(0);
                    if (!weapon.IsValid || weapon.IsMeleeWeapon) return true;
                }
                if (Read("EnableCutThroughEveryoneMod", false) && Read("EnableFriendlyUnitsBlockCutThrough", true)) return true;
                if (attacker.Team == null || victim.Team == null || !attacker.Team.IsValid || !victim.Team.IsValid) return true;
                __result = false;
                return false;
            }
            catch { return true; }
        }

        private static bool Read(string property, bool fallback)
        {
            var value = LegacyBridge.ReadSetting("XorberaxLegacy.Settings.Settings", property, fallback);
            return value is bool result ? result : fallback;
        }
    }

    internal static class EmotionalServiceFinalizer
    {
        public static Exception Finalizer(MethodBase __originalMethod, Exception __exception)
        {
            if (__exception != null) Diagnostics.Report("EmotionalLife." + __originalMethod?.DeclaringType?.Name, __exception, false);
            return null;
        }
    }
}
