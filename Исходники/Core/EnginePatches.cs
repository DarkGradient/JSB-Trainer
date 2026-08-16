// Логгер, разблокировка дебага (без isFastSkip), пропуск интро и блокировка хоткеев
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace jsb_new
{
    public static class DebugStrings
    {
        public static bool Enabled = true;
        public static void Log(string message)
        {
            if (Enabled) MelonLogger.Msg($"[DEBUG] {message}");
        }
    }

    public static class DebugUnlock
    {
        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(
                typeof(MainGame).GetMethod("init"),
                prefix: new HarmonyMethod(typeof(DebugUnlock), nameof(OnInit))
            );
            DebugStrings.Log("DebugUnlock initialized");
        }

        public static void OnInit()
        {
            VersionInfo.isDebug = true; // isFastSkip УБРАН
        }
    }

    public static class IntroSkipper
    {
        private static bool _introSkipped = false;

        public static void Update()
        {
            if (MainGame.instance == null) return;
            if (!_introSkipped) SkipLongIntro();
        }

        private static void SkipLongIntro()
        {
            _introSkipped = true;
            try
            {
                MainGame.hardKillGameOnNextStableMoment(new Callback(new System.Action(UI_MainMenu02.loadAndCreateSkipIntro)));
                DebugStrings.Log("Intro skipped: success");
            }
            catch (System.Exception ex)
            {
                _introSkipped = false;
                MelonLogger.Error($"==== INTRO SKIPPED: FAILED - {ex.Message} ====");
            }
        }
    }

    public static class DebugShortcutBlocker
    {
        public static bool BLOCK_F1_DEBUG_PANEL = true;
        public static bool BLOCK_F2_GC = true;
        public static bool BLOCK_F3_KILL_ALL_HEROES = true;
        public static bool BLOCK_F4_CHEAT_UI = true;
        public static bool BLOCK_F_FULLSCREEN = true;
        public static bool BLOCK_F5_MUSIC_SPEED = true;
        public static bool BLOCK_F6_MP_START_NEW_RUN = true;
        public static bool BLOCK_F7_MP_VOTE = true;
        public static bool BLOCK_F8_SCREEN_PREV = true;
        public static bool BLOCK_F9_VSYNC_TOGGLE = true;
        public static bool BLOCK_DELETE_HARD_KILL = true;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(
                typeof(DebugShortcut).GetMethod(nameof(DebugShortcut.update)),
                prefix: new HarmonyMethod(typeof(DebugShortcutBlocker), nameof(Update_Prefix))
            );
            MelonLogger.Msg("[DebugShortcutBlocker] Debug hotkey blocker active.");
        }

        private static bool Update_Prefix()
        {
            if (BLOCK_F1_DEBUG_PANEL && KeyManager.IsKeyPressed(Keyboard.F1)) return false;
            if (BLOCK_F2_GC && KeyManager.IsKeyPressed(Keyboard.F2)) return false;
            if (BLOCK_F3_KILL_ALL_HEROES && KeyManager.IsKeyPressed(Keyboard.F3)) return false;
            if (BLOCK_F4_CHEAT_UI && KeyManager.IsKeyPressed(Keyboard.F4)) return false;
            if (BLOCK_F_FULLSCREEN && KeyManager.IsKeyPressed(Keyboard.F)) return false;
            if (BLOCK_F5_MUSIC_SPEED && (KeyManager.IsKeyPressed(Keyboard.F5) || KeyManager.IsKeyReleased(Keyboard.F5))) return false;
            if (BLOCK_F6_MP_START_NEW_RUN && KeyManager.IsKeyPressed(Keyboard.F6)) return false;
            if (BLOCK_F7_MP_VOTE && KeyManager.IsKeyPressed(Keyboard.F7)) return false;
            if (BLOCK_F8_SCREEN_PREV && KeyManager.IsKeyPressed(Keyboard.F8)) return false;
            if (BLOCK_F9_VSYNC_TOGGLE && KeyManager.IsKeyPressed(Keyboard.F9)) return false;
            if (BLOCK_DELETE_HARD_KILL && KeyManager.IsKeyPressed(Keyboard.DELETE)) return false;

            return true;
        }
    }
}
