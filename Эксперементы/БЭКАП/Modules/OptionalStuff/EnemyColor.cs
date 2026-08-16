using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace jsb_new
{
    public static class EnemyColor
    {
        private const string FEATURE_NAME = "Custom Enemy Color";
        private const string RGB_FEATURE_NAME = "Enemy RGB Mode";
        private const string SLIDER_NAME = "Enemy Color";

        private static float _hue = 0f;
        private static float _selectedHue = 0f;

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
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_RenderComponent_AddToActor));

            ModuleRegistry.RegisterCheckbox(FEATURE_NAME, () => Enabled, (enabled) => {
                Enabled = enabled;
                if (!enabled) ResetAllEnemiesOnScene();
            });

                ModuleRegistry.RegisterCheckbox(RGB_FEATURE_NAME, () => RgbEnabled, (enabled) => RgbEnabled = enabled);

                ModuleRegistry.RegisterSlider(SLIDER_NAME, 0f, 1f, 0f, (val) => _selectedHue = val);

                DebugStrings.Log("[EnemyColor] Initialized!");
        }

        public static void Update()
        {
            if (!Enabled) return;

            // Красим ТОЛЬКО врагов, fxManager не трогаем!
            RefreshEnemies(GetActiveColor());
        }

        private static Color GetActiveColor()
        {
            if (RgbEnabled)
            {
                _hue = (_hue + Time.deltaTime * 0.5f) % 1.0f;
                return Color.HSVToRGB(_hue, 1f, 1f);
            }
            return Color.HSVToRGB(_selectedHue, 1f, 1f);
        }

        private static void RefreshEnemies(Color color)
        {
            var gameScene = GameScene.instance;
            if (gameScene?.enemyManager == null) return;

            var actorList = gameScene.enemyManager.actorList;
            if (actorList == null) return;

            for (int i = 0; i < actorList.Count; i++)
            {
                var actor = actorList[i];
                if (actor != null && !actor.destroyed && actor.renderComponent != null)
                {
                    ApplyColorToRenderComponent(actor.renderComponent, color);
                }
            }
        }

        public static void ResetAllEnemiesOnScene()
        {
            var gameScene = GameScene.instance;
            if (gameScene?.enemyManager == null) return;

            var actorList = gameScene.enemyManager.actorList;
            if (actorList == null) return;

            for (int i = 0; i < actorList.Count; i++)
            {
                var actor = actorList[i];
                if (actor != null && !actor.destroyed && actor.renderComponent != null)
                {
                    ResetRenderComponent(actor.renderComponent);
                }
            }
        }

        public static void ApplyColorToRenderComponent(RenderComponent renderComp, Color color)
        {
            if (renderComp?.animView?.anim == null) return;
            uint uintColor = ColorToUint(color);
            Utils.setColorUnity(renderComp.animView.anim, uintColor);
        }

        public static void ResetRenderComponent(RenderComponent renderComp)
        {
            if (renderComp?.animView?.anim == null) return;
            Utils.resetColor(renderComp.animView.anim);
        }

        private static uint ColorToUint(Color color)
        {
            byte r = (byte)(Mathf.Clamp01(color.r) * 255);
            byte g = (byte)(Mathf.Clamp01(color.g) * 255);
            byte b = (byte)(Mathf.Clamp01(color.b) * 255);
            return (uint)((r << 16) | (g << 8) | b);
        }

        [HarmonyPatch(typeof(RenderComponent), nameof(RenderComponent.addToActor))]
        private static class Patch_RenderComponent_AddToActor
        {
            static void Postfix(RenderComponent __result, Actor actor)
            {
                if (!Enabled || __result == null) return;
                if (actor is Spawn) ApplyColorToRenderComponent(__result, GetActiveColor());
            }
        }
    }
}
