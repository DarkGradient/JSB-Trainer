using UnityEngine;

namespace jsb_new
{
    public static class SettingsMenu
    {
        public static bool IsOpen = false;
        private static KeyCode ToggleKey = KeyCode.K;

        private static Rect _windowRect = new Rect(100f, 100f, 480f, 540f);
        private static Vector2 _scrollPos = Vector2.zero;
        private static int _activeTabIndex = 0;

        private static bool _stylesReady = false;

        // Metro Styles
        private static GUIStyle? _windowStyle;
        private static GUIStyle? _headerStyle;
        private static GUIStyle? _labelStyle;
        private static GUIStyle? _checkboxStyle;
        private static GUIStyle? _sliderStyle;
        private static GUIStyle? _sliderThumbStyle;
        private static GUIStyle? _tabButtonStyle;
        private static GUIStyle? _activeTabButtonStyle;
        private static GUIStyle? _actionButtonStyle;
        private static GUIStyle? _lockedStyle;
        private static GUIStyle? _spacerStyle;

        private static Texture2D? _bgTexture;
        private static Texture2D? _tabBgTexture;
        private static Texture2D? _activeTabBgTexture;
        private static Texture2D? _buttonBgTexture;
        private static Texture2D? _sliderBarTexture;
        private static Texture2D? _sliderThumbTexture;

        private static readonly Dictionary<string, float> _sliderCache = new();

        public static void Initialize()
        {
            DebugStrings.Log("[SettingsMenu] Metro IMGUI menu ready. Press K to open.");
        }

        public static void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                IsOpen = !IsOpen;
            }

            if (IsOpen)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        public static void OnGUI()
        {
            if (!IsOpen) return;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            EnsureStyles();

            _windowRect = GUI.Window(
                69420,
                _windowRect,
                (GUI.WindowFunction)DrawWindow,
                                     "",
                                     _windowStyle
            );
        }

        // Замена отстрипанного GUILayout.Space
        private static void CustomSpace(float height)
        {
            GUILayout.Label("", _spacerStyle, GUILayout.Height(height));
        }

        private static void DrawWindow(int id)
        {
            // --- ТИТУЛЬНИК ---
            GUILayout.BeginHorizontal();
            GUILayout.Label("JSAB EXTRA STUFF", _headerStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("[K]", _labelStyle);
            GUILayout.EndHorizontal();

            CustomSpace(8f);

            // --- ВЫТАСКИВАЕМ ВКЛАДКИ ИЗ MENULAYOUT ---
            var tabs = GetParsedTabs();

            if (tabs.Count > 0)
            {
                if (_activeTabIndex >= tabs.Count) _activeTabIndex = 0;

                // --- ШАПКА ВКЛАДОК ---
                GUILayout.BeginHorizontal();
                for (int i = 0; i < tabs.Count; i++)
                {
                    bool isActive = (i == _activeTabIndex);
                    GUIStyle style = isActive ? _activeTabButtonStyle! : _tabButtonStyle!;

                    if (GUILayout.Button(tabs[i].Name, style, GUILayout.Height(28f)))
                    {
                        _activeTabIndex = i;
                    }
                }
                GUILayout.EndHorizontal();

                CustomSpace(8f);

                // --- КОНТЕНТ АКТИВНОЙ ВКЛАДКИ ---
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, true);

                foreach (var entry in tabs[_activeTabIndex].Entries)
                {
                    switch (entry)
                    {
                        case MenuLayout.HeaderEntry h:
                            CustomSpace(6f);
                            GUILayout.Label(h.Text.ToUpper(), _headerStyle);
                            CustomSpace(2f);
                            break;

                        case MenuLayout.SpacerEntry:
                            CustomSpace(8f);
                            break;

                        case MenuLayout.ItemEntry item:
                            DrawItem(item.Name);
                            break;
                    }
                }

                GUILayout.EndScrollView();
            }

            CustomSpace(8f);

            // --- НИЖНЯЯ ПАНЕЛЬ С КНОПКАМИ ---
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("СБРОСИТЬ ВСЁ", _actionButtonStyle, GUILayout.Height(28f)))
            {
                ResetAllSettings();
            }

            CustomSpace(6f);

            if (GUILayout.Button("ЗАКРЫТЬ", _actionButtonStyle, GUILayout.Height(28f)))
            {
                IsOpen = false;
            }

            GUILayout.EndHorizontal();

