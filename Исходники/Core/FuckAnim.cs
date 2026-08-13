using HarmonyLib;
using MelonLoader;
using Il2Cpp;

namespace jsb_new
{
    /// <summary>
    /// Глобальный ускоритель всех Flash-эмулированных MovieClip-анимаций.
    /// Всё UI/FX в игре крутится через FlashAnimationView.update():
    ///
    ///   if (timeBased) checkNextFrameTimeBased():
    ///       float num = 1000f / fps;
    ///       deltaTime = GameSpeed.getDeltaTime() + accumulatedDeltaTime;
    ///       while (deltaTime > num) { deltaTime -= num; gotoNextFrame(); }
    ///
    /// fps и timeBased — публичные поля, поэтому лезть в приватные checkNextFrameTimeBased/
    /// gotoNextFrame не нужно: временно взвинчиваем fps перед оригинальным update(),
    /// даём ему сожрать кучу кадров за один тик через while-цикл, и возвращаем fps
    /// обратно в Postfix — чтобы никакой другой код, читающий .fps как обычное число
    /// (десятки мест вида ".fps = 30f"), не увидел "испорченное" постоянное значение.
    ///
    /// НЕ трогает checkNextFrameSpeedBased() (timeBased == false, таких мало, там
    /// GameSpeed.getSpeed() напрямую) и НЕ трогает GameSpeed.getDeltaTime() глобально —
    /// та же дельта кормит PhysicComponentTimed/CallbackTimerEffect, разгон там задел бы
    /// физику и геймплейные таймеры, а не только интерфейс.
    ///
    /// Осторожно с зацикленными (isLooping) анимациями — 1000x их не "ускорит" в смысле
    /// пользы, они просто будут молотить while-цикл по кругу тысячи раз за тик впустую.
    /// Полезнее всего это на одноразовых construction/transition-анимациях конца
    /// уровня/попапов — тех самых "тупых" переходах, которые обычно просто ждёшь.
    /// </summary>
    public static class UIAnimationSpeedUp
    {
        public static bool Enabled = true;
        public static float Multiplier = 1000f;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            var original = AccessTools.Method(typeof(FlashAnimationView), "update");
            if (original == null)
            {
                MelonLogger.Warning("[UIAnimationSpeedUp] Не нашёл FlashAnimationView.update()");
                return;
            }

            var prefix = AccessTools.Method(typeof(UIAnimationSpeedUp), nameof(Prefix));
            var postfix = AccessTools.Method(typeof(UIAnimationSpeedUp), nameof(Postfix));
            harmony.Patch(original, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));

            DebugStrings.Log("[UIAnimationSpeedUp] Пропатчил FlashAnimationView.update()");

            ModuleRegistry.RegisterCheckbox(
                "Ускорить UI-анимации",
                () => Enabled,
                v => Enabled = v);

            ModuleRegistry.RegisterSlider(
                "UI Anim Speed x",
                1f, 1000f, Multiplier,
                v => Multiplier = v);
        }

        // __state прокидывает исходный fps из Prefix в Postfix, чтобы вернуть как было
        private static void Prefix(FlashAnimationView __instance, out float __state)
        {
            __state = __instance.fps;

            if (Enabled && __instance.timeBased)
            {
                __instance.fps = __state * Multiplier;
            }
        }

        private static void Postfix(FlashAnimationView __instance, float __state)
        {
            __instance.fps = __state;
        }
    }
}
