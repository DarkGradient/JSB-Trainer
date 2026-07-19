using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;

namespace jsb_new
{
    public static class DebugSettingsBuilder
    {
        private static bool _testCheckboxValue = false;
        private static float _testSliderValue = 0.5f;
        private static int _testDropdownIndex = 0;
        private static readonly List<string> _testDropdownOptions = new List<string> { "Cyan", "Orange", "Yellow", "Green" };

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            DebugStrings.Log("DebugSettings: initializing test settings set...");

            TrainerSettingsBuilder.Initialize(harmony);

            TrainerSettingsBuilder.AddSpacer();
            TrainerSettingsBuilder.AddHeader("SETTINGS BUILDER TEST");
            TrainerSettingsBuilder.AddSpacer();

            TrainerSettingsBuilder.AddCheckbox(
                "Test Toggle Checkbox",
                () => _testCheckboxValue,
                                               (newValue) =>
                                               {
                                                   _testCheckboxValue = newValue;
                                                   DebugStrings.Log($"DebugSettings checkbox changed to: {newValue}");
                                               }
            );

            TrainerSettingsBuilder.AddButton(
                "Test Click Button",
                () => DebugStrings.Log("DebugSettings button pressed")
            );

            TrainerSettingsBuilder.AddSlider(
                "Test Range Slider",
                _testSliderValue,
                (newValue) =>
                {
                    _testSliderValue = newValue;
                    DebugStrings.Log($"DebugSettings slider changed to: {Math.Round(newValue * 100f)}%");
                }
            );

            TrainerSettingsBuilder.AddDropdown(
                "Test Color Selector",
                _testDropdownOptions,
                () => _testDropdownIndex,
                                               (newIndex) =>
                                               {
                                                   _testDropdownIndex = newIndex;
                                                   DebugStrings.Log($"DebugSettings dropdown selected option {newIndex}: {_testDropdownOptions[newIndex]}");
                                               }
            );

            TrainerSettingsBuilder.AddSpacer();
            DebugStrings.Log("DebugSettings: all test elements registered");
        }
    }
}
