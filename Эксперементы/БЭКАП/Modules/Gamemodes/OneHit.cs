using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using Il2CppInterop.Runtime;

namespace jsb_new
{
    public static class OneHit
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("OneHit");
            set
            {
                bool wasEnabled = ModuleRegistry.IsActive("OneHit");
                ModuleRegistry.SetActive("OneHit", value);

                if (wasEnabled && !value && IsMultiplayer())
                {
                    RestoreGhostPositionAfterDisable();
                }
            }
        }

        public static bool TrueOneHitEnabled
        {
            get => Enabled && ModuleRegistry.IsActive("TrueOneHit");
            set
            {
                if (!Enabled && value)
                    Enabled = true; // авто-включаем базовый

                    ModuleRegistry.SetActive("TrueOneHit", value);
            }
        }

        private static void RestoreGhostPositionAfterDisable()
        {
            try
            {
                if (PlayerManager.instance == null || CameraFlash.mainCamera == null) return;

                var firstLocalPlayer = PlayerManager.instance.GetFirstLocalPlayer();
                if (firstLocalPlayer == null) return;

                var ghost = HeroGhost.getHeroFromModelPlayer(firstLocalPlayer.modelPlayer);
                if (ghost != null && !ghost.destroyed)
                {
                    ghost.px = CameraFlash.mainCamera.px;
                    ghost.py = CameraFlash.mainCamera.py;
                    DebugStrings.Log("OneHit: Ghost teleported back to camera view after disable.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[OneHit] Failed to restore ghost position: {ex.Message}");
            }
        }

        public static bool IsActiveGameplay = false;
        public static bool BlockCheckpointResurrection = false;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            // Патчи
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HitByEnemy));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_CheckPoint_Start));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_ViewCheckpointProgress_refreshViews));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_LogicCheckPointCheck_CompleteCheckPoint));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_LogicCheckPointCheck_HasHeroReachedCheckpoint));

            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_ResurrectPlayerNormal));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_ResurrectPlayerMultiplayer));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_OnCollectGhost));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_CreateGhost_TeleportAway));

            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_GameplayScene_Start));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_LogicStartLevel_StartLevel));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_LogicRewindMusic_RewindMusic));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_LogicRewindMusic_StartLevelAfterRewind));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_LogicEndLevel_OnCompleteLastCheckpoint));

            // Чекбоксы
            ModuleRegistry.RegisterCheckbox("One-Hit Mode",
                                            () => Enabled,
                                            (newValue) => { Enabled = newValue; },
                                            isLocked: () => ModuleRegistry.IsActive("MouseControl")
            );

            ModuleRegistry.RegisterCheckbox("True One-Hit (Beta)",
                                            () => TrueOneHitEnabled,
                                            (newValue) => { TrueOneHitEnabled = newValue; },
                                            isLocked: () => !Enabled || ModuleRegistry.IsActive("MouseControl")
            );

            // HUD Плашка – улучшена: для обычного OneHit показываем только надпись, для True – со статусом.
            // Высота 35 => автоматически получаем шрифт 18 (по логике HUDManager).
            HUDManager.CreateHUD(
                key: "OneHit",
                textGetter: () => TrueOneHitEnabled
                ? $"True One-Hit (Beta) Enabled\n{GetOneHitStatus()}"
                : "One-Hit Enabled",
                baseColor: Color.white,
                pulseColor: new Color(1f, 0.6f, 0.1f, 1f),
                                          activeGetter: () => Enabled,
                                          height: 35f
                                          // order: 10
            );
        }

        public static bool IsMultiplayer()
        {
            if (GameScene.instance?.logicManager == null)
                return false;

            var type = Il2CppType.Of<ActorMultiplayerLevelLogic>();
            var result = GameScene.instance.logicManager.getFirst(type);
            return result != null;
        }

        private static void ReviveLocalPlayer()
        {
            try
            {
                if (GameScene.instance == null || PlayerManager.instance == null) return;
                var mp = PlayerManager.instance.GetFirstLocalPlayer();
                if (mp == null) return;

                var hero = Hero.getHeroFromModelPlayer(mp.modelPlayer);
                if (hero == null || hero.destroyed)
                {
                    var playersLogic = GetActivePlayersLogic();
                    if (playersLogic != null)
                    {
                        DebugStrings.Log("OneHit disabled: Resurrecting local player back to life.");
                        playersLogic.resurrectPlayer(mp.modelPlayer);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"[OneHit] Failed to revive local player: {ex.Message}");
            }
        }

        private static LogicPlayers? GetActivePlayersLogic()
        {
            if (GameScene.instance?.logicManager == null) return null;
            try
            {
                var type = Il2CppType.Of<LogicPlayers>();
                var obj = GameScene.instance.logicManager.getFirst(type);
                if (obj != null) return obj.Cast<LogicPlayers>();

                var normalType = Il2CppType.Of<ActorNormalLevelLogic>();
                var normalLogicObj = GameScene.instance.logicManager.getFirst(normalType);
                if (normalLogicObj != null) return normalLogicObj.Cast<ActorNormalLevelLogic>().logicPlayers;
            }
            catch { }
            return null;
        }

        public static LogicRewindMusic? GetRewindMusicLogic()
        {
            if (GameScene.instance?.logicManager == null) return null;

            var normalType = Il2CppType.Of<ActorNormalLevelLogic>();
            var normalLogicObj = GameScene.instance.logicManager.getFirst(normalType);
            if (normalLogicObj != null)
            {
                var normalLogic = normalLogicObj.Cast<ActorNormalLevelLogic>();
                return normalLogic?.logicRewindMusic;
            }

            var multiType = Il2CppType.Of<ActorMultiplayerLevelLogic>();
            var multiLogicObj = GameScene.instance.logicManager.getFirst(multiType);
            if (multiLogicObj != null)
            {
                var multiLogic = multiLogicObj.Cast<ActorMultiplayerLevelLogic>();
                return multiLogic?.logicRewindMusic;
            }
            return null;
        }

        public static bool IsResurrectionAllowed()
        {
            try
            {
                if (GameScene.instance?.logicManager == null) return true;

                var rewindLogic = GetRewindMusicLogic();
                if (rewindLogic != null && rewindLogic.isRewinding())
                    return true;

                var normalType = Il2CppType.Of<ActorNormalLevelLogic>();
                var normalLogicObj = GameScene.instance.logicManager.getFirst(normalType);
                if (normalLogicObj != null)
                {
                    var logic = normalLogicObj.Cast<ActorNormalLevelLogic>();
                    if (logic != null && logic.logicEnemyCreator != null)
                        return !logic.logicEnemyCreator.enabled;
                }

                var multiType = Il2CppType.Of<ActorMultiplayerLevelLogic>();
                var multiLogicObj = GameScene.instance.logicManager.getFirst(multiType);
                if (multiLogicObj != null)
                {
                    var logic = multiLogicObj.Cast<ActorMultiplayerLevelLogic>();
                    if (logic != null && logic.logicEnemyCreator != null)
                        return !logic.logicEnemyCreator.enabled;
                }
            }
            catch { }
            return true;
        }

        private static string GetOneHitStatus()
        {
            if (GameScene.instance == null || PlayerManager.instance == null)
                return "(Unknown)";

            try
            {
                var mp = PlayerManager.instance.GetFirstLocalPlayer();
                if (mp == null) return "(Unknown)";

                var hero = Hero.getHeroFromModelPlayer(mp.modelPlayer);
                if (hero == null || hero.destroyed)
                {
                    if (IsMultiplayer()) return "(Dead. Waiting rewind | end.)";
                    return "(Dead)";
                }
                return "(Alive)";
            }
            catch
            {
                return "(Unknown)";
            }
        }

        // ====================== Патчи ======================

        [HarmonyPatch(typeof(HeroCollisionWithEnemy), "hitByEnemy")]
        private static class Patch_HitByEnemy
        {
            static bool Prefix(HeroCollisionWithEnemy __instance, Actor h)
            {
                if (!Enabled) return true;

                Hero? hero = __instance.actor.TryCast<Hero>();
                if (hero == null) return true;

                if (IsMultiplayer())
                {
                    DebugStrings.Log("OneHit: hit in multiplayer, setting player HP to 1");
                    if (hero.lifeComponent != null)
                        hero.lifeComponent.hp = 1f;
                    return true;
                }

                __instance.animDie();
                hero.destroy();
                RegisterManager.instance.RegisterDeath(hero.modelPlayer);

                ActorNormalLevelLogic level = ActorNormalLevelLogic.getNormalLevelLogic();
                if (level != null && level.logicRewindMusic != null)
                    level.logicRewindMusic.rewindMusicToRestart();

                return false;
            }
        }

        [HarmonyPatch(typeof(GameplayScene), "start")]
        private static class Patch_GameplayScene_Start
        {
            static void Postfix() => IsActiveGameplay = false;
        }

        [HarmonyPatch(typeof(LogicStartLevel), "startLevel")]
        private static class Patch_LogicStartLevel_StartLevel
        {
            static void Postfix()
            {
                if (Enabled) IsActiveGameplay = true;
            }
        }

        [HarmonyPatch(typeof(LogicRewindMusic), "rewindMusic")]
        private static class Patch_LogicRewindMusic_RewindMusic
        {
            static bool Prefix(LogicRewindMusic __instance)
            {
                if (!Enabled) return true;
                IsActiveGameplay = false;
                return true;
            }
        }

        [HarmonyPatch(typeof(LogicRewindMusic), "startLevelAfterRewind")]
        private static class Patch_LogicRewindMusic_StartLevelAfterRewind
        {
            static void Postfix()
            {
                if (Enabled) IsActiveGameplay = true;
            }
        }

        [HarmonyPatch(typeof(LogicEndLevel), "onCompleteLastCheckpoint")]
        private static class Patch_LogicEndLevel_OnCompleteLastCheckpoint
        {
            static void Prefix()
            {
                if (Enabled) IsActiveGameplay = false;
            }
        }

        // Блокировка воскрешений (только TrueOneHit)
        [HarmonyPatch(typeof(LogicPlayersNormalLevel), "resurrectPlayer")]
        private static class Patch_ResurrectPlayerNormal
        {
            static bool Prefix(ModelPlayer modelPlayer, ref Hero __result)
            {
                if (TrueOneHitEnabled && IsMultiplayer() && IsActiveGameplay && modelPlayer != null)
                {
                    if (!IsResurrectionAllowed())
                    {
                        var mp = PlayerManager.instance.getFromModel(modelPlayer);
                        if (mp != null && mp.isLocalMainPlayer())
                        {
                            __result = null!;
                            return false;
                        }
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(LogicMultiplayerPlayers), "resurrectPlayer")]
        private static class Patch_ResurrectPlayerMultiplayer
        {
            static bool Prefix(ModelPlayer modelPlayer, ref Hero __result)
            {
                if (TrueOneHitEnabled && IsMultiplayer() && IsActiveGameplay && modelPlayer != null)
                {
                    if (!IsResurrectionAllowed())
                    {
                        var mp = PlayerManager.instance.getFromModel(modelPlayer);
                        if (mp != null && mp.isLocalMainPlayer())
                        {
                            __result = null!;
                            return false;
                        }
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(LogicPlayersNormalLevel), "onCollectGhost")]
        private static class Patch_OnCollectGhost
        {
            static bool Prefix(HeroGhost ghost)
            {
                if (TrueOneHitEnabled && IsMultiplayer() && ghost?.modelPlayer != null)
                {
                    if (!IsResurrectionAllowed())
                    {
                        var mp = PlayerManager.instance.getFromModel(ghost.modelPlayer);
                        if (mp != null && mp.isLocalMainPlayer())
                            return false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(LogicPlayersNormalLevel), "createGhost")]
        private static class Patch_CreateGhost_TeleportAway
        {
            static void Postfix(HeroGhost __result)
            {
                if (!TrueOneHitEnabled || !IsMultiplayer() || __result == null) return;
                if (CameraFlash.mainCamera != null)
                    __result.px = CameraFlash.mainCamera.boundsFOV.left - 400f;
            }
        }

        // Чекпоинты (под обычным OneHit)
        [HarmonyPatch(typeof(CheckPoint), "start")]
        private static class Patch_CheckPoint_Start
        {
            static void Postfix(CheckPoint __instance)
            {
                if (Enabled && !IsMultiplayer())
                {
                    if (__instance.renderComponent?.animView?.anim != null)
                        __instance.renderComponent.animView.anim.visible = false;
                    if (__instance.checkpointMc != null)
                        __instance.checkpointMc.visible = false;
                    if (__instance.fxBack?.renderComponent?.animView?.anim != null)
                        __instance.fxBack.renderComponent.animView.anim.visible = false;
                }
            }
        }

        [HarmonyPatch(typeof(ViewCheckpointProgress), "refreshViews")]
        private static class Patch_ViewCheckpointProgress_refreshViews
        {
            static bool Prefix(ViewCheckpointProgress __instance)
            {
                if (Enabled && !IsMultiplayer())
                {
                    __instance.destroyAllChildren();
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(LogicCheckPointCheck), "hasHeroReachedCheckpoint")]
        private static class Patch_LogicCheckPointCheck_HasHeroReachedCheckpoint
        {
            static bool Prefix(ref bool __result)
            {
                if (Enabled && !IsMultiplayer())
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(LogicCheckPointCheck), "completeCheckPoint")]
        private static class Patch_LogicCheckPointCheck_CompleteCheckPoint
        {
            static bool Prefix(LogicCheckPointCheck __instance, CheckPoint c, bool isFromLocalPlayer)
            {
                if (!Enabled || IsMultiplayer()) return true;
                if (!__instance.enabled) return false;

                var fxBg = __instance.actorNormalLevel.fxBackground;
                if (fxBg != null)
                {
                    fxBg.setBackground(c.modelBackGround);
                    if (c.forceLastCheckPoint || c.metaSpawn.isLastCheckPoint(__instance.actorNormalLevel.modelSong.metaSong))
                        __instance.actorNormalLevel.logicEndLevel.onCompleteLastCheckpoint();
                }

                __instance.callbackOnNewCheckpoint.call();
                if (isFromLocalPlayer)
                    __instance.callbackOnLocalPlayerCheckpoint.call();

                return false;
            }
        }
    }
}
