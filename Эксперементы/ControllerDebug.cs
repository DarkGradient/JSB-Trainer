using Il2Cpp;
using Il2CppRewired;
using Il2CppRewired.ControllerExtensions;
using System;
using UnityEngine;

namespace jsb_new
{
    public static class ControllerLedModule
    {
        // === Настройки LED ===
        public static bool LedEnabled { get; set; } = true;
        public static bool BreatheEffect { get; set; } = false;
        public static float BreatheSpeed { get; set; } = 0.5f;
        public static bool RainbowEffect { get; set; } = false;
        public static float RainbowSpeed { get; set; } = 0.3f;
        public static bool FlashEffect { get; set; } = false;
        public static float FlashSpeed { get; set; } = 0.5f;
        public static bool PlayerSync { get; set; } = false;
        public static float Red { get; set; } = 1f;
        public static float Green { get; set; } = 0f;
        public static float Blue { get; set; } = 0f;

        // === Настройки Вибрации (Vibration Debug) ===
        public static bool VibEnabled { get; set; } = false;
        public static float VibLeft { get; set; } = 0.5f;   // Тяжелый низкочастотный мотор (Левый)
        public static float VibRight { get; set; } = 0.5f;  // Легкий высокочастотный мотор (Правый)
        public static bool VibPulse { get; set; } = false;  // Пульсирующая вибрация (Импульсы)
        public static float VibPulseSpeed { get; set; } = 0.5f;

        private static bool _lastVibState = false;

        public static void Initialize()
        {
            // --- LED Меню ---
            ModuleRegistry.RegisterCheckbox("Enable Controller LED", () => LedEnabled, v => LedEnabled = v);
            ModuleRegistry.RegisterCheckbox("LED: Sync Player Color", () => PlayerSync, v => PlayerSync = v);
            ModuleRegistry.RegisterCheckbox("LED Effect: Breathe", () => BreatheEffect, v => BreatheEffect = v);
            ModuleRegistry.RegisterSlider("LED Breathe Speed", 0.5f, v => BreatheSpeed = v);
            ModuleRegistry.RegisterCheckbox("LED Effect: Rainbow", () => RainbowEffect, v => RainbowEffect = v);
            ModuleRegistry.RegisterSlider("LED Rainbow Speed", 0.3f, v => RainbowSpeed = v);
            ModuleRegistry.RegisterCheckbox("LED Effect: Flash", () => FlashEffect, v => FlashEffect = v);
            ModuleRegistry.RegisterSlider("LED Flash Speed", 0.5f, v => FlashSpeed = v);
            ModuleRegistry.RegisterSlider("LED Red", 1f, v => Red = v);
            ModuleRegistry.RegisterSlider("LED Green", 0f, v => Green = v);
            ModuleRegistry.RegisterSlider("LED Blue", 0f, v => Blue = v);

            // --- Vibration Debug Меню ---
            ModuleRegistry.RegisterCheckbox("Vib Test: Enable", () => VibEnabled, v => VibEnabled = v);
            ModuleRegistry.RegisterSlider("Vib Left (Heavy Motor)", 0.5f, v => VibLeft = v);
            ModuleRegistry.RegisterSlider("Vib Right (Light Motor)", 0.5f, v => VibRight = v);
            ModuleRegistry.RegisterCheckbox("Vib Effect: Pulse", () => VibPulse, v => VibPulse = v);
            ModuleRegistry.RegisterSlider("Vib Pulse Speed", 0.5f, v => VibPulseSpeed = v);
        }

        public static void Update()
        {
            if (!ReInput.isReady || ReInput.controllers == null) return;

            int joystickCount = ReInput.controllers.joystickCount;
            if (joystickCount == 0) return;

            for (int i = 0; i < joystickCount; i++)
            {
                var joystick = ReInput.controllers.Joysticks[i];
                if (joystick == null) continue;

                // 1. Управление LED
                if (LedEnabled)
                {
                    var ds4 = joystick.GetExtension<IDualShock4Extension>();
                    if (ds4 != null)
                    {
                        Color finalColor = CalculateColor(joystick.id);
                        ds4.SetLightColor(finalColor.r, finalColor.g, finalColor.b);
                    }
                }

                // 2. Управление Вибрацией
                HandleVibration(joystick);
            }
        }

        private static void HandleVibration(Joystick joystick)
        {
            if (!joystick.supportsVibration) return;

            if (VibEnabled)
            {
                _lastVibState = true;

                float left = VibLeft;
                float right = VibRight;

                // Импульсный/Пульсирующий эффект вибрации
                if (VibPulse)
                {
                    float interval = Mathf.Lerp(0.5f, 0.05f, VibPulseSpeed);
                    bool isOn = (Time.time % (interval * 2f)) < interval;
                    if (!isOn)
                    {
                        left = 0f;
                        right = 0f;
                    }
                }

                // Передаем силу на тяжелый (0) и легкий (1) моторы
                joystick.SetVibration(left, right);
            }
            else if (_lastVibState)
            {
                // Гасим вибрацию при выключении тумблера
                joystick.StopVibration();
                _lastVibState = false;
            }
        }

        private static Color CalculateColor(int controllerId)
        {
            if (FlashEffect)
            {
                float interval = Mathf.Lerp(0.5f, 0.05f, FlashSpeed);
                bool isOn = (Time.time % (interval * 2f)) < interval;
                if (!isOn) return Color.black;
            }

            Color baseColor = new Color(Red, Green, Blue);

            if (PlayerSync && PlayerManager.instance != null)
            {
                baseColor = GetPlayerColorByController(controllerId);
            }
            else if (RainbowEffect)
            {
                float speed = Mathf.Max(0.05f, RainbowSpeed * 2f);
                float hue = (Time.time * speed) % 1f;
                baseColor = Color.HSVToRGB(hue, 1f, 1f);
            }

            if (BreatheEffect)
            {
                float speed = Mathf.Max(0.5f, BreatheSpeed * 5f);
                float factor = (Mathf.Sin(Time.time * speed) + 1f) / 2f;
                baseColor *= factor;
            }

            return baseColor;
        }

        private static Color GetPlayerColorByController(int controllerId)
        {
            var metaPlayer = PlayerManager.instance.GetPlayerByControllerId(controllerId);
            if (metaPlayer != null && metaPlayer.modelPlayer != null)
            {
                switch (metaPlayer.modelPlayer.playerId)
                {
                    case 0: return new Color(0f, 0.8f, 1f);   // P1: Cyan
                    case 1: return new Color(1f, 0.9f, 0f);   // P2: Yellow
                    case 2: return new Color(1f, 0.1f, 0.5f);  // P3: Pink
                    case 3: return new Color(0.1f, 1f, 0.3f);  // P4: Green
                }
            }

            return new Color(Red, Green, Blue);
        }
    }
}
