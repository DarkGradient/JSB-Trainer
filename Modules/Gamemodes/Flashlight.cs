using Il2Cpp;
using UnityEngine;
using Il2CppInterop.Runtime;

namespace jsb_new
{
    public static class Flashlight
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("Flashlight");
            set
            {
                ModuleRegistry.SetActive("Flashlight", value);
                if (!value && _texture != null)
                {
                    UnityEngine.Object.Destroy(_texture);
                    _texture = null;
                }
            }
        }

        // --- Настройки ---
        public static float BaseRadius = 180f;        // Радиус пятна в пикселях (1280x720)
        private const float PulseAmplitude = 0.08f;   // ±8% дыхание
        private const float PulseSpeed = 1.6f;        // Скорость пульсации
        private const float MoveMultiplierMax = 1.5f;  // Расширение при беге
        private const float MoveSpeedForMax = 900f;   // Скорость персонажа
        private const float RadiusSmoothTime = 0.18f; // Сглаживание изменения размера

        private const int TexRes = 256;
        private const float InnerNorm = 0.20f;  // Прозрачный центр
        private const float OuterNorm = 0.40f;  // Граница перехода в 100% черный цвет

        private static Texture2D? _texture;
        private static float _currentRadius = -1f;
        private static float _radiusVelocity;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            ModuleRegistry.RegisterCheckbox("Gamemodes", "Flashlight", () => Enabled, (v) => Enabled = v, order: 60);

            DebugStrings.Log("[Flashlight] Initialized");
        }

        public static bool IsPlayerOnLevel()
        {
            if (GameScene.instance == null || GameScene.instance.destroyed)
                return false;

            try
            {
                if (GameScene.instance.TryCast<GameplayScene>() != null)
                    return true;

                if (GameScene.instance.logicManager != null)
                {
                    var normalType = Il2CppType.Of<ActorNormalLevelLogic>();
                    if (GameScene.instance.logicManager.getFirst(normalType) != null) return true;

                    var multiType = Il2CppType.Of<ActorMultiplayerLevelLogic>();
                    if (GameScene.instance.logicManager.getFirst(multiType) != null) return true;

                    var challengeType = Il2CppType.Of<ActorChallengeLevelLogic>();
                    if (GameScene.instance.logicManager.getFirst(challengeType) != null) return true;

                    var tutorialType = Il2CppType.Of<ActorTutorialLevelLogic>();
                    if (GameScene.instance.logicManager.getFirst(tutorialType) != null) return true;
                }
            }
            catch { }

            return false;
        }

        public static bool IsMenuOrPauseOpen()
        {
            try
            {
                if (HeroControl.PAUSE_CONTROL) return true;
                if (GameScene.instance != null && GameScene.instance.IsPaused) return true;
            }
            catch { }

            return false;
        }

        public static void Update()
        {
            if (!Enabled || !IsPlayerOnLevel() || IsMenuOrPauseOpen()) return;

            float targetRadius = BaseRadius;
            try
            {
                targetRadius *= 1f + PulseAmplitude * Mathf.Sin(Time.unscaledTime * PulseSpeed);

            }
            catch { }

            if (_currentRadius < 0f) _currentRadius = targetRadius;
            _currentRadius = Mathf.SmoothDamp(_currentRadius, targetRadius, ref _radiusVelocity, RadiusSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        public static void OnGUI()
        {
            if (!Enabled || !IsPlayerOnLevel() || IsMenuOrPauseOpen()) return;

            Vector2? screenPos = GetHeroScreenPos();
            if (screenPos == null) return;

            if (_texture == null) _texture = BuildGradientTexture();

            var cam = CameraFlash.mainCamera;
            float zoom = (cam != null && cam.zoomScale > 0.001f) ? cam.zoomScale : 1f;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);

            float radius = _currentRadius > 0f ? _currentRadius : BaseRadius;
            float effectiveRadius = radius * zoom * scale;

            float half = effectiveRadius / InnerNorm;
            float rectSize = 2f * half;

            Rect rect = new Rect(
                Mathf.Round(screenPos.Value.x - half),
                                 Mathf.Round(screenPos.Value.y - half),
                                 Mathf.Round(rectSize),
                                 Mathf.Round(rectSize)
            );

            int prevDepth = GUI.depth;
            GUI.depth = -1000;

            Color prevColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, _texture, ScaleMode.StretchToFill, true);

            FillOutsideWithBlackSeamless(rect);

            GUI.color = prevColor;
            GUI.depth = prevDepth;
        }

        private static void FillOutsideWithBlackSeamless(Rect rect)
        {
            Texture2D white = Texture2D.whiteTexture;
            GUI.color = Color.black;

            float sw = Screen.width;
            float sh = Screen.height;
            const float over = 2f;

            if (rect.yMin > 0f)
                GUI.DrawTexture(new Rect(0f, 0f, sw, rect.yMin + over), white);

            if (rect.yMax < sh)
                GUI.DrawTexture(new Rect(0f, rect.yMax - over, sw, sh - (rect.yMax - over)), white);

            if (rect.xMin > 0f)
                GUI.DrawTexture(new Rect(0f, rect.yMin, rect.xMin + over, rect.height), white);

            if (rect.xMax < sw)
                GUI.DrawTexture(new Rect(rect.xMax - over, rect.yMin, sw - (rect.xMax - over), rect.height), white);
        }

        private static Vector2? GetHeroScreenPos()
        {
            Hero? hero = GetLocalHero();
            if (hero == null) return null;

            var cam = CameraFlash.mainCamera;
            if (cam == null) return null;

            float zoom = cam.zoomScale > 0.001f ? cam.zoomScale : 1f;

            float shakeX = cam.actorForTransform != null ? cam.actorForTransform.px : 0f;
            float shakeY = cam.actorForTransform != null ? cam.actorForTransform.py : 0f;

            // ИСПРАВЛЕНИЕ: + shakeX и + shakeY для правильного направления тряски
            float gameX = 640f + (hero.px - cam.px + shakeX) * zoom;
            float gameY = 360f + (hero.py - cam.py + shakeY) * zoom;

            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            float offsetX = (Screen.width - 1280f * scale) * 0.5f;
            float offsetY = (Screen.height - 720f * scale) * 0.5f;

            return new Vector2(offsetX + gameX * scale, offsetY + gameY * scale);
        }

        private static Hero? GetLocalHero()
        {
            if (PlayerManager.instance == null) return null;
            var player = PlayerManager.instance.GetFirstLocalPlayer();
            if (player == null || player.modelPlayer == null || player.modelPlayer.Pointer == System.IntPtr.Zero)
                return null;

            try { return Hero.getHeroFromModelPlayer(player.modelPlayer); }
            catch { return null; }
        }

        private static Texture2D BuildGradientTexture()
        {
            var tex = new Texture2D(TexRes, TexRes, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            float center = TexRes / 2f;
            for (int y = 0; y < TexRes; y++)
            {
                for (int x = 0; x < TexRes; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = Mathf.InverseLerp(InnerNorm, OuterNorm, d);
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.Clamp01(alpha)));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
