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
            Header("Construction Anim");
            Item("Restore Small Construction Squares");
            Item("Construction Square Size");

            Item("Deltarune Grid BG");

            Header("Grid Layout");
            Item("DG Cell Size");
            Item("DG Line Thickness");
            Item("DG Lines X");
            Item("DG Lines Y");

            Header("Grid Movement");
            Item("DG Speed X1");
            Item("DG Speed Y1");
            Item("DG Speed X2");
            Item("DG Speed Y2");

            Header("Grid Colors");
            Item("DG Alpha 1");
            Item("DG Alpha 2");
            Item("DG Color R");
            Item("DG Color G");
            Item("DG Color B");

            Header("Grid Render");
            Item("DG Sorting Order");
            Item("DG Pos Z");

            // --- ВКЛАДКА 2: ГЕЙМПЛЕЙ ---
            Tab("Геймплей");
            Header("Модификаторы");
            Item("Nightcore Mode (NC)");
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
            Item("Focus Mode (Shift)");
            Item("Focus Speed Multiplier");
            Item("Force Rewind Music");

            Tab("Host");
            Header("Только для хоста MP");
            Item("Host Tools");
            Item("HT Force Start Run");
            Item("HT Force Vote (Tuto)");
            Item("HT Skip Lobby Timer");
            Item("HT Force Game Started");
            Item("HT Force Checkpoint");
            Item("HT Rewind (1 left)");
            Item("HT Rewind (3 left)");
            Item("HT Rewind (0 = SD)");
            Item("HT Force End Level");
            Item("HT Resurrect All (local)");
            Item("HT Kill All Heroes (local)");
            Item("HT Kill All Online Heroes");
            Item("HT Sudden Death (0 rewind)");
            Item("HT Mercy +1 Rewind");
            Item("HT Spam Vote Tuto");

            Tab("DEBUG");
            Item("[DEBUG] Hidden Mode");
            Item("[DEBUG] Full Invisibility");
            Item("[DEBUG] HD Delay");

        }

        public static IReadOnlyList<Entry> GetEntries() => _layout;
    }
}
