namespace jsb_new
{
    public static class UI
    {
        public static void BuildSettingsMenu(HarmonyLib.Harmony harmony)
        {
            TrainerSettingsBuilder.Initialize(harmony);

            TrainerSettingsBuilder.AddSpacer();
            TrainerSettingsBuilder.AddHeader("Hello, you can configure me here");
            TrainerSettingsBuilder.AddSpacer();

            TrainerSettingsBuilder.AddHeader("Gamemodes");
            TrainerSettingsBuilder.AddSpacer();
            BuildCategory("Gamemodes");

            TrainerSettingsBuilder.AddSpacer();
            TrainerSettingsBuilder.AddHeader("Dunno where to put it, so it will be here");
            TrainerSettingsBuilder.AddHeader("Optional Stuff");
            TrainerSettingsBuilder.AddSpacer();
            BuildCategory("Optional Stuff");

            TrainerSettingsBuilder.AddSpacer();
        }

        private static void BuildCategory(string category)
        {
            var checkboxes = ModuleRegistry.Checkboxes.Where(c => c.Category == category).OrderBy(c => c.Order).ToList();
            foreach (var cb in checkboxes)
            {
                TrainerSettingsBuilder.AddCheckbox(cb.Name, cb.Getter, cb.Setter, cb.IsLocked);
            }

            var sliders = ModuleRegistry.Sliders.Where(s => s.Category == category).OrderBy(s => s.Order).ToList();
            foreach (var sl in sliders)
            {
                TrainerSettingsBuilder.AddSlider(sl.Name, sl.DefaultValue, sl.OnChanged);
            }
        }
    }
}
