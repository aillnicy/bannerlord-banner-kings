using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace AillGameplaySuite
{
    public sealed class UnifiedSubModule : MBSubModuleBase
    {
        private readonly Dictionary<string, object> _modules = new Dictionary<string, object>(StringComparer.Ordinal);
        private Harmony _safetyHarmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Diagnostics.Guard("bootstrap.create", CreateLegacyModules);
            Diagnostics.Guard("emotional.ui", () => LegacyBridge.Invoke(_modules, "EmotionalLife", "OnSubModuleLoad"));
            Diagnostics.Guard("patch.original", PatchOriginalNamespaces);
            Diagnostics.Guard("patch.safety", PatchSafetyLayer);
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            Diagnostics.Guard("emotional.compatibility", () => LegacyBridge.Invoke(_modules, "EmotionalLife", "OnBeforeInitialModuleScreenSetAsRoot"));
            Diagnostics.Guard("lord.settings", () => LegacyBridge.Invoke(_modules, "LordSurrender", "OnBeforeInitialModuleScreenSetAsRoot"));
            Diagnostics.Guard("xor.settings", () => LegacyBridge.Invoke(_modules, "XorberaxLegacy", "OnBeforeInitialModuleScreenSetAsRoot"));
            Diagnostics.Guard("mcm.organizer", () => LegacyBridge.Invoke(_modules, "AillMcmOrganizer", "OnBeforeInitialModuleScreenSetAsRoot"));
            Diagnostics.DisplayOnce("loaded", "Aill Gameplay Suite v1.0.0 loaded: unified lifecycle and safety layer active.", Colors.Green);
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            if (!(gameStarter is CampaignGameStarter campaignStarter)) return;

            Diagnostics.Guard("emotional.behaviors", () =>
            {
                LegacyBridge.AddBehavior(campaignStarter, "EmotionalLife.Behaviors.EmotionalLifeCampaignBehavior");
                LegacyBridge.AddBehavior(campaignStarter, "EmotionalLife.Behaviors.NpcAutonomyBehavior");
            });
            Diagnostics.Guard("governor.behavior", () => LegacyBridge.Invoke(_modules, "GovernorsHandleIssues", "OnGameStart", game, gameStarter));
            Diagnostics.Guard("lord.behavior", () => LegacyBridge.Invoke(_modules, "LordSurrender", "InitializeGameStarter", game, gameStarter));
            Diagnostics.Guard("xor.behaviors", () => LegacyBridge.Invoke(_modules, "XorberaxLegacy", "OnGameStart", game, gameStarter));
            campaignStarter.AddBehavior(new UnifiedCampaignBehavior());
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            Diagnostics.GuardQuiet("xor.tick", () => LegacyBridge.Invoke(_modules, "XorberaxLegacy", "OnApplicationTick", dt));
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            Diagnostics.Guard("xor.mission", () => LegacyBridge.Invoke(_modules, "XorberaxLegacy", "OnMissionBehaviorInitialize", mission));
        }

        private void CreateLegacyModules()
        {
            _modules["EmotionalLife"] = LegacyBridge.Create("EmotionalLife.EmotionalLifeSubModule");
            _modules["GovernorsHandleIssues"] = LegacyBridge.Create("GovernorsHandleIssues.SubModule");
            _modules["LordSurrender"] = LegacyBridge.Create("LordSurrender.SubModule");
            _modules["XorberaxLegacy"] = LegacyBridge.Create("XorberaxLegacy.Main");
            _modules["AillMcmOrganizer"] = LegacyBridge.Create("AillMcmOrganizer.OrganizerSubModule");
        }

        private void PatchOriginalNamespaces()
        {
            LegacyBridge.PatchNamespace("emotional.life.v2", "EmotionalLife", null);
            LegacyBridge.PatchNamespace("carbon.governorshandleissues", "GovernorsHandleIssues", null);
            LegacyBridge.PatchNamespace("lord.surrender", "LordSurrender", null);
            LegacyBridge.PatchNamespace("XorberaxLegacy", "XorberaxLegacy", "XorberaxLegacy.Patches.FriendlyFirePatch");
            LegacyBridge.PatchNamespace("Aill.McmOrganizer", "AillMcmOrganizer", null);
        }

        private void PatchSafetyLayer()
        {
            _safetyHarmony = new Harmony("aill.gameplay.suite.safety");
            foreach (var type in new[]
            {
                typeof(CompatibilityTraitXpPatch),
                typeof(SafeSettlementDailyTickPatch),
                typeof(GhiSettlementFinalizerPatch),
                typeof(LordConversationConditionPatch),
                typeof(LordConversationConsequencePatch),
                typeof(LordWeeklyTickSuppressPatch)
            })
            {
                _safetyHarmony.CreateClassProcessor(type).Patch();
            }
            SafetyPatchInstaller.InstallFriendlyFire(_safetyHarmony);
            SafetyPatchInstaller.InstallEmotionalDailyFinalizers(_safetyHarmony);
        }
    }

    internal static class LegacyBridge
    {
        private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal static Assembly Assembly => typeof(UnifiedSubModule).Assembly;
        internal static object Create(string fullName) => Activator.CreateInstance(Assembly.GetType(fullName, true), true);

        internal static void Invoke(IDictionary<string, object> modules, string key, string methodName, params object[] args)
        {
            if (!modules.TryGetValue(key, out var instance) || instance == null) return;
            var method = FindMethod(instance.GetType(), methodName, args?.Length ?? 0);
            if (method == null) throw new MissingMethodException(instance.GetType().FullName, methodName);
            try { method.Invoke(instance, args); }
            catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
        }

        internal static void AddBehavior(CampaignGameStarter starter, string fullName)
        {
            var behavior = Activator.CreateInstance(Assembly.GetType(fullName, true), true) as CampaignBehaviorBase;
            if (behavior == null) throw new InvalidOperationException(fullName + " is not a CampaignBehaviorBase");
            starter.AddBehavior(behavior);
        }

        internal static void PatchNamespace(string harmonyId, string namespacePrefix, string excludedType)
        {
            var harmony = new Harmony(harmonyId);
            foreach (var type in GetTypesSafe().Where(t => t != null && t.Namespace != null && t.Namespace.StartsWith(namespacePrefix, StringComparison.Ordinal)))
            {
                if (string.Equals(type.FullName, excludedType, StringComparison.Ordinal) || !HasHarmonyPatch(type)) continue;
                try { harmony.CreateClassProcessor(type).Patch(); }
                catch (Exception ex) { Diagnostics.Report("Patch skipped: " + type.FullName, ex, false); }
            }
        }

        internal static Type[] GetTypesSafe()
        {
            try { return Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null).ToArray(); }
        }

        internal static object ReadSetting(string typeName, string propertyName, object fallback)
        {
            try
            {
                var type = Assembly.GetType(typeName, false) ?? AccessTools.TypeByName(typeName);
                var instance = type?.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null, null);
                var property = instance?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return property?.GetValue(instance, null) ?? fallback;
            }
            catch { return fallback; }
        }

        internal static MethodInfo FindCompilerMethod(string parentType, string methodName)
        {
            var type = Assembly.GetType(parentType, false);
            if (type == null) return null;
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                var method = nested.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).FirstOrDefault(m => m.Name == methodName);
                if (method != null) return method;
            }
            return null;
        }

        private static MethodInfo FindMethod(Type type, string name, int parameterCount)
        {
            for (var cursor = type; cursor != null; cursor = cursor.BaseType)
            {
                var method = cursor.GetMethods(AllInstance).FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameterCount);
                if (method != null) return method;
            }
            return null;
        }

        private static bool HasHarmonyPatch(Type type)
        {
            try
            {
                if (type.GetCustomAttributesData().Any(a => a.AttributeType.FullName == typeof(HarmonyPatch).FullName)) return true;
                return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).Any(m => m.GetCustomAttributesData().Any(a => a.AttributeType.FullName == typeof(HarmonyPatch).FullName));
            }
            catch { return false; }
        }
    }
}
