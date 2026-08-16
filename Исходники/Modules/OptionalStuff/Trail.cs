// using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace jsb_new
{
    public static class Trail
    {
        private const string FEATURE_NAME = "Full Power Trail";
        private static bool _hasApplied;

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
                                                    enabled ? Color.cyan : Color.gray,
                                                    1.5f
                                                );

                                                if (enabled)
                                                {
                                                    _hasApplied = true;
                                                }
                                                else if (_hasApplied)
                                                {
                                                    ResetPlayerParticles();
                                                    _hasApplied = false;
                                                }
                                                // если выключен и никогда не включали — ничего не делаем
                                            }
            );

            DebugStrings.Log($"[FullPowerTrail] Initialized!");
        }

        public static void Update()
        {
            if (!ModuleRegistry.IsActive(FEATURE_NAME))
                return;

            var gameScene = MainGame.instance?.gameSceneManager?.gameScene;
            if (gameScene == null || gameScene.heroManager == null)
                return;

            var actorList = gameScene.heroManager.actorList;
            for (int i = 0; i < actorList.Count; i++)
            {
                var actor = actorList[i];
                if (actor == null || actor.destroyed)
                    continue;

                var hero = actor.TryCast<Hero>();
                if (hero == null)
                    continue;

                // Проверяем текущий компонент
                if (hero.particuleComponent == null || hero.particuleComponent.TryCast<HeroParticuleComponentFullPower>() == null)
                {
                    var fullPowerParticles = new HeroParticuleComponentFullPower();
                    hero.switchParticleComponent(fullPowerParticles);

                    // ФИКС NULL POINTER EXCEPTION:
                    // Обновляем ссылку на частицы внутри Dash-компонента!
                    if (hero.dashComponent != null)
                    {
                        hero.dashComponent.heroParticule = fullPowerParticles;
                    }

                    DebugStrings.Log($"[FullPowerTrail] Applied Full Power particles & updated DashComponent!");
                }
            }
        }

        private static void ResetPlayerParticles()
        {
            var gameScene = MainGame.instance?.gameSceneManager?.gameScene;
            if (gameScene == null || gameScene.heroManager == null)
                return;

            var actorList = gameScene.heroManager.actorList;
            for (int i = 0; i < actorList.Count; i++)
            {
                var hero = actorList[i]?.TryCast<Hero>();
                if (hero != null && !hero.destroyed)
                {
                    var defaultParticles = new HeroParticuleComponent();
                    hero.switchParticleComponent(defaultParticles);

                    // При сбросе тоже обновляем ссылку в DashComponent
                    if (hero.dashComponent != null)
                    {
                        hero.dashComponent.heroParticule = defaultParticles;
                    }
                }
            }
        }
    }
}
