using System.Reflection;
using MelonLoader;

namespace jsb_new
{
    public static class ModuleRegistry
    {
        // === ИНИЦИАЛИЗАЦИЯ MELON PREFERENCES ===
        // Категория настроек в файле UserData/MelonPreferences.cfg
        private static readonly MelonPreferences_Category _prefCategory =
        MelonPreferences.CreateCategory("JSAB_ExtraStuff", "JS&B Extra Stuff Settings");

        // Кэш созданных записей
        private static readonly Dictionary<string, MelonPreferences_Entry<bool>> _boolEntries = new Dictionary<string, MelonPreferences_Entry<bool>>();
        private static readonly Dictionary<string, MelonPreferences_Entry<float>> _floatEntries = new Dictionary<string, MelonPreferences_Entry<float>>();

        // === ЕДИНЫЙ РЕЕСТР СОСТОЯНИЙ ===
        private static readonly Dictionary<string, bool> _states = new Dictionary<string, bool>();

        public static bool IsActive(string name) => _states.TryGetValue(name, out var val) && val;
        public static void SetActive(string name, bool val) => _states[name] = val;

        private static Action _updateActions = delegate { };
        private static Action _guiActions = delegate { };

        public static void InitializeAll(HarmonyLib.Harmony harmony)
        {
            var assembly = Assembly.GetExecutingAssembly();

            foreach (var type in assembly.GetTypes())
            {
                if (type.Name == "Main" || type.Name == "ModuleRegistry" || type.Name == "TrainerSettingsBuilder" || type.Name == "UI")
                    continue;

                try
                {
                    var initMethod = type.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                    if (initMethod != null)
                    {
                        var parameters = initMethod.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(HarmonyLib.Harmony))
                            initMethod.Invoke(null, new object[] { harmony });
                        else if (parameters.Length == 0)
                            initMethod.Invoke(null, null);

                        DebugStrings.Log($"[Registry] Initialized: {type.Name}");
                    }

                    var updateMethod = type.GetMethod("Update", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                    if (updateMethod != null)
                        _updateActions += (Action)Delegate.CreateDelegate(typeof(Action), updateMethod);

                    var guiMethod = type.GetMethod("OnGUI", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                    if (guiMethod != null)
                        _guiActions += (Action)Delegate.CreateDelegate(typeof(Action), guiMethod);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[Registry] Failed to hook {type.Name}: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            // Сохраняем первичную конфигурацию при старте
            _prefCategory.SaveToFile(false);
        }

        public static void UpdateAll() => _updateActions();
        public static void GUIAll() => _guiActions();


        // === ДИНАМИЧЕСКОЕ ПОСТРОЕНИЕ UI С АВТОСОХРАНЕНИЕМ ===
        public class CheckboxDef
        {
            public string Category { get; set; } = null!;
            public string Name { get; set; } = null!;
            public Func<bool> Getter { get; set; } = null!;
            public Action<bool> Setter { get; set; } = null!;
            public Func<bool>? IsLocked { get; set; }
            public int Order { get; set; }
        }

        public class SliderDef
        {
            public string Category { get; set; } = null!;
            public string Name { get; set; } = null!;
            public float DefaultValue { get; set; }
            public Action<float> OnChanged { get; set; } = null!;
            public int Order { get; set; }
        }

        public static List<CheckboxDef> Checkboxes = new List<CheckboxDef>();
        public static List<SliderDef> Sliders = new List<SliderDef>();

        // --- АВТОМАТИЧЕСКАЯ РЕГИСТРАЦИЯ И СОХРАНЕНИЕ ЧЕКБОКСОВ ---
        public static void RegisterCheckbox(string category, string name, Func<bool> getter, Action<bool> setter, Func<bool>? isLocked = null, int order = 0)
        {
            // Формируем уникальный ключ для конфига, например "Optional_Stuff_Auto_Dash"
            string key = $"{category}_{name}".Replace(" ", "_");

            // 1. Создаем или читаем значение из MelonPreferences (значение по умолчанию берем из гетера)
            var entry = _prefCategory.CreateEntry(key, getter(), name);
            _boolEntries[key] = entry;

            // 2. Сразу применяем сохраненное из файла значение в модуль при запуске игры!
            setter(entry.Value);

            // 3. Создаем обертку для сеттера: при клике в UI обновляем модуль + сохраняем файл
            Action<bool> autoSavingSetter = (newValue) =>
            {
                setter(newValue);
                entry.Value = newValue;
                _prefCategory.SaveToFile(false); // Записывает изменение на диск
            };

            Checkboxes.Add(new CheckboxDef
            {
                Category = category,
                Name = name,
                Getter = getter,
                Setter = autoSavingSetter,
                IsLocked = isLocked,
                Order = order
            });
        }

        // --- АВТОМАТИЧЕСКАЯ РЕГИСТРАЦИЯ И СОХРАНЕНИЕ СЛАЙДЕРОВ ---
        public static void RegisterSlider(string category, string name, float defaultValue, Action<float> onChanged, int order = 0)
        {
            string key = $"{category}_{name}".Replace(" ", "_");

            var entry = _prefCategory.CreateEntry(key, defaultValue, name);
            _floatEntries[key] = entry;

            // Сразу применяем сохраненный уровень слайдера при запуске
            onChanged(entry.Value);

            Action<float> autoSavingOnChanged = (newValue) =>
            {
                onChanged(newValue);
                entry.Value = newValue;
                _prefCategory.SaveToFile(false);
            };

            Sliders.Add(new SliderDef
            {
                Category = category,
                Name = name,
                DefaultValue = entry.Value,
                OnChanged = autoSavingOnChanged,
                Order = order
            });
        }
    }
}
