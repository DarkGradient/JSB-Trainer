#pragma warning disable CS8618 // Поля, не допускающие значения NULL
#pragma warning disable CS8600 // Преобразование null
#pragma warning disable CS8603 // Возможный возврат null

using System;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using Il2CppInterop.Runtime;

namespace jsb_new
{
    public static class DodgeBot
    {
        // Состояние автопилота игрока
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("DodgeBot");
            set
            {
                if (ModuleRegistry.IsActive("DodgeBot") != value)
                {
                    ModuleRegistry.SetActive("DodgeBot", value);
                    DebugStrings.Log($"DodgeBot changed to: {value}");

                    if (value)
                        HUDManager.CreateToast("DODGE BOT ACTIVATED", Color.green);
                    else
                        HUDManager.CreateToast("DODGE BOT DEACTIVATED", Color.white);
                }
            }
        }

        // Состояние улучшенного ИИ союзников (бывший AIEnhancer)
        public static bool TeammatesEnabled
        {
            get => ModuleRegistry.IsActive("AIEnhancer");
            set
            {
                if (ModuleRegistry.IsActive("AIEnhancer") != value)
                {
                    ModuleRegistry.SetActive("AIEnhancer", value);
                    DebugStrings.Log($"AIEnhancer changed to: {value}");
                }
            }
        }

        private static Vector2 _smoothedInput = Vector2.zero; // Вектор сглаженного движения
        private static Vector2 _targetSafeSpot = Vector2.zero; // Лучшая безопасная точка на сцене
        private static float _lastDashTime = 0f;              // Таймер кулдауна паники бота

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            // Регистрация чекбоксов меню
            ModuleRegistry.RegisterCheckbox("Gamemodes", "Auto-Evade (Dodge Bot)",
                                            () => Enabled,
                                            (newValue) => { Enabled = newValue; },
                                            order: 50
            );

            ModuleRegistry.RegisterCheckbox("Optional Stuff", "Smarter Bot AI (Evasion)",
                                            () => TeammatesEnabled,
                                            (newValue) => { TeammatesEnabled = newValue; },
                                            isLocked: null,
                                            order: 45
            );

            // Регистрация плашки в HUD
            HUDManager.CreateHUD(
                key: "DodgeBot",
                textGetter: () => "AUTO-EVADE ON",
                                 baseColor: Color.white,
                                 pulseColor: Color.green,
                                 activeGetter: () => Enabled,
                                 height: 35f,
                                 order: 30
            );

