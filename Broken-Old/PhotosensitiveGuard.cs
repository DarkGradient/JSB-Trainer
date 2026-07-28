using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace jsb_new
{
    // Реальный фоточувствительный режим, а не декорация.
    //
    // В игре уже есть MetaGameProgress.instance.metaOptions.metaOptionsSettings
    // .photosensitiveMode — гейтит ~20 источников белых/цветных вспышек
    // (в основном через EffectFlashColor.canDoFlashColor как choke-point:
    // DangerZone, BossVolcan, BossNewGame, ExplodingSpikes[Big],
    // LavaPlatformAnimated, FxThunderLoop, SpectraIntro, LogicSpectraRoomHit,
    // FxPlescoCheckerSquare[Wall], FxPlescoMidBoss, TryThisBlackHole, BlackHole,
    // Wall, BossVolcanContraRound, LogicBossNewGamePart4Beat, LogicBossNewGamePipe).
    // Но ваниль даёт выставить его только один раз при первом запуске
    // (UI_PhotosensitiveMode.createIfNeeded) либо руками в опциях — на лету
    // никак. И флаг вообще не трогает два отдельных источника укачивания:
    //
    //   1. Тряска камеры — EffectShakeConstant.applyShake (и её наследник
    //      EffectShake, использует ту же applyShake без override).
    //   2. "Дыхание" камеры — LogicMapCavernCamBreath, гоняет EffectRotate
    //      камеры синусоидой каждый кадр (пещеры, боссы, интенсивные моменты).
    //
    // EffectRotate — общий компонент (~35 других классов гоняют им патроны,
    // врагов, декорации), поэтому дыхание глушим не в EffectRotate.applyChanges
    // (сломало бы визуал по всей игре), а точечно в LogicMapCavernCamBreath
    // .update(), которая трогает только камерный экземпляр.
    public static class PhotosensitiveGuard
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("PhotosensitiveGuard");
            set
            {
                if (ModuleRegistry.IsActive("PhotosensitiveGuard") == value) return;
                ModuleRegistry.SetActive("PhotosensitiveGuard", value);

                var settings = MetaGameProgress.instance.metaOptions.metaOptionsSettings;
                if (value)
                {
                    _savedFlagBeforeOverride = settings.photosensitiveMode;
                    settings.photosensitiveMode = true;
                    _overrideActive = true;
                }
                else if (_overrideActive)
                {
                    settings.photosensitiveMode = _savedFlagBeforeOverride;
                    _overrideActive = false;
                }

                DebugStrings.Log($"PhotosensitiveGuard changed manually: {value}");
            }
        }

        // на сколько глушим амплитуду тряски и дыхания камеры.
        // 0.15f = 15% от обычной силы, не полный ноль — некоторые логики
        // ждут какого-то движения камеры для синхронизации, но перцептивно
        // уже безопасно
        private const float DampFactor = 0.15f;

        private static bool _savedFlagBeforeOverride;
        private static bool _overrideActive;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_DampenCameraShake));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_DampenCameraBreath));

            ModuleRegistry.RegisterCheckbox("Optional Stuff", "Photosensitive Guard",
                                            () => Enabled,
                                            (newValue) => { Enabled = newValue; },
                                            isLocked: null,
                                            order: 41
            );

            HUDManager.CreateHUD(
                key: "PhotosensitiveGuard",
                textGetter: () => "Photosensitive Guard Enabled",
                baseColor: Color.white,
                pulseColor: new Color(0.4f, 0.8f, 1f, 1f),
                activeGetter: () => Enabled,
                height: 35f,
                position: HUDPosition.TopRight
            );

            DebugStrings.Log("PhotosensitiveGuard initialized");
        }

        [HarmonyPatch(typeof(EffectShakeConstant), "applyShake")]
        private static class Patch_DampenCameraShake
        {
            static void Prefix(EffectShakeConstant __instance)
            {
                if (!Enabled) return;

                // shakeAppliedX/Y уже свежепересчитаны в update() из
                // нетронутых shakeForceX/Y на этом кадре — масштабируем
                // именно их, а не force, чтобы не накапливать затухание
                __instance.shakeAppliedX *= DampFactor;
                __instance.shakeAppliedY *= DampFactor;
            }
        }

        [HarmonyPatch(typeof(LogicMapCavernCamBreath), "update")]
        private static class Patch_DampenCameraBreath
        {
            static void Postfix(LogicMapCavernCamBreath __instance)
            {
                if (!Enabled) return;
                if (__instance.effectRotate == null) return;

                __instance.effectRotate.xDistance *= DampFactor;
                __instance.effectRotate.yDistance *= DampFactor;
            }
        }
    }
}
