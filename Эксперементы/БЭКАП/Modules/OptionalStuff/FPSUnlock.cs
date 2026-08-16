using MelonLoader;
using UnityEngine;

namespace jsb_new
{
    public static class FPSUnlock
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("FPSUnlock");
            set => ModuleRegistry.SetActive("FPSUnlock", value);
        }

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            MelonEvents.OnUpdate.Subscribe(OnUpdate);

            ModuleRegistry.RegisterCheckbox("FPS Unlock (400 FPS)",
                                            () => Enabled,
                                            (newValue) => SetEnabledState(newValue)
            );

            if (Enabled)
                ApplyFPSUnlock();
            else
                ResetFPS();
        }

        private static void OnUpdate()
        {
            if (Enabled)
            {
                if (QualitySettings.vSyncCount != 0 || Application.targetFrameRate != 400)
                {
                    ApplyFPSUnlock();
                }
            }
        }

        public static void SetEnabledState(bool newValue)
        {
            Enabled = newValue;
            if (Enabled)
            {
                ApplyFPSUnlock();
                DebugStrings.Log("FPSUnlock: enabled — 400 FPS");
            }
            else
            {
                ResetFPS();
                DebugStrings.Log("FPSUnlock: disabled");
            }
        }

        public static void ApplyFPSUnlock()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 400;
        }

        public static void ResetFPS()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
        }
    }
}
