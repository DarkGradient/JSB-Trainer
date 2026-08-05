using System.Collections.Generic;

namespace jsb_new
{
    // Единственное место, где описан порядок построения меню настроек.
    // Модули (читы) ничего не знают о своей позиции в меню — они лишь
    // регистрируют чекбокс/слайдер по уникальному Name через ModuleRegistry.
    // Здесь мы этот Name находим и расставляем в нужном порядке, вперемешку
    // с заголовками и разделителями.
    //
    // Если удалить файл чита — соответствующий Item() просто ничего не найдёт
    // в ModuleRegistry.Checkboxes/Sliders и будет молча пропущен.
    public static class MenuLayout
    {
        private abstract class Entry { }
        private sealed class HeaderEntry : Entry { public string Text = ""; }
        private sealed class SpacerEntry : Entry { }
        private sealed class ItemEntry : Entry { public string Name = ""; }

        private static readonly List<Entry> _layout = new();

        // ---- Мини-DSL для описания структуры ----
        private static void Header(string text) => _layout.Add(new HeaderEntry { Text = text });
        private static void Spacer() => _layout.Add(new SpacerEntry());
        private static void Item(string name) => _layout.Add(new ItemEntry { Name = name });

        // ---- Порядок построения меню описывается только здесь ----
        static MenuLayout()
        {
            Spacer();
            Header("Hello, you can configure me here");
            Spacer();

            Header("Gamemodes");
            Spacer();
            Item("One-Hit Mode");
            Item("True One-Hit (Beta)");
            Item("Purple SOUL Mode");
            Item("Orange SOUL Mode");
            Item("Mouse Control");
            Item("Flashlight");

            Spacer();
            Header("Dunno where to put it, so it will be here");
            Header("Optional Stuff");
            Spacer();
            Item("Enable Audio Visualizer");
            Item("Noclip");
            Item("Hitboxes");
            Item("Always black BG");
            Item("Hide Timeline");
            Item("FPS Unlock (400 FPS)");
            Item("Dash Cooldown");
            Item("Auto Dash");

            Spacer();
        }

        // Строит меню в заданном порядке через TrainerSettingsBuilder.
        public static void Build()
        {
            foreach (var entry in _layout)
            {
                switch (entry)
                {
                    case HeaderEntry h:
                        TrainerSettingsBuilder.AddHeader(h.Text);
                        break;
                    case SpacerEntry:
                        TrainerSettingsBuilder.AddSpacer();
                        break;
                    case ItemEntry i:
                        AddItem(i.Name);
                        break;
                }
            }
        }

        private static void AddItem(string name)
        {
            var checkbox = ModuleRegistry.Checkboxes.Find(c => c.Name == name);
            if (checkbox != null)
            {
                TrainerSettingsBuilder.AddCheckbox(checkbox.Name, checkbox.Getter, checkbox.Setter, checkbox.IsLocked);
                return;
            }

            var slider = ModuleRegistry.Sliders.Find(s => s.Name == name);
            if (slider != null)
            {
                TrainerSettingsBuilder.AddSlider(slider.Name, slider.DefaultValue, slider.OnChanged);
                return;
            }

            // Пункт не зарегистрирован — файл чита отсутствует в сборке. Пропускаем.
            DebugStrings.Log($"[MenuLayout] Пункт \"{name}\" не найден в реестре — пропущен.");
        }
    }
}
