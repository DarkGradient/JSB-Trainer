using HarmonyLib;
using Il2Cpp;
using Il2CppRewired;
using Il2CppRewired.ControllerExtensions;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace jsb_new
{
    public static class ControllerGameSyncModule
    {
        private static readonly Dictionary<int, float> _hitTimers = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> _dashTimers = new Dictionary<int, float>();

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroCollisionWithEnemy));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroDashComponent));
        }

        public static void Update()
        {
            if (!ReInput.isReady) return;

            var controllers = ReInput.controllers;
            if (controllers == null) return;

            var playerManager = PlayerManager.instance;
            if (playerManager == null) return;

            int joystickCount = controllers.joystickCount;
            for (int i = 0; i < joystickCount; i++)
            {
                var joystick = controllers.Joysticks[i];
                if (joystick == null) continue;

                int controllerId = joystick.id;
                var ds4 = joystick.GetExtension<IDualShock4Extension>();

                bool isHitting = _hitTimers.TryGetValue(controllerId, out float hitTimer) && hitTimer > 0f;
                bool isDashing = _dashTimers.TryGetValue(controllerId, out float dashTimer) && dashTimer > 0f;

                if (isHitting)
                {
                    _hitTimers[controllerId] = hitTimer - Time.deltaTime;
                    if (ds4 != null) ds4.SetLightColor(1f, 0f, 0f);
                    if (joystick.supportsVibration) joystick.SetVibration(1.0f, 1.0f);
                    continue;
                }

                if (isDashing)
                {
                    _dashTimers[controllerId] = dashTimer - Time.deltaTime;
                    if (joystick.supportsVibration) joystick.SetVibration(0.1f, 0.6f);
                }
                else if (joystick.supportsVibration && (_hitTimers.ContainsKey(controllerId) || _dashTimers.ContainsKey(controllerId)))
                {
                    joystick.StopVibration();
                    _hitTimers.Remove(controllerId);
                    _dashTimers.Remove(controllerId);
                }

                if (ds4 != null)
                {
                    var metaPlayer = playerManager.GetPlayerByControllerId(controllerId);
                    if (metaPlayer?.modelPlayer != null)
                    {
                        if (IsPlayerDead(metaPlayer.modelPlayer))
                        {
                            ds4.SetLightColor(0.8f, 0f, 0f); // Мертв - горит красным
                        }
                        else
                        {
                            Color playerColor = UintToColor(metaPlayer.modelPlayer.color);
                            ds4.SetLightColor(playerColor.r, playerColor.g, playerColor.b);
                        }
                    }
                }
            }
        }

        public static void OnHeroHit(Hero? hero)
        {
            if (hero?.metaPlayer == null) return;
            int controllerId = hero.metaPlayer.getControllerId();
            if (controllerId >= 0) _hitTimers[controllerId] = 0.4f;
        }

        public static void OnHeroDash(Hero? hero)
        {
            if (hero?.metaPlayer == null) return;
            int controllerId = hero.metaPlayer.getControllerId();
            if (controllerId >= 0) _dashTimers[controllerId] = 0.1f;
        }

        private static bool IsPlayerDead(ModelPlayer? modelPlayer)
        {
            if (modelPlayer == null) return false;

            try
            {
                // ЗАЩИТА: Проверяем, находимся ли мы в уровне (а не в меню)
                if (MainGame.instance == null ||
                    MainGame.instance.gameSceneManager == null ||
                    MainGame.instance.gameSceneManager.gameScene == null)
                {
                    return false;
                }

                var hero = Hero.getHeroFromModelPlayer(modelPlayer);
                if (hero?.lifeComponent != null && hero.lifeComponent.isDead) return true;

                if (MainGame.instance.gameSceneManager.gameScene.itemManager != null)
                {
                    var ghost = HeroGhost.getHeroFromModelPlayer(modelPlayer);
                    if (ghost != null && !ghost.destroyed) return true;
                }
            }
            catch
            {
                // Гасим любые ошибки IL2CPP при переходах между сценами
            }

            return false;
        }

        private static Color UintToColor(uint hexColor)
        {
            float r = ((hexColor >> 16) & 0xFF) / 255f;
            float g = ((hexColor >> 8) & 0xFF) / 255f;
            float b = (hexColor & 0xFF) / 255f;
            return new Color(r, g, b, 1f);
        }

        [HarmonyPatch(typeof(HeroCollisionWithEnemy), "hitByEnemy")]
        private static class Patch_HeroCollisionWithEnemy
        {
            static void Postfix(HeroCollisionWithEnemy? __instance)
            {
                if (__instance?.actor != null)
                {
                    var hero = __instance.actor.TryCast<Hero>();
                    if (hero != null) OnHeroHit(hero);
                }
            }
        }

        [HarmonyPatch(typeof(HeroDashComponent), "startDash")]
        private static class Patch_HeroDashComponent
        {
            static void Postfix(HeroDashComponent? __instance)
            {
                if (__instance?.actor != null)
                {
                    var hero = __instance.actor.TryCast<Hero>();
                    if (hero != null) OnHeroDash(hero);
                }
            }
        }
    }
}