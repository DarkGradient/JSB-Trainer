using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace jsb_new
{
    public static class DebugShortcutBlocker
    {
        // ====================== ФЛАГИ БЛОКИРОВКИ ======================
        // true  = команда ЗАБЛОКИРОВАНА (нажатие игнорируется)
        // false = команда РАБОТАЕТ (передаётся в оригинал)
        // ==============================================================

        public static bool BLOCK_F1_DEBUG_PANEL          = true;  // F1 — открытие дебаг-панели
        public static bool BLOCK_F2_GC                   = true;  // F2 — принудительный GC.Collect()
        public static bool BLOCK_F3_KILL_ALL_HEROES      = true;  // F3 — убить всех героев
        public static bool BLOCK_F4_CHEAT_UI             = true;  // F4 — UI_CheatDetected
        public static bool BLOCK_F_FULLSCREEN            = true;  // F — toggle fullscreen
        public static bool BLOCK_F5_MUSIC_SPEED          = true;  // F5 — pitch 2x / 1x
        public static bool BLOCK_F6_MP_START_NEW_RUN     = true;  // F6 — хост: start new run
        public static bool BLOCK_F7_MP_VOTE              = true;  // F7 — хост: принудительный vote
        public static bool BLOCK_F8_SCREEN_PREV          = true;  // F8 — предыдущий экран + сейв
        public static bool BLOCK_F9_VSYNC_TOGGLE         = true;  // F9 — переключение vsync
        public static bool BLOCK_F10_SKIP_TO_END         = true;  // F10 — перемотка музыки в конец
        public static bool BLOCK_DELETE_HARD_KILL        = true;  // DELETE — hardKillGame
        public static bool BLOCK_CLEAR_SAVE              = true;  // C+L+E+A+R — полный Clear() сейва
        public static bool BLOCK_NUMPAD_ADD_SKIP         = true;  // Numpad+ — скип чекпоинтов
        public static bool BLOCK_NUMPAD_SUB_LIVES        = true;  // Numpad- — setLives(1)
        public static bool BLOCK_END_GAMEOVER            = true;  // END — tryToGameOverSong

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(
                typeof(DebugShortcut).GetMethod(nameof(DebugShortcut.update)),
                          prefix: new HarmonyMethod(typeof(DebugShortcutBlocker), nameof(Update_Prefix))
            );

            MelonLogger.Msg("[DebugShortcutBlocker] Блокировщик дебаг-хоткеев успешно активирован.");
        }

        private static bool Update_Prefix()
        {
            // Проверяем: если нажата заблокированная клавиша — перехватываем её (return false).

            // --- F1 Debug panel ---
            if (BLOCK_F1_DEBUG_PANEL && KeyManager.IsKeyPressed(Keyboard.F1))
                return false;

            // --- F2 GC ---
            if (BLOCK_F2_GC && KeyManager.IsKeyPressed(Keyboard.F2))
                return false;

            // --- F3 Kill all heroes ---
            if (BLOCK_F3_KILL_ALL_HEROES && KeyManager.IsKeyPressed(Keyboard.F3))
                return false;

            // --- F4 Cheat UI ---
            if (BLOCK_F4_CHEAT_UI && KeyManager.IsKeyPressed(Keyboard.F4))
                return false;

            // --- F Fullscreen ---
            if (BLOCK_F_FULLSCREEN && KeyManager.IsKeyPressed(Keyboard.F))
                return false;

            // --- F5 Music speed ---
            if (BLOCK_F5_MUSIC_SPEED && (KeyManager.IsKeyPressed(Keyboard.F5) || KeyManager.IsKeyReleased(Keyboard.F5)))
                return false;

            // --- F6 / F7 Multiplayer ---
            if (BLOCK_F6_MP_START_NEW_RUN && KeyManager.IsKeyPressed(Keyboard.F6))
                return false;

            if (BLOCK_F7_MP_VOTE && KeyManager.IsKeyPressed(Keyboard.F7))
                return false;

            // --- F8 Screen prev ---
            if (BLOCK_F8_SCREEN_PREV && KeyManager.IsKeyPressed(Keyboard.F8))
                return false;

            // --- F9 VSync ---
            if (BLOCK_F9_VSYNC_TOGGLE && KeyManager.IsKeyPressed(Keyboard.F9))
                return false;

            // --- F10 Skip to near end ---
            if (BLOCK_F10_SKIP_TO_END && KeyManager.IsKeyPressed(Keyboard.F10))
                return false;

            // --- DELETE hard kill ---
            if (BLOCK_DELETE_HARD_KILL && KeyManager.IsKeyPressed(Keyboard.DELETE))
                return false;

            // --- CLEAR save (C + L + E + A + R) ---
            if (BLOCK_CLEAR_SAVE &&
                KeyManager.IsKeyDown(Keyboard.C) && KeyManager.IsKeyDown(Keyboard.L) &&
                KeyManager.IsKeyDown(Keyboard.E) && KeyManager.IsKeyDown(Keyboard.A) &&
                KeyManager.IsKeyDown(Keyboard.R))
            {
                return false;
            }

            // --- Numpad+ / Numpad- / END ---
            if (BLOCK_NUMPAD_ADD_SKIP && KeyManager.IsKeyReleased(Keyboard.NUMPAD_ADD))
                return false;

            if (BLOCK_NUMPAD_SUB_LIVES && KeyManager.IsKeyReleased(Keyboard.NUMPAD_SUBTRACT))
                return false;

            // --- END game over ---
            if (BLOCK_END_GAMEOVER && KeyManager.IsKeyReleased(Keyboard.END))
                return false;

            // Если ни одна из опасных клавиш НЕ нажата, возвращаем true:
            // Игра выполняет свой родной update() в штатном режиме, и меню загружается нормально!
            return true;
        }
    }
}
