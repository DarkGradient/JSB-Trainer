// using System;
// using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Il2Cpp;
using Il2CppRewired;
using Il2CppRewired.ControllerExtensions;

namespace jsb_new
{
    public static class ControllerGameSyncModule
    {
        // ── Debug ──────────────────────────────────────────────────────────────
        /// <summary>Set to true to log player color resolution every ~2 seconds.</summary>
        private const bool EnableColorDebug = false;

        // ── State ──────────────────────────────────────────────────────────────
        private static readonly Dictionary<int, float> _hitTimers  = new();
        private static readonly Dictionary<int, float> _dashTimers = new();

        private static FieldInfo? _onlinePlayerIdField;

        // ── Lifecycle ──────────────────────────────────────────────────────────
        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            ArgumentNullException.ThrowIfNull(harmony);
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

                bool isHitting = _hitTimers.TryGetValue(controllerId, out float hitTimer)  && hitTimer  > 0f;
                bool isDashing = _dashTimers.TryGetValue(controllerId, out float dashTimer) && dashTimer > 0f;

                var metaPlayer = playerManager.GetPlayerByControllerId(controllerId);
                bool isDead = metaPlayer?.modelPlayer != null && IsPlayerDead(metaPlayer.modelPlayer);

                // ── 1. Highest priority: damage / death ───────────────────────
                if (isHitting || isDead)
                {
                    if (isHitting)
                        _hitTimers[controllerId] = hitTimer - Time.deltaTime;

                    ds4?.SetLightColor(1f, 0f, 0f); // bright red

                    if (joystick.supportsVibration)
                    {
                        if (isHitting) joystick.SetVibration(1.0f, 1.0f);
                        else           joystick.StopVibration();
                    }
                    continue;
                }

                // ── 2. Dash priority ──────────────────────────────────────────
                if (isDashing)
                {
                    _dashTimers[controllerId] = dashTimer - Time.deltaTime;
                    if (joystick.supportsVibration)
                        joystick.SetVibration(0.0f, 0.7f);
                    continue;
                }

                // ── 3. Background effects ─────────────────────────────────────
                Color targetColor = Color.white;
                float vibLeft  = 0f;
                float vibRight = 0f;

                if (OneHit.Enabled)
                {
                    float bpm          = OneHit.TrueOneHitEnabled ? 120f : 80f;
                    float beatInterval = 60f / bpm;
                    float t            = Time.unscaledTime % beatInterval;
                    bool  isBeat       = (t < 0.1f) || (t > 0.2f && t < 0.3f);

                    targetColor = isBeat
                        ? new Color(1f, 0f, 0f)
                        : new Color(0.15f, 0f, 0f);

                    if (isBeat) vibRight = 0.15f;
                }
                else if (OrangeSoul.Enabled)
                {
                    targetColor = new Color(1.0f, 0.4f, 0.0f);
                    vibLeft     = 0.03f;
                }
                else if (PurpleSoul.Enabled)
                {
                    targetColor = new Color(0.6f, 0.1f, 0.8f);
                }
                else if (metaPlayer != null)
                {
                    targetColor = GetPlayerColor(metaPlayer);

                    if (EnableColorDebug && Time.frameCount % 120 == 0 && metaPlayer.modelPlayer != null)
                        LogColorDebug(controllerId, metaPlayer, targetColor);
                }

                // Apply lightbar
                if (ds4 != null)
                {
                    Color calibrated = CalibrateForDS4Lightbar(targetColor);
                    ds4.SetLightColor(calibrated.r, calibrated.g, calibrated.b);
                }

                // Apply vibration
                if (joystick.supportsVibration)
                {
                    if (vibLeft > 0f || vibRight > 0f)
                        joystick.SetVibration(vibLeft, vibRight);
                    else
                        joystick.StopVibration();
                }

