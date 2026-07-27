using HarmonyLib;
using Il2Cpp;

namespace jsb_new
{
    public static class DebugUnlock
    {
        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(
                typeof(MainGame).GetMethod("init"),
                          prefix: new HarmonyMethod(typeof(DebugUnlock), nameof(OnInit))
            );
            DebugStrings.Log("DebugUnlock initialized");
        }

        public static void OnInit()
        {
            VersionInfo.isDebug = true;
            VersionInfo.isFastSkip = true;
        }
    }
}
