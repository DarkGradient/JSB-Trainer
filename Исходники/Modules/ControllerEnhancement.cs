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
        // Включено НАВСЕГДА по умолчанию и не выводится в меню
        public static bool Enabled { get; set; } = true;
        public static bool LightbarEnabled { get; set; } = true;
        public static bool VibrationEnabled { get; set; } = true;

        private static readonly Dictionary<int, float> _hitTimers = new();
        private static readonly Dictionary<int, float> _dashTimers = new();
        private static FieldInfo? _onlinePlayerIdField;
        private static bool _wasVibratingLastFrame = false;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            ArgumentNullException.ThrowIfNull(harmony);

            Enabled = true;
            LightbarEnabled = true;
            VibrationEnabled = true;

            // Патчи отслеживания урона и рывка
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroCollisionWithEnemy));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroDashComponent));

            DebugStrings.Log("[ControllerSync] Module initialized (Always Active in Background).");
        }

        public static void Update()
        {
            if (!Enabled || !ReInput.isReady) return;

            var controllers = ReInput.controllers;
            if (controllers == null) return;

            var playerManager = PlayerManager.instance;
            if (playerManager == null) return;

            bool anyVibrationActive = false;
            int joystickCount = controllers.joystickCount;

            for (int i = 0; i < joystickCount; i++)
            {
                var joystick = controllers.Joysticks[i];
                if (joystick == null) continue;

                int controllerId = joystick.id;
                var ds4 = joystick.GetExtension<IDualShock4Extension>();

                bool isHitting = _hitTimers.TryGetValue(controllerId, out float hitTimer) && hitTimer > 0f;
                bool isDashing = _dashTimers.TryGetValue(controllerId, out float dashTimer) && dashTimer > 0f;

                var metaPlayer = playerManager.GetPlayerByControllerId(controllerId);
                bool isDead = metaPlayer?.modelPlayer != null && IsPlayerDead(metaPlayer.modelPlayer);

                // 1. Урон / Смерть
                if (isHitting || isDead)
                {
                    if (isHitting) _hitTimers[controllerId] = hitTimer - Time.deltaTime;

                    if (LightbarEnabled && ds4 != null)
                        ds4.SetLightColor(1f, 0f, 0f);

                    if (VibrationEnabled && joystick.supportsVibration)
                    {
                        if (isHitting)
                        {
                            joystick.SetVibration(1.0f, 1.0f);
                            anyVibrationActive = true;
                        }
                        else
                        {
                            joystick.StopVibration();
                        }
                    }
                    continue;
                }

                // 2. Рывок (Dash)
                if (isDashing)
                {
                    _dashTimers[controllerId] = dashTimer - Time.deltaTime;

                    if (VibrationEnabled && joystick.supportsVibration)
                    {
                        joystick.SetVibration(0.0f, 0.7f);
                        anyVibrationActive = true;
                    }
                    continue;
                }

                // 3. Цвета профилей и режимов
                Color targetColor = Color.white;
                float vibLeft = 0f;
                float vibRight = 0f;

                if (OneHit.Enabled)
                {
                    float bpm = OneHit.TrueOneHitEnabled ? 120f : 80f;
                    float beatInterval = 60f / bpm;
                    float t = Time.unscaledTime % beatInterval;
                    bool isBeat = (t < 0.1f) || (t > 0.2f && t < 0.3f);

                    targetColor = isBeat ? new Color(1f, 0f, 0f) : new Color(0.15f, 0f, 0f);
                    if (isBeat) vibRight = 0.15f;
                }
                else if (OrangeSoul.Enabled)
                {
                    targetColor = new Color(1.0f, 0.4f, 0.0f);
                    vibLeft = 0.03f;
                }
                else if (PurpleSoul.Enabled)
                {
                    targetColor = new Color(0.6f, 0.1f, 0.8f);
                }
                else if (metaPlayer != null)
                {
                    targetColor = GetPlayerColor(metaPlayer);
                }

                if (LightbarEnabled && ds4 != null)
                {
                    Color calibrated = CalibrateForDS4Lightbar(targetColor);
                    ds4.SetLightColor(calibrated.r, calibrated.g, calibrated.b);
                }

                if (joystick.supportsVibration)
                {
                    if (VibrationEnabled && (vibLeft > 0f || vibRight > 0f))
                    {
                        joystick.SetVibration(vibLeft, vibRight);
                        anyVibrationActive = true;
                    }
                    else
                    {
                        joystick.StopVibration();
                    }
                }

                if (!isHitting && _hitTimers.ContainsKey(controllerId)) _hitTimers.Remove(controllerId);
                if (!isDashing && _dashTimers.ContainsKey(controllerId)) _dashTimers.Remove(controllerId);
            }

            _wasVibratingLastFrame = anyVibrationActive;
        }

        public static void OnHeroHit(Hero? hero)
        {
            if (!Enabled || hero?.metaPlayer == null) return;
            int controllerId = hero.metaPlayer.getControllerId();
            if (controllerId >= 0) _hitTimers[controllerId] = 0.4f;
        }

        public static void OnHeroDash(Hero? hero)
        {
            if (!Enabled || hero?.metaPlayer == null) return;
            int controllerId = hero.metaPlayer.getControllerId();
            if (controllerId >= 0) _dashTimers[controllerId] = 0.1f;
        }

        private static bool IsPlayerDead(ModelPlayer? modelPlayer)
        {
            if (modelPlayer == null) return false;
            try
            {
                if (MainGame.instance?.gameSceneManager?.gameScene == null) return false;
                var hero = Hero.getHeroFromModelPlayer(modelPlayer);
                if (hero?.lifeComponent != null && hero.lifeComponent.isDead) return true;

                if (MainGame.instance.gameSceneManager.gameScene.itemManager != null)
                {
                    var ghost = HeroGhost.getHeroFromModelPlayer(modelPlayer);
                    if (ghost != null && !ghost.destroyed) return true;
                }
            }
            catch { }
            return false;
        }

        private static Color GetPlayerColor(MetaPlayer metaPlayer)
        {
            try
            {
                if (MetaGameProgress.instance?.modelTypeOfGame?.isOnline() == true)
                {
                    if (_onlinePlayerIdField == null)
                    {
                        _onlinePlayerIdField = typeof(MetaPlayer).GetField("onlinePlayerId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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
            catch { }

            if (metaPlayer.modelPlayer != null) return UintToColor(metaPlayer.modelPlayer.color);
            return Color.white;
        }

        private static Color UintToColor(uint hexColor)
        {
            float r = ((hexColor >> 16) & 0xFF) / 255f;
            float g = ((hexColor >> 8) & 0xFF) / 255f;
            float b = (hexColor & 0xFF) / 255f;
            return new Color(r, g, b, 1f);
        }

        private static Color CalibrateForDS4Lightbar(Color c)
        {
            return new Color(Mathf.Pow(c.r, 1.6f), Mathf.Pow(c.g, 2.6f), Mathf.Pow(c.b, 1.6f), 1f);
        }

        [HarmonyPatch(typeof(HeroCollisionWithEnemy), "hitByEnemy")]
        private static class Patch_HeroCollisionWithEnemy
        {
            static void Postfix(HeroCollisionWithEnemy? __instance)
            {
                if (__instance?.actor == null) return;
                var hero = __instance.actor.TryCast<Hero>();
                if (hero != null) OnHeroHit(hero);
            }
        }

        [HarmonyPatch(typeof(HeroDashComponent), "startDash")]
        private static class Patch_HeroDashComponent
        {
            static void Postfix(HeroDashComponent? __instance)
            {
                if (__instance?.actor == null) return;
                var hero = __instance.actor.TryCast<Hero>();
                if (hero != null) OnHeroDash(hero);
            }
        }
    }
}
