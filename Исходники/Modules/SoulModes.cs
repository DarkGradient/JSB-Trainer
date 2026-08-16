//  Orange & Purple SOUL Режимы
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;
using Il2CppInterop.Runtime;

namespace jsb_new
{
    // --- ВСПОМОГАТЕЛЬНЫЙ КЛАСС БЕЗОПАСНОСТИ ---
    public static class SoulUtils
    {
        public static MetaPlayer? GetValidMetaPlayer(Hero hero, MetaPlayer? directMp)
        {
            if (directMp != null && directMp.Pointer != IntPtr.Zero)
                return directMp;

            if (hero == null || hero.Pointer == IntPtr.Zero) return null;
            if (hero.modelPlayer == null || hero.modelPlayer.Pointer == IntPtr.Zero) return null;

            var pm = PlayerManager.instance;
            if (pm == null || pm.Pointer == IntPtr.Zero) return null;

            try
            {
                var mp = pm.getFromModel(hero.modelPlayer);
                return (mp != null && mp.Pointer != IntPtr.Zero) ? mp : null;
            }
            catch
            {
                return null;
            }
        }
    }

    // --- ORANGE SOUL ---
    public static class OrangeSoul
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("OrangeSOUL");
            set => ModuleRegistry.SetActive("OrangeSOUL", value);
        }

        private static Vector2 _lastDirection = new Vector2(1f, 0f);

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroInputLocal_Update_Orange));
            ModuleRegistry.RegisterCheckbox("Orange SOUL Mode", () => Enabled, (v) => Enabled = v);
        }

        [HarmonyPatch(typeof(HeroInputLocal), "update")]
        private static class Patch_HeroInputLocal_Update_Orange
        {
            static void Postfix(HeroInputLocal __instance)
            {
                if (!Enabled || ModuleRegistry.IsActive("MouseControl") || ModuleRegistry.IsActive("PurpleSOUL")) return;

                Hero hero = __instance.hero;
                if (hero == null || hero.Pointer == IntPtr.Zero || hero.controlComponent == null) return;
                if (hero.heroInputLocal == null || !hero.heroInputLocal.enabled) return;

                MetaPlayer? mp = SoulUtils.GetValidMetaPlayer(hero, __instance.metaPlayer);
                if (mp == null || !mp.isLocalMainPlayer()) return;

                float mx = hero.controlComponent.moveInput.x;
                float my = hero.controlComponent.moveInput.y;

                if (Mathf.Abs(mx) > 0.01f || Mathf.Abs(my) > 0.01f) _lastDirection = new Vector2(mx, my).normalized;
                hero.controlComponent.moveInput.x = _lastDirection.x;
                hero.controlComponent.moveInput.y = _lastDirection.y;
            }
        }
    }

    // --- PURPLE SOUL ---
    public static class PurpleSoul
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("PurpleSOUL");
            set => SetEnabledState(value);
        }

        private static float[] LineYPositions = { -240f, -150f, 0f, 150f, 240f };
        private static int _currentLineIndex = 2;
        private static float _lastVerticalInput = 0f;
        private static bool _isTransitioning = false;

        private static GameObject? _canvasObject;
        private static GameObject? _linesContainer;
        private static GameObject[]? _uiLines;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroInputLocal_Update_Purple));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroControl_Update_Purple));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroRenderer_Squash_Purple));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroDashComponent_Start_Purple));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroControl_Start_Purple));

            ModuleRegistry.RegisterCheckbox("Purple SOUL Mode", () => ModuleRegistry.IsActive("PurpleSOUL"), (v) => Enabled = v);
        }

        public static void SetEnabledState(bool newValue)
        {
            ModuleRegistry.SetActive("PurpleSOUL", newValue);
            if (newValue)
            {
                _currentLineIndex = 2;
                _isTransitioning = false;
            }
            else
            {
                _isTransitioning = false;
                DestroyVisualLines();
            }
        }

        public static void Update()
        {
            if (!Enabled)
            {
                DestroyVisualLines();
                return;
            }

            if (IsPlayerOnLevel())
            {
                CreateVisualLines();
                UpdateShakeAndZoom();
            }
            else
            {
                DestroyVisualLines();
            }
        }

        public static bool IsPlayerOnLevel()
        {
            if (GameScene.instance == null || GameScene.instance.Pointer == IntPtr.Zero) return false;
            if (GameScene.instance.logicManager == null || GameScene.instance.logicManager.Pointer == IntPtr.Zero) return false;

            try
            {
                if (GameScene.instance.logicManager.getFirst(Il2CppType.Of<ActorNormalLevelLogic>()) != null) return true;
                if (GameScene.instance.logicManager.getFirst(Il2CppType.Of<ActorMultiplayerLevelLogic>()) != null) return true;
            }
            catch { }
            return false;
        }

        private static void UpdateShakeAndZoom()
        {
            if (_canvasObject == null || _linesContainer == null) return;

            float shakeX = 0f, shakeY = 0f;
            if (CameraFlash.mainCamera != null && CameraFlash.mainCamera.Pointer != IntPtr.Zero && CameraFlash.mainCamera.actorForTransform != null)
            {
                shakeX = CameraFlash.mainCamera.actorForTransform.px;
                shakeY = CameraFlash.mainCamera.actorForTransform.py;
            }

            _canvasObject.transform.position = new Vector3(0f, 0f, 1.5f);
            _canvasObject.transform.localScale = Vector3.one;

            var rect = _linesContainer.GetComponent<RectTransform>();
            if (rect != null) rect.anchoredPosition = new Vector2(shakeX, -shakeY);
        }

        private static void CreateVisualLines()
        {
            if (_canvasObject != null) return;

            _canvasObject = new GameObject("PurpleSoulCanvas");
            _canvasObject.layer = 0;
            UnityEngine.Object.DontDestroyOnLoad(_canvasObject);

            Canvas canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = -10;

            _canvasObject.transform.position = new Vector3(0f, 0f, 1.5f);
            _canvasObject.transform.localScale = Vector3.one;

            var rect = _canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1280f, 720f);

            _linesContainer = new GameObject("LinesContainer");
            _linesContainer.layer = 0;
            _linesContainer.transform.SetParent(_canvasObject.transform, false);

            var containerRect = _linesContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.sizeDelta = Vector2.zero;
            containerRect.anchoredPosition = Vector2.zero;

            _uiLines = new GameObject[LineYPositions.Length];

            for (int i = 0; i < LineYPositions.Length; i++)
            {
                GameObject line = new GameObject($"PurpleLine_{i}");
                line.layer = 0;
                line.transform.SetParent(_linesContainer.transform, false);

                var lineRect = line.AddComponent<RectTransform>();
                lineRect.anchorMin = new Vector2(0f, 0.5f);
                lineRect.anchorMax = new Vector2(1f, 0.5f);
                lineRect.pivot = new Vector2(0.5f, 0.5f);
                lineRect.anchoredPosition = new Vector2(0f, -LineYPositions[i]);
                lineRect.sizeDelta = new Vector2(0f, 4f);

                var image = line.AddComponent<Image>();
                image.color = new Color(0.55f, 0.15f, 0.75f, 0.45f);

                _uiLines[i] = line;
            }
        }

        private static void DestroyVisualLines()
        {
            if (_canvasObject != null)
            {
                UnityEngine.Object.Destroy(_canvasObject);
                _canvasObject = null;
                _linesContainer = null;
                _uiLines = null;
            }
        }

        [HarmonyPatch(typeof(HeroInputLocal), "update")]
        private static class Patch_HeroInputLocal_Update_Purple
        {
            static void Postfix(HeroInputLocal __instance)
            {
                if (!Enabled || ModuleRegistry.IsActive("MouseControl") || ModuleRegistry.IsActive("OrangeSOUL")) return;

                Hero hero = __instance.hero;
                if (hero == null || hero.Pointer == IntPtr.Zero || hero.controlComponent == null) return;
                if (hero.heroInputLocal == null || !hero.heroInputLocal.enabled) return;

                MetaPlayer? mp = SoulUtils.GetValidMetaPlayer(hero, __instance.metaPlayer);
                if (mp == null || !mp.isLocalMainPlayer()) return;

                float verticalInput = __instance.getVerticalAxisInput();

                if (verticalInput < -0.4f && _lastVerticalInput >= -0.4f)
                {
                    if (_currentLineIndex > 0) _currentLineIndex--;
                    else hero.heroRenderer?.animBlocked(ModelSideEnum.UP);
                }
                else if (verticalInput > 0.4f && _lastVerticalInput <= 0.4f)
                {
                    if (_currentLineIndex < LineYPositions.Length - 1) _currentLineIndex++;
                    else hero.heroRenderer?.animBlocked(ModelSideEnum.DOWN);
                }

                _lastVerticalInput = verticalInput;
                hero.controlComponent.moveInput.x = __instance.getHorizontalAxisInput();
                hero.controlComponent.moveInput.y = verticalInput;
            }
        }

        [HarmonyPatch(typeof(HeroControl), "update")]
        private static class Patch_HeroControl_Update_Purple
        {
            static void Postfix(HeroControl __instance)
            {
                if (!Enabled) return;

                __instance.speedDash = 16.5f;
                if (__instance.dashComponent != null) __instance.dashComponent.dashTime = 7.5f;

                if (ModuleRegistry.IsActive("MouseControl") || ModuleRegistry.IsActive("OrangeSOUL") || HeroControl.PAUSE_CONTROL) return;

                Hero hero = __instance.hero;
                if (hero == null || hero.Pointer == IntPtr.Zero || hero.heroInputLocal == null || !hero.heroInputLocal.enabled) return;

                MetaPlayer? mp = SoulUtils.GetValidMetaPlayer(hero, hero.metaPlayer);
                if (mp == null || !mp.isLocalMainPlayer()) return;

                float cameraY = (CameraFlash.mainCamera != null && CameraFlash.mainCamera.Pointer != IntPtr.Zero) ? CameraFlash.mainCamera.py : 0f;
                float zoom = (CameraFlash.mainCamera != null && CameraFlash.mainCamera.Pointer != IntPtr.Zero && CameraFlash.mainCamera.zoomScale > 0.001f) ? CameraFlash.mainCamera.zoomScale : 1f;

                float targetY = cameraY + (LineYPositions[_currentLineIndex] / zoom);
                float deltaY = targetY - hero.py;

                if (Mathf.Abs(deltaY) < 1f)
                {
                    hero.py = targetY;
                    if (hero.physicComponent != null) hero.physicComponent.vy = 0f;
                    _isTransitioning = false;
                }
                else
                {
                    if (hero.physicComponent != null) hero.physicComponent.vy = Mathf.Clamp(deltaY * 0.33f, -45f, 45f);
                    _isTransitioning = true;
                }
            }
        }

        [HarmonyPatch(typeof(HeroRenderer), "squash")]
        private static class Patch_HeroRenderer_Squash_Purple
        {
            static bool Prefix(HeroRenderer __instance)
            {
                if (Enabled && _isTransitioning)
                {
                    if (__instance.heroContainerMc != null)
                    {
                        __instance.heroContainerMc.scaleX = 1f;
                        __instance.heroContainerMc.scaleY = 1f;
                    }
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(HeroDashComponent), "start")]
        private static class Patch_HeroDashComponent_Start_Purple
        {
            static void Postfix(HeroDashComponent __instance)
            {
                if (Enabled) __instance.dashTime = 7.5f;
            }
        }

        [HarmonyPatch(typeof(HeroControl), "start")]
        private static class Patch_HeroControl_Start_Purple
        {
            static void Postfix(HeroControl __instance)
            {
                if (Enabled) __instance.speedDash = 16.5f;
            }
        }
    }
}
