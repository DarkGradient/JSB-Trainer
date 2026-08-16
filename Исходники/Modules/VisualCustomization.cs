// Цвета игрока, врагов и Full Power Trail (исправлен)
using Il2Cpp;
using UnityEngine;

namespace jsb_new
{
    // --- 1. ПАРТИКЛЫ ТРЕЙЛА ИГРОКА ---
    public static class Trail
    {
        private const string FEATURE_NAME = "Full Power Trail";

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            ModuleRegistry.RegisterCheckbox(FEATURE_NAME, () => ModuleRegistry.IsActive(FEATURE_NAME), (enabled) => {
                ModuleRegistry.SetActive(FEATURE_NAME, enabled);
                if (!enabled) ResetPlayerParticles();
            });
        }

        public static void Update()
        {
            if (!ModuleRegistry.IsActive(FEATURE_NAME)) return;

            var gameScene = MainGame.instance?.gameSceneManager?.gameScene;
            if (gameScene?.heroManager == null) return;

            var actorList = gameScene.heroManager.actorList;
            for (int i = 0; i < actorList.Count; i++)
            {
                var hero = actorList[i]?.TryCast<Hero>();
                if (hero == null || hero.destroyed) continue;

                if (hero.particuleComponent == null || hero.particuleComponent.TryCast<HeroParticuleComponentFullPower>() == null)
                {
                    try { hero.particuleComponent?.destroy(); } catch { }
                    var fullPowerParticles = new HeroParticuleComponentFullPower();
                    hero.switchParticleComponent(fullPowerParticles);
                }
            }
        }

