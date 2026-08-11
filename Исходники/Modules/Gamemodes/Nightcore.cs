using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace jsb_new
{
    public static class NightcoreMode
    {
        public const string FEATURE_NAME = "Nightcore Mode (NC)";
        private const float TARGET_SPEED = 1.5f;

        private static bool _wasActiveLastFrame = false;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            ModuleRegistry.RegisterCheckbox(
                FEATURE_NAME,
                () => ModuleRegistry.IsActive(FEATURE_NAME),
                                            (enabled) =>
                                            {
                                                if (enabled && IsInLevel() && IsMultiplayer())
                                                {
                                                    HUDManager.CreateToast("NC DISABLED: MULTIPLAYER DETECTED!", Color.red, 2.0f);
                                                    ModuleRegistry.SetActive(FEATURE_NAME, false);
                                                    ResetAll();
                                                    return;
                                                }

                                                ModuleRegistry.SetActive(FEATURE_NAME, enabled);

                                                HUDManager.CreateToast(
                                                    enabled ? "NIGHTCORE MODE: ON (1.5x pitch)" : "NIGHTCORE MODE: OFF",
                                                                       enabled ? Color.yellow : Color.gray,
                                                                       1.5f
                                                );

                                                if (!enabled)
                                                    ResetAll();
                                            },
                                            isLocked: () => IsInLevel() && IsMultiplayer()
            );

            DebugStrings.Log("[NightcoreMode] Initialized!");
        }

        public static void Update()
        {
            bool isActive = ModuleRegistry.IsActive(FEATURE_NAME);

            if (!isActive)
            {
                if (_wasActiveLastFrame)
                {
                    ResetAll();
                    _wasActiveLastFrame = false;
                }
                return;
            }

            if (!IsInLevel())
            {
                if (_wasActiveLastFrame)
                {
                    ResetAll();
                    _wasActiveLastFrame = false;
                }
                return;
            }

            _wasActiveLastFrame = true;

            var gameScene = GameScene.instance;
            if (gameScene == null || gameScene.IsPaused)
            {
                ResetAll();
                return;
            }

            if (IsMultiplayer())
            {
                ModuleRegistry.SetActive(FEATURE_NAME, false);
                ResetAll();
                HUDManager.CreateToast("NC DISABLED: MULTIPLAYER DETECTED", Color.red, 2f);
                return;
            }

            if (IsPlayerRewindingOrDead(gameScene))
            {
                ResetAll();
                return;
            }

            // Ускоряем ТОЛЬКО воспроизведение трека + pitch (без багованного GameSpeed)
            ApplyMusicPitch(TARGET_SPEED);
        }

        // Безопасная проверка нахождения на уровне
        private static bool IsInLevel()
        {
            var mainGame = MainGame.instance;
            if (mainGame == null || mainGame.Pointer == IntPtr.Zero)
                return false;

            var gsm = mainGame.gameSceneManager;
            if (gsm == null || gsm.Pointer == IntPtr.Zero)
                return false;

            var scene = gsm.gameScene;
            if (scene == null || scene.Pointer == IntPtr.Zero)
                return false;

            return scene.logicManager != null && scene.logicManager.Pointer != IntPtr.Zero;
        }

        private static void ApplyMusicPitch(float pitch)
        {
            try
            {
                var logic = GetNormalLevelLogic();
                if (logic?.music?.sfxViewDynamic != null && logic.music.sfxViewDynamic.Pointer != IntPtr.Zero)
                {
                    logic.music.sfxViewDynamic.playbackSpeed = pitch;
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[Nightcore] pitch set failed: {ex.Message}");
            }
        }

        private static ActorNormalLevelLogic? GetNormalLevelLogic()
        {
            if (!IsInLevel() || GameScene.instance?.logicManager == null)
                return null;

            var type = Il2CppType.Of<ActorNormalLevelLogic>();
            var result = GameScene.instance.logicManager.getFirst(type);
            return result != null ? result.TryCast<ActorNormalLevelLogic>() : null;
        }

        private static bool IsPlayerRewindingOrDead(GameScene gameScene)
        {
            var logicManager = gameScene.logicManager;
            if (logicManager == null || logicManager.Pointer == IntPtr.Zero || logicManager.actorList == null)
                return false;

            for (int i = 0; i < logicManager.actorList.Count; i++)
            {
                var actor = logicManager.actorList[i];
                if (actor == null || actor.Pointer == IntPtr.Zero) continue;

                var normalLogic = actor.TryCast<ActorNormalLevelLogic>();
                if (normalLogic != null && normalLogic.logicRewindMusic != null)
                {
                    if (normalLogic.logicRewindMusic.isRewinding())
                        return true;
                }
            }
            return false;
        }

        private static bool IsMultiplayer()
        {
            if (!IsInLevel()) return false;

            if (GameScene.instance?.logicManager != null && GameScene.instance.logicManager.Pointer != IntPtr.Zero)
            {
                var type = Il2CppType.Of<ActorMultiplayerLevelLogic>();
                if (GameScene.instance.logicManager.getFirst(type) != null)
                    return true;
            }

            try
            {
                var pm = PlayerManager.instance;
                if (pm != null && pm.Pointer != IntPtr.Zero)
                {
                    return !pm.IsSinglePlayer();
                }
            }
            catch { }

            return false;
        }

        public static void ResetAll()
        {
            ApplyMusicPitch(1.0f);
        }
    }
}
