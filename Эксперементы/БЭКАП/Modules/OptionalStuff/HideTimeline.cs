using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace jsb_new
{
    public static class HideTimeline
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("HideTimeline");
            set => ModuleRegistry.SetActive("HideTimeline", value);
        }

        private static bool _wasTimelineHidden = false;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            try
            {
                var originalRefresh = AccessTools.Method(typeof(ViewCheckpointProgress), "refreshViews");
                var prefixRefresh = new HarmonyMethod(typeof(HideTimeline).GetMethod(nameof(ViewCheckpointProgress_RefreshViews_Prefix)));
                harmony.Patch(originalRefresh, prefix: prefixRefresh);

                var originalUpdate = AccessTools.Method(typeof(ViewCheckpointProgress), "update");
                var postfixUpdate = new HarmonyMethod(typeof(HideTimeline).GetMethod(nameof(ViewCheckpointProgress_Update_Postfix)));
                harmony.Patch(originalUpdate, postfix: postfixUpdate);

                var originalShow = AccessTools.Method(typeof(ViewCheckpointProgress), "show");
                var prefixShow = new HarmonyMethod(typeof(HideTimeline).GetMethod(nameof(ViewCheckpointProgress_Show_Prefix)));
                harmony.Patch(originalShow, prefix: prefixShow);

                DebugStrings.Log("Hide timeline patch: success");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"==== HIDE TIMELINE PATCH: FAILED - {ex.Message} ====");
            }

            // Регистрирует свой чекбокс
            ModuleRegistry.RegisterCheckbox("Hide Timeline",
                () => Enabled,
                (newValue) =>
                {
                    Enabled = newValue;
                    DebugStrings.Log($"Hide timeline set to {newValue}");
                }
            );
        }

        public static bool ViewCheckpointProgress_RefreshViews_Prefix(ViewCheckpointProgress __instance)
        {
            if (Enabled)
            {
                __instance.destroyAllChildren();

                if (__instance.visual != null)
                {
                    __instance.visual.visible = false;
                    __instance.visual.alpha = 0f;
                }
                return false;
            }
            return true;
        }

        public static void ViewCheckpointProgress_Show_Prefix(ref bool b)
        {
            if (Enabled)
            {
                b = false;
            }
        }

        public static void ViewCheckpointProgress_Update_Postfix(ViewCheckpointProgress __instance)
        {
            if (Enabled)
            {
                _wasTimelineHidden = true;
                __instance.show(false);
                __instance.destroyAllChildren();

                if (__instance.visual != null)
                {
                    __instance.visual.visible = false;
                    __instance.visual.alpha = 0f;
                }
                if (__instance.progressCheckPointMc != null)
                {
                    __instance.progressCheckPointMc.visible = false;
                    __instance.progressCheckPointMc.alpha = 0f;
                }
            }
            else
            {
                if (_wasTimelineHidden)
                {
                    if (__instance.visual != null)
                    {
                        __instance.visual.visible = true;
                        __instance.visual.alpha = 1f;
                    }
                    if (__instance.progressCheckPointMc != null)
                    {
                        __instance.progressCheckPointMc.visible = true;
                        __instance.progressCheckPointMc.alpha = 1f;
                    }
                    __instance.show(true);
                    _wasTimelineHidden = false;
                }
            }
        }
    }
}
