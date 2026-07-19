#pragma warning disable CS8618 // Поля, не допускающие значения NULL, не инициализированы
#pragma warning disable CS8600 // Преобразование null-литерала
#pragma warning disable CS8603 // Возможный возврат null

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace jsb_new
{
    public static class ModuleRegistry
    {
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
        }

        public static void UpdateAll() => _updateActions();
        public static void GUIAll() => _guiActions();

        // === ДИНАМИЧЕСКОЕ ПОСТРОЕНИЕ UI ===
        public class CheckboxDef { public string Category; public string Name; public Func<bool> Getter; public Action<bool> Setter; public Func<bool>? IsLocked; public int Order; }
        public class SliderDef { public string Category; public string Name; public float DefaultValue; public Action<float> OnChanged; public int Order; }

        public static List<CheckboxDef> Checkboxes = new List<CheckboxDef>();
        public static List<SliderDef> Sliders = new List<SliderDef>();

        public static void RegisterCheckbox(string category, string name, Func<bool> getter, Action<bool> setter, Func<bool>? isLocked = null, int order = 0)
        => Checkboxes.Add(new CheckboxDef { Category = category, Name = name, Getter = getter, Setter = setter, IsLocked = isLocked, Order = order });

        public static void RegisterSlider(string category, string name, float defaultValue, Action<float> onChanged, int order = 0)
        => Sliders.Add(new SliderDef { Category = category, Name = name, DefaultValue = defaultValue, OnChanged = onChanged, Order = order });
    }
}
