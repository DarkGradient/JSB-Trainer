namespace jsb_new
{
    public static class UI
    {
        public static void BuildSettingsMenu(HarmonyLib.Harmony harmony)
        {
            TrainerSettingsBuilder.Initialize(harmony);
            MenuLayout.Build();
        }
    }
}
