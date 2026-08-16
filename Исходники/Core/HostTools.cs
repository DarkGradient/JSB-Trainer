using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace jsb_new
{
    public static class HostTools
    {
        public const string FEATURE_NAME = "Host Tools";

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
                v =>
                {
                    Enabled = v;
                    HUDManager.CreateToast(
                        v ? "HOST TOOLS: ON" : "HOST TOOLS: OFF",
                        v ? Color.red : Color.gray,
                        1.5f
                    );
                },
                isLocked: () => !IsHostNow()
            );

            Btn("HT Force Start Run", ForceStartRun);
            Btn("HT Force Vote (Tuto)", ForceVoteTuto);
            Btn("HT Skip Lobby Timer", SkipLobbyTimer);
            Btn("HT Force Game Started", ForceGameStarted);

            Btn("HT Force Checkpoint", ForceCheckpoint);
            Btn("HT Rewind (1 left)", () => ForceRewind(1));
            Btn("HT Rewind (3 left)", () => ForceRewind(3));
            Btn("HT Rewind (0 = SD)", () => ForceRewind(0));
            Btn("HT Force End Level", ForceEndLevel);

            Btn("HT Resurrect All (local)", ResurrectAllLocal);
            Btn("HT Kill All Heroes (local)", KillAllLocal);
            Btn("HT Kill All Online Heroes", KillAllOnline);

            Btn("HT Sudden Death (0 rewind)", () => ForceRewind(0));
            Btn("HT Mercy +1 Rewind", () => ForceRewind(1));
            Btn("HT Spam Vote Tuto", ForceVoteTuto);

            DebugStrings.Log("[HostTools] Initialized");
        }

        private static void Btn(string name, System.Action action)
        {
            ModuleRegistry.RegisterButton(name, () =>
            {
                if (!Enabled)
                {
                    Toast("Host Tools выключены", Color.gray);
                    return;
                }
                if (!IsHostNow())
                {
                    Toast("Ты не хост / не в MP", Color.red);
                    return;
                }
                try { action(); }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[HostTools] {name}: {ex.Message}");
                    Toast($"FAIL: {name}", Color.red);
                }
            });
        }

        private static void Toast(string msg, Color c) =>
            HUDManager.CreateToast(msg, c, 1.5f);

        private static ActorMultiplayerLevelLogic? GetLogic()
        {
            if (GameScene.instance?.logicManager == null) return null;
            var t = Il2CppType.Of<ActorMultiplayerLevelLogic>();
            var a = GameScene.instance.logicManager.getFirst(t);
            return a != null ? a.Cast<ActorMultiplayerLevelLogic>() : null;
        }

        private static bool IsHostNow()
        {
            try
            {
                var logic = GetLogic();
                return logic?.multiplayerRoom != null && logic.IsHost();
            }
            catch { return false; }
        }

        private static LogicMultiplayerSendReliablePacket? GetSender(ActorMultiplayerLevelLogic logic) =>
            logic.logicMultiplayerSendEventPackets;

        private static void ForceStartRun()
        {
            var s = GetSender(GetLogic()!);
            if (s == null) { Toast("No sender", Color.red); return; }
            s.sendStartNewRun();
            Toast("START NEW RUN", Color.green);
        }

        private static void ForceVoteTuto()
        {
            var s = GetSender(GetLogic()!);
            if (s == null) { Toast("No sender", Color.red); return; }

            var list = new Il2CppSystem.Collections.Generic.List<ModelStoryCheckpoint>();
            list.Add(ModelStoryCheckpointEnum.TUTO_1);
            list.Add(ModelStoryCheckpointEnum.TUTO_2);
            list.Add(ModelStoryCheckpointEnum.TUTO_3);

            s.sendVoteStarted(list);
            Toast("VOTE (tuto)", Color.yellow);
        }

        private static void SkipLobbyTimer()
        {
            var s = GetSender(GetLogic()!);
            if (s == null) { Toast("No sender", Color.red); return; }
            s.sendLobbyUpdate(0f);
            Toast("LOBBY TIMER 0", Color.cyan);
        }

        private static void ForceGameStarted()
        {
            var s = GetSender(GetLogic()!);
            if (s == null) { Toast("No sender", Color.red); return; }
            s.sendGameStarted();
            Toast("GAME STARTED", Color.green);
        }

        private static void ForceCheckpoint()
        {
            var s = GetSender(GetLogic()!);
            if (s == null) { Toast("No sender", Color.red); return; }
            s.sendCheckPoint(0);
            Toast("CHECKPOINT 0", Color.cyan);
        }

        private static void ForceRewind(int left)
        {
            var s = GetSender(GetLogic()!);
            if (s == null) { Toast("No sender", Color.red); return; }
            s.sendRewind(left);
            Toast($"REWIND left={left}", left == 0 ? Color.red : Color.yellow);
        }

        private static void ForceEndLevel()
        {
            var s = GetSender(GetLogic()!);
            if (s == null) { Toast("No sender", Color.red); return; }
            s.sendEndLevel();
            Toast("END LEVEL", Color.red);
        }

        private static void ResurrectAllLocal()
        {
            var logic = GetLogic();
            if (logic?.logicMultiplayerPlayers == null) { Toast("No players logic", Color.red); return; }
            logic.logicMultiplayerPlayers.resurrectAllFromCheckPoint();
            Toast("Resurrect all", Color.green);
        }

        private static void KillAllLocal()
        {
            var list = GameScene.instance?.heroManager?.actorList;
            if (list == null) { Toast("No heroes", Color.red); return; }
            for (int i = 0; i < list.Count; i++)
            {
                var hero = list[i]?.TryCast<Hero>();
                if (hero != null && !hero.destroyed && hero.lifeComponent != null)
                    hero.lifeComponent.damage(999999f);
            }
            Toast("Kill local heroes", Color.red);
        }

        private static void KillAllOnline()
        {
            var logic = GetLogic();
            if (logic?.logicMultiplayerPlayers == null) { Toast("No players logic", Color.red); return; }
            logic.logicMultiplayerPlayers.KillAllOnlineHeroes();
            Toast("KillAllOnlineHeroes", Color.red);
        }

        public static void Update() { }
    }
}