        private static void ResetPlayerParticles()
        {
            var gameScene = MainGame.instance?.gameSceneManager?.gameScene;
            if (gameScene?.heroManager == null) return;

            var actorList = gameScene.heroManager.actorList;
            for (int i = 0; i < actorList.Count; i++)
            {
                var hero = actorList[i]?.TryCast<Hero>();
                if (hero != null && !hero.destroyed)
                {
                    try { hero.particuleComponent?.destroy(); } catch { }
                    var defaultParticles = new HeroParticuleComponent();
                    hero.switchParticleComponent(defaultParticles);
                }
            }
        }
    }

    // --- 2. ЦВЕТ ИГРОКА ---
    public static class PlayerColor
    {
        private const string FEATURE_NAME = "Custom Player Color";
        private const string RGB_FEATURE_NAME = "Player RGB Mode";
        private const string SLIDER_NAME = "Player Color";

        private static readonly Color OrangeColor = new Color(1f, 0.4f, 0f);
        private static readonly Color PurpleColor = new Color(0.7f, 0.1f, 0.95f);

        private static float _hue = 0f;
        private static float _selectedHue = 0f;
        private static bool _wasApplied = false;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            ModuleRegistry.RegisterCheckbox(FEATURE_NAME, () => ModuleRegistry.IsActive(FEATURE_NAME), (enabled) => {
                ModuleRegistry.SetActive(FEATURE_NAME, enabled);
                if (!enabled && !ModuleRegistry.IsActive(RGB_FEATURE_NAME)) ResetPlayerColor();
            });

            ModuleRegistry.RegisterCheckbox(RGB_FEATURE_NAME, () => ModuleRegistry.IsActive(RGB_FEATURE_NAME), (enabled) => {
                ModuleRegistry.SetActive(RGB_FEATURE_NAME, enabled);
            });

            ModuleRegistry.RegisterSlider(SLIDER_NAME, 0f, 1f, 0f, (val) => _selectedHue = val);
        }

        public static void Update()
        {
            Color? targetColor = GetTargetColor();
            if (!targetColor.HasValue)
            {
                if (_wasApplied) { ResetPlayerColor(); _wasApplied = false; }
                return;
            }

            var gameScene = GameScene.instance;
            if (gameScene?.heroManager?.actorList == null) return;

            var actorList = gameScene.heroManager.actorList;
            for (int i = 0; i < actorList.Count; i++)
            {
                var hero = actorList[i]?.TryCast<Hero>();
                if (hero != null && !hero.destroyed)
                {
                    ApplyColorToHero(hero, targetColor.Value);
                    _wasApplied = true;
                }
            }
        }

        private static Color? GetTargetColor()
        {
            if (ModuleRegistry.IsActive("OrangeSOUL") || ModuleRegistry.IsActive("Orange SOUL Mode")) return OrangeColor;
            if (ModuleRegistry.IsActive("PurpleSOUL") || ModuleRegistry.IsActive("Purple SOUL Mode")) return PurpleColor;

            if (ModuleRegistry.IsActive(RGB_FEATURE_NAME))
            {
                _hue = (_hue + Time.deltaTime * 0.5f) % 1.0f;
                return Color.HSVToRGB(_hue, 1f, 1f);
            }

            if (ModuleRegistry.IsActive(FEATURE_NAME)) return Color.HSVToRGB(_selectedHue, 1f, 1f);
            return null;
        }

        private static void ApplyColorToHero(Hero hero, Color color)
        {
            uint uintColor = VisualUtils.ColorToUint(color);
            if (hero.heroRenderer?.heroMc != null) Utils.setColorUnity(hero.heroRenderer.heroMc, uintColor);
            else if (hero.heroRenderer?.heroContainerMc != null) Utils.setColorUnity(hero.heroRenderer.heroContainerMc, uintColor);
        }

        private static void ResetPlayerColor()
        {
            var gameScene = GameScene.instance;
            if (gameScene?.heroManager?.actorList == null) return;

            var actorList = gameScene.heroManager.actorList;
            for (int i = 0; i < actorList.Count; i++)
            {
                var hero = actorList[i]?.TryCast<Hero>();
                if (hero != null)
                {
                    if (hero.heroRenderer?.heroMc != null) Utils.resetColor(hero.heroRenderer.heroMc);
                    else if (hero.heroRenderer?.heroContainerMc != null) Utils.resetColor(hero.heroRenderer.heroContainerMc);
                }
            }
        }
    }

    // --- 3. ЦВЕТ ВРАГОВ И ТОРНАДО-РЕЖИМ ---
    public static class EnemyColor
    {
        private const string FEATURE_NAME = "Custom Enemy Color";
        private const string RGB_FEATURE_NAME = "Enemy RGB Mode";
        private const string SLIDER_NAME = "Enemy Color";

        private static float _hue = 0f;
        private static float _selectedHue = 0f;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            ModuleRegistry.RegisterCheckbox(FEATURE_NAME, () => ModuleRegistry.IsActive(FEATURE_NAME), (enabled) => {
                ModuleRegistry.SetActive(FEATURE_NAME, enabled);
                if (!enabled) ResetAllEnemies();
            });

            ModuleRegistry.RegisterCheckbox(RGB_FEATURE_NAME, () => ModuleRegistry.IsActive(RGB_FEATURE_NAME), (enabled) => {
                ModuleRegistry.SetActive(RGB_FEATURE_NAME, enabled);
            });

            ModuleRegistry.RegisterSlider(SLIDER_NAME, 0f, 1f, 0f, (val) => _selectedHue = val);

            // Торнадо режим
            ModuleRegistry.RegisterCheckbox("Tornado / Spin Mode", () => ModuleRegistry.IsActive("Tornado / Spin Mode"), (enabled) => {
                ModuleRegistry.SetActive("Tornado / Spin Mode", enabled);
            });
            ModuleRegistry.RegisterSlider("Spin Speed", -100f, 100f, 100f, (v) => EnemySpinner.RotationSpeed = (v / 100f) * 1800f);
        }

        public static void Update()
        {
            if (ModuleRegistry.IsActive(FEATURE_NAME))
            {
                RefreshEnemies(GetActiveColor());
            }
            EnemySpinner.Update();
        }

        private static Color GetActiveColor()
        {
            if (ModuleRegistry.IsActive(RGB_FEATURE_NAME))
            {
                _hue = (_hue + Time.deltaTime * 0.5f) % 1.0f;
                return Color.HSVToRGB(_hue, 1f, 1f);
            }
            return Color.HSVToRGB(_selectedHue, 1f, 1f);
        }

        private static void RefreshEnemies(Color color)
        {
            var gameScene = GameScene.instance;
            if (gameScene?.enemyManager?.actorList == null) return;

            var actorList = gameScene.enemyManager.actorList;
            uint uintColor = VisualUtils.ColorToUint(color);

            for (int i = 0; i < actorList.Count; i++)
            {
                var actor = actorList[i];
                if (actor != null && !actor.destroyed && actor.renderComponent?.animView?.anim != null)
                {
                    Utils.setColorUnity(actor.renderComponent.animView.anim, uintColor);
                }
            }
        }

        private static void ResetAllEnemies()
        {
            var gameScene = GameScene.instance;
            if (gameScene?.enemyManager?.actorList == null) return;

            var actorList = gameScene.enemyManager.actorList;
            for (int i = 0; i < actorList.Count; i++)
            {
                var actor = actorList[i];
                if (actor != null && !actor.destroyed && actor.renderComponent?.animView?.anim != null)
                {
                    Utils.resetColor(actor.renderComponent.animView.anim);
                }
            }
        }
    }

    public static class EnemySpinner
    {
        public static float RotationSpeed = 1800f;

        public static void Update()
        {
            if (!ModuleRegistry.IsActive("Tornado / Spin Mode") || Mathf.Approximately(RotationSpeed, 0f)) return;

            var gameScene = GameScene.instance;
            if (gameScene?.enemyManager?.actorList == null) return;

            var actorList = gameScene.enemyManager.actorList;
            float deltaRotation = Time.deltaTime * RotationSpeed;

            for (int i = 0; i < actorList.Count; i++)
            {
                var actor = actorList[i];
                if (actor != null && !actor.destroyed && actor.renderComponent?.animView != null)
                {
                    actor.renderComponent.animView.rotation += deltaRotation;
                }
            }
        }
    }

    public static class VisualUtils
    {
        public static uint ColorToUint(Color color)
        {
            byte r = (byte)(Mathf.Clamp01(color.r) * 255);
            byte g = (byte)(Mathf.Clamp01(color.g) * 255);
            byte b = (byte)(Mathf.Clamp01(color.b) * 255);
            return (uint)((r << 16) | (g << 8) | b);
        }
    }
}
