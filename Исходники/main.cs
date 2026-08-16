// Точка входа
using MelonLoader;

[assembly: MelonInfo(typeof(jsb_new.Main), "JSAB Extra Stuff", "4.1.0", "dveroeb")]
[assembly: MelonGame(null, null)]

namespace jsb_new
{
    public class Main : MelonMod
    {
        private HarmonyLib.Harmony? _harmony;

        public override void OnInitializeMelon()
        {
            _harmony = new HarmonyLib.Harmony("com.dveroeb.jsb_extra_stuff");

            ModuleRegistry.InitializeAll(_harmony);
            SettingsMenu.Initialize();

            LoggerInstance.Msg("==== INIT: JSAB Extra Stuff Ready (Press K for Settings) ====");
        }

        public override void OnUpdate() => ModuleRegistry.UpdateAll();

        public override void OnGUI() => ModuleRegistry.GUIAll();
    }
}
