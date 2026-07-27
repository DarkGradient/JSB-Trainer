#pragma warning disable CS8618
#pragma warning disable CS8600
#pragma warning disable CS8602

using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace jsb_new
{
    public static class AIEnhancer
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("AIEnhancer");
            set => ModuleRegistry.SetActive("AIEnhancer", value);
        }

        // Дистанция, на которой бот считает врага угрозой и начинает убегать
        private const float PanicRadius = 150f;
        private const float PanicRadiusSq = PanicRadius * PanicRadius;

        // Насколько далеко бот убегает за один "приступ паники"
        private const float FleeDistance = 200f;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_AI_Evasion));

            ModuleRegistry.RegisterCheckbox(
                category: "Optional Stuff",
                name: "Smarter Bot AI (Evasion)",
                getter: () => Enabled,
                setter: (newValue) => { Enabled = newValue; },
                isLocked: null,
                order: 45
            );

            DebugStrings.Log("AIEnhancer initialized");
        }

        // Перехватываем update() бота ДО его штатной логики.
        // Если рядом враг - подменяем цель движения на "убегание" и форсим дэш,
        // пропуская оригинальный метод в этом кадре (return false).
        // Если угрозы нет - пропускаем в оригинал как обычно (return true).
        [HarmonyPatch(typeof(HeroAI_SomehowIntelligent), "update")]
        private static class Patch_AI_Evasion
        {
            static bool Prefix(HeroAI_SomehowIntelligent __instance)
            {
                if (!Enabled) return true;

                try
                {
                    Actor actor = __instance.actor;
                    if (actor == null || actor.destroyed) return true;

                    CollisionCircleComponent? nearest = null;
                    float nearestDistSq = float.MaxValue;

                    for (int i = 0; i < Enemy.allCollision.length; i++)
                    {
                        var col = Enemy.allCollision[i];
                        if (col == null) continue;

                        Actor enemyActor = col.actor;
                        if (enemyActor == null || enemyActor.destroyed) continue;

                        float dx = enemyActor.px - actor.px;
                        float dy = enemyActor.py - actor.py;
                        float distSq = dx * dx + dy * dy;

                        if (distSq < nearestDistSq)
                        {
                            nearestDistSq = distSq;
                            nearest = col;
                        }
                    }

                    if (nearest != null && nearestDistSq < PanicRadiusSq)
                    {
                        Actor threat = nearest.actor;

                        float dx = actor.px - threat.px;
                        float dy = actor.py - threat.py;
                        float len = MathFlash.sqrt(dx * dx + dy * dy);

                        if (len < 0.01f)
                        {
                            // Угроза точно в той же точке - убегаем в случайном направлении,
                            // чтобы не делить на ноль
                            float randAngle = MathFlash.random() * 360f;
                            dx = MyMath.myCos(randAngle);
                            dy = MyMath.mySin(randAngle);
                            len = 1f;
                        }

                        dx /= len;
                        dy /= len;

                        __instance.logicGoStraight.gotoPos.x = actor.px + dx * FleeDistance;
                        __instance.logicGoStraight.gotoPos.y = actor.py + dy * FleeDistance;
                        __instance.logicGoStraight.isDashing = true;

                        return false; // пропускаем оригинальную логику - в этом кадре бот убегает
                    }
                }
                catch (Exception ex)
                {
                    MelonLoader.MelonLogger.Error($"[AIEnhancer] Evasion check failed: {ex.Message}");
                }

                return true; // угрозы нет (или ошибка) - штатная логика бота как обычно
            }
        }
    }
}
