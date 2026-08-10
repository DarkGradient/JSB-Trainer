using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace jsb_new
{
    public static class EnemyColor
    {
        private const string FEATURE_NAME = "Custom Enemy Color";

        private static float _hue = 0f;
        private static bool _wasApplied = false;

        public static bool Enabled
        {
            get => ModuleRegistry.IsActive(FEATURE_NAME);
            set => ModuleRegistry.SetActive(FEATURE_NAME, value);
        }

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            // Патчим создание видео-компонента у всех врагов и объектов
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_RenderComponent_AddToActor));

            ModuleRegistry.RegisterCheckbox(
                FEATURE_NAME,
                () => Enabled,
                                            (enabled) =>
                                            {
                                                Enabled = enabled;

                                                HUDManager.CreateToast(
                                                    enabled ? "RGB ENEMIES: ON" : "RGB ENEMIES: OFF",
                                                    enabled ? Color.green : Color.gray,
                                                    1.5f
                                                );

                                                RefreshAllEnemiesOnScene(enabled ? GetCurrentColor() : GetRandomColor());
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
                    // Выключили RGB? Рандомим цвета всем объектам
                    RefreshAllEnemiesOnScene(GetRandomColor());
                    _wasApplied = false;
                }
                return;
            }

            // Каждый кадр переливашка
            Color currentColor = GetCurrentColor();
            RefreshAllEnemiesOnScene(currentColor);
            _wasApplied = true;
        }

        private static Color GetCurrentColor()
        {
            _hue = (_hue + Time.deltaTime * 0.5f) % 1.0f;
            return Color.HSVToRGB(_hue, 1f, 1f);
        }

        private static Color GetRandomColor()
        {
            return Color.HSVToRGB(UnityEngine.Random.value, 1f, 1f);
        }

        public static void RefreshAllEnemiesOnScene(Color color)
        {
            var gameScene = GameScene.instance;
            if (gameScene == null || gameScene.enemyManager == null)
                return;

            var actorList = gameScene.enemyManager.actorList;
            if (actorList == null) return;

            for (int i = 0; i < actorList.Count; i++)
            {
                var actor = actorList[i];
                if (actor == null || actor.destroyed)
                    continue;

                // Перекрашиваем абсолютно любые актеры с рендерером
                if (actor.renderComponent != null)
                {
                    ApplyColorToRenderComponent(actor.renderComponent, color);
                }
            }
        }

        public static void ApplyColorToRenderComponent(RenderComponent renderComp, Color color)
        {
            if (renderComp == null || renderComp.animView == null || renderComp.animView.anim == null)
                return;

            uint uintColor = ColorToUint(color);
            Utils.setColorUnity(renderComp.animView.anim, uintColor);
        }

        private static uint ColorToUint(Color color)
        {
            byte r = (byte)(Mathf.Clamp01(color.r) * 255);
            byte g = (byte)(Mathf.Clamp01(color.g) * 255);
            byte b = (byte)(Mathf.Clamp01(color.b) * 255);

            return (uint)((r << 16) | (g << 8) | b);
        }

        // Ловим спавн ВСЕХ объектов (CircleFromEdges, Beam, Enemy и т.д.)
        [HarmonyPatch(typeof(RenderComponent), nameof(RenderComponent.addToActor))]
        private static class Patch_RenderComponent_AddToActor
        {
            static void Postfix(RenderComponent __result, Actor actor)
            {
                if (!Enabled || __result == null)
                    return;

                // Если это спавнящийся вражеский объект
                if (actor is Spawn)
                {
                    ApplyColorToRenderComponent(__result, GetCurrentColor());
                }
            }
        }
    }
}
