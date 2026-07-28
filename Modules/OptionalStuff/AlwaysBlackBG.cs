using HarmonyLib;
using Il2Cpp;

namespace jsb_new
{
    public static class DisableColorSwap
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("DisableColorSwap");
            set
            {
                if (ModuleRegistry.IsActive("DisableColorSwap") == value) return;
                ModuleRegistry.SetActive("DisableColorSwap", value);
                DebugStrings.Log($"DisableColorSwap changed manually: {value}");
            }
        }

        public static bool EffectiveEnabled => Enabled || ModuleRegistry.IsActive("OneHit");

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_FxBackground_SetBackGroundColor));

            ModuleRegistry.RegisterCheckbox("Optional Stuff", "Always black BG",
                                            () => EffectiveEnabled,
                                            (newValue) => { Enabled = newValue; },
                                            isLocked: () => ModuleRegistry.IsActive("OneHit"),
                                            order: 40
            );

            DebugStrings.Log("Color swap blocker initialized");
        }

        [HarmonyPatch(typeof(FxBackground), "setBackGroundColor")]
        private static class Patch_FxBackground_SetBackGroundColor
        {
            static bool Prefix(ref uint color)
            {
                if (EffectiveEnabled)
                {
                    MainGame.stage.color = 0u;
                    FxBackground.lastStageColor = 0u;
                    return false;
                }
                return true;
            }
        }
    }
}
