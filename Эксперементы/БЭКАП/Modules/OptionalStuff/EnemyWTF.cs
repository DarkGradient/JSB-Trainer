// using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace jsb_new
{
    public static class EnemySpinner
    {
        private const string FEATURE_NAME = "Tornado / Spin Mode";
        private const string SLIDER_NAME = "Spin Speed";

        public static float RotationSpeed = 360f;

        public static bool Enabled
        {
            get => ModuleRegistry.IsActive(FEATURE_NAME);
            set => ModuleRegistry.SetActive(FEATURE_NAME, value);
        }

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            ModuleRegistry.RegisterCheckbox(
                FEATURE_NAME,
                () => Enabled,
                                            (enabled) =>
                                            {
                                                Enabled = enabled;
                                                HUDManager.CreateToast(
                                                    enabled ? "TORNADO MODE: ON" : "TORNADO MODE: OFF",
                                                    enabled ? Color.green : Color.gray,
                                                    1.5f
                                                );
                                            }
            );

            // Если слайдер принимает диапазон [-100, 100]:
            ModuleRegistry.RegisterSlider(
                SLIDER_NAME,
                -100f,  // min (обратное вращение)
            100f,   // max (быстро вперед)
            100f,   // defaultValue
            (sliderValue) =>
            {
                // При sliderValue от -100 до 100 получаем RotationSpeed от -1800 до 1800
                RotationSpeed = (sliderValue / 100f) * 1800f;
            }
            );

            DebugStrings.Log("[EnemySpinner] Initialized!");
        }

        public static void Update()
        {
            if (!Enabled || Mathf.Approximately(RotationSpeed, 0f))
                return;

            var gameScene = GameScene.instance;
            if (gameScene == null || gameScene.enemyManager == null)
                return;

            var actorList = gameScene.enemyManager.actorList;
            if (actorList == null) return;

            float deltaRotation = Time.deltaTime * RotationSpeed;

            for (int i = 0; i < actorList.Count; i++)
            {
                var actor = actorList[i];
                if (actor == null || actor.destroyed)
                    continue;

                if (actor.renderComponent != null && actor.renderComponent.animView != null)
                {
                    actor.renderComponent.animView.rotation += deltaRotation;
                }
            }
        }
    }
}
