using HarmonyLib;
using MelonLoader;
using Il2Cpp;

namespace jsb_new
{
    /// <summary>
    /// В новой версии игры уменьшили ЧАСТОТУ клеток construction-анимации
    /// (widthPerSquare/heightPerSquare увеличили, судя по декомпилу: 50 у квадрата/
    /// пятиугольника/круга, 140 у шипа) — видимо ради производительности.
    /// Раньше клетки были размером примерно с игрока и плотно заполняли весь экран.
    ///
    /// widthPerSquare/heightPerSquare нигде не переопределяются снаружи класса —
    /// значение зашито в дефолт поля и используется в start() для построения сетки
    /// И для масштаба спрайта (setScale(widthPerSquare / 100f)), поэтому единственный
    /// путь всё вернуть — патчить start() ДО того, как он построит сетку.
    ///
    /// Actor.start() реально вызывается не в конструкторе, а на следующем тике
    /// (ActorObjectManager.add -> newActorList), так что Harmony Prefix на start()
    /// успевает подменить поля до всей логики построения.
    /// </summary>
    public static class ConstructionWallSizeFix
    {
        // Дефолты из деколмпила (на случай, если понадобится сравнить/откатить)
        private const int DEFAULT_SQUARE_PENTAGON_CIRCLE = 50;
        private const float DEFAULT_TRIANGLE = 50f;
        private const int DEFAULT_SPIKE = 140;
        private const int DEFAULT_CHALLENGE = 100; // FxConstructionSquareWallChallengeAnim (Arcade/Challenge конец уровня)

        public static bool Enabled = true;

        // "Размер игрока" — подстройте слайдером в меню под свой вкус,
        // 16-20 обычно и даёт тот самый плотный ковёр мелких клеток
        public static float TargetSquareSize = 32f;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            Patch(harmony, typeof(FxConstructionSquareWallAnim), nameof(PrefixSquare));
            Patch(harmony, typeof(FxConstructionTriangleWallAnim), nameof(PrefixTriangle));
            Patch(harmony, typeof(FxConstructionPentagonWallAnim), nameof(PrefixPentagon));
            Patch(harmony, typeof(FxConstructionCircleWallAnim), nameof(PrefixCircle));
            Patch(harmony, typeof(FxConstructionSpikeWallAnim), nameof(PrefixSpike));
            // Отдельный класс для конца уровня в Arcade/Challenge — НЕ то же самое,
            // что FxConstructionSquareWallAnim, вызывается через createWallFxEndChallenge()
            Patch(harmony, typeof(FxConstructionSquareWallChallengeAnim), nameof(PrefixChallenge));

            ModuleRegistry.RegisterCheckbox(
                "Restore Small Construction Squares",
                () => Enabled,
                                            v => Enabled = v);

            ModuleRegistry.RegisterSlider(
                "Construction Square Size",
                8f, 60f, TargetSquareSize,
                v => TargetSquareSize = v);
        }

        private static void Patch(HarmonyLib.Harmony harmony, Type target, string prefixMethodName)
        {
            var original = AccessTools.Method(target, "start");
            if (original == null)
            {
                MelonLogger.Warning($"[ConstructionWallSizeFix] Не нашёл start() в {target.Name}, пропускаю.");
                return;
            }

            var prefix = AccessTools.Method(typeof(ConstructionWallSizeFix), prefixMethodName);
            harmony.Patch(original, prefix: new HarmonyMethod(prefix));

            DebugStrings.Log($"[ConstructionWallSizeFix] Пропатчил start() у {target.Name}");
        }

        // Square / Pentagon / Circle / Spike — поля widthPerSquare/heightPerSquare типа int.
        // Имена намеренно РАЗНЫЕ (без перегрузок) — AccessTools.Method(name) падает
        // с AmbiguousMatchException, если под одним именем висит несколько сигнатур.
        private static void PrefixSquare(FxConstructionSquareWallAnim __instance)
        {
            if (!Enabled) return;
            int size = (int)TargetSquareSize;
            __instance.widthPerSquare = size;
            __instance.heightPerSquare = size;
        }

        private static void PrefixPentagon(FxConstructionPentagonWallAnim __instance)
        {
            if (!Enabled) return;
            int size = (int)TargetSquareSize;
            __instance.widthPerSquare = size;
            __instance.heightPerSquare = size;
        }

        private static void PrefixCircle(FxConstructionCircleWallAnim __instance)
        {
            if (!Enabled) return;
            int size = (int)TargetSquareSize;
            __instance.widthPerSquare = size;
            __instance.heightPerSquare = size;
        }

        private static void PrefixSpike(FxConstructionSpikeWallAnim __instance)
        {
            if (!Enabled) return;
            int size = (int)TargetSquareSize;
            __instance.widthPerSquare = size;
            __instance.heightPerSquare = size;
        }

        // Triangle — единственный, у кого widthPerSquare/heightPerSquare типа float
        private static void PrefixTriangle(FxConstructionTriangleWallAnim __instance)
        {
            if (!Enabled) return;
            __instance.widthPerSquare = TargetSquareSize;
            __instance.heightPerSquare = TargetSquareSize;
        }

        // Отдельный класс конца уровня для Arcade/Challenge (дефолт 100, а не 50)
        private static void PrefixChallenge(FxConstructionSquareWallChallengeAnim __instance)
        {
            if (!Enabled) return;
            int size = (int)TargetSquareSize;
            __instance.widthPerSquare = size;
            __instance.heightPerSquare = size;
        }
    }
}
