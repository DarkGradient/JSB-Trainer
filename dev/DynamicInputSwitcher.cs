using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace jsb_new
{
    public static class DynamicInputSwitcher
    {
        private static bool _enabled = false;

        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        private static float _lastSwitchTime = 0f;
        private const float SWITCH_COOLDOWN = 0.25f;
        private static int _lastControllerId = 0;

        // Вызывается автоматически при старте через ModuleRegistry.InitializeAll
        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            // Патчим ТОЛЬКО проверенные безопасные методы Il2Cpp
            TryPatch(harmony, typeof(MetaPlayer), nameof(MetaPlayer.unassignKeyboard), nameof(Prefix_UnassignKeyboard));
            TryPatch(harmony, typeof(MetaPlayer), nameof(MetaPlayer.unassignController), nameof(Prefix_UnassignController));
            TryPatch(harmony, typeof(InputManager), nameof(InputManager.getAnyPlayerWantsToJoinWithAnyButton), nameof(Prefix_BlockJoinInt));

            // Регистрируем чекбокс в меню
            ModuleRegistry.RegisterCheckbox(
                "Optional Stuff",
                "Dynamic Input Switcher",
                () => Enabled,
                                            (newValue) => { Enabled = newValue; },
                                            order: 26
            );

            DebugStrings.Log("DynamicInputSwitcher Ready");
        }

        private static void TryPatch(HarmonyLib.Harmony harmony, System.Type targetType, string methodName, string patchMethodName)
        {
            try
            {
                var target = AccessTools.Method(targetType, methodName);
                if (target == null)
                {
                    MelonLogger.Warning($"[DynamicInputSwitcher] Target method {targetType?.Name}.{methodName} not found. Skipping.");
                    return;
                }

                var prefix = AccessTools.Method(typeof(DynamicInputSwitcher), patchMethodName);
                if (prefix == null)
                {
                    MelonLogger.Error($"[DynamicInputSwitcher] Prefix method {patchMethodName} not found.");
                    return;
                }

                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                DebugStrings.Log($"[DynamicInputSwitcher] Successfully patched {targetType?.Name}.{methodName}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[DynamicInputSwitcher] Error patching {targetType?.Name}.{methodName}: {ex.Message}");
            }
        }

        // Автоматически вызывается каждый кадр всей игры через ModuleRegistry.UpdateAll
        public static void Update()
        {
            if (!Enabled || PlayerManager.instance == null)
                return;

            MetaPlayer p1 = PlayerManager.instance.GetFirstLocalPlayer();
            if (p1 == null)
                return;

            if (Time.realtimeSinceStartup - _lastSwitchTime < SWITCH_COOLDOWN)
                return;

            // Если UI Игрока 1 в режиме ГЕЙМПАДА, но нажата КЛАВИАТУРА
            if (p1.modelPlayerControlType.isController())
            {
                int currentId = p1.getControllerId();
                if (currentId != -1)
                {
                    _lastControllerId = currentId;
                }

                if (Input.anyKeyDown)
                {
                    p1.setKeyboard();
                    _lastSwitchTime = Time.realtimeSinceStartup;
                }
            }
            // Если UI Игрока 1 в режиме КЛАВИАТУРЫ, но нажат ГЕЙМПАД
            else if (p1.modelPlayerControlType.isKey())
            {
                if (IsAnyJoystickButtonPressed())
                {
                    p1.setController(_lastControllerId);
                    _lastSwitchTime = Time.realtimeSinceStartup;
                }
            }
        }

        // --- БЕЗОПАСНЫЕ ПАТЧИ ---

        private static bool Prefix_BlockJoinInt(ref int __result)
        {
            if (Enabled)
            {
                __result = -1;
                return false; // Блокируем запросы присоединения
            }
            return true;
        }

        private static bool Prefix_UnassignKeyboard(MetaPlayer __instance)
        {
            if (Enabled && __instance != null && __instance.isFirstPlayer())
            {
                return false; // Не отдаем клавиатуру в SystemPlayer
            }
            return true;
        }

        private static bool Prefix_UnassignController(MetaPlayer __instance)
        {
            if (Enabled && __instance != null && __instance.isFirstPlayer())
            {
                return false; // Не отдаем геймпад в SystemPlayer
            }
            return true;
        }

        private static bool IsAnyJoystickButtonPressed()
        {
            for (int i = (int)KeyCode.JoystickButton0; i <= (int)KeyCode.JoystickButton19; i++)
            {
                if (Input.GetKey((KeyCode)i))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
