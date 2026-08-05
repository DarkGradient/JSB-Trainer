using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace jsb_new
{
    public static class Noclip
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
                    DebugStrings.Log($"Noclip: {(_enabled ? "ON" : "OFF")}");
                }
            }
        }

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(
                typeof(HeroCollisionWithEnemy).GetMethod("onCollision"),
                          prefix: new HarmonyMethod(typeof(Noclip), nameof(OnCollision))
            );

            ModuleRegistry.RegisterCheckbox("Noclip",
                                            () => Enabled,
                                            (newValue) => { Enabled = newValue; }
            );

            // РЕГИСТРИРУЕМ ПЛАШКУ В HUD-МЕНЕДЖЕРЕ!
            // Мы передаем геттер текста, цвет мигания (белый в красный), геттер активности и высоту
            HUDManager.CreateHUD(
                key: "Noclip",
                textGetter: () => "NOCLIP ON",
                                          baseColor: Color.white,
                                          pulseColor: new Color(1f, 0.15f, 0.15f, 1f),
                                          activeGetter: () => Enabled,
                                          height: 35f // Высота под 1 строчку
                                          // order: 20
            );

            DebugStrings.Log("Noclip initialized");
        }

        public static void Update()
        {
            if (Input.GetKeyDown(KeyCode.RightShift))
            {
                Enabled = !Enabled;
            }
        }

        public static bool OnCollision() => !Enabled;
    }
}
