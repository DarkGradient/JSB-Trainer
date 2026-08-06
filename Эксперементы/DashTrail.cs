#pragma warning disable CS8618
#pragma warning disable CS8600
#pragma warning disable CS8602

using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace jsb_new
{
    public static class DashTrail
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("DashTrail");
            set => ModuleRegistry.SetActive("DashTrail", value);
        }

        // Throttle, чтобы не спавнить partикулы каждый кадр (60/сек — перебор)
        private static float _trailTimer = 0f;
        private const float TrailInterval = 0.03f; // ~33 партикул-волны в секунду во время дэша

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_StopDash_ImpactBurst));

            ModuleRegistry.RegisterCheckbox(
                category: "Optional Stuff",
                name: "Enhanced Dash Trail",
                getter: () => Enabled,
                                            setter: (newValue) => { Enabled = newValue; },
                                            isLocked: null,
                                            order: 71 // сразу после Dash Cooldown (70)
            );

            DebugStrings.Log("DashTrail initialized");
        }

        // Возвращает Hero локального игрока, или null если недоступен/не в геймплее
        private static Hero? GetLocalHero()
        {
            try
            {
                if (PlayerManager.instance == null) return null;
                var firstLocalPlayer = PlayerManager.instance.GetFirstLocalPlayer();
                if (firstLocalPlayer == null) return null;
                return Hero.getHeroFromModelPlayer(firstLocalPlayer.modelPlayer);
            }
            catch
            {
                return null;
            }
        }

        public static void Update()
        {
            if (!Enabled) return;

            try
            {
                Hero? hero = GetLocalHero();
                if (hero == null || hero.destroyed) return;

                var dash = hero.dashComponent;
                var particule = hero.particuleComponent;
                if (dash == null || particule == null) return;

                if (dash.isDashing)
                {
                    _trailTimer += Time.unscaledDeltaTime;
                    if (_trailTimer >= TrailInterval)
                    {
                        _trailTimer = 0f;
                        // Переиспользуем штатный публичный метод игры — та же система
                        // партиклов/цвета/трейла, что и в ванильном рывке, просто чаще.
                        particule.createDashParticule();
                    }
                }
                else
                {
                    _trailTimer = 0f;
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"[DashTrail] Update failed: {ex.Message}");
            }
        }

        // "Бах" в момент окончания рывка — крупная разовая вспышка на месте остановки
        [HarmonyPatch(typeof(HeroDashComponent), "stopDash")]
        private static class Patch_StopDash_ImpactBurst
        {
            static void Postfix(HeroDashComponent __instance)
            {
                if (!Enabled) return;

                try
                {
                    Hero? hero = __instance.actor as Hero;
                    if (hero == null || hero.destroyed) return;

                    // Переиспользуем штатный метод целиком - он сам создаёт вспышку
                    // + партиклы нужным ассетом, без необходимости ссылаться на
                    // ModelFlashAnimationEnum напрямую (сложности с неймспейсом interop-сборки).
                    hero.particuleComponent?.createDashParticule();
                }
                catch (Exception ex)
                {
                    MelonLoader.MelonLogger.Error($"[DashTrail] Impact burst failed: {ex.Message}");
                }
            }
        }
    }
}
