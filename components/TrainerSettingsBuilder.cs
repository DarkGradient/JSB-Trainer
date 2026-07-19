using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace jsb_new
{
    public static class TrainerSettingsBuilder
    {
        private static int _currentId = 15000;
        private static float _lastToggleTime = 0f;
        private static bool _initialized = false;

        private static readonly Dictionary<int, string> _customLocales = new Dictionary<int, string>();
        private static readonly List<IMetaSettingsElement> _pendingElements = new List<IMetaSettingsElement>();

        private static readonly List<CustomCheckbox> _checkboxes = new List<CustomCheckbox>();
        private static readonly List<CustomButton> _buttons = new List<CustomButton>();
        private static readonly List<CustomSlider> _sliders = new List<CustomSlider>();
        private static readonly List<CustomDropdown> _dropdowns = new List<CustomDropdown>();

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            if (_initialized)
                return;
            _initialized = true;

            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_MetaSettings_BuildSettings));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_SettingViewDataBinding_RefreshBinding));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_UI_Options_Settings_OnChangeSetting));

            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_ModelLanguage_GetText));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_TextFactory_GetMsg));
        }

        // ---- Публичный API для регистрации элементов ----

        public static void AddSpacer()
        {
            _pendingElements.Add(new MetaSettingsSpacerElement().Cast<IMetaSettingsElement>());
        }

        public static void AddHeader(string text)
        {
            int id = GetNextId();
            RegisterLocale(id, text);

            MetaSettingsCategoryHeaderElement header = new MetaSettingsCategoryHeaderElement();
            header.text = new TextInstance(id, "");
            _pendingElements.Add(header.Cast<IMetaSettingsElement>());
        }

        public static void AddCheckbox(string text, Func<bool> getValue, Action<bool> onChanged, Func<bool>? isLocked = null)
        {
            int id = GetNextId();
            RegisterLocale(id, text);

            MetaSettingsCheckboxElement checkbox = new MetaSettingsCheckboxElement(MetaSettingsDataType.None);
            checkbox.text = new TextInstance(id, "");

            _checkboxes.Add(new CustomCheckbox
            {
                Id = id,
                GetValue = getValue,
                OnChanged = onChanged,
                IsLocked = isLocked,
                NativeElement = checkbox
            });

            _pendingElements.Add(checkbox.Cast<IMetaSettingsElement>());
        }

        public static void AddButton(string text, Action onPressed)
        {
            int id = GetNextId();
            RegisterLocale(id, text);

            MetaSettingsButtonElement button = new MetaSettingsButtonElement(MetaSettingsDataType.None);
            button.text = new TextInstance(id, "");

            _buttons.Add(new CustomButton
            {
                Id = id,
                OnPressed = onPressed,
                NativeElement = button
            });

            _pendingElements.Add(button.Cast<IMetaSettingsElement>());
        }

        public static void AddSlider(string text, float defaultValue, Action<float> onChanged)
        {
            int id = GetNextId();
            RegisterLocale(id, text);

            MetaSettingsSliderElement slider = new MetaSettingsSliderElement(MetaSettingsDataType.None);
            slider.text = new TextInstance(id, "");

            _sliders.Add(new CustomSlider
            {
                Id = id,
                Value = Mathf.Clamp01(defaultValue),
                         OnChanged = onChanged,
                         NativeElement = slider
            });

            _pendingElements.Add(slider.Cast<IMetaSettingsElement>());
        }

        // Дропдаун в live-биндинг стиле, как чекбокс: GetSelectedIndex спрашивается
        // каждый рефреш, а не копируется один раз при регистрации. Это важно,
        // если индекс меняется откуда-то помимо самого меню (хоткей, другой модуль).
        public static void AddDropdown(string text, List<string> options, Func<int> getSelectedIndex, Action<int> onChanged, Func<bool>? isLocked = null)
        {
            if (options == null || options.Count == 0)
                throw new ArgumentException("[TrainerSettingsBuilder] AddDropdown requires at least one option.");

            int id = GetNextId();
            RegisterLocale(id, text);

            MetaSettingsDropdownElement dropdown = new MetaSettingsDropdownElement(MetaSettingsDataType.None);
            dropdown.text = new TextInstance(id, "");

            _dropdowns.Add(new CustomDropdown
            {
                Id = id,
                Options = options,
                GetSelectedIndex = getSelectedIndex,
                OnChanged = onChanged,
                IsLocked = isLocked,
                NativeElement = dropdown
            });

            _pendingElements.Add(dropdown.Cast<IMetaSettingsElement>());
        }

        // ---- Внутренние утилиты ----

        private static int GetNextId()
        {
            return _currentId++;
        }

        private static void RegisterLocale(int id, string text)
        {
            _customLocales[id] = text;

            try
            {
                if (ModelLanguageEnum.ENGLISH != null)
                {
                    ModelLanguageEnum.ENGLISH.addText(id, text);
                }
                if (ModelLanguageEnum.RUSSIAN != null)
                {
                    ModelLanguageEnum.RUSSIAN.addText(id, text);
                }
            }
            catch (Exception ex)
            {
                DebugStrings.Log($"TrainerSettingsBuilder: failed to register fallback locale for ID {id}: {ex.Message}");
            }
        }

        public class CustomCheckbox
        {
            public int Id;
            public Func<bool> GetValue = default!;
            public Action<bool> OnChanged = default!;
            public Func<bool>? IsLocked;
            public MetaSettingsCheckboxElement NativeElement = default!;
        }

        public class CustomButton
        {
            public int Id;
            public Action OnPressed = default!;
            public MetaSettingsButtonElement NativeElement = default!;
        }

        public class CustomSlider
        {
            public int Id;
            public float Value;
            public Action<float> OnChanged = default!;
            public MetaSettingsSliderElement NativeElement = default!;
        }

        public class CustomDropdown
        {
            public int Id;
            public List<string> Options = default!;
            public Func<int> GetSelectedIndex = default!;
            public Action<int> OnChanged = default!;
            public Func<bool>? IsLocked;
            public MetaSettingsDropdownElement NativeElement = default!;
        }

        public static CustomCheckbox? FindCheckbox(IntPtr pointer) => _checkboxes.Find(c => c.NativeElement.Pointer == pointer);
        public static CustomButton? FindButton(IntPtr pointer) => _buttons.Find(b => b.NativeElement.Pointer == pointer);
        public static CustomSlider? FindSlider(IntPtr pointer) => _sliders.Find(s => s.NativeElement.Pointer == pointer);
        public static CustomDropdown? FindDropdown(IntPtr pointer) => _dropdowns.Find(d => d.NativeElement.Pointer == pointer);

        // ==== Единые Harmony-патчи для управления элементами ====

        [HarmonyPatch(typeof(MetaSettings), "BuildSettings")]
        private static class Patch_MetaSettings_BuildSettings
        {
            static void Postfix()
            {
                var list = MetaSettings.GetMetaSettingsElements();

                if (_pendingElements.Count > 0)
                {
                    var firstElement = _pendingElements[0];
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] != null && list[i].Pointer == firstElement.Pointer)
                        {
                            return;
                        }
                    }
                }

                for (int i = 0; i < _pendingElements.Count; i++)
                {
                    list.Add(_pendingElements[i]);
                }
            }
        }

        [HarmonyPatch(typeof(SettingViewDataBinding), "RefreshBinding")]
        private static class Patch_SettingViewDataBinding_RefreshBinding
        {
            static void Postfix(SettingViewDataBinding __instance)
            {
                if (__instance.Element == null)
                    return;

                var checkbox = FindCheckbox(__instance.Element.Pointer);
                if (checkbox != null)
                {
                    ViewOptionCheckBox? checkboxView = __instance.View.TryCast<ViewOptionCheckBox>();
                    if (checkboxView != null)
                    {
                        checkboxView.isOn = checkbox.GetValue();
                        bool locked = checkbox.IsLocked?.Invoke() ?? false;
                        checkboxView.visual.alpha = locked ? 0.4f : 1f;
                    }
                    return;
                }

                var slider = FindSlider(__instance.Element.Pointer);
                if (slider != null)
                {
                    ViewOptionSlider? sliderView = __instance.View.TryCast<ViewOptionSlider>();
                    if (sliderView != null)
                    {
                        sliderView.prct = slider.Value;
                    }
                    return;
                }

                var dropdown = FindDropdown(__instance.Element.Pointer);
                if (dropdown != null)
                {
                    ViewOptionDropDown? dropdownView = __instance.View.TryCast<ViewOptionDropDown>();
                    if (dropdownView != null)
                    {
                        // Обязательно сбрасываем modelBase — иначе refresh() в самом
                        // ViewOptionDropDown проигнорирует .value и возьмёт modelBase.toString()
                        dropdownView.modelBase = null;

                        int index = Mathf.Clamp(dropdown.GetSelectedIndex(), 0, dropdown.Options.Count - 1);
                        dropdownView.value = dropdown.Options[index];

                        bool locked = dropdown.IsLocked?.Invoke() ?? false;
                        dropdownView.visual.alpha = locked ? 0.4f : 1f;
                    }
                    return;
                }
            }
        }

        [HarmonyPatch(typeof(UI_Options_Settings), "OnChangeSetting")]
        private static class Patch_UI_Options_Settings_OnChangeSetting
        {
            static void Postfix(IMetaSettingsElement element, int mod)
            {
                if (element == null)
                    return;

                var checkbox = FindCheckbox(element.Pointer);
                if (checkbox != null && mod == 0)
                {
                    float currentTime = Time.realtimeSinceStartup;
                    if (currentTime - _lastToggleTime < 0.1f) return;
                    _lastToggleTime = currentTime;

                    if (checkbox.IsLocked?.Invoke() ?? false)
                        return;

                    bool newValue = !checkbox.GetValue();
                    checkbox.OnChanged?.Invoke(newValue);
                    return;
                }

                var button = FindButton(element.Pointer);
                if (button != null && mod == 0)
                {
                    float currentTime = Time.realtimeSinceStartup;
                    if (currentTime - _lastToggleTime < 0.1f) return;
                    _lastToggleTime = currentTime;

                    button.OnPressed?.Invoke();
                    return;
                }

                var slider = FindSlider(element.Pointer);
                if (slider != null && mod != 0)
                {
                    float step = 0.1f;
                    slider.Value = Mathf.Clamp01(slider.Value + (mod * step));
                    slider.OnChanged?.Invoke(slider.Value);
                    return;
                }

                var dropdown = FindDropdown(element.Pointer);
                if (dropdown != null && mod != 0)
                {
                    float currentTime = Time.realtimeSinceStartup;
                    if (currentTime - _lastToggleTime < 0.1f) return;
                    _lastToggleTime = currentTime;

                    if (dropdown.IsLocked?.Invoke() ?? false)
                        return;

                    int count = dropdown.Options.Count;
                    int newIndex = ((dropdown.GetSelectedIndex() + mod) % count + count) % count; // циклично, без отрицательного остатка

                    dropdown.OnChanged?.Invoke(newIndex);
                    return;
                }
            }
        }

        // ==== ПАТЧИ-ПЕРЕХВАТЧИКИ ДЛЯ ЛОКАЛИЗАЦИИ ====

        [HarmonyPatch(typeof(ModelLanguage), "getText")]
        private static class Patch_ModelLanguage_GetText
        {
            static bool Prefix(int id, ref string __result)
            {
                if (id >= 15000 && _customLocales.TryGetValue(id, out string? text) && text != null)
                {
                    __result = text;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(TextFactory), "getMsg")]
        private static class Patch_TextFactory_GetMsg
        {
            static bool Prefix(int id, ref string __result)
            {
                if (id >= 15000 && _customLocales.TryGetValue(id, out string? text) && text != null)
                {
                    __result = text;
                    return false;
                }
                return true;
            }
        }
    }
}