                // Cleanup expired timers
                if (!isHitting && _hitTimers.ContainsKey(controllerId))  _hitTimers.Remove(controllerId);
                if (!isDashing && _dashTimers.ContainsKey(controllerId)) _dashTimers.Remove(controllerId);
            }
        }

        // ── Public event hooks ─────────────────────────────────────────────────
        public static void OnHeroHit(Hero? hero)
        {
            if (hero?.metaPlayer == null) return;
            int controllerId = hero.metaPlayer.getControllerId();
            if (controllerId >= 0)
                _hitTimers[controllerId] = 0.4f;
        }

        public static void OnHeroDash(Hero? hero)
        {
            if (hero?.metaPlayer == null) return;
            int controllerId = hero.metaPlayer.getControllerId();
            if (controllerId >= 0)
                _dashTimers[controllerId] = 0.1f;
        }

        public static void PlayHapticClick(float leftMotor, float rightMotor, float durationSec = 0.1f)
        {
            if (!ReInput.isReady || ReInput.controllers == null) return;

            var player = ReInput.players.GetSystemPlayer();
            if (player == null) return;

            int joystickCount = player.controllers.joystickCount;
            for (int i = 0; i < joystickCount; i++)
            {
                var joystick = player.controllers.Joysticks[i];
                if (joystick != null && joystick.supportsVibration)
                {
                    joystick.SetVibration(0, leftMotor,  durationSec);
                    joystick.SetVibration(1, rightMotor, durationSec);
                }
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private static bool IsPlayerDead(ModelPlayer? modelPlayer)
        {
            if (modelPlayer == null) return false;

            try
            {
                if (MainGame.instance?.gameSceneManager?.gameScene == null)
                    return false;

                var hero = Hero.getHeroFromModelPlayer(modelPlayer);
                if (hero?.lifeComponent != null && hero.lifeComponent.isDead)
                    return true;

                if (MainGame.instance.gameSceneManager.gameScene.itemManager != null)
                {
                    var ghost = HeroGhost.getHeroFromModelPlayer(modelPlayer);
                    if (ghost != null && !ghost.destroyed)
                        return true;
                }
            }
            catch { /* ignore */ }

            return false;
        }

        private static Color GetPlayerColor(MetaPlayer metaPlayer)
        {
            try
            {
                // Online: colour comes from lobby slot (onlinePlayerId), not the selected figure.
                if (MetaGameProgress.instance?.modelTypeOfGame?.isOnline() == true)
                {
                    if (_onlinePlayerIdField == null)
                    {
                        _onlinePlayerIdField = typeof(MetaPlayer).GetField(
                            "onlinePlayerId",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    }

                    if (_onlinePlayerIdField != null)
                    {
                        object? value = _onlinePlayerIdField.GetValue(metaPlayer);
                        if (value is int onlineId)
                        {
                            var slotPlayerModel = ModelPlayerEnum.getFromPlayerId(onlineId);
                            if (slotPlayerModel != null && slotPlayerModel != ModelPlayerEnum.NULL)
                                return UintToColor(slotPlayerModel.color);
                        }
                    }
                }
            }
            catch { /* ignore */ }

            // Local / offline: colour always comes from the figure.
            if (metaPlayer.modelPlayer != null)
                return UintToColor(metaPlayer.modelPlayer.color);

            return Color.white;
        }

        private static Color UintToColor(uint hexColor)
        {
            float r = ((hexColor >> 16) & 0xFF) / 255f;
            float g = ((hexColor >>  8) & 0xFF) / 255f;
            float b = ( hexColor        & 0xFF) / 255f;
            return new Color(r, g, b, 1f);
        }

        /// <summary>
        /// Calibrates colours for the physical LEDs of a DualShock 4 lightbar
        /// (green channel is less bright, so we apply a stronger gamma).
        /// </summary>
        private static Color CalibrateForDS4Lightbar(Color c)
        {
            float r = Mathf.Pow(c.r, 1.6f);
            float g = Mathf.Pow(c.g, 2.0f);
            float b = Mathf.Pow(c.b, 1.6f);
            return new Color(r, g, b, 1f);
        }

        private static void LogColorDebug(int controllerId, MetaPlayer metaPlayer, Color targetColor)
        {
            int onlineIdForLog = -1;
            if (_onlinePlayerIdField != null)
            {
                try { onlineIdForLog = (int)_onlinePlayerIdField.GetValue(metaPlayer)!; }
                catch { /* ignore */ }
            }

            MelonLoader.MelonLogger.Msg(
                $"[ColorDebug] controllerId={controllerId} " +
                $"isOnline={MetaGameProgress.instance?.modelTypeOfGame?.isOnline()} " +
                $"modelPlayer.id={metaPlayer.modelPlayer.id} " +
                $"playerId={metaPlayer.modelPlayer.playerId} " +
                $"onlinePlayerId={onlineIdForLog} " +
                $"color=0x{metaPlayer.modelPlayer.color:X6} " +
                $"-> RGB({targetColor.r:F2},{targetColor.g:F2},{targetColor.b:F2})");
        }

        // ── Harmony patches ────────────────────────────────────────────────────
        [HarmonyPatch(typeof(HeroCollisionWithEnemy), "hitByEnemy")]
        private static class Patch_HeroCollisionWithEnemy
        {
            static void Postfix(HeroCollisionWithEnemy? __instance)
            {
                if (__instance?.actor == null) return;

                var hero = __instance.actor.TryCast<Hero>();
                if (hero != null)
                    OnHeroHit(hero);
            }
        }

        [HarmonyPatch(typeof(HeroDashComponent), "startDash")]
        private static class Patch_HeroDashComponent
        {
            static void Postfix(HeroDashComponent? __instance)
            {
                if (__instance?.actor == null) return;

                var hero = __instance.actor.TryCast<Hero>();
                if (hero != null)
                    OnHeroDash(hero);
            }
        }
    }
}
