using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace jsb_new
{
    public static class Pizdec
    {
        // --- СОСТОЯНИЯ ФУНКЦИЙ ---
        public static bool EarthquakeEnabled
        {
            get => ModuleRegistry.IsActive("Pizdec_Earthquake");
            set => ModuleRegistry.SetActive("Pizdec_Earthquake", value);
        }
        public static float EarthquakeForce = 50f;

        public static bool DashMinesEnabled
        {
            get => ModuleRegistry.IsActive("Pizdec_DashMines");
            set => ModuleRegistry.SetActive("Pizdec_DashMines", value);
        }

        public static bool PlayerScaleEnabled
        {
            get => ModuleRegistry.IsActive("Pizdec_PlayerScale");
            set => ModuleRegistry.SetActive("Pizdec_PlayerScale", value);
        }
        public static float PlayerScaleValue = 3.0f;

        public static bool CamPanEnabled
        {
            get => ModuleRegistry.IsActive("Pizdec_CamPan");
            set => ModuleRegistry.SetActive("Pizdec_CamPan", value);
        }
        public static float CamPanSpeed = 3.0f;
        public static float CamPanDistance = 200.0f;

        // --- КЭШ И ТРЕКИНГ ---
        private static ActorNormalLevelLogic? _cachedLogic;
        private static float _lastEndLevelTime = 0f;
        private const float COOLDOWN = 3.0f;
        private static readonly HashSet<IntPtr> _dashingHeroPointers = new HashSet<IntPtr>();

        // --- ИНИЦИАЛИЗАЦИЯ ---
        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(
                typeof(ActorNormalLevelLogic).GetMethod("start"),
                          postfix: new HarmonyMethod(typeof(Pizdec), nameof(OnLogicStarted))
            );

            // Регистрация в UI
            ModuleRegistry.RegisterButton("PIZDEC (Force End Level)", ExecuteEndLevel);

            ModuleRegistry.RegisterCheckbox("Earthquake Mode",
                                            () => EarthquakeEnabled,
                                            (val) => EarthquakeEnabled = val
            );
            ModuleRegistry.RegisterSlider("Earthquake Force", 10f, 150f, 50f, (val) => EarthquakeForce = val);

            ModuleRegistry.RegisterCheckbox("Dash Mines",
                                            () => DashMinesEnabled,
                                            (val) => DashMinesEnabled = val
            );

            ModuleRegistry.RegisterCheckbox("Custom Player Scale",
                                            () => PlayerScaleEnabled,
                                            (val) => PlayerScaleEnabled = val
            );
            ModuleRegistry.RegisterSlider("Player Scale Value", 0.1f, 10.0f, 3.0f, (val) => PlayerScaleValue = val);

            ModuleRegistry.RegisterCheckbox("Oscillating Camera",
                                            () => CamPanEnabled,
                                            (val) => CamPanEnabled = val
            );
            ModuleRegistry.RegisterSlider("Cam Pan Speed", 0.5f, 10.0f, 3.0f, (val) => CamPanSpeed = val);
            ModuleRegistry.RegisterSlider("Cam Pan Distance", 50f, 600f, 200f, (val) => CamPanDistance = val);

            ModuleRegistry.RegisterButton("Spawn Fake Ghost", SpawnFakeGhost);
        }

        private static void OnLogicStarted(ActorNormalLevelLogic __instance)
        {
            _cachedLogic = __instance;
            _dashingHeroPointers.Clear();
        }

        // --- UPDATE LOOP ---
        public static void Update()
        {

            // 1. ЗЕМЛЕТРЯСЕНИЕ
            if (EarthquakeEnabled && CameraFlash.mainCamera != null && !CameraFlash.mainCamera.destroyed)
            {
                float randomAngle = UnityEngine.Random.Range(0f, 360f);
                CameraFlash.mainCamera.kickback(randomAngle, (int)EarthquakeForce);
            }

            // 2. РАСКАЧКА КАМЕРЫ ИЗ СТОРОНЫ В СТОРОНУ
            if (CamPanEnabled && CameraFlash.mainCamera != null && !CameraFlash.mainCamera.destroyed)
            {
                float offsetX = Mathf.Sin(Time.unscaledTime * CamPanSpeed) * CamPanDistance;
                CameraFlash.mainCamera.px = CameraFlash.mainCamera.startingPos.x + offsetX;
            }

            // БЕЗОПАСНАЯ ОБРАБОТКА ГЕРОЕВ
            ProcessHeroes();
        }

        private static void ProcessHeroes()
        {
            if (MainGame.instance == null || MainGame.instance.gameSceneManager == null) return;
            var gameScene = MainGame.instance.gameSceneManager.gameScene;
            if (gameScene == null || gameScene.heroManager == null) return;

            var heroList = gameScene.heroManager.actorList;
            if (heroList == null) return;

            for (int i = 0; i < heroList.Count; i++)
            {
                var actor = heroList[i];
                if (actor == null || actor.destroyed) continue;

                Hero hero = actor.TryCast<Hero>();
                if (hero == null) continue;

                // 3. ОБРАБОТКА МИН ПРИ ДАШЕ (БЕЗ ПАТЧЕЙ)
                if (DashMinesEnabled && hero.dashComponent != null)
                {
                    IntPtr ptr = hero.Pointer;
                    bool isDashing = hero.dashComponent.isDashing;

                    if (isDashing && !_dashingHeroPointers.Contains(ptr))
                    {
                        _dashingHeroPointers.Add(ptr);
                        try
                        {
                            RoundSpikes spike = RoundSpikes.create();
                            if (spike != null)
                            {
                                spike.px = hero.px;
                                spike.py = hero.py;
                                spike.setScale(1.2f);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            MelonLogger.Error($"[Pizdec] Failed to spawn mine: {ex.Message}");
                        }
                    }
                    else if (!isDashing && _dashingHeroPointers.Contains(ptr))
                    {
                        _dashingHeroPointers.Remove(ptr);
                    }
                }

                // 4. ИЗМЕНЕНИЕ РАЗМЕРА ИГРОКА (БЕЗ NULL REF)
                if (hero.heroRenderer != null && hero.heroRenderer.heroContainerMc != null)
                {
                    float targetScale = PlayerScaleEnabled ? PlayerScaleValue : 1.0f;
                    hero.heroRenderer.heroContainerMc.scaleX = targetScale;
                    hero.heroRenderer.heroContainerMc.scaleY = targetScale;
                }
            }
        }

        // --- МЕХАНИКА: FORCE END LEVEL ---
        public static void ExecuteEndLevel()
        {
            if (Time.unscaledTime - _lastEndLevelTime < COOLDOWN)
            {
                HUDManager.CreateToast("Pizdec на кулдауне!", Color.yellow, 1.5f);
                return;
            }

            try
            {
                var logic = GetLogic();
                if (logic == null || logic.destroyed)
                {
                    _cachedLogic = null;
                    HUDManager.CreateToast("Игрок не на уровне!", Color.red, 2f);
                    return;
                }

                if (logic.logicEndLevel == null)
                {
                    HUDManager.CreateToast("logicEndLevel == null", Color.red, 1.8f);
                    return;
                }

                if (logic is ActorMultiplayerLevelLogic multiLogic)
                {
                    if (multiLogic.logicMultiplayerLobby != null && multiLogic.logicMultiplayerLobby.InLobby)
                    {
                        HUDManager.CreateToast("Сейчас лобби!", Color.yellow, 1.8f);
                        return;
                    }

                    if (!multiLogic.IsHost())
                    {
                        HUDManager.CreateToast("Ты не хост!", Color.red, 1.8f);
                        return;
                    }
                }

                if (logic.logicEndLevel.isGameOver)
                {
                    HUDManager.CreateToast("Уровень уже завершается", Color.yellow, 1.5f);
                    return;
                }

                logic.logicEndLevel.onCompleteLastCheckpoint();

                _lastEndLevelTime = Time.unscaledTime;
                HUDManager.CreateToast("ПИЗДЕЦ АКТИВИРОВАН!", Color.green, 2.0f);
            }
            catch (System.Exception ex)
            {
                string msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                MelonLogger.Error($"[Pizdec] Exception: {msg}");
                HUDManager.CreateToast("Pizdec error (см. консоль)", Color.red, 2.5f);
            }
        }

        // --- МЕХАНИКА: СПАВН ПРИЗРАКА ---
        public static void SpawnFakeGhost()
        {
            try
            {
                if (PlayerManager.instance == null)
                {
                    HUDManager.CreateToast("PlayerManager не найден", Color.red, 2f);
                    return;
                }

                MetaPlayer firstLocalPlayer = PlayerManager.instance.GetFirstLocalPlayer();
                if (firstLocalPlayer == null)
                {
                    HUDManager.CreateToast("Локальный игрок не найден", Color.red, 2f);
                    return;
                }

                Hero hero = Hero.getHeroFromModelPlayer(firstLocalPlayer.modelPlayer);
                if (hero == null || hero.destroyed)
                {
                    HUDManager.CreateToast("Герой не на уровне!", Color.red, 2f);
                    return;
                }

                HeroGhost ghost = new HeroGhost(hero.modelPlayer);
                ghost.px = hero.px;
                ghost.py = hero.py;
                ghost.refresh();

                HUDManager.CreateToast("Призрак заспавнен!", Color.cyan, 1.8f);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[Pizdec] Error spawning ghost: {ex.Message}");
                HUDManager.CreateToast("Ошибка спавна призрака", Color.red, 2f);
            }
        }

        private static ActorNormalLevelLogic? GetLogic()
        {
            if (_cachedLogic != null && !_cachedLogic.destroyed)
                return _cachedLogic;

            _cachedLogic = null;
            return null;
        }
    }
}
