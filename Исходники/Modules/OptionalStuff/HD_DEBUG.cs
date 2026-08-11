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

        // Настройки по умолчанию
        private static float _visibleDuration = 0.75f; // Дефолтное значение задержки (0.75)
        private static float _fadeDuration = 0.30f;    // Длительность плавного перехода

        // Настройки для функции Recall (Клавиша V)
        private const float RECALL_DURATION = 0.50f;   // Длительность проявления (0.5 сек)
        private const float RECALL_COOLDOWN = 1.00f;   // Кулдаун (1.0 сек)
        private static float _lastRecallTriggerTime = -100f;
        private static float _currentRecallAlpha = 0f;

        // Отслеживание времени спавна объектов
        private static readonly Dictionary<IntPtr, float> _spawnTimes = new();

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            ModuleRegistry.RegisterCheckbox(
                FEATURE_NAME,
                () => ModuleRegistry.IsActive(FEATURE_NAME),
                                            (enabled) =>
                                            {
                                                ModuleRegistry.SetActive(FEATURE_NAME, enabled);
                                                if (enabled)
                                                {
                                                    ModuleRegistry.SetActive(FULL_INVISIBLE_FEATURE, false);
                                                    _spawnTimes.Clear();
                                                }
                                                else
                                                {
                                                    ResetAll();
                                                }

                                                HUDManager.CreateToast(
                                                    enabled ? "DEBUG HD MODE: ON\nPress [V] to temporarily reveal objects" : "DEBUG HD MODE: OFF",
                                                    enabled ? Color.magenta : Color.gray,
                                                    2.5f
                                                );
                                            }
            );

            ModuleRegistry.RegisterCheckbox(
                FULL_INVISIBLE_FEATURE,
                () => ModuleRegistry.IsActive(FULL_INVISIBLE_FEATURE),
                                            (enabled) =>
                                            {
                                                ModuleRegistry.SetActive(FULL_INVISIBLE_FEATURE, enabled);
                                                if (enabled)
                                                {
                                                    ModuleRegistry.SetActive(FEATURE_NAME, false);
                                                    _spawnTimes.Clear();
                                                }
                                                else
                                                {
                                                    ResetAll();
                                                }

                                                HUDManager.CreateToast(
                                                    enabled ? "DEBUG INVISIBILITY: ON\nPress [V] to temporarily reveal objects" : "DEBUG INVISIBILITY: OFF",
                                                    enabled ? Color.red : Color.gray,
                                                    2.5f
                                                );
                                            }
            );

            // Слайдер от -5 до 5 с дефолтом 0.75
            ModuleRegistry.RegisterSlider(
                FADE_SLIDER_NAME,
                -5.0f, // min
                5.0f, // max
                0.75f, // default
                (val) =>
                {
                    _visibleDuration = val;
                }
            );

            // Патчим OnUnPool для моментального обновления времени спавна
            harmony.Patch(
                AccessTools.Method(typeof(Actor), nameof(Actor.OnUnPool)),
                          postfix: new HarmonyMethod(typeof(DebugHiddenMode), nameof(OnActorUnpooled_Postfix))
            );

            DebugStrings.Log("[DebugHiddenMode] Initialized!");
        }

        private static void OnActorUnpooled_Postfix(Actor __instance)
        {
            if (__instance != null && __instance.Pointer != IntPtr.Zero)
            {
                _spawnTimes[__instance.Pointer] = Time.time;
            }
        }

        public static void Update()
        {
            bool isHdActive = ModuleRegistry.IsActive(FEATURE_NAME);
            bool isFullInvisActive = ModuleRegistry.IsActive(FULL_INVISIBLE_FEATURE);

            if (!isHdActive && !isFullInvisActive)
            {
                if (_spawnTimes.Count > 0)
                {
                    ResetAll();
                }
                _currentRecallAlpha = 0f;
                return;
            }

            if (!IsInLevel())
            {
                if (_spawnTimes.Count > 0)
                {
                    _spawnTimes.Clear();
                }
                _currentRecallAlpha = 0f;
                return;
            }

            // --- ОБРАБОТКА RECALL (Клавиша V) ---
            float now = Time.time;
            float timeSinceLastRecall = now - _lastRecallTriggerTime;

            // Кулдаун 1.0 секунды после завершения показа (0.5s + 1.0s)
            bool isRecallOnCooldown = timeSinceLastRecall < (RECALL_DURATION + RECALL_COOLDOWN);

            if (Input.GetKeyDown(KeyCode.V) && !isRecallOnCooldown)
            {
                _lastRecallTriggerTime = now;
                HUDManager.CreateToast("RECALL!", Color.cyan, 0.5f);
            }

            // Плавный расчет альфы вспышки Recall (мягкое проявление -> удержание -> плавное гашение)
            if (timeSinceLastRecall <= RECALL_DURATION)
            {
                float p = timeSinceLastRecall / RECALL_DURATION; // 0..1
                if (p < 0.15f)
                    _currentRecallAlpha = Mathf.SmoothStep(0f, 1f, p / 0.15f); // Плавный вход (0.075 сек)
                    else if (p > 0.75f)
                        _currentRecallAlpha = Mathf.SmoothStep(1f, 0f, (p - 0.75f) / 0.25f); // Плавный выход (0.125 сек)
                        else
                            _currentRecallAlpha = 1.0f; // Удержание полной видимости
            }
            else
            {
                _currentRecallAlpha = 0f;
            }

            var gameScene = GameScene.instance;
            if (gameScene == null) return;

            ProcessManager(gameScene.enemyManager, isFullInvisActive);
            ProcessManager(gameScene.fxManager, isFullInvisActive);
        }

        private static void ProcessManager(ActorObjectManager manager, bool isFullInvis)
        {
            var actorList = manager?.actorList;
            if (actorList == null) return;

            float now = Time.time;

            for (int i = 0; i < actorList.Count; i++)
            {
                var actor = actorList[i];
                if (actor == null || actor.destroyed || actor.Pointer == IntPtr.Zero)
                    continue;

                float targetAlpha = 0.0f;

                if (isFullInvis)
                {
                    targetAlpha = 0.0f;
                }
                else
                {
                    if (!_spawnTimes.TryGetValue(actor.Pointer, out float spawnTime))
                    {
                        spawnTime = now;
                        _spawnTimes[actor.Pointer] = spawnTime;
                    }

                    float age = now - spawnTime;

                    // --- РАСЧЕТ АЛЬФЫ С ИСПОЛЬЗОВАНИЕМ СГЛАЖИВАНИЯ (Mathf.SmoothStep) ---
                    if (_visibleDuration == 0f)
                    {
                        targetAlpha = 0.0f;
                    }
                    else if (_visibleDuration > 0f)
                    {
                        // Положительное значение: спавн видимым -> плавный уход в невидимость
                        if (age <= _visibleDuration)
                        {
                            targetAlpha = 1.0f;
                        }
                        else if (age <= _visibleDuration + _fadeDuration)
                        {
                            float fadeProgress = (age - _visibleDuration) / _fadeDuration;
                            targetAlpha = Mathf.SmoothStep(1.0f, 0.0f, fadeProgress); // Мягкая S-кривая
                        }
                        else
                        {
                            targetAlpha = 0.0f;
                        }
                    }
                    else // _visibleDuration < 0f
                    {
                        // Отрицательное значение: спавн невидимым -> плавное проявление
                        float delay = Mathf.Abs(_visibleDuration);

                        if (age <= delay)
                        {
                            targetAlpha = 0.0f;
                        }
                        else if (age <= delay + _fadeDuration)
                        {
                            float fadeProgress = (age - delay) / _fadeDuration;
                            targetAlpha = Mathf.SmoothStep(0.0f, 1.0f, fadeProgress); // Мягкая S-кривая
                        }
                        else
                        {
                            targetAlpha = 1.0f;
                        }
                    }
                }

                // Учитываем вспышку Recall (если она активна — берем её плавную альфу)
                float finalAlpha = Mathf.Max(targetAlpha, _currentRecallAlpha);

                ApplyAlpha(actor.renderComponent, finalAlpha);
            }
        }

        private static void ApplyAlpha(RenderComponent renderComp, float alpha)
        {
            if (renderComp == null || renderComp.Pointer == IntPtr.Zero)
                return;

            if (renderComp.animView == null || renderComp.animView.Pointer == IntPtr.Zero)
                return;

            if (renderComp.animView.anim == null || renderComp.animView.anim.Pointer == IntPtr.Zero)
                return;

            renderComp.animView.anim.alpha = alpha;
        }

        public static void ResetAll()
        {
            _spawnTimes.Clear();
            _currentRecallAlpha = 0f;

            var gameScene = GameScene.instance;
            if (gameScene == null) return;

            ResetManager(gameScene.enemyManager);
            ResetManager(gameScene.fxManager);
        }

        private static void ResetManager(ActorObjectManager manager)
        {
            var actorList = manager?.actorList;
            if (actorList == null) return;

            for (int i = 0; i < actorList.Count; i++)
            {
                var actor = actorList[i];
                if (actor != null && actor.Pointer != IntPtr.Zero)
                {
                    ApplyAlpha(actor.renderComponent, 1.0f);
                }
            }
        }

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
    }
}
