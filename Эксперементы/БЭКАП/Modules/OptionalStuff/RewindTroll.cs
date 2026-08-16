using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace jsb_new
{
    public static class RewindTroll
    {
        private static float _lastTriggerTime = 0f;
        private const float COOLDOWN = 3.5f;

        private static ActorMultiplayerLevelLogic? _cachedLogic;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            // Патчим создание, чтобы всегда иметь актуальную ссылку
            harmony.Patch(
                typeof(ActorMultiplayerLevelLogic).GetMethod("start"),
                          postfix: new HarmonyMethod(typeof(RewindTroll), nameof(OnLogicStarted))
            );

            // Регистрируем кнопку в меню
            ModuleRegistry.RegisterButton("Force Rewind Music", TryForceRewind);
        }

        private static void OnLogicStarted(ActorMultiplayerLevelLogic __instance)
        {
            _cachedLogic = __instance;
        }

        public static void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                TryForceRewind();
            }
        }

        public static void TryForceRewind()
        {
            if (Time.unscaledTime - _lastTriggerTime < COOLDOWN)
            {
                HUDManager.CreateToast("Rewind Troll на кулдауне", Color.yellow, 1.6f);
                return;
            }

            try
            {
                var logic = GetLogic();
                if (logic == null)
                {
                    HUDManager.CreateToast("Нет ActorMultiplayerLevelLogic", Color.red, 2f);
                    return;
                }

                if (logic.destroyed)
                {
                    _cachedLogic = null;
                    HUDManager.CreateToast("Logic destroyed", Color.red, 1.8f);
                    return;
                }

                if (!logic.IsHost())
                {
                    HUDManager.CreateToast("Ты не хост", Color.red, 1.8f);
                    return;
                }

                if (logic.logicMultiplayerLobby != null && logic.logicMultiplayerLobby.InLobby)
                {
                    HUDManager.CreateToast("Сейчас лобби", Color.yellow, 1.8f);
                    return;
                }

                if (logic.logicRewindMusic == null)
                {
                    HUDManager.CreateToast("logicRewindMusic == null", Color.red, 1.8f);
                    return;
                }

                logic.logicRewindMusic.rewindMusic();

                _lastTriggerTime = Time.unscaledTime;
                HUDManager.CreateToast("Rewind отправлен", Color.green, 1.6f);
            }
            catch (Exception ex)
            {
                string msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                MelonLogger.Error($"[RewindTroll] Exception: {msg}");
                HUDManager.CreateToast("RewindTroll exception (см. консоль)", Color.red, 2.5f);
            }
        }

        private static ActorMultiplayerLevelLogic? GetLogic()
        {
            if (_cachedLogic != null && !_cachedLogic.destroyed)
                return _cachedLogic;

            _cachedLogic = null;
            return null;
        }
    }
}
