using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace AillGameplaySuite
{
    public sealed class SubModule : MBSubModuleBase
    {
        private const string HarmonyId = "aill.gameplay.suite";
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            SuiteRuntime.RunGuarded("Harmony", () =>
            {
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll();
            });
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            if (gameStarter is CampaignGameStarter campaignStarter)
            {
                campaignStarter.AddBehavior(new SuiteCampaignBehavior());
            }
        }
    }
}
