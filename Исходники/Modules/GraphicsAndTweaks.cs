using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace jsb_new
{
    // --- 1. ВСЕГДА ЧЕРНЫЙ ФОН ---
    public static class DisableColorSwap
    {
        public static bool EffectiveEnabled => ModuleRegistry.IsActive("Always black BG") || ModuleRegistry.IsActive("OneHit");

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_FxBackground_SetBackGroundColor));
            ModuleRegistry.RegisterCheckbox("Always black BG", () => EffectiveEnabled, (v) => ModuleRegistry.SetActive("Always black BG", v));
        }

        [HarmonyPatch(typeof(FxBackground), "setBackGroundColor")]
        private static class Patch_FxBackground_SetBackGroundColor
        {
            static bool Prefix(ref uint color)
            {
                if (EffectiveEnabled)
                {
                    MainGame.stage.color = 0u;
                    FxBackground.lastStageColor = 0u;
                    return false;
                }
                return true;
            }
        }
    }

    // --- 2. СКРЫТИЕ ТАЙМЛАЙНА (С КОРРЕКТНЫМ ВОЗВРАТОМ) ---
    public static class HideTimeline
    {
        private static bool _wasTimelineHidden = false;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(ViewCheckpointProgress), "refreshViews"), prefix: new HarmonyMethod(typeof(HideTimeline), nameof(Prefix_Refresh)));
            harmony.Patch(AccessTools.Method(typeof(ViewCheckpointProgress), "update"), postfix: new HarmonyMethod(typeof(HideTimeline), nameof(Postfix_Update)));
            harmony.Patch(AccessTools.Method(typeof(ViewCheckpointProgress), "show"), prefix: new HarmonyMethod(typeof(HideTimeline), nameof(Prefix_Show)));

            ModuleRegistry.RegisterCheckbox("Hide Timeline", () => ModuleRegistry.IsActive("Hide Timeline"), (v) => ModuleRegistry.SetActive("Hide Timeline", v));
        }

        public static bool Prefix_Refresh(ViewCheckpointProgress __instance)
        {
            if (ModuleRegistry.IsActive("Hide Timeline"))
            {
                __instance.destroyAllChildren();
                if (__instance.visual != null) { __instance.visual.visible = false; __instance.visual.alpha = 0f; }
                return false;
            }
            return true;
        }

        public static void Prefix_Show(ref bool b)
        {
            if (ModuleRegistry.IsActive("Hide Timeline"))
            {
                b = false;
            }
        }

        public static void Postfix_Update(ViewCheckpointProgress __instance)
        {
            if (ModuleRegistry.IsActive("Hide Timeline"))
            {
                _wasTimelineHidden = true;
                __instance.show(false);
                __instance.destroyAllChildren();

                if (__instance.visual != null)
                {
                    __instance.visual.visible = false;
                    __instance.visual.alpha = 0f;
                }
                if (__instance.progressCheckPointMc != null)
                {
                    __instance.progressCheckPointMc.visible = false;
                    __instance.progressCheckPointMc.alpha = 0f;
                }
            }
            else
            {
                // ВОССТАНОВЛЕНИЕ ТАЙМЛАЙНА ПОСЛЕ ВЫКЛЮЧЕНИЯ
                if (_wasTimelineHidden)
                {
                    if (__instance.visual != null)
                    {
                        __instance.visual.visible = true;
                        __instance.visual.alpha = 1f;
                    }
                    if (__instance.progressCheckPointMc != null)
                    {
                        __instance.progressCheckPointMc.visible = true;
                        __instance.progressCheckPointMc.alpha = 1f;
                    }
                    __instance.show(true);
                    __instance.refreshViews();
                    _wasTimelineHidden = false;
                }
            }
        }
    }

    // --- 3. РАЗБЛОКИРОВКА 400 FPS ---
    public static class FPSUnlock
    {
        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            MelonEvents.OnUpdate.Subscribe(OnUpdate);
            ModuleRegistry.RegisterCheckbox("FPS Unlock (400 FPS)", () => ModuleRegistry.IsActive("FPSUnlock"), (v) => {
                ModuleRegistry.SetActive("FPSUnlock", v);
                if (v) ApplyFPS(); else ResetFPS();
            });
        }

        private static void OnUpdate()
        {
            if (ModuleRegistry.IsActive("FPSUnlock"))
            {
                if (QualitySettings.vSyncCount != 0 || Application.targetFrameRate != 400) ApplyFPS();
            }
        }

        public static void ApplyFPS() { QualitySettings.vSyncCount = 0; Application.targetFrameRate = 400; }
        public static void ResetFPS() { Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0; }
    }

    // --- 4. СЕТКА DELTARUNE (ПОЛНАЯ ГЕНЕРАЦИЯ ЛИНИЙ) ---
    public static class DeltaruneGrid
    {
        public static float CellSize = 96f;
        public static float LineThickness = 2.5f;
        public static float SpeedX1 = -18f, SpeedY1 = 12f;
        public static float SpeedX2 = 15f, SpeedY2 = -10f;
        public static float ColorR = 0.55f, ColorG = 0.18f, ColorB = 0.80f, Alpha1 = 0.30f;

        private static GameObject? _canvasObject;
        private static RectTransform? _container1;
        private static RectTransform? _container2;
        private static float _offsetX1, _offsetY1;
        private static float _offsetX2, _offsetY2;
        private static bool _needRebuild;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            ModuleRegistry.RegisterCheckbox("Deltarune Grid BG", () => ModuleRegistry.IsActive("Deltarune Grid BG"), (v) => {
                ModuleRegistry.SetActive("Deltarune Grid BG", v);
                if (v) CreateGrid(); else DestroyGrid();
            });

                ModuleRegistry.RegisterSlider("DG Cell Size", 20f, 150f, 96f, v => { CellSize = v; _needRebuild = true; });
                ModuleRegistry.RegisterSlider("DG Line Thickness", 0.5f, 8f, 2.5f, v => { LineThickness = v; _needRebuild = true; });
                ModuleRegistry.RegisterSlider("DG Speed X1", -80f, 80f, -18f, v => SpeedX1 = v);
                ModuleRegistry.RegisterSlider("DG Speed Y1", -80f, 80f, 12f, v => SpeedY1 = v);
                ModuleRegistry.RegisterSlider("DG Color R", 0f, 1f, 0.55f, v => ColorR = v);
                ModuleRegistry.RegisterSlider("DG Color G", 0f, 1f, 0.18f, v => ColorG = v);
                ModuleRegistry.RegisterSlider("DG Color B", 0f, 1f, 0.80f, v => ColorB = v);
                ModuleRegistry.RegisterSlider("DG Alpha 1", 0f, 1f, 0.30f, v => Alpha1 = v);
        }

        public static void Update()
        {
            if (!ModuleRegistry.IsActive("Deltarune Grid BG")) { if (_canvasObject != null) DestroyGrid(); return; }
            if (_needRebuild) { DestroyGrid(); CreateGrid(); _needRebuild = false; }
            if (_canvasObject == null) CreateGrid();
            if (_container1 == null || _container2 == null) return;

            if (_canvasObject == null) CreateGrid();
            if (_canvasObject == null || _container1 == null) return; // Guard-check спасет от CS8602

            _canvasObject.transform.position = new Vector3(0f, 0f, 1.5f);
            _canvasObject.transform.localScale = Vector3.one;

            float shakeX = 0f, shakeY = 0f;
            if (CameraFlash.mainCamera != null && CameraFlash.mainCamera.Pointer != System.IntPtr.Zero && CameraFlash.mainCamera.actorForTransform != null)
            {
                shakeX = CameraFlash.mainCamera.actorForTransform.px;
                shakeY = CameraFlash.mainCamera.actorForTransform.py;
            }

            _offsetX1 += SpeedX1 * Time.deltaTime;
            _offsetY1 += SpeedY1 * Time.deltaTime;
            _container1.anchoredPosition = new Vector2((_offsetX1 % CellSize) + shakeX, -(_offsetY1 % CellSize) - shakeY);

            _offsetX2 += SpeedX2 * Time.deltaTime;
            _offsetY2 += SpeedY2 * Time.deltaTime;
            _container2.anchoredPosition = new Vector2((_offsetX2 % CellSize) + shakeX, -(_offsetY2 % CellSize) - shakeY);
        }

        private static void CreateGrid()
        {
            if (_canvasObject != null) return;

            _canvasObject = new GameObject("DeltaruneGridCanvas");
            _canvasObject.layer = 0;
            UnityEngine.Object.DontDestroyOnLoad(_canvasObject);

            Canvas canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = -20;

            _canvasObject.transform.position = new Vector3(0f, 0f, 1.5f);
            _canvasObject.transform.localScale = Vector3.one;

            var canvasRect = _canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1280f, 720f);

            _offsetX1 = _offsetY1 = _offsetX2 = _offsetY2 = 0f;

            Color c1 = new Color(ColorR, ColorG, ColorB, Alpha1);
            Color c2 = new Color(ColorR * 0.8f, ColorG * 0.8f, ColorB * 0.9f, Alpha1 * 0.65f);

            _container1 = BuildGridContainer("Grid1", c1);
            _container2 = BuildGridContainer("Grid2", c2);
        }

        private static RectTransform BuildGridContainer(string name, Color color)
        {
            int linesX = Mathf.CeilToInt(1280f / CellSize) + 4;
            int linesY = Mathf.CeilToInt(720f / CellSize) + 4;

            float gridW = linesX * CellSize;
            float gridH = linesY * CellSize;

            GameObject container = new GameObject(name);
            container.layer = 0;
            container.transform.SetParent(_canvasObject!.transform, false);

            var rect = container.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(gridW, gridH);
            rect.anchoredPosition = Vector2.zero;

            float startX = -gridW * 0.5f;
            for (int i = 0; i <= linesX; i++)
            {
                MakeLine(container.transform, $"V_{i}", new Vector2(startX + i * CellSize, 0f), new Vector2(LineThickness, gridH + CellSize), color);
            }

            float startY = -gridH * 0.5f;
            for (int i = 0; i <= linesY; i++)
            {
                MakeLine(container.transform, $"H_{i}", new Vector2(0f, startY + i * CellSize), new Vector2(gridW + CellSize, LineThickness), color);
            }

            return rect;
        }

        private static void MakeLine(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            GameObject line = new GameObject(name);
            line.layer = 0;
            line.transform.SetParent(parent, false);

            var r = line.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;

            var img = line.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private static void DestroyGrid()
        {
            if (_canvasObject != null)
            {
                UnityEngine.Object.Destroy(_canvasObject);
                _canvasObject = null;
                _container1 = null;
                _container2 = null;
            }
            _offsetX1 = _offsetY1 = _offsetX2 = _offsetY2 = 0f;
        }
    }

    // --- 5. ВОССТАНОВЛЕНИЕ РАЗМЕРА КЛЕТОК СТЕН ---
    public static class ConstructionWallSizeFix
    {
        public static float TargetSquareSize = 32f;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            Patch(harmony, typeof(FxConstructionSquareWallAnim));
            Patch(harmony, typeof(FxConstructionTriangleWallAnim));
            Patch(harmony, typeof(FxConstructionPentagonWallAnim));
            Patch(harmony, typeof(FxConstructionCircleWallAnim));

            ModuleRegistry.RegisterCheckbox("Restore Small Construction Squares", () => ModuleRegistry.IsActive("ConstructionFix"), (v) => ModuleRegistry.SetActive("ConstructionFix", v));
            ModuleRegistry.RegisterSlider("Construction Square Size", 8f, 60f, TargetSquareSize, v => TargetSquareSize = v);
        }

        private static void Patch(HarmonyLib.Harmony harmony, Type target)
        {
            var original = AccessTools.Method(target, "start");
            if (original != null) harmony.Patch(original, prefix: new HarmonyMethod(AccessTools.Method(typeof(ConstructionWallSizeFix), nameof(Prefix))));
        }

        private static void Prefix(object __instance)
        {
            if (!ModuleRegistry.IsActive("ConstructionFix")) return;
            var type = __instance.GetType();
            var w = type.GetField("widthPerSquare");
            var h = type.GetField("heightPerSquare");
            if (w != null && h != null)
            {
                if (w.FieldType == typeof(int)) { w.SetValue(__instance, (int)TargetSquareSize); h.SetValue(__instance, (int)TargetSquareSize); }
                else { w.SetValue(__instance, TargetSquareSize); h.SetValue(__instance, TargetSquareSize); }
            }
        }
    }
}
