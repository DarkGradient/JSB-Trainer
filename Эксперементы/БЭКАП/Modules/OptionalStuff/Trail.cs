using Il2Cpp;
using UnityEngine;

namespace jsb_new
{
    public static class Trail
    {
        private const string FEATURE_NAME = "Full Power Trail";

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            ModuleRegistry.RegisterCheckbox(
                FEATURE_NAME,
                () => ModuleRegistry.IsActive(FEATURE_NAME),
                                            (enabled) =>
                                            {
                                                ModuleRegistry.SetActive(FEATURE_NAME, enabled);
                                                HUDManager.CreateToast(
                                                    enabled ? "FULL POWER TRAIL: ON" : "FULL POWER TRAIL: OFF",
                                                    enabled ? Color.cyan : Color.gray, 1.5f
                                                );

                                                if (!enabled) ResetPlayerParticles();
                                            }
            );

            DebugStrings.Log($"[FullPowerTrail] Initialized!");
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
                    // Уничтожаем предыдущие частицы
                    try
                    {
                        if (hero.particuleComponent != null)
                            hero.particuleComponent.destroy();
                    }
                    catch { }

                    var fullPowerParticles = new HeroParticuleComponentFullPower();
                    hero.switchParticleComponent(fullPowerParticles);

                    // Убрана повторная привязка к dashComponent!
                    DebugStrings.Log($"[FullPowerTrail] Applied Full Power particles cleanly!");
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
                    try
                    {
                        if (hero.particuleComponent != null)
                            hero.particuleComponent.destroy();
                    }
                    catch { }

                    var defaultParticles = new HeroParticuleComponent();
                    hero.switchParticleComponent(defaultParticles);
                }
            }
        }
    }
}
