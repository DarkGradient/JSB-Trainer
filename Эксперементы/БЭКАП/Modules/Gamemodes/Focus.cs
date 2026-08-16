using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace jsb_new
{
    public static class FocusMode
    {
        public const string FEATURE_NAME = "Focus Mode (Shift)";
        public const string SLIDER_NAME = "Focus Speed Multiplier";

        // Значение замедления по умолчанию (0.4 = 40% от нормальной скорости)
        public static float SpeedMultiplier = 0.4f;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            // 1. Регистрируем чекбокс включения/выключения
            ModuleRegistry.RegisterCheckbox(
                FEATURE_NAME,
                () => ModuleRegistry.IsActive(FEATURE_NAME),
                (enabled) =>
                {
                    ModuleRegistry.SetActive(FEATURE_NAME, enabled);
                    HUDManager.CreateToast(
                        enabled ? "FOCUS MODE: ON" : "FOCUS MODE: OFF",
                        enabled ? Color.cyan : Color.gray,
                        1.5f
                    );
                }
            );

            // 2. Регистрируем слайдер настройки мощности замедления (от 10% до 90% скорости)
            ModuleRegistry.RegisterSlider(
                SLIDER_NAME,
                0.1f, // min
                0.9f, // max
                0.4f, // default
                (val) =>
                {
                    SpeedMultiplier = val;
                }
            );

            DebugStrings.Log($"[FocusMode] Initialized!");
        }

        // --- HARMONY PATCH ---
        // Перехватываем метод применения ввода движения в HeroControl
        [HarmonyPatch(typeof(HeroControl), nameof(HeroControl.applyMoveInputToHero))]
        public static class HeroControl_ApplyMoveInput_Patch
        {
            [HarmonyPrefix]
            public static void Prefix(HeroControl __instance)
            {
                // Проверяем, включен ли модуль
                if (!ModuleRegistry.IsActive(FEATURE_NAME))
                    return;

                if (__instance == null || __instance.dashComponent == null)
                    return;

                // Если игрок делает дэш, замедление не должно перебивать скорость дэша
                if (__instance.dashComponent.isDashing)
                    return;

                // Проверяем зажатие клавиш Shift
                bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                if (isShiftPressed)
                {
                    // Вычисляем целевую сниженную скорость
                    float targetFocusSpeed = __instance.speedNormal * SpeedMultiplier;

                    // Плавно интерполируем скорость к замедленной
                    __instance.speed += (targetFocusSpeed - __instance.speed) * 0.2f;
                }
            }
        }
    }
}
