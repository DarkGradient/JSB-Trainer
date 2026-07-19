using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(jsb_new.Main), "JSAB Trainer", "2.0.0", "dveroeb")]
[assembly: MelonGame(null, null)]

namespace jsb_new
{
    public class Main : MelonMod
    {
        private HarmonyLib.Harmony? _harmony;

        public override void OnInitializeMelon()
        {
            _harmony = new HarmonyLib.Harmony("com.dveroeb.jsb_renewed");

            ModuleRegistry.InitializeAll(_harmony);
            UI.BuildSettingsMenu(_harmony);

            LoggerInstance.Msg("==== JSAB TRAINER LOADED ====");
        }

        public override void OnUpdate()
        {
            ModuleRegistry.UpdateAll();
        }

        public override void OnGUI()
        {
            ModuleRegistry.GUIAll();
        }
    }
}
