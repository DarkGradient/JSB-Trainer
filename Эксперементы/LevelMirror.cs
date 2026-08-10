using System;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace jsb_new
{
    public static class LevelMirror
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("Level Mirror");
            set
            {
                bool wasEnabled = ModuleRegistry.IsActive("Level Mirror");
                ModuleRegistry.SetActive("Level Mirror", value);

                DebugStrings.Log($"[LevelMirror] Enabled: {value}");

                if (wasEnabled != value && !value)
                {
                    ResetMatrix();
                }
            }
        }

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            // Патчим метод Draw кастомного движка JSB
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_DisplayObjectRendererTk2d_Draw));

            // Патч управления (чтобы Вправо на клавиатуре/геймпаде двигало перса Вправо на экране)
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroInputLocal_Update_Mirror));

            // Совместимость с MouseControl
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_MouseControl_Mirror));

            // Регистрация в меню
            ModuleRegistry.RegisterCheckbox(
                "Level Mirror",
                () => Enabled,
                                            (newValue) => { Enabled = newValue; }
            );

            // Регистрация в HUDManager
            HUDManager.CreateHUD(
                key: "LevelMirror",
                textGetter: () => "LEVEL MIRROR ACTIVE",
                                 baseColor: Color.white,
                                 pulseColor: new Color(0.85f, 0.3f, 1f, 1f),
                                 activeGetter: () => Enabled,
                                 height: 35f
            );

            DebugStrings.Log("[LevelMirror] Initialized via Property Accessor.");
        }

        public static void Update()
        {
            if (!Enabled)
            {
                ResetMatrix();
            }
        }

        // Универсальный метод установки матрицы с поддержкой свойств Il2Cpp
        private static void SetStartMatrix(Matrix4x4 mat)
        {
            try
            {
                // В Il2CppInterop статические поля генерируются как свойства (Properties)
                var prop = AccessTools.Property(typeof(DisplayObjectRendererTk2d), "_startMatrix");
                if (prop != null)
                {
                    prop.SetValue(null, mat);
                    return;
                }

                // На случай если это поле (Field) в некоторых версиях MelonLoader
                var field = AccessTools.Field(typeof(DisplayObjectRendererTk2d), "_startMatrix");
                if (field != null)
                {
                    field.SetValue(null, mat);
                    return;
                }

                // Поиск по всем статическим свойствам
                foreach (var p in typeof(DisplayObjectRendererTk2d).GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (p.Name.Contains("_startMatrix"))
                    {
                        p.SetValue(null, mat);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[LevelMirror] SetStartMatrix error: {ex.Message}");
            }
        }

        private static void ResetMatrix()
        {
            Matrix4x4 mat = Matrix4x4.identity;
            Vector4 col = mat.GetColumn(3);
            col.x = -640f;
            col.y = 360f;
            mat.SetColumn(3, col);
            SetStartMatrix(mat);
        }

        // ====================== Главный Патч Движка ======================

        [HarmonyPatch(typeof(DisplayObjectRendererTk2d), "Draw")]
        private static class Patch_DisplayObjectRendererTk2d_Draw
        {
            static void Prefix()
            {
                Matrix4x4 mat = Matrix4x4.identity;
                Vector4 col = mat.GetColumn(3);

                if (Enabled)
                {
                    // Переворачиваем шкалу X и меняем сдвиг центра
                    mat.m00 = -1f;   // Инверсия горизонтальной оси
                    mat.m11 = 1f;
                    col.x = 640f;    // Центр +640 вместо -640
                    col.y = 360f;
                }
                else
                {
                    // Стандартная матрица JSB
                    mat.m00 = 1f;
                    mat.m11 = 1f;
                    col.x = -640f;
                    col.y = 360f;
                }

                mat.SetColumn(3, col);
                SetStartMatrix(mat);
            }
        }

        // ====================== Патчи Управления ======================

        [HarmonyPatch(typeof(HeroInputLocal), "update")]
        private static class Patch_HeroInputLocal_Update_Mirror
        {
            static void Postfix(HeroInputLocal __instance)
            {
                if (!Enabled || __instance == null || !__instance.enabled) return;

                if (ModuleRegistry.IsActive("MouseControl")) return;

                var hero = __instance.hero;
                if (hero?.controlComponent == null) return;

                // Инвертируем X-ввод под отзеркаленную матрицу
                hero.controlComponent.moveInput.x = -hero.controlComponent.moveInput.x;
            }
        }

        [HarmonyPatch(typeof(HeroControl), "update")]
        private static class Patch_MouseControl_Mirror
        {
            static void Postfix(HeroControl __instance)
            {
                if (!Enabled || !ModuleRegistry.IsActive("MouseControl") || __instance == null) return;

                var hero = __instance.hero;
                if (hero == null || hero.heroInputLocal == null || !hero.heroInputLocal.enabled) return;

                Point mousePos = KeyManager.mousePos;
                if (mousePos == null || CameraFlash.mainCamera == null) return;

                float cameraX = CameraFlash.mainCamera.px;
                float cameraY = CameraFlash.mainCamera.py;
                float zoom = CameraFlash.mainCamera.zoomScale > 0.001f ? CameraFlash.mainCamera.zoomScale : 1f;

                Vector2 gamePos = ResolutionManager.screenToGamePos(mousePos);

                float targetX = cameraX - (gamePos.x - 640f) / zoom;
                float targetY = cameraY + (gamePos.y - 360f) / zoom;

                hero.px = targetX;
                hero.py = targetY;

                if (hero.physicComponent != null)
                {
                    hero.physicComponent.vx = 0f;
                    hero.physicComponent.vy = 0f;
                }
            }
        }
    }
}
