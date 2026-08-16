using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace jsb_new
{
    public static class DashCooldown
    {
        public static float DashCooldownValue { get; set; } = 10f;

        private const float DefaultCooldown = 10f;
        private const float Epsilon = 0.001f;

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

            ModuleRegistry.RegisterSlider(
                "Dash Cooldown",
                0f,     // min
                1f,     // max
                1.0f,   // defaultValue
                (val) =>
                {
                    DashCooldownValue = val * 10f;
                    DebugStrings.Log($"Dash cooldown set to {DashCooldownValue}");
                }
            );

            HUDManager.CreateHUD(
                key: "WhatDash",
                textGetter: () => "WHAT DASH?",
                                 baseColor: Color.white,
                                 pulseColor: Color.cyan,
                                 activeGetter: () => DashCooldownValue <= Epsilon,
                                 height: 35f
            );

            HUDManager.CreateHUD(
                key: "ModdedDash",
                textGetter: () => "MODDED DASH!",
                                 baseColor: Color.white,
                                 pulseColor: Color.red,
                                 activeGetter: () => DashCooldownValue > Epsilon && DashCooldownValue < DefaultCooldown - Epsilon,
                                 height: 35f
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