            // Регистрируем блокировщик ввода клавиатуры и патч для уклонения ИИ-тиммейтов
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroInputLocal_Update_DodgeBot));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_AI_Evasion));
        }

        // === ЧЕСТНЫЙ ПОИСК БЕЗОПАСНЫХ ЗОН НА СЦЕНЕ (Grid Pathfinding) ===
        // Сканирует сетку 6х4 по всему экрану и находит точку с наименьшей опасностью
        private static Vector2 FindBestSafeSpot(Vector2 currentPos, float padding)
        {
            if (CameraFlash.mainCamera == null) return currentPos;

            float left = CameraFlash.mainCamera.boundsFOV.left + padding;
            float right = CameraFlash.mainCamera.boundsFOV.right - padding;
            float top = CameraFlash.mainCamera.boundsFOV.top + padding;
            float bottom = CameraFlash.mainCamera.boundsFOV.bottom - padding;

            float bestX = currentPos.x;
            float bestY = currentPos.y;
            float lowestThreat = float.MaxValue;

            int cols = 6;
            int rows = 4;

            for (int c = 0; c < cols; c++)
            {
                float gx = Mathf.Lerp(left, right, (float)c / (cols - 1));
                for (int r = 0; r < rows; r++)
                {
                    float gy = Mathf.Lerp(top, bottom, (float)r / (rows - 1));

                    float totalThreat = 0f;

                    // 1. Считаем угрозу от всех активных препятствий на сцене
                    var enemyManager = GameScene.instance?.enemyManager;
                    if (enemyManager != null && enemyManager.actorList != null)
                    {
                        for (int i = 0; i < enemyManager.actorList.Count; i++)
                        {
                            Actor enemy = enemyManager.actorList[i];
                            if (enemy == null || enemy.destroyed) continue;

                            float dx = gx - enemy.px;
                            float dy = gy - enemy.py;
                            float distSq = dx * dx + dy * dy;

                            float radius = GetActorRadius(enemy);
                            float safeDist = radius + 35f; // Граница опасной зоны снаряда
                            float safeDistSq = safeDist * safeDist;

                            if (distSq < safeDistSq)
                            {
                                // Если точка внутри опасной зоны врага — накладываем колоссальный штраф
                                totalThreat += 1500f * (safeDistSq - distSq);
                            }
                            else
                            {
                                // Вне опасной зоны угроза плавно затухает по закону обратных квадратов
                                totalThreat += 120f / (distSq + 10f);
                            }
                        }
                    }

                    // 2. Легкое притяжение к текущей позиции игрока (чтобы бот плавно перетекал, а не метался)
                    float dxPlayer = gx - currentPos.x;
                    float dyPlayer = gy - currentPos.y;
                    float distPlayerSq = dxPlayer * dxPlayer + dyPlayer * dyPlayer;
                    totalThreat += distPlayerSq * 0.00015f;

                    if (totalThreat < lowestThreat)
                    {
                        lowestThreat = totalThreat;
                        bestX = gx;
                        bestY = gy;
                    }
                }
            }

            return new Vector2(bestX, bestY);
        }

        // Обновление логики автопилота игрока
        public static void Update()
        {
            if (!Enabled) return;

            try
            {
                if (GameScene.instance == null || PlayerManager.instance == null) return;

                var mp = PlayerManager.instance.GetFirstLocalPlayer();
                if (mp == null) return;

                Hero hero = Hero.getHeroFromModelPlayer(mp.modelPlayer);
                if (hero == null || hero.destroyed || hero.controlComponent == null) return;

                float moveX = 0f;
                float moveY = 0f;
                float repelX = 0f;
                float repelY = 0f;
                bool anyThreatInRange = false;

                // 1. СИЛА ОТТАЛКИВАНИЯ ОТ БЛИЖАЙШИХ ПРЕПЯТСТВИЙ (Локальный обход)
                var enemyManager = GameScene.instance.enemyManager;
                bool dangerClose = false;
                Actor nearestThreat = null;
                float nearestThreatDist = float.MaxValue;

                if (enemyManager != null && enemyManager.actorList != null)
                {
                    for (int i = 0; i < enemyManager.actorList.Count; i++)
                    {
                        Actor enemy = enemyManager.actorList[i];
                        if (enemy == null || enemy.destroyed) continue;

                        float dx = hero.px - enemy.px;
                        float dy = hero.py - enemy.py;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        float enemyRadius = GetActorRadius(enemy);

                        float minCollideDist = enemyRadius + 15f;
                        float safetyZone = minCollideDist + 85f;

                        if (dist < nearestThreatDist)
                        {
                            nearestThreatDist = dist;
                            nearestThreat = enemy;
                        }

                        if (dist < safetyZone && dist > 1f)
                        {
                            anyThreatInRange = true;

                            float force = (safetyZone - dist) / safetyZone;
                            float sqrForce = force * force;
                            float weight = 5.0f;

                            repelX += (dx / dist) * sqrForce * weight;
                            repelY += (dy / dist) * sqrForce * weight;

                            if (dist < minCollideDist + 15f)
                            {
                                dangerClose = true;
                            }
                        }
                    }

                    // 2. УМНЫЙ АВТО-ДЭШ
                    if (dangerClose && (Time.unscaledTime - _lastDashTime > 0.75f))
                    {
                        if (hero.dashComponent != null && hero.dashComponent.enabled)
                        {
                            if (hero.dashComponent.dashCooldown == null || hero.dashComponent.dashCooldown.isCompleted())
                            {
                                hero.controlComponent.isDashing = true;
                                _lastDashTime = Time.unscaledTime;
                                DebugStrings.Log("DodgeBot: Panic dash triggered!");
                            }
                        }
                    }
                }

                float repelMag = Mathf.Sqrt(repelX * repelX + repelY * repelY);

                if (anyThreatInRange && repelMag > 0.05f)
                {
                    // Вблизи препятствий используем локальный вектор уклонения
                    moveX = repelX;
                    moveY = repelY;
                }
                else if (anyThreatInRange && nearestThreat != null)
                {
                    // Силы взаимно погасились (окружен) - бежим строго от ближайшей угрозы
                    float dx = hero.px - nearestThreat.px;
                    float dy = hero.py - nearestThreat.py;
                    float len = Mathf.Sqrt(dx * dx + dy * dy);
                    if (len > 0.01f)
                    {
                        moveX = dx / len;
                        moveY = dy / len;
                    }
                }
                else
                {
                    // Угроз в упор нет - плавно перетекаем в глобальную БЕЗОПАСНУЮ зону!
                    // Чтобы не грузить процессор, ищем ее раз в 5 кадров
                    if (Time.frameCount % 5 == 0)
                    {
                        _targetSafeSpot = FindBestSafeSpot(new Vector2(hero.px, hero.py), 80f);
                    }

                    float toCenterX = _targetSafeSpot.x - hero.px;
                    float toCenterY = _targetSafeSpot.y - hero.py;
                    float distToCenter = Mathf.Sqrt(toCenterX * toCenterX + toCenterY * toCenterY);

                    if (distToCenter > 10f)
                    {
                        // Тянемся к упругой безопасной точке, а не к центру экрана!
                        float pullStrength = Mathf.Lerp(0.15f, 0.75f, distToCenter / 450f);
                        moveX = (toCenterX / distToCenter) * pullStrength;
                        moveY = (toCenterY / distToCenter) * pullStrength;
                    }
                }

                // 4. ПЛАВНОЕ СГЛАЖИВАНИЕ ДВИЖЕНИЯ
                Vector2 targetInput = new Vector2(moveX, moveY);
                if (targetInput.magnitude > 0.01f)
                {
                    targetInput.Normalize();
                }

                _smoothedInput = Vector2.MoveTowards(_smoothedInput, targetInput, Time.unscaledDeltaTime * 8f);

                hero.controlComponent.moveInput.x = _smoothedInput.x;
                hero.controlComponent.moveInput.y = _smoothedInput.y;
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"[DodgeBot] Error in update: {ex.Message}");
            }
        }

        private static float GetActorRadius(Actor actor)
        {
            if (actor == null) return 15f;
            try
            {
                var field = actor.GetType().GetField("radius", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    var val = field.GetValue(actor);
                    if (val != null && float.TryParse(val.ToString(), out float r))
                    {
                        return r;
                    }
                }
            }
            catch {}
            return 20f;
        }

        // Блокируем ручной ввод с клавиатуры/геймпада, когда работает автопилот
        [HarmonyPatch(typeof(HeroInputLocal), "update")]
        private static class Patch_HeroInputLocal_Update_DodgeBot
        {
            static bool Prefix()
            {
                if (Enabled)
                {
                    return false; // Отменяем стандартный опрос кнопок клавиатуры
                }
                return true;
            }
        }

        // === ПАТЧ ДЛЯ УЛУЧШЕНИЯ ИИ СОЮЗНИКОВ (AIEnhancer) ===
        // Теперь союзники-боты используют НАШ ЖЕ передовой поисковик безопасной зоны!
        [HarmonyPatch(typeof(HeroAI_SomehowIntelligent), "update")]
        private static class Patch_AI_Evasion
        {
            static bool Prefix(HeroAI_SomehowIntelligent __instance)
            {
                if (!TeammatesEnabled) return true;

                try
                {
                    Actor actor = __instance.actor;
                    if (actor == null || actor.destroyed) return true;

                    Vector2 currentPos = new Vector2(actor.px, actor.py);

                    // Сканируем всю арену и находим лучшее свободное место для бота-тиммейта
                    Vector2 bestSpot = FindBestSafeSpot(currentPos, 90f);

                    float dx = bestSpot.x - actor.px;
                    float dy = bestSpot.y - actor.py;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // Если безопасная зона в стороне — направляем бота-союзника туда!
                    if (dist > 25f)
                    {
                        __instance.logicGoStraight.gotoPos.x = bestSpot.x;
                        __instance.logicGoStraight.gotoPos.y = bestSpot.y;

                        // Если до безопасной зоны бежать далеко — бот прожимает дэш
                        __instance.logicGoStraight.isDashing = (dist > 110f);

                        return false; // Полностью перехватываем глупую стандартную логику
                    }
                }
                catch (Exception ex)
                {
                    MelonLoader.MelonLogger.Error($"[AIEnhancer] Evasion check failed: {ex.Message}");
                }

                return true; // Угрозы нет — работает штатный дрейф бота
            }
        }
    }
}