            // Перетаскивание
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 35f));
        }

        private static void DrawItem(string name)
        {
            // 1. Сначала проверяем, не кнопка ли это
            var button = ModuleRegistry.Buttons.Find(b => b.Name == name);
            if (button != null)
            {
                DrawButton(button);
                return;
            }

            // 2. Чекбокс
            var checkbox = ModuleRegistry.Checkboxes.Find(c => c.Name == name);
            if (checkbox != null)
            {
                DrawCheckbox(checkbox);
                return;
            }

            // 3. Слайдер
            var slider = ModuleRegistry.Sliders.Find(s => s.Name == name);
            if (slider != null)
            {
                DrawSlider(slider);
                return;
            }

            GUILayout.Label($"  {name} (не найдено)", _lockedStyle);
        }

        private static void DrawButton(ModuleRegistry.ButtonDef btn)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(btn.Name, _actionButtonStyle, GUILayout.Height(26f)))
            {
                btn.OnClick?.Invoke();
            }
            GUILayout.EndHorizontal();
            CustomSpace(2f);
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
            GUILayout.Label($"{sl.Name}: {val:0.00}", _labelStyle, GUILayout.Width(200f));

            // Берем реальные минимальные и максимальные границы из объекта SliderDef!
            float newVal = GUILayout.HorizontalSlider(val, sl.MinValue, sl.MaxValue, _sliderStyle, _sliderThumbStyle);

            if (Mathf.Abs(newVal - val) > 0.001f)
            {
                _sliderCache[sl.Name] = newVal;
                sl.OnChanged(newVal);
            }

            GUILayout.EndHorizontal();
            CustomSpace(2f);
        }

        private static void ResetAllSettings()
        {
            foreach (var cb in ModuleRegistry.Checkboxes)
            {
                cb.Setter(false);
            }

            foreach (var sl in ModuleRegistry.Sliders)
            {
                _sliderCache[sl.Name] = sl.DefaultValue;
                sl.OnChanged(sl.DefaultValue);
            }

            HUDManager.CreateToast("ALL SETTINGS RESET", Color.yellow, 2f);
        }

        private class TabData
        {
            public string Name = "General";
            public List<MenuLayout.Entry> Entries = new();
        }

        private static List<TabData> GetParsedTabs()
        {
            var result = new List<TabData>();
            TabData currentTab = new TabData { Name = "Main" };

            foreach (var entry in MenuLayout.GetEntries())
            {
                if (entry is MenuLayout.TabEntry tab)
                {
                    if (currentTab.Entries.Count > 0 || result.Count == 0)
                    {
                        if (currentTab.Entries.Count > 0) result.Add(currentTab);
                    }
                    currentTab = new TabData { Name = tab.Name };
                }
                else
                {
                    currentTab.Entries.Add(entry);
                }
            }

            if (currentTab.Entries.Count > 0 && !result.Contains(currentTab))
            {
                result.Add(currentTab);
            }

            return result;
        }

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private static RectOffset MakeOffset(int left, int right, int top, int bottom)
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

            Color bgCol = new Color(0.09f, 0.09f, 0.11f, 0.95f);
            Color tabBg = new Color(0.14f, 0.14f, 0.17f, 1f);
            Color activeTabBg = new Color(0.0f, 0.47f, 0.84f, 1f);
            Color buttonBg = new Color(0.18f, 0.18f, 0.22f, 1f);
            Color sliderBarCol = new Color(0.2f, 0.2f, 0.25f, 1f);

            _bgTexture = MakeTex(2, 2, bgCol);
            _tabBgTexture = MakeTex(2, 2, tabBg);
            _activeTabBgTexture = MakeTex(2, 2, activeTabBg);
            _buttonBgTexture = MakeTex(2, 2, buttonBg);
            _sliderBarTexture = MakeTex(2, 2, sliderBarCol);
            _sliderThumbTexture = MakeTex(2, 2, activeTabBg);

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _bgTexture;
            _windowStyle.onNormal.background = _bgTexture;
            _windowStyle.padding = MakeOffset(10, 10, 10, 10);
            _windowStyle.border = MakeOffset(0, 0, 0, 0);

            _headerStyle = new GUIStyle(GUI.skin.label);
            _headerStyle.fontSize = 12;
            _headerStyle.fontStyle = FontStyle.Bold;
            _headerStyle.normal.textColor = new Color(0.0f, 0.6f, 1f);

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 12;
            _labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            _lockedStyle = new GUIStyle(_labelStyle);
            _lockedStyle.normal.textColor = new Color(0.4f, 0.4f, 0.4f);

            _checkboxStyle = new GUIStyle(GUI.skin.toggle);
            _checkboxStyle.fontSize = 12;
            _checkboxStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            _checkboxStyle.onNormal.textColor = new Color(0.2f, 0.9f, 0.4f);
            _checkboxStyle.padding = MakeOffset(18, 0, 0, 0);

            _tabButtonStyle = new GUIStyle(GUI.skin.button);
            _tabButtonStyle.normal.background = _tabBgTexture;
            _tabButtonStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            _tabButtonStyle.fontSize = 11;
            _tabButtonStyle.fontStyle = FontStyle.Bold;
            _tabButtonStyle.margin = MakeOffset(1, 1, 0, 0);

            _activeTabButtonStyle = new GUIStyle(_tabButtonStyle);
            _activeTabButtonStyle.normal.background = _activeTabBgTexture;
            _activeTabButtonStyle.normal.textColor = Color.white;

            _actionButtonStyle = new GUIStyle(GUI.skin.button);
            _actionButtonStyle.normal.background = _buttonBgTexture;
            _actionButtonStyle.normal.textColor = Color.white;
            _actionButtonStyle.fontSize = 11;
            _actionButtonStyle.fontStyle = FontStyle.Bold;

            _sliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
            _sliderStyle.normal.background = _sliderBarTexture;
            _sliderStyle.fixedHeight = 8f;
            _sliderStyle.margin = MakeOffset(0, 0, 6, 0);

            _sliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb);
            _sliderThumbStyle.normal.background = _sliderThumbTexture;
            _sliderThumbStyle.fixedWidth = 8f;
            _sliderThumbStyle.fixedHeight = 14f;

            _spacerStyle = new GUIStyle();
            _spacerStyle.fixedHeight = 0f;
            _spacerStyle.stretchHeight = false;

            _stylesReady = true;
        }
    }
}
