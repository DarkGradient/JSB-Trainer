using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace jsb_new
{
    // FULL CHAOS — реально всё, что нашли в дампе, в одном модуле.
    // Бывший ChronoChaos (тайм-джампы) + всё остальное, до чего дотянулись:
    //
    //   - TIME     — LogicSpawnCreator.putMusicTo(), сдвиг/телепорт по
    //                таймлайну песни (было и раньше, тут прокачано)
    //   - SHAKE    — EffectShake.addToActor(CameraFlash.mainCamera, x, y),
    //                та же система, что и родная тряска камеры
    //   - ZOOM     — CameraFlash.mainCamera.zoomScale, резкий панч с
    //                самокоррекцией обратно к 1 каждый кадр
    //   - FLASH    — EffectFlashColor.create(color, frames), тот же
    //                choke-point, через который идут все вспышки в игре
    //   - SPEED    — DynamicMusic.playbackSpeed на текущем треке
    //                (sfxViewDynamic), тоже с самокоррекцией к 1x
    //   - BGCOLOR  — MainGame.stage.color / FxBackground.lastStageColor,
    //                тот же путь, что патчит DisableColorSwap
    //
    // Каждый вектор — свой независимый таймер с try/catch и гарантированным
    // реселлером в finally. Это не перестраховка ради перестраховки — уже
    // словили реальный краш-луп на голом ChronoChaos (NRE в
    // skipCallbackToCurrentPosition из-за гонки тирдауна), когда исключение
    // до реселлера не доходило и модуль долбил один и тот же краш каждый
    // кадр до конца сессии. Здесь векторов в 6 раз больше — без этого
    // паттерна первый же одиночный сбой в любом из них тихо убьёт всю
    // сессию хаоса.
    //
    // FLASH единственный вектор с явным нижним пределом частоты (не чаще
    // раза в ~0.4с) — это не про "жалеть", это чтобы не влететь в
    // буквальный строб-эффект (>3 вспышек/сек — порог, где реально можно
    // словить фотосенситивный приступ, а не просто "неприятно"). Плюс FLASH
    // сам себя глушит, если включён PhotosensitiveGuard — модуль уже есть
    // в проекте, незачем с ним воевать.
    //
    // Только соло/NormalLevel — то же обоснование, что и раньше:
    // putMusicTo и вся эта возня чисто локальные, в мультиплеере ничего
    // не реплицируется, будет только твой собственный десинк.
    public static class FullChaos
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("FullChaos");
            set
            {
                ModuleRegistry.SetActive("FullChaos", value);
                if (!value)
                {
                    // подчищаем панч-эффекты при выключении, чтобы не
                    // застрять с перекошенным зумом/скоростью навсегда
                    ResetPunchState();
                }
                _nextTimeAt = _nextShakeAt = _nextZoomAt = _nextFlashAt = _nextSpeedAt = _nextBgAt = -1f;
                _lastBeatIndex = -1;
            }
        }

        private static bool SubEnabled(string key) => Enabled && ModuleRegistry.IsActive(key);
        public static bool TimeEnabled => SubEnabled("FullChaos_Time");
        public static bool ShakeEnabled => SubEnabled("FullChaos_Shake");
        public static bool ZoomEnabled => SubEnabled("FullChaos_Zoom");
        public static bool FlashEnabled => SubEnabled("FullChaos_Flash");
        public static bool SpeedEnabled => SubEnabled("FullChaos_Speed");
        public static bool BgEnabled => SubEnabled("FullChaos_Bg");
        public static bool BpmShakeEnabled => SubEnabled("FullChaos_BpmShake");

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            MelonEvents.OnUpdate.Subscribe(OnUpdate);

            ModuleRegistry.RegisterCheckbox("Gamemodes", "FULL CHAOS",
                                            () => Enabled,
                                            (v) => { Enabled = v; },
                                            isLocked: () => ModuleRegistry.IsActive("MouseControl"),
                                            order: 50
            );
            ModuleRegistry.RegisterCheckbox("Gamemodes", "  Chaos: Time Jumps",
                                            () => ModuleRegistry.IsActive("FullChaos_Time"),
                                            (v) => ModuleRegistry.SetActive("FullChaos_Time", v),
                                            isLocked: () => !Enabled, order: 51);
            ModuleRegistry.RegisterCheckbox("Gamemodes", "  Chaos: Camera Shake",
                                            () => ModuleRegistry.IsActive("FullChaos_Shake"),
                                            (v) => ModuleRegistry.SetActive("FullChaos_Shake", v),
                                            isLocked: () => !Enabled, order: 52);
            ModuleRegistry.RegisterCheckbox("Gamemodes", "  Chaos: Camera Zoom",
                                            () => ModuleRegistry.IsActive("FullChaos_Zoom"),
                                            (v) => ModuleRegistry.SetActive("FullChaos_Zoom", v),
                                            isLocked: () => !Enabled, order: 53);
            ModuleRegistry.RegisterCheckbox("Gamemodes", "  Chaos: Screen Flash",
                                            () => ModuleRegistry.IsActive("FullChaos_Flash"),
                                            (v) => ModuleRegistry.SetActive("FullChaos_Flash", v),
                                            isLocked: () => !Enabled, order: 54);
            ModuleRegistry.RegisterCheckbox("Gamemodes", "  Chaos: Music Speed",
                                            () => ModuleRegistry.IsActive("FullChaos_Speed"),
                                            (v) => ModuleRegistry.SetActive("FullChaos_Speed", v),
                                            isLocked: () => !Enabled, order: 55);
            ModuleRegistry.RegisterCheckbox("Gamemodes", "  Chaos: BG Color",
                                            () => ModuleRegistry.IsActive("FullChaos_Bg"),
                                            (v) => ModuleRegistry.SetActive("FullChaos_Bg", v),
                                            isLocked: () => !Enabled, order: 56);
            ModuleRegistry.RegisterCheckbox("Gamemodes", "  Chaos: BPM Shake",
                                            () => ModuleRegistry.IsActive("FullChaos_BpmShake"),
                                            (v) => ModuleRegistry.SetActive("FullChaos_BpmShake", v),
                                            isLocked: () => !Enabled, order: 57);

            // по умолчанию включаем все векторы разом при первом включении
            // мастер-тумблера — раз просили "показать все возможности",
            // не заставлять руками тыкать галочки
            foreach (var key in new[] { "FullChaos_Time", "FullChaos_Shake", "FullChaos_Zoom",
                                         "FullChaos_Flash", "FullChaos_Speed", "FullChaos_Bg", "FullChaos_BpmShake" })
            {
                if (!ModuleRegistry.IsActive(key))
                    ModuleRegistry.SetActive(key, true);
            }

            HUDManager.CreateHUD(
                key: "FullChaos",
                textGetter: () => "FULL CHAOS",
                baseColor: Color.white,
                pulseColor: Color.red,
                activeGetter: () => Enabled,
                height: 35f
            );

            DebugStrings.Log("FullChaos initialized");
        }

        // ====================== ПЛАНИРОВЩИКИ ======================

        private static float _nextTimeAt = -1f;
        private static float _nextShakeAt = -1f;
        private static float _nextZoomAt = -1f;
        private static float _nextFlashAt = -1f;
        private static float _nextSpeedAt = -1f;
        private static float _nextBgAt = -1f;

        private static void OnUpdate()
        {
            if (!Enabled) return;

            // панч-эффекты (zoom/speed) сами себя тянут обратно к норме
            // каждый кадр, независимо от того, активны их тумблеры сейчас
            // или нет — иначе выключение подвектора посреди панча оставит
            // камеру/музыку перекошенной навсегда
            DecayZoom();
            DecaySpeed();

            var logic = GetNormalLevelLogic();
            if (logic == null)
            {
                _nextTimeAt = _nextShakeAt = _nextZoomAt = _nextFlashAt = _nextSpeedAt = _nextBgAt = -1f;
                _lastBeatIndex = -1;
                return;
            }

            // не через RunScheduled — тут не случайный интервал, а
            // детекция пересечения доли такта, гонять нужно каждый кадр
            if (BpmShakeEnabled)
                DoBpmShakeCheck();
            else
                _lastBeatIndex = -1; // чтобы при повторном включении не поймать ложный огромный скачок beatIndex

            RunScheduled(TimeEnabled, ref _nextTimeAt, () => DoTimeJump(logic),
                         ShortRange(1f, 3f, 8f, 20f, 0.6f, burstChance: 0.2f, burstDelay: (0.1f, 0.4f)));

            RunScheduled(ShakeEnabled, ref _nextShakeAt, DoShake,
                         ShortRange(0.5f, 1.5f, 3f, 6f, 0.7f));

            RunScheduled(ZoomEnabled, ref _nextZoomAt, DoZoomPunch,
                         ShortRange(1f, 2.5f, 4f, 8f, 0.6f));

            RunScheduled(FlashEnabled, ref _nextFlashAt, DoFlash,
                         ShortRange(0.8f, 2f, 3f, 7f, 0.55f), minFloor: 0.4f);

            RunScheduled(SpeedEnabled, ref _nextSpeedAt, () => DoSpeedWarp(logic),
                         ShortRange(2f, 4f, 6f, 12f, 0.55f));

            RunScheduled(BgEnabled, ref _nextBgAt, DoBgColor,
                         ShortRange(1.5f, 3.5f, 5f, 10f, 0.6f));
        }

        // единая обвязка планировщика: если что-то из вектора упало —
        // логируем и едем дальше, реселлер всегда отрабатывает
        private static void RunScheduled(bool enabledFlag, ref float nextAt, System.Action action,
                                          (float, float) interval, float minFloor = 0f)
        {
            if (!enabledFlag) { nextAt = -1f; return; }

            if (nextAt < 0f) { nextAt = Time.unscaledTime + UnityEngine.Random.Range(interval.Item1, interval.Item2); return; }

            if (Time.unscaledTime >= nextAt)
            {
                try { action(); }
                catch (System.Exception ex) { MelonLogger.Warning($"[FullChaos] vector skipped: {ex.Message}"); }
                finally
                {
                    float next = UnityEngine.Random.Range(interval.Item1, interval.Item2);
                    nextAt = Time.unscaledTime + Mathf.Max(next, minFloor);
                }
            }
        }

        // бимодальный интервал: shortMin/shortMax чаще, longMin/longMax реже
        private static (float, float) ShortRange(float shortMin, float shortMax, float longMin, float longMax,
                                                   float shortChance, float burstChance = 0f, (float, float)? burstDelay = null)
        {
            if (burstChance > 0f && burstDelay.HasValue && UnityEngine.Random.value < burstChance)
                return burstDelay!.Value;

            return UnityEngine.Random.value < shortChance ? (shortMin, shortMax) : (longMin, longMax);
        }

        // ====================== ВЕКТОРЫ ХАОСА ======================

        // --- TIME: см. историю ChronoChaos, логика та же, без урезаний ---
        private const float MaxJumpSeconds = 10f;
        private const float MinMagnitudeFraction = 0.3f;
        private const float TeleportChance = 0.3f;

        private static void DoTimeJump(ActorNormalLevelLogic logic)
        {
            var spawnCreator = logic.logicEnemyCreator;
            if (spawnCreator == null || spawnCreator.metaSong == null || spawnCreator.sfxView == null) return;

            float currentPosMs = spawnCreator.sfxView.position;
            float newPosMs;
            string label;

            if (UnityEngine.Random.value < TeleportChance && spawnCreator.sfxView.length > 0)
            {
                newPosMs = UnityEngine.Random.Range(0f, spawnCreator.sfxView.length * 1000f);
                label = "TELEPORT";
            }
            else
            {
                float minMag = MaxJumpSeconds * MinMagnitudeFraction;
                float magnitude = UnityEngine.Random.Range(minMag, MaxJumpSeconds);
                float offsetSeconds = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * magnitude;
                newPosMs = currentPosMs + offsetSeconds * 1000f;
                label = offsetSeconds >= 0f ? $"+{offsetSeconds:F1}s" : $"{offsetSeconds:F1}s";
            }

            newPosMs = Mathf.Max(0f, newPosMs);
            spawnCreator.putMusicTo(newPosMs);
            HUDManager.CreateToast(label, newPosMs >= currentPosMs ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.4f, 0.4f), 1.2f);
        }

        // --- SHAKE: та же система, что рвёт камеру в ваниле на боссах ---
        private const float MaxShakeForce = 120f;

        private static void DoShake()
        {
            var cam = CameraFlash.mainCamera;
            if (cam == null) return;

            float fx = UnityEngine.Random.Range(30f, MaxShakeForce);
            float fy = UnityEngine.Random.Range(30f, MaxShakeForce);
            EffectShake.addToActor(cam, fx, fy);
        }

        // --- BPM SHAKE: не рандом по таймеру, а честная детекция
        // пересечения доли такта через BPMProvider.CurrentBeat. Каждый
        // раз, когда floor(beat) меняется — новая доля наступила —
        // дёргаем камеру. Сила чуть плавает, чтобы не звучать метрономом.
        private const float BpmShakeMin = 15f;
        private const float BpmShakeMax = 55f;

        private static int _lastBeatIndex = -1;

        private static void DoBpmShakeCheck()
        {
            try
            {
                float beat = BPMProvider.CurrentBeat;
                if (beat <= 0f) return; // трек ещё не поймался/не играет

                int beatIndex = Mathf.FloorToInt(beat);
                if (beatIndex == _lastBeatIndex) return;

                // защита от ложного огромного скачка: если beatIndex
                // прыгнул больше чем на пару долей за кадр (первый вызов
                // после включения, смена трека, TIME-вектор дёрнул позицию
                // на другой конец песни) — просто ресинкаемся без тряски,
                // а не бьём разом ворохом пропущенных "долей"
                bool bigJump = _lastBeatIndex < 0 || Mathf.Abs(beatIndex - _lastBeatIndex) > 2;
                _lastBeatIndex = beatIndex;
                if (bigJump) return;

                var cam = CameraFlash.mainCamera;
                if (cam == null) return;

                float force = UnityEngine.Random.Range(BpmShakeMin, BpmShakeMax);
                EffectShake.addToActor(cam, force, force);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[FullChaos] BPM shake skipped: {ex.Message}");
            }
        }

        // --- ZOOM: резкий панч zoomScale с самокоррекцией к 1 ---
        private const float ZoomPunchMin = 0.55f;
        private const float ZoomPunchMax = 1.8f;
        private const float ZoomDecaySpeed = 2.2f; // скорость возврата к 1 в секунду

        private static void DoZoomPunch()
        {
            var cam = CameraFlash.mainCamera;
            if (cam == null) return;
            cam.zoomScale = UnityEngine.Random.Range(ZoomPunchMin, ZoomPunchMax);
        }

        private static void DecayZoom()
        {
            var cam = CameraFlash.mainCamera;
            if (cam == null) return;
            cam.zoomScale = Mathf.MoveTowards(cam.zoomScale, 1f, Time.unscaledDeltaTime * ZoomDecaySpeed);
        }

        // --- FLASH: тот же choke-point, что рвёт экран боссам ---
        private static void DoFlash()
        {
            if (PhotosensitiveGuard.Enabled) return; // не воюем со своим же модулем

            uint color = (uint)UnityEngine.Random.Range(0, 0xFFFFFF);
            int frames = UnityEngine.Random.Range(10, 40); // ~0.15-0.65с при 60fps-эквиваленте
            EffectFlashColor.create(color, frames);
        }

        // --- SPEED: playbackSpeed текущего трека, самокоррекция к 1x ---
        private const float SpeedPunchMin = 0.4f;
        private const float SpeedPunchMax = 2.2f;
        private const float SpeedDecaySpeed = 0.9f;

        private static void DoSpeedWarp(ActorNormalLevelLogic logic)
        {
            if (logic.music?.sfxViewDynamic == null) return;
            logic.music.sfxViewDynamic.playbackSpeed = UnityEngine.Random.Range(SpeedPunchMin, SpeedPunchMax);
        }

        private static void DecaySpeed()
        {
            var logic = GetNormalLevelLogic();
            if (logic?.music?.sfxViewDynamic == null) return;
            var view = logic.music.sfxViewDynamic;
            view.playbackSpeed = Mathf.MoveTowards(view.playbackSpeed, 1f, Time.unscaledDeltaTime * SpeedDecaySpeed);
        }

        // --- BGCOLOR: тот же путь, что патчит DisableColorSwap ---
        private static void DoBgColor()
        {
            if (DisableColorSwap.EffectiveEnabled) return; // не воюем со своим же модулем

            uint color = (uint)UnityEngine.Random.Range(0, 0xFFFFFF);
            MainGame.stage.color = color;
            FxBackground.lastStageColor = color;
        }

        private static void ResetPunchState()
        {
            var cam = CameraFlash.mainCamera;
            if (cam != null) cam.zoomScale = 1f;

            var logic = GetNormalLevelLogic();
            if (logic?.music?.sfxViewDynamic != null)
                logic.music.sfxViewDynamic.playbackSpeed = 1f;
        }

        private static ActorNormalLevelLogic? GetNormalLevelLogic()
        {
            if (GameScene.instance?.logicManager == null) return null;
            try
            {
                var normalType = Il2CppType.Of<ActorNormalLevelLogic>();
                var normalLogicObj = GameScene.instance.logicManager.getFirst(normalType);
                return normalLogicObj != null ? normalLogicObj.Cast<ActorNormalLevelLogic>() : null;
            }
            catch { return null; }
        }
    }
}
