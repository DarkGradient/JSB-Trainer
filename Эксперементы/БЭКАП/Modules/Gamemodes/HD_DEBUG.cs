using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace jsb_new
{
    public static class DebugHiddenMode
    {
        public const string FEATURE_NAME = "[DEBUG] Hidden Mode";
        public const string FULL_INVISIBLE_FEATURE = "[DEBUG] Full Invisibility";
        public const string FADE_SLIDER_NAME = "[DEBUG] HD Delay";

        private static float _visibleDuration = 0.75f;
        private static float _fadeDuration = 0.60f;

        private const float RECALL_DURATION = 1.00f;
        private const float RECALL_COOLDOWN = 0.35f;
        private static float _lastRecallTriggerTime = -100f;
        private static float _currentRecallAlpha = 0f;

        private static readonly Dictionary<IntPtr, float> _spawnTimes = new();

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            ModuleRegistry.RegisterCheckbox(
                FEATURE_NAME,
                () => ModuleRegistry.IsActive(FEATURE_NAME),
                                            (enabled) =>
                                            {
                                                ModuleRegistry.SetActive(FEATURE_NAME, enabled);
                                                if (enabled) { ModuleRegistry.SetActive(FULL_INVISIBLE_FEATURE, false); _spawnTimes.Clear(); }
                                                else ResetAll();

                                                HUDManager.CreateToast(
                                                    enabled ? "DEBUG HD MODE: ON\nPress [V] to reveal" : "DEBUG HD MODE: OFF",
                                                    enabled ? Color.magenta : Color.gray, 2.5f
                                                );
                                            }
            );

            ModuleRegistry.RegisterCheckbox(
                FULL_INVISIBLE_FEATURE,
                () => ModuleRegistry.IsActive(FULL_INVISIBLE_FEATURE),
                                            (enabled) =>
                                            {
                                                ModuleRegistry.SetActive(FULL_INVISIBLE_FEATURE, enabled);
                                                if (enabled) { ModuleRegistry.SetActive(FEATURE_NAME, false); _spawnTimes.Clear(); }
                                                else ResetAll();

                                                HUDManager.CreateToast(
                                                    enabled ? "DEBUG INVISIBILITY: ON\nPress [V] to reveal" : "DEBUG INVISIBILITY: OFF",
                                                    enabled ? Color.red : Color.gray, 2.5f
                                                );
                                            }
            );

            ModuleRegistry.RegisterSlider(FADE_SLIDER_NAME, -5.0f, 5.0f, 0.75f, (val) => _visibleDuration = val);

            // ПАТЧ Actor.OnUnPool УДАЛЕН НАВСЕГДА!
            DebugStrings.Log("[DebugHiddenMode] Initialized!");
        }

        public static void Update()
        {
            bool isHdActive = ModuleRegistry.IsActive(FEATURE_NAME);
            bool isFullInvisActive = ModuleRegistry.IsActive(FULL_INVISIBLE_FEATURE);

            if (!isHdActive && !isFullInvisActive)
            {
                if (_spawnTimes.Count > 0) ResetAll();
                _currentRecallAlpha = 0f;
                return;
            }

            if (!IsInLevel())
            {
                if (_spawnTimes.Count > 0) _spawnTimes.Clear();
                _currentRecallAlpha = 0f;
                return;
            }

            float now = Time.time;
            float timeSinceLastRecall = now - _lastRecallTriggerTime;
            bool isRecallOnCooldown = timeSinceLastRecall < (RECALL_DURATION + RECALL_COOLDOWN);

            if (Input.GetKeyDown(KeyCode.V) && !isRecallOnCooldown)
            {
                _lastRecallTriggerTime = now;
                HUDManager.CreateToast("RECALL!", Color.cyan, 0.5f);
            }

            if (timeSinceLastRecall <= RECALL_DURATION)
            {
                float p = timeSinceLastRecall / RECALL_DURATION;
                if (p < 0.15f) _currentRecallAlpha = Mathf.SmoothStep(0f, 1f, p / 0.15f);
                else if (p > 0.75f) _currentRecallAlpha = Mathf.SmoothStep(1f, 0f, (p - 0.75f) / 0.25f);
                else _currentRecallAlpha = 1.0f;
            }
            else _currentRecallAlpha = 0f;

            var gameScene = GameScene.instance;
            if (gameScene == null) return;

            // Обрабатываем ТОЛЬКО врагов, fxManager не трогаем!
            ProcessManager(gameScene.enemyManager, isFullInvisActive);
        }

        private static void ProcessManager(ActorObjectManager manager, bool isFullInvis)
        {
            var actorList = manager?.actorList;
            if (actorList == null) return;

            float now = Time.time;

            for (int i = 0; i < actorList.Count; i++)
            {
                var actor = actorList[i];
                if (actor == null || actor.destroyed || actor.Pointer == IntPtr.Zero) continue;

                float targetAlpha = 0.0f;

                if (!isFullInvis)
                {
                    // Безопасная регистрация времени без перехватов спавна
                    if (!_spawnTimes.TryGetValue(actor.Pointer, out float spawnTime))
                    {
                        spawnTime = now;
                        _spawnTimes[actor.Pointer] = spawnTime;
                    }

                    float age = now - spawnTime;

                    if (_visibleDuration == 0f) targetAlpha = 0.0f;
                    else if (_visibleDuration > 0f)
                    {
                        if (age <= _visibleDuration) targetAlpha = 1.0f;
                        else if (age <= _visibleDuration + _fadeDuration)
                            targetAlpha = Mathf.SmoothStep(1.0f, 0.0f, (age - _visibleDuration) / _fadeDuration);
                        else targetAlpha = 0.0f;
                    }
                    else
                    {
                        float delay = Mathf.Abs(_visibleDuration);
                        if (age <= delay) targetAlpha = 0.0f;
                        else if (age <= delay + _fadeDuration)
                            targetAlpha = Mathf.SmoothStep(0.0f, 1.0f, (age - delay) / _fadeDuration);
                        else targetAlpha = 1.0f;
                    }
                }

                float finalAlpha = Mathf.Max(targetAlpha, _currentRecallAlpha);
                ApplyAlpha(actor.renderComponent, finalAlpha);
            }
        }

        private static void ApplyAlpha(RenderComponent renderComp, float alpha)
        {
            if (renderComp == null || renderComp.Pointer == IntPtr.Zero) return;
            if (renderComp.animView == null || renderComp.animView.Pointer == IntPtr.Zero) return;
            if (renderComp.animView.anim == null || renderComp.animView.anim.Pointer == IntPtr.Zero) return;

            renderComp.animView.anim.alpha = alpha;
        }

        public static void ResetAll()
        {
            _spawnTimes.Clear();
            _currentRecallAlpha = 0f;

            var gameScene = GameScene.instance;
            if (gameScene == null) return;

            ResetManager(gameScene.enemyManager);
        }

        private static void ResetManager(ActorObjectManager manager)
        {
            var actorList = manager?.actorList;
            if (actorList == null) return;

            for (int i = 0; i < actorList.Count; i++)
            {
                var actor = actorList[i];
                if (actor != null && actor.Pointer != IntPtr.Zero) ApplyAlpha(actor.renderComponent, 1.0f);
            }
        }

        private static bool IsInLevel()
        {
            return MainGame.instance?.gameSceneManager?.gameScene?.logicManager != null;
        }
    }
}
