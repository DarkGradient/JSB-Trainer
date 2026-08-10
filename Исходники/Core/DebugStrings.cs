using MelonLoader;

namespace jsb_new
{
    public static class DebugStrings
    {
        public static bool Enabled = true;

        public static void Log(string message)
        {
            if (Enabled)
                MelonLogger.Msg($"[DEBUG] {message}");
        }
    }
}
