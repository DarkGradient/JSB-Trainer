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

                // Получаем мета-игрока (нужно для проверок смерти и родного цвета)
                var metaPlayer = playerManager.GetPlayerByControllerId(controllerId);
                bool isDead = metaPlayer?.modelPlayer != null && IsPlayerDead(metaPlayer.modelPlayer);

                // --- 1. ПРИОРИТЕТ УРОНА И СМЕРТИ ---
                if (isHitting || isDead)
                {
                    if (isHitting) _hitTimers[controllerId] = hitTimer - Time.deltaTime;

                    if (ds4 != null) ds4.SetLightColor(1f, 0f, 0f); // Ярко-красный
                    if (joystick.supportsVibration)
                    {
                        if (isHitting) joystick.SetVibration(1.0f, 1.0f); // Максимальный удар
                        else joystick.StopVibration(); // Мертв - лежим тихо
                    }
                    continue; // Блокируем остальные эффекты
                }

                // --- 2. ПРИОРИТЕТ РЫВКА ---
                if (isDashing)
                {
                    _dashTimers[controllerId] = dashTimer - Time.deltaTime;
                    if (joystick.supportsVibration) joystick.SetVibration(0.0f, 0.7f); // Резкий микро-щелчок
                    continue;
                }

                // === ФОНОВЫЕ ЭФФЕКТЫ (если нет урона и рывка) ===

                Color targetColor = Color.white;
                float vibLeft = 0f;
                float vibRight = 0f;

                // --- 3. ONE-HIT MODE (СЕРДЦЕБИЕНИЕ ОБРЕЧЕННОСТИ) ---
                if (OneHit.Enabled)
                {
                    // True One-Hit = 120 ударов в минуту, Обычный = 80 ударов
                    float bpm = OneHit.TrueOneHitEnabled ? 120f : 80f;
                    float beatInterval = 60f / bpm;
                    float t = Time.unscaledTime % beatInterval;

                    // Паттерн сердцебиения: тук(0.0-0.1) ... тук(0.2-0.3)
                    bool isBeat = (t < 0.1f) || (t > 0.2f && t < 0.3f);

                    targetColor = isBeat ? new Color(1f, 0f, 0f) : new Color(0.15f, 0f, 0f); // Бордовый <-> Красный

                    if (isBeat) vibRight = 0.15f; // Очень тихий стук пульса в руках
                }
                // --- 4. ORANGE SOUL (ИНЕРЦИЯ) ---
                else if (OrangeSoul.Enabled)
                {
                    targetColor = new Color(1.0f, 0.4f, 0.0f); // Оранжевый
                    vibLeft = 0.03f; // Едва заметный фоновый гул заведенного мотора
                }
                // --- 5. PURPLE SOUL (ФИОЛЕТОВЫЙ) ---
                else if (PurpleSoul.Enabled)
                {
                    targetColor = new Color(0.6f, 0.1f, 0.8f); // Фиолетовый
                }
                // --- 6. ОБЫЧНАЯ ИГРА (РОДНОЙ ЦВЕТ ИГРОКА) ---
                else if (metaPlayer?.modelPlayer != null)
                {
                    targetColor = UintToColor(metaPlayer.modelPlayer.color);
                }

                // Применяем вычисленный фон
                if (ds4 != null) ds4.SetLightColor(targetColor.r, targetColor.g, targetColor.b);

                if (joystick.supportsVibration)
                {
                    if (vibLeft > 0f || vibRight > 0f) joystick.SetVibration(vibLeft, vibRight);
                    else joystick.StopVibration();
                }

                // Сборщик мусора для таймеров
                if (!isHitting && _hitTimers.ContainsKey(controllerId)) _hitTimers.Remove(controllerId);
                if (!isDashing && _dashTimers.ContainsKey(controllerId)) _dashTimers.Remove(controllerId);
            }
        }

        public static void OnHeroHit(Hero? hero)
        {
            if (hero?.metaPlayer == null) return;
            int controllerId = hero.metaPlayer.getControllerId();
            if (controllerId >= 0) _hitTimers[controllerId] = 0.4f; // 0.4 сек боли
        }

        public static void OnHeroDash(Hero? hero)
        {
            if (hero?.metaPlayer == null) return;
            int controllerId = hero.metaPlayer.getControllerId();
            if (controllerId >= 0) _dashTimers[controllerId] = 0.1f; // 0.1 сек щелчка
        }

        // Вызов тактильного "Щелчка" из других модулей (например из PurpleSoul)
        public static void PlayHapticClick(float leftMotor, float rightMotor, float durationSec = 0.1f)
        {
            if (!ReInput.isReady || ReInput.controllers == null) return;
            var player = ReInput.players.GetSystemPlayer();
            if (player == null) return;

            // ИСПРАВЛЕНО: Используем for вместо foreach для IL2CPP коллекций
            int joystickCount = player.controllers.joystickCount;
            for (int i = 0; i < joystickCount; i++)
            {
                var joystick = player.controllers.Joysticks[i];
                if (joystick != null && joystick.supportsVibration)
                {
                    // Встроенный метод на время (motor, power, duration)
                    joystick.SetVibration(0, leftMotor, durationSec);
                    joystick.SetVibration(1, rightMotor, durationSec);
                }
            }
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

        private static Color UintToColor(uint hexColor)
        {
            float r = ((hexColor >> 16) & 0xFF) / 255f;
            float g = ((hexColor >> 8) & 0xFF) / 255f;
            float b = (hexColor & 0xFF) / 255f;
            return new Color(r, g, b, 1f);
        }

        // ==== ПАТЧИ ====
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