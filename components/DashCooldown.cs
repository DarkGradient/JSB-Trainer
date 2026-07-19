using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace jsb_new
{
    public static class DashCooldown
    {
        public static float DashCooldownValue { get; set; } = 10f;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            try
            {
                var original = AccessTools.Method(typeof(HeroDashComponent), "update");
                var postfix = new HarmonyMethod(typeof(DashCooldown).GetMethod(nameof(HeroDashComponent_Update_Postfix)));

                harmony.Patch(original, postfix: postfix);
                DebugStrings.Log("Dash cooldown patch: success");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"==== DASH COOLDOWN PATCH: FAILED - {ex.Message} ====");
            }

            // Регистрирует свой слайдер
            ModuleRegistry.RegisterSlider("Optional Stuff", "Dash Cooldown", 1.0f,
                (val) =>
                {
                    DashCooldownValue = val * 10f;
                    DebugStrings.Log($"Dash cooldown set to {DashCooldownValue}");
                },
                order: 70
            );
        }

        public static void HeroDashComponent_Update_Postfix(HeroDashComponent __instance)
        {
            if (__instance.dashCooldown != null)
            {
                __instance.dashCooldown.waitMax = DashCooldownValue;
            }
        }
    }
}
