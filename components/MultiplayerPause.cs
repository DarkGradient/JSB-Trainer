using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using Il2CppInterop.Runtime;

namespace jsb_new
{
    public static class MultiplayerPause
    {
        private static UI_PauseNew _activePauseMenu;
        public static void Update()
        {
            if (MainGame.instance == null) return;

            if (Input.GetKeyDown(KeyCode.K))
            {
                if (_activePauseMenu != null && !_activePauseMenu.destroyed)
                {
                    _activePauseMenu.close();   // мгновенно закрываем, без Esc/O логики попапа вообще
                    _activePauseMenu = null;
                    return;
                }
                OpenSettingsInMultiplayer();
            }
        }

        private static void OpenSettingsInMultiplayer()
        {
            try
            {
                if (GameScene.instance?.logicManager == null)
                {
                    DebugStrings.Log("Konfig: not in a level");
                    return;
                }

                if (UIBase.manager.hasInstanceOf(Il2CppType.Of<UI_PauseNew>()))
                {
                    return;
                }

                var type = Il2CppType.Of<ActorMultiplayerLevelLogic>();
                var result = GameScene.instance.logicManager.getFirst(type);

                if (result == null)
                {
                    DebugStrings.Log("Konfig: not in multiplayer (use Esc/O instead)");
                    return;
                }

                var multiplayerLogic = result.Cast<ActorMultiplayerLevelLogic>();
                var pauseMenu = new UI_PauseNew(multiplayerLogic.modelSong);
                _activePauseMenu = pauseMenu;

                if (pauseMenu.exitBtn != null)
                    pauseMenu.exitBtn.visible = false;
                if (pauseMenu.backToTitleBtn != null)
                    pauseMenu.backToTitleBtn.visible = false;

                DebugStrings.Log("Konfig: settings menu opened in multiplayer");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"==== KONFIG: FAILED TO OPEN SETTINGS - {ex.Message} ====");
            }
        }
    }
}
