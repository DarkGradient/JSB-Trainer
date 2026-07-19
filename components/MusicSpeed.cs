using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using Il2CppInterop.Runtime;

namespace jsb_new
{
    public static class MusicSpeed
    {
        public static void Update()
        {
            if (MainGame.instance == null) return;

            if (Input.GetKeyDown(KeyCode.F5))
                SetMusicSpeed(2f);
            else if (Input.GetKeyUp(KeyCode.F5))
                SetMusicSpeed(1f);
        }

        private static void SetMusicSpeed(float speed)
        {
            try
            {
                var logic = GetNormalLevelLogic();
                if (logic == null)
                {
                    DebugStrings.Log("Music speed: not in normal level");
                    return;
                }

                if (logic.music != null && logic.music.sfxViewDynamic != null)
                {
                    logic.music.sfxViewDynamic.playbackSpeed = speed;
                    DebugStrings.Log($"Music speed set to {speed}x");
                }
                else
                {
                    DebugStrings.Log("Music speed: component not found");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"==== MUSIC SPEED: CRITICAL ERROR - {ex.Message} ====");
            }
        }

        private static ActorNormalLevelLogic? GetNormalLevelLogic()
        {
            if (GameScene.instance?.logicManager == null) return null;

            var type = Il2CppType.Of<ActorNormalLevelLogic>();
            var result = GameScene.instance.logicManager.getFirst(type);

            return result != null ? result.Cast<ActorNormalLevelLogic>() : null;
        }
    }
}
