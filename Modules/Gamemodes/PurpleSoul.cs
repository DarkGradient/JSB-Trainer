using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Il2CppInterop.Runtime;

namespace jsb_new
{
    public static class PurpleSoul
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("PurpleSoul");
            set => ModuleRegistry.SetActive("PurpleSoul", value);
        }

        private static float[] LineYPositions = { -240f, -150f, 0f, 150f, 240f };
        private static int _currentLineIndex = 2;
        private static float _lastVerticalInput = 0f;
        private static bool _isTransitioning = false;
        private static bool _wasEnabled = false;

        private static GameObject _canvasObject = null!;
        private static GameObject _linesContainer = null!;
        private static GameObject[] _uiLines = null!;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroInputLocal_Update_PurpleSoul));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroControl_Update_PurpleSoul));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_KeyManager_Update_PurpleSoul));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroRenderer_Squash_PurpleSoul));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroDashComponent_Start_PurpleSoul));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroControl_Start_PurpleSoul));

            ModuleRegistry.RegisterCheckbox("Purple SOUL Mode",
                                            () => Enabled,
                                            (newValue) => SetEnabledState(newValue),
                                            isLocked: () => ModuleRegistry.IsActive("MouseControl") || ModuleRegistry.IsActive("OrangeSoul")
            );
        }

        public static void SetEnabledState(bool newValue)
        {
            Enabled = newValue;
            DebugStrings.Log($"PurpleSoul mode changed to: {newValue}");
            if (newValue)
            {
                ResetLinesToDefault();
                _currentLineIndex = 2;
                _isTransitioning = false;
            }
            else
            {
                _isTransitioning = false;
                DestroyVisualLines();
            }
        }

        public static bool IsPlayerOnLevel()
        {
            if (GameScene.instance == null || GameScene.instance.logicManager == null)
                return false;

            try
            {
                var normalType = Il2CppType.Of<ActorNormalLevelLogic>();
                if (GameScene.instance.logicManager.getFirst(normalType) != null)
                    return true;

                var multiType = Il2CppType.Of<ActorMultiplayerLevelLogic>();
                if (GameScene.instance.logicManager.getFirst(multiType) != null)
                    return true;
            }
            catch
            {
            }

            return false;
        }

        private static void RestoreDefaultDashStats()
        {
            try
            {
                if (!IsPlayerOnLevel()) return;
                if (PlayerManager.instance == null) return;

                MetaPlayer firstLocalPlayer = PlayerManager.instance.GetFirstLocalPlayer();
                if (firstLocalPlayer == null) return;

                Hero hero = Hero.getHeroFromModelPlayer(firstLocalPlayer.modelPlayer);
                if (hero == null) return;

                if (hero.controlComponent != null)
                {
                    hero.controlComponent.speedDash = 20f;
                }
                if (hero.dashComponent != null)
                {
                    hero.dashComponent.dashTime = 9f;
                }

                _isTransitioning = false;
                DebugStrings.Log("PurpleSoul: dash stats successfully restored to default JSaB values");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[PurpleSoul] Не удалось восстановить статы рывка: {ex.Message}");
            }
        }

        public static bool IsTransitioning()
        {
            return Enabled && _isTransitioning;
        }

        private static void UpdateShakeAndZoom()
        {
            if (_canvasObject == null || _linesContainer == null) return;

            float shakeX = 0f;
            float shakeY = 0f;

            if (CameraFlash.mainCamera != null)
            {
                if (CameraFlash.mainCamera.actorForTransform != null)
                {
                    shakeX = CameraFlash.mainCamera.actorForTransform.px;
                    shakeY = CameraFlash.mainCamera.actorForTransform.py;
                }
            }

            _canvasObject.transform.position = new Vector3(0f, 0f, 1.5f);
            _canvasObject.transform.localScale = Vector3.one;

            var rect = _linesContainer.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(shakeX, -shakeY);
            }
        }

        private static void ResetLinesToDefault()
        {
            if (LineYPositions.Length != 5)
            {
                LineYPositions = new float[5];
            }
            LineYPositions[0] = -240f;
            LineYPositions[1] = -150f;
            LineYPositions[2] = 0f;
            LineYPositions[3] = 150f;
            LineYPositions[4] = 240f;
            UpdateAllVisualLines();
        }

        private static void UpdateVisualLinePosition(int index)
        {
            if (_uiLines == null || index < 0 || index >= _uiLines.Length)
                return;

            GameObject line = _uiLines[index];
            if (line != null)
            {
                var rect = line.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition = new Vector2(0f, -LineYPositions[index]);
                }
            }
        }

        private static void UpdateAllVisualLines()
        {
            if (_uiLines == null) return;
            for (int i = 0; i < LineYPositions.Length; i++)
            {
                UpdateVisualLinePosition(i);
            }
        }

        public static void CreateVisualLines()
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

        public static void DestroyVisualLines()
        {
            if (_canvasObject != null)
            {
                UnityEngine.Object.Destroy(_canvasObject);
                _canvasObject = null!;
                _linesContainer = null!;
                _uiLines = null!;
            }
        }

        [HarmonyPatch(typeof(KeyManager), "update")]
        private static class Patch_KeyManager_Update_PurpleSoul
        {
            static void Postfix()
            {
                if (!Enabled)
                {
                    DestroyVisualLines();

                    if (_wasEnabled)
                    {
                        RestoreDefaultDashStats();
                        _wasEnabled = false;
                    }
                    return;
                }

                _wasEnabled = true;

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
        }

        [HarmonyPatch(typeof(HeroInputLocal), "update")]
        private static class Patch_HeroInputLocal_Update_PurpleSoul
        {
            static void Postfix(HeroInputLocal __instance)
            {
                if (!Enabled || ModuleRegistry.IsActive("MouseControl") || ModuleRegistry.IsActive("OrangeSoul"))
                    return;

                Hero hero = __instance.hero;
                if (hero == null || hero.controlComponent == null)
                    return;

                if (hero.heroInputLocal == null || !hero.heroInputLocal.enabled)
                    return;

                MetaPlayer mp = __instance.metaPlayer;
                if (mp == null && PlayerManager.instance != null)
                {
                    mp = PlayerManager.instance.getFromModel(hero.modelPlayer);
                }

                if (mp == null || !mp.isLocalMainPlayer())
                    return;

                float verticalInput = __instance.getVerticalAxisInput();

                if (verticalInput < -0.4f && _lastVerticalInput >= -0.4f)
                {
                    if (_currentLineIndex > 0)
                    {
                        _currentLineIndex--;
                    }
                    else
                    {
                        if (hero.heroRenderer != null)
                        {
                            hero.heroRenderer.animBlocked(ModelSideEnum.UP);
                        }
                    }
                }
                else if (verticalInput > 0.4f && _lastVerticalInput <= 0.4f)
                {
                    if (_currentLineIndex < LineYPositions.Length - 1)
                    {
                        _currentLineIndex++;
                    }
                    else
                    {
                        if (hero.heroRenderer != null)
                        {
                            hero.heroRenderer.animBlocked(ModelSideEnum.DOWN);
                        }
                    }
                }

                _lastVerticalInput = verticalInput;

                float horizontalInput = __instance.getHorizontalAxisInput();
                hero.controlComponent.moveInput.x = horizontalInput;
                hero.controlComponent.moveInput.y = verticalInput;
            }
        }

        [HarmonyPatch(typeof(HeroControl), "update")]
        private static class Patch_HeroControl_Update_PurpleSoul
        {
            static void Postfix(HeroControl __instance)
            {
                if (!Enabled)
                    return;

                __instance.speedDash = 16.5f;
                if (__instance.dashComponent != null)
                {
                    __instance.dashComponent.dashTime = 7.5f;
                }

                if (ModuleRegistry.IsActive("MouseControl") || ModuleRegistry.IsActive("OrangeSoul") || HeroControl.PAUSE_CONTROL)
                    return;

                Hero hero = __instance.hero;
                if (hero == null)
                    return;

                if (hero.heroInputLocal == null || !hero.heroInputLocal.enabled)
                    return;

                MetaPlayer mp = hero.metaPlayer;
                if (mp == null && PlayerManager.instance != null)
                {
                    mp = PlayerManager.instance.getFromModel(hero.modelPlayer);
                }

                if (mp == null || !mp.isLocalMainPlayer())
                    return;

                float cameraY = 0f;
                float zoom = 1f;

                if (CameraFlash.mainCamera != null)
                {
                    cameraY = CameraFlash.mainCamera.py;
                    if (CameraFlash.mainCamera.zoomScale > 0.001f)
                    {
                        zoom = CameraFlash.mainCamera.zoomScale;
                    }
                }

                float targetY = cameraY + (LineYPositions[_currentLineIndex] / zoom);
                float deltaY = targetY - hero.py;

                if (Mathf.Abs(deltaY) < 1f)
                {
                    hero.py = targetY;
                    if (hero.physicComponent != null)
                    {
                        hero.physicComponent.vy = 0f;
                    }
                    _isTransitioning = false;
                }
                else
                {
                    if (hero.physicComponent != null)
                    {
                        hero.physicComponent.vy = Mathf.Clamp(deltaY * 0.33f, -45f, 45f);
                    }
                    _isTransitioning = true;
                }
            }
        }

        [HarmonyPatch(typeof(HeroRenderer), "squash")]
        private static class Patch_HeroRenderer_Squash_PurpleSoul
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
        private static class Patch_HeroDashComponent_Start_PurpleSoul
        {
            static void Postfix(HeroDashComponent __instance)
            {
                if (Enabled)
                {
                    __instance.dashTime = 7.5f;
                }
            }
        }

        [HarmonyPatch(typeof(HeroControl), "start")]
        private static class Patch_HeroControl_Start_PurpleSoul
        {
            static void Postfix(HeroControl __instance)
            {
                if (Enabled)
                {
                    __instance.speedDash = 16.5f;
                }
            }
        }
    }
}
