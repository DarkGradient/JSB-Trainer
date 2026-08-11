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
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_RenderComponent_AddToActor));

            ModuleRegistry.RegisterCheckbox(
                FEATURE_NAME,
                () => Enabled,
                                            (enabled) =>
                                            {
                                                Enabled = enabled;
                                                HUDManager.CreateToast(
                                                    enabled ? "ENEMY COLOR: ON" : "ENEMY COLOR: OFF",
                                                    enabled ? Color.green : Color.gray,
                                                    1.5f
                                                );

                                                if (enabled) RefreshAllEnemiesOnScene(GetActiveColor());
                                                else ResetAllEnemiesOnScene();
                                            }
            );

            ModuleRegistry.RegisterCheckbox(
                RGB_FEATURE_NAME,
                () => RgbEnabled,
                                            (enabled) =>
                                            {
                                                RgbEnabled = enabled;
                                                if (Enabled)
                                                {
                                                    RefreshAllEnemiesOnScene(GetActiveColor());
                                                }
                                            }
            );

            ModuleRegistry.RegisterSlider(
                SLIDER_NAME,
                0f,
                (val) =>
                {
                    _selectedHue = val;
                    if (Enabled && !RgbEnabled)
                    {
                        RefreshAllEnemiesOnScene(GetActiveColor());
                    }
                }
            );

            DebugStrings.Log("[EnemyColor] Initialized!");
        }

        public static void Update()
        {
            if (!Enabled)
            {
                if (_wasApplied)
                {
                    ResetAllEnemiesOnScene();
                    _wasApplied = false;
                }
                return;
            }

            // Заставляем перекрашивать в Update всегда, когда включен кастомный цвет
            RefreshAllEnemiesOnScene(GetActiveColor());
            _wasApplied = true;
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

        public static void RefreshAllEnemiesOnScene(Color color)
        {
            var gameScene = GameScene.instance;
            if (gameScene == null)
                return;

            // enemyManager: враги, боссы, спайки, лучи и т.п. (Actor / Enemy / Spawn)
            RecolorEnemyManager(gameScene.enemyManager, color);

            // fxManager: GameEffect и все его потомки (FxBossNewGameCircle, BossNewGamePart1Spiral и т.д.)
            // регистрируются САМИ через GameScene.instance.fxManager.add(this) внутри GameEffect,
            // а не в enemyManager — поэтому без этого прохода они остаются некрашеными.
            RecolorEnemyManager(gameScene.fxManager, color);
        }

        // Настоящий сброс: не подбор цвета, а полный clear() colorTransform-а,
        // после чего DisplayObject возвращается к своему оригинальному виду.
        public static void ResetAllEnemiesOnScene()
        {
            var gameScene = GameScene.instance;
            if (gameScene == null)
                return;

            ResetEnemyManager(gameScene.enemyManager);
            ResetEnemyManager(gameScene.fxManager);
        }

        // Принимает ActorObjectManager (тип и enemyManager, и fxManager) — не типизируем
        // саму коллекцию actorList явно, т.к. не видели её точную сигнатуру (VectorFlash<T>/List<T>/др.).
        private static void RecolorEnemyManager(ActorObjectManager manager, Color color)
        {
            var actorList = manager?.actorList;
            if (actorList == null) return;

            for (int i = 0; i < actorList.Count; i++)
            {
                var actor = actorList[i];
                if (actor == null || actor.destroyed)
                    continue;

                if (actor.renderComponent != null)
                {
                    ApplyColorToRenderComponent(actor.renderComponent, color);
                }
            }
        }

        private static void ResetEnemyManager(ActorObjectManager manager)
        {
            var actorList = manager?.actorList;
            if (actorList == null) return;

            for (int i = 0; i < actorList.Count; i++)
            {
                var actor = actorList[i];
                if (actor == null || actor.destroyed)
                    continue;

                ResetRenderComponent(actor.renderComponent);
            }
        }

        public static void ApplyColorToRenderComponent(RenderComponent renderComp, Color color)
        {
            if (renderComp == null || renderComp.animView == null || renderComp.animView.anim == null)
                return;

            uint uintColor = ColorToUint(color);
            Utils.setColorUnity(renderComp.animView.anim, uintColor);
        }

        public static void ResetRenderComponent(RenderComponent renderComp)
        {
            if (renderComp == null || renderComp.animView == null || renderComp.animView.anim == null)
                return;

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
                if (!Enabled || __result == null)
                    return;

                if (actor is Spawn)
                {
                    ApplyColorToRenderComponent(__result, GetActiveColor());
                }
            }
        }
    }
}
