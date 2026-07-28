using HarmonyLib;
using Il2Cpp;

namespace jsb_new
{
    public static class AutoDash
    {
        private static bool _enabled = false;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    DebugStrings.Log($"AutoDash: {(_enabled ? "ON" : "OFF")}");
                }
            }
        }

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            // Патчим listenToController вместо isDashingInput,
            // чтобы избежать проблем с инлайнингом методов в Il2Cpp
            harmony.Patch(
                AccessTools.Method(typeof(HeroInputLocal), "listenToController"),
                          postfix: new HarmonyMethod(typeof(AutoDash), nameof(AutoDash.OnListenToController))
            );

            ModuleRegistry.RegisterCheckbox(
                "Optional Stuff",
                "Auto Dash",
                () => Enabled,
                                            (newValue) => { Enabled = newValue; },
                                            order: 25
            );

            DebugStrings.Log("AutoDash Ready");
        }

        private static void OnListenToController(HeroInputLocal __instance)
        {
            // Если мод выключен или игрок не существует — ничего не делаем
            if (!Enabled || __instance == null || __instance.metaPlayer == null)
            {
                return;
            }

            // Проверяем, зажата ли основная кнопка рывка
            bool isDashHeld = __instance.metaPlayer.isActionDown(ModelControlActionEnum.DASH);

            // Если не зажата основная, проверяем альтернативную (для One Hand Mode)
            if (!isDashHeld && MetaGameProgress.instance.metaOptions.metaOptionsSettings.oneHandMode)
            {
                isDashHeld = __instance.metaPlayer.isActionDown(ModelControlActionEnum.DASH_ALT);
            }

            // Если любая из кнопок зажата — принудительно устанавливаем флаг рывка
            if (isDashHeld)
            {
                __instance.hero.controlComponent.isDashing = true;
            }
        }
    }
}
