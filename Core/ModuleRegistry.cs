using System.Reflection;
using MelonLoader;

namespace jsb_new
{
    public static class ModuleRegistry
    {
        private static readonly MelonPreferences_Category _prefCategory =
        MelonPreferences.CreateCategory("JSAB_ExtraStuff", "JS&B Extra Stuff Settings");

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

            _prefCategory.SaveToFile(false);
        }

        public static void UpdateAll() => _updateActions();
        public static void GUIAll() => _guiActions();


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

        // Обычная простая регистрация чекбоксов
        public static void RegisterCheckbox(
            string category,
            string name,
            Func<bool> getter,
            Action<bool> setter,
            Func<bool>? isLocked = null,
            int order = 0)
        {
            string key = $"{category}_{name}".Replace(" ", "_");

            var entry = _prefCategory.CreateEntry(key, getter(), name);
            bool loadedValue = entry.Value;

            // --- ВСЯ ЗАЩИТА В 2 СТРОЧКАХ ---
            // Если из файла прочиталось true, но опция заблокирована твоим isLocked — сбрасываем в false!
            if (isLocked != null && isLocked() && loadedValue)
            {
                loadedValue = false;
                entry.Value = false;
            }

            setter(loadedValue);

            Action<bool> autoSavingSetter = (newValue) =>
            {
                // Если кнопка заблокирована — не даем ее включить
                if (newValue && isLocked != null && isLocked())
                    return;

                setter(newValue);
                entry.Value = newValue;
                _prefCategory.SaveToFile(false); // Автосохранение на диск
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

        public static void RegisterSlider(string category, string name, float defaultValue, Action<float> onChanged, int order = 0)
        {
            string key = $"{category}_{name}".Replace(" ", "_");

            var entry = _prefCategory.CreateEntry(key, defaultValue, name);
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
