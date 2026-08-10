// using System.Collections.Generic;
using UnityEngine;

namespace jsb_new
{
    /// <summary>
    /// Чистый IMGUI-меню под Il2Cpp. Без stripped-методов.
    /// Открывается/закрывается по K.
    /// </summary>
    public static class SettingsMenu
    {
        public static bool IsOpen = false;

        private static KeyCode ToggleKey = KeyCode.K;

        private static Rect _windowRect = new Rect(80f, 80f, 420f, 620f);
        private static Vector2 _scrollPos = Vector2.zero;
        private static bool _stylesReady = false;

        private static GUIStyle? _windowStyle;
        private static GUIStyle? _headerStyle;
        private static GUIStyle? _labelStyle;
        private static GUIStyle? _checkboxStyle;
        private static GUIStyle? _sliderStyle;
        private static GUIStyle? _sliderThumbStyle;
        private static GUIStyle? _buttonStyle;
        private static GUIStyle? _lockedStyle;
        private static GUIStyle? _spacerStyle;

        private static readonly Dictionary<string, float> _sliderCache = new();

        public static void Initialize()
        {
            DebugStrings.Log("[SettingsMenu] IMGUI menu ready. Press K to open.");
        }

        public static void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                IsOpen = !IsOpen;
            }

            // Пока меню открыто — забираем курсор у игры
            if (IsOpen)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        public static void OnGUI()
        {
            if (!IsOpen) return;

            // На всякий случай ещё раз заставляем курсор быть видимым
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            EnsureStyles();

            _windowRect = GUI.Window(
                69420,
                _windowRect,
                (GUI.WindowFunction)DrawWindow,
                                     "JSAB Extra Stuff  |  K — закрыть",
                                     _windowStyle
            );
        }

        private static void DrawWindow(int id)
        {
            // Вместо GUILayout.Space используем пустой Label с высотой
            GUILayout.Label("", _spacerStyle, GUILayout.Height(6f));

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, true);

            foreach (var entry in MenuLayout.GetEntries())
            {
                switch (entry)
                {
                    case MenuLayout.HeaderEntry h:
                        DrawHeader(h.Text);
                        break;

                    case MenuLayout.SpacerEntry:
                        GUILayout.Label("", _spacerStyle, GUILayout.Height(12f));
                        break;

                    case MenuLayout.ItemEntry item:
                        DrawItem(item.Name);
                        break;
                }
            }

            GUILayout.Label("", _spacerStyle, GUILayout.Height(10f));
            GUILayout.EndScrollView();

            if (GUILayout.Button("Закрыть (K)", _buttonStyle, GUILayout.Height(28f)))
            {
                IsOpen = false;
            }

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 28f));
        }

        private static void DrawHeader(string text)
        {
            GUILayout.Label("", _spacerStyle, GUILayout.Height(8f));
            GUILayout.Label(text, _headerStyle);
            GUILayout.Label("", _spacerStyle, GUILayout.Height(2f));
        }

        private static void DrawItem(string name)
        {
            var checkbox = ModuleRegistry.Checkboxes.Find(c => c.Name == name);
            if (checkbox != null)
            {
                DrawCheckbox(checkbox);
                return;
            }

            var slider = ModuleRegistry.Sliders.Find(s => s.Name == name);
            if (slider != null)
            {
                DrawSlider(slider);
                return;
            }

            GUILayout.Label($"  {name}  (не найдено)", _lockedStyle);
        }

        private static void DrawCheckbox(ModuleRegistry.CheckboxDef cb)
        {
            bool locked = cb.IsLocked != null && cb.IsLocked();
            bool current = cb.Getter();

            GUILayout.BeginHorizontal();

            if (locked)
            {
                GUI.enabled = false;
                GUILayout.Toggle(current, "  " + cb.Name, _checkboxStyle);
                GUI.enabled = true;
            }
            else
            {
                bool newVal = GUILayout.Toggle(current, "  " + cb.Name, _checkboxStyle);
                if (newVal != current)
                {
                    cb.Setter(newVal);
                }
            }

            GUILayout.EndHorizontal();
        }

        private static void DrawSlider(ModuleRegistry.SliderDef sl)
        {
            if (!_sliderCache.ContainsKey(sl.Name))
                _sliderCache[sl.Name] = sl.DefaultValue;

            float val = _sliderCache[sl.Name];

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{sl.Name}: {val:0.00}", _labelStyle, GUILayout.Width(210f));

            float newVal = GUILayout.HorizontalSlider(val, 0f, 1f, _sliderStyle, _sliderThumbStyle);

            if (Mathf.Abs(newVal - val) > 0.001f)
            {
                _sliderCache[sl.Name] = newVal;
                sl.OnChanged(newVal);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("", _spacerStyle, GUILayout.Height(2f));
        }

        private static RectOffset MakeRectOffset(int left, int right, int top, int bottom)
        {
            var offset = new RectOffset();
            offset.left = left;
            offset.right = right;
            offset.top = top;
            offset.bottom = bottom;
            return offset;
        }

        private static void EnsureStyles()
        {
            if (_stylesReady) return;

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.fontSize = 14;
            _windowStyle.fontStyle = FontStyle.Bold;
            _windowStyle.alignment = TextAnchor.UpperCenter;
            _windowStyle.padding = MakeRectOffset(12, 12, 28, 12);
            _windowStyle.normal.textColor = new Color(0.9f, 0.9f, 0.95f);
            _windowStyle.onNormal.textColor = _windowStyle.normal.textColor;

            _headerStyle = new GUIStyle(GUI.skin.label);
            _headerStyle.fontSize = 15;
            _headerStyle.fontStyle = FontStyle.Bold;
            _headerStyle.alignment = TextAnchor.MiddleLeft;
            _headerStyle.padding = MakeRectOffset(4, 4, 4, 4);
            _headerStyle.normal.textColor = new Color(0.4f, 0.85f, 1f);

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 13;
            _labelStyle.alignment = TextAnchor.MiddleLeft;
            _labelStyle.normal.textColor = Color.white;

            _checkboxStyle = new GUIStyle(GUI.skin.toggle);
            _checkboxStyle.fontSize = 13;
            _checkboxStyle.fontStyle = FontStyle.Normal;
            _checkboxStyle.padding = MakeRectOffset(4, 4, 3, 3);
            _checkboxStyle.margin = MakeRectOffset(0, 0, 1, 1);
            _checkboxStyle.normal.textColor = Color.white;
            _checkboxStyle.onNormal.textColor = new Color(0.5f, 1f, 0.6f);
            _checkboxStyle.hover.textColor = new Color(0.8f, 1f, 0.9f);
            _checkboxStyle.onHover.textColor = new Color(0.6f, 1f, 0.7f);

            _lockedStyle = new GUIStyle(_checkboxStyle);
            _lockedStyle.normal.textColor = new Color(0.45f, 0.45f, 0.45f);
            _lockedStyle.onNormal.textColor = new Color(0.45f, 0.45f, 0.45f);

            _sliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
            _sliderStyle.fixedHeight = 14f;

            _sliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb);
            _sliderThumbStyle.fixedWidth = 14f;
            _sliderThumbStyle.fixedHeight = 14f;

            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.fontSize = 13;
            _buttonStyle.fontStyle = FontStyle.Bold;
            _buttonStyle.fixedHeight = 28f;

            // Пустой стиль для имитации Space
            _spacerStyle = new GUIStyle();
            _spacerStyle.fixedHeight = 0f;
            _spacerStyle.stretchHeight = false;

            _stylesReady = true;
        }
    }
}
