// using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace jsb_new
{
    public static class PlayerColor
    {
        private const string FEATURE_NAME = "Custom Player Color";
        private const string RGB_FEATURE_NAME = "Player RGB Mode";
        private const string SLIDER_NAME = "Player Color";

        private static readonly Color OrangeColor = new Color(1f, 0.4f, 0f);        // Оранжевый
        private static readonly Color PurpleColor = new Color(0.7f, 0.1f, 0.95f);   // Фиолетовый

        private static float _hue = 0f;
        private static float _selectedHue = 0f;
        private static bool _wasApplied = false;

        public static bool Enabled
        {
            get => ModuleRegistry.IsActive(FEATURE_NAME);
            set => ModuleRegistry.SetActive(FEATURE_NAME, value);
        }

        public static bool RgbEnabled
        {
            get => ModuleRegistry.IsActive(RGB_FEATURE_NAME);
            set => ModuleRegistry.SetActive(RGB_FEATURE_NAME, value);
        }

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            // 1. Главный тумблер
            ModuleRegistry.RegisterCheckbox(
                FEATURE_NAME,
                () => Enabled,
                                            (enabled) =>
                                            {
                                                Enabled = enabled;
                                                HUDManager.CreateToast(
                                                    enabled ? "PLAYER COLOR: ON" : "PLAYER COLOR: OFF",
                                                    enabled ? Color.green : Color.gray,
                                                    1.5f
                                                );
                                                if (!enabled && !RgbEnabled) ResetPlayerColor();
                                            },
                                            isLocked: () => ModuleRegistry.IsActive("OrangeSoul") ||
                                            ModuleRegistry.IsActive("Orange SOUL Mode") ||
                                            ModuleRegistry.IsActive("PurpleSoul") ||
                                            ModuleRegistry.IsActive("Purple SOUL Mode")
            );

            // 2. RGB Режим
            ModuleRegistry.RegisterCheckbox(
                RGB_FEATURE_NAME,
                () => RgbEnabled,
                                            (enabled) =>
                                            {
                                                RgbEnabled = enabled;
                                                HUDManager.CreateToast(
                                                    enabled ? "PLAYER RGB: ON" : "PLAYER RGB: OFF",
                                                    enabled ? Color.green : Color.gray,
                                                    1.5f
                                                );
                                            }
            );

            // 3. Слайдер цвета
            ModuleRegistry.RegisterSlider(
                SLIDER_NAME,
                0f,
                (val) =>
                {
                    _selectedHue = val;
                }
            );

            DebugStrings.Log($"[PlayerColor] Initialized!");
        }

        public static void Update()
        {
            Color? targetColor = GetTargetColorFromRegistry();

            if (!targetColor.HasValue)
            {
                if (_wasApplied)
                {
                    ResetPlayerColor();
                    _wasApplied = false;
                }
                return;
            }

            var gameScene = GameScene.instance;
            if (gameScene == null || gameScene.heroManager == null)
                return;

            var actorList = gameScene.heroManager.actorList;
            if (actorList == null) return;

            for (int i = 0; i < actorList.Count; i++)
            {
                var actor = actorList[i];
                if (actor == null || actor.destroyed)
                    continue;

                var hero = actor.TryCast<Hero>();
                if (hero == null)
                    continue;

                ApplyColorToHero(hero, targetColor.Value);
                _wasApplied = true;
            }
        }

        private static Color? GetTargetColorFromRegistry()
        {
            // 1. Приоритет: Orange SOUL
            if (ModuleRegistry.IsActive("OrangeSoul") || ModuleRegistry.IsActive("Orange SOUL Mode"))
                return OrangeColor;

            // 2. Приоритет: Purple SOUL
            if (ModuleRegistry.IsActive("PurpleSoul") || ModuleRegistry.IsActive("Purple SOUL Mode"))
                return PurpleColor;

            // 3. RGB Режим (теперь работает независимо, если включен сам RGB)
            if (RgbEnabled)
            {
                _hue = (_hue + Time.deltaTime * 0.5f) % 1.0f;
                return Color.HSVToRGB(_hue, 1f, 1f);
            }

            // 4. Статичный цвет со слайдера (работает, если включен главный тумблер)
            if (Enabled)
            {
                return Color.HSVToRGB(_selectedHue, 1f, 1f);
            }

            return null;
        }

        private static void ApplyColorToHero(Hero hero, Color color)
        {
            uint uintColor = ColorToUint(color);

            if (hero.heroRenderer?.heroMc != null)
            {
                Utils.setColorUnity(hero.heroRenderer.heroMc, uintColor);
            }
            else if (hero.heroRenderer?.heroContainerMc != null)
            {
                Utils.setColorUnity(hero.heroRenderer.heroContainerMc, uintColor);
            }
        }

        private static uint ColorToUint(Color color)
        {
            byte r = (byte)(Mathf.Clamp01(color.r) * 255);
            byte g = (byte)(Mathf.Clamp01(color.g) * 255);
            byte b = (byte)(Mathf.Clamp01(color.b) * 255);

            return (uint)((r << 16) | (g << 8) | b);
        }

        private static void ResetPlayerColor()
        {
            var gameScene = GameScene.instance;
            if (gameScene == null || gameScene.heroManager == null)
                return;

            float randomHue = UnityEngine.Random.value;
            Color randomColor = Color.HSVToRGB(randomHue, 1f, 1f);

            var actorList = gameScene.heroManager.actorList;
            if (actorList == null) return;

            for (int i = 0; i < actorList.Count; i++)
            {
                var hero = actorList[i]?.TryCast<Hero>();
                if (hero != null)
                {
                    ApplyColorToHero(hero, randomColor);
                }
            }
        }
    }
}
