namespace jsb_new
{
    public static class MenuLayout
    {
        public abstract class Entry { }

        public sealed class TabEntry : Entry
        {
            public string Name = "";
        }

        public sealed class HeaderEntry : Entry
        {
            public string Text = "";
        }

        public sealed class SpacerEntry : Entry { }

        public sealed class ItemEntry : Entry
        {
            public string Name = "";
        }

        private static readonly List<Entry> _layout = new();

        private static void Tab(string name) => _layout.Add(new TabEntry { Name = name });
        private static void Header(string text) => _layout.Add(new HeaderEntry { Text = text });
        private static void Spacer() => _layout.Add(new SpacerEntry());
        private static void Item(string name) => _layout.Add(new ItemEntry { Name = name });

        static MenuLayout()
        {
            // --- ВКЛАДКА 1: ВИЗУАЛ ---
            Tab("Визуал");
            Header("Игрок и Враги");
            Item("Full Power Trail");
            Item("Custom Player Color");
            Item("Player RGB Mode");
            Item("Player Color");
            Spacer();
            Item("Custom Enemy Color");
            Item("Enemy RGB Mode");
            Item("Enemy Color");
            Item("Tornado / Spin Mode");
            Item("Spin Speed");

            // --- ВКЛАДКА 2: ГЕЙМПЛЕЙ ---
            Tab("Геймплей");
            Header("Модификаторы");
            Item("One-Hit Mode");
            Item("True One-Hit (Beta)");
            Item("Purple SOUL Mode");
            Item("Orange SOUL Mode");
            Item("Mouse Control");
            Item("Flashlight");
            Item("Flashlight Size");
            Item("Noclip");
            Item("Hitboxes");
            Item("Auto Dash");
            Item("Dash Cooldown");

            // --- ВКЛАДКА 3: ПРОЧЕЕ ---
            Tab("Прочее");
            Header("Система и Графика");
            Item("Enable Audio Visualizer");
            Item("Always black BG");
            Item("Hide Timeline");
            Item("FPS Unlock (400 FPS)");

            Header("Троллинг");
            Item("Force Rewind Music");
        }

        public static IReadOnlyList<Entry> GetEntries() => _layout;
    }
}
