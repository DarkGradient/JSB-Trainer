using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace jsb_new
{
    public static class SceneScanner
    {
        public static void Initialize()
        {
            ModuleRegistry.RegisterButton("Scan Scene (Deep Dump)", Scan);
        }

        public static void Update()
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                Scan();
            }
        }

        public static void Scan()
        {
            try
            {
                var scene = MainGame.instance?.gameSceneManager?.gameScene;
                if (scene == null)
                {
                    MelonLogger.Msg("[SceneScanner] Нет активной сцены (не в уровне).");
                    return;
                }

                MelonLogger.Msg("==== SCENE SCAN START ====");

                var enemyList = scene.enemyManager?.actorList;
                int enemyCount = enemyList != null ? enemyList.Count : 0;
                MelonLogger.Msg($"[SceneScanner] Enemies: {enemyCount}");
                if (enemyList != null)
                {
                    for (int i = 0; i < enemyList.Count; i++)
                        DumpActor(enemyList[i]);
                }

                var heroList = scene.heroManager?.actorList;
                int heroCount = heroList != null ? heroList.Count : 0;
                MelonLogger.Msg($"[SceneScanner] Heroes: {heroCount}");
                if (heroList != null)
                {
                    for (int i = 0; i < heroList.Count; i++)
                        DumpActor(heroList[i]);
                }

                MelonLogger.Msg("==== SCENE SCAN END ====");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SceneScanner] Scan failed: {ex.Message}");
            }
        }

        private static void DumpActor(Actor actor)
        {
            if (actor == null) return;
            try
            {
                // Реальный Il2Cpp-класс объекта, а не то, во что мы его сейчас держим (Actor).
                string typeName;
                try { typeName = actor.GetIl2CppType().Name; }
                catch { typeName = actor.GetType().Name; }

                // Пытаемся достать public-поле radius через рефлексию, если оно есть у конкретного
                // подкласса (Beam/BeamBorderless/Laser4 и т.п. — у Actor его в базе нет).
                string radiusInfo = "";
                try
                {
                    var radiusField = actor.GetType().GetField("radius");
                    if (radiusField != null)
                        radiusInfo = $" radius={radiusField.GetValue(actor)}";
                }
                catch { }

                MelonLogger.Msg($"  [{typeName}] px={actor.px:F0} py={actor.py:F0} destroyed={actor.destroyed}{radiusInfo}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SceneScanner] DumpActor error: {ex.Message}");
            }
        }
    }
}
