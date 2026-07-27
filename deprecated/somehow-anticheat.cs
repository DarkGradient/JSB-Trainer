using HarmonyLib;
using Il2Cpp;

namespace jsb_new
{
    [HarmonyPatch(typeof(LogicMultiplayerUpdateMusic), "isMusicWayTooLate")]
    public static class Patch_BlockForcedCatchup
    {
        static bool Prefix(ref bool __result)
        {
            __result = false;
            return false; // оригинальный метод не выполняется вообще
        }
    }
}
