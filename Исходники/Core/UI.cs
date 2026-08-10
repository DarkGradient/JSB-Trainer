namespace jsb_new
{
    public static class UI
    {
        public static void BuildSettingsMenu(HarmonyLib.Harmony harmony)
        {
            // Старый TrainerSettingsBuilder больше не используем
            SettingsMenu.Initialize();
        }
    }
}
