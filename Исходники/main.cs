using MelonLoader;
[assembly: MelonInfo(typeof(jsb_new.Main), "JSAB Extra Stuff", "3.0.0", "dveroeb")]
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
            UI.BuildSettingsMenu(_harmony);

            LoggerInstance.Msg("==== INIT. IMGUI Settings ready (K to open).");
            // LoggerInstance.Msg($"[jsb_new] Unity version: {Application.unityVersion}");
            // LoggerInstance.Msg($"[jsb_new] App version: {Application.version}");
            // LoggerInstance.Msg($"[jsb_new] Platform: {Application.platform}");
        }

        public override void OnUpdate() => ModuleRegistry.UpdateAll();

        public override void OnGUI() => ModuleRegistry.GUIAll();
    }
}
