using UnityEngine;

namespace jsb_new
{
    public static class SettingsMenu
    {
        public static bool IsOpen = false;
        private static KeyCode ToggleKey = KeyCode.K;

        // Увеличенные габариты окна под крупный шрифт
        private static Rect _windowRect = new Rect(100f, 80f, 640f, 720f);
        private static Vector2 _scrollPos = Vector2.zero;
        private static int _activeTabIndex = 0;

        private static bool _stylesReady = false;
        private static GUIStyle? _windowStyle, _headerStyle, _labelStyle, _checkboxStyle, _sliderStyle, _sliderThumbStyle, _tabButtonStyle, _activeTabButtonStyle, _actionButtonStyle, _lockedStyle, _cardHeaderStyle, _spacerStyle;
        private static Texture2D? _bgTexture, _tabBgTexture, _activeTabBgTexture, _buttonBgTexture, _sliderBarTexture, _sliderThumbTexture;

        private static readonly Dictionary<string, float> _sliderCache = new();

        public class MenuTab
        {
            public string Title;
            public List<MenuGroup> Groups = new();
            public MenuTab(string title) => Title = title;
        }

        public class MenuGroup
        {
            public string Header;
            public List<string> ItemNames = new();
            public MenuGroup(string header, params string[] items)
            {
                Header = header;
                ItemNames.AddRange(items);
            }
        }

        private static readonly List<MenuTab> MenuLayout = new()
        {
            new MenuTab("Визуал")
            {
                Groups = {
                    new MenuGroup("Персонаж", "Full Power Trail", "Custom Player Color", "Player RGB Mode", "Player Color"),
                    new MenuGroup("Враги", "Custom Enemy Color", "Enemy RGB Mode", "Enemy Color", "Tornado / Spin Mode", "Spin Speed"),
                    new MenuGroup("Фон и Сетка Deltarune", "Deltarune Grid BG", "DG Cell Size", "DG Line Thickness", "DG Speed X1", "DG Speed Y1", "DG Color R", "DG Color G", "DG Color B", "DG Alpha 1"),
                    new MenuGroup("Анимация Стен", "Restore Small Construction Squares", "Construction Square Size")
                }
            },
            new MenuTab("Геймплей")
            {
                Groups = {
                    new MenuGroup("Модификаторы Движения", "Mouse Control", "Noclip", "Auto Dash", "Dash Cooldown"),
                    new MenuGroup("Режимы Душ Undertale", "Orange SOUL Mode", "Purple SOUL Mode"),
                    new MenuGroup("Режимы Сложности", "One-Hit Mode", "True One-Hit (Beta)", "Flashlight", "Flashlight Size")
                }
            },
            new MenuTab("Графика и Звук")
            {
                Groups = {
                    new MenuGroup("Графика и Элементы", "Hitboxes", "Always black BG", "Hide Timeline", "FPS Unlock (400 FPS)"),
                    new MenuGroup("Аудио", "Enable Audio Visualizer")
                }
            }
        };

        public static void Initialize()
        {
            DebugStrings.Log("[SettingsMenu] Metro Menu ready. Press K to open.");
        }

        public static void Update()
        {
            if (Input.GetKeyDown(ToggleKey)) IsOpen = !IsOpen;
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

            _windowRect = GUI.Window(69420, _windowRect, (GUI.WindowFunction)DrawWindow, "", _windowStyle);
        }

        private static void CustomSpace(float height)
        {
            if (_spacerStyle == null)
            {
                _spacerStyle = new GUIStyle();
                _spacerStyle.fixedHeight = 0f;
                _spacerStyle.stretchHeight = false;
            }
            GUILayout.Label("", _spacerStyle, GUILayout.Height(height));
        }

        private static void DrawWindow(int id)
        {
            // Шапка
            GUILayout.BeginHorizontal();
            GUILayout.Label("JSAB EXTRA STUFF", _headerStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("[K]", _labelStyle);
            GUILayout.EndHorizontal();

            CustomSpace(10f);

            // Вкладки
            GUILayout.BeginHorizontal();
            for (int i = 0; i < MenuLayout.Count; i++)
            {
                bool isActive = (i == _activeTabIndex);
                GUIStyle style = isActive ? _activeTabButtonStyle! : _tabButtonStyle!;

                if (GUILayout.Button(MenuLayout[i].Title, style, GUILayout.Height(34f)))
                {
                    _activeTabIndex = i;
                }
            }
            GUILayout.EndHorizontal();

            CustomSpace(12f);

            // Контент
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, true);
            var currentTab = MenuLayout[_activeTabIndex];

            foreach (var group in currentTab.Groups)
            {
                CustomSpace(6f);
                GUILayout.Label(group.Header.ToUpper(), _cardHeaderStyle);
                CustomSpace(4f);

                foreach (var itemName in group.ItemNames)
                {
                    DrawItem(itemName);
                }
                CustomSpace(10f);
            }

            GUILayout.EndScrollView();
            CustomSpace(10f);

            // Нижние кнопки
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("СБРОСИТЬ ВСЁ", _actionButtonStyle, GUILayout.Height(36f)))
            {
                ResetAllSettings();
            }
            CustomSpace(8f);
            if (GUILayout.Button("ЗАКРЫТЬ", _actionButtonStyle, GUILayout.Height(36f)))
            {
                IsOpen = false;
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 40f));
        }

        private static void DrawItem(string name)
        {
            var button = ModuleRegistry.Buttons.Find(b => b.Name == name);
            if (button != null) { DrawButton(button); return; }

            var checkbox = ModuleRegistry.Checkboxes.Find(c => c.Name == name);
            if (checkbox != null) { DrawCheckbox(checkbox); return; }

            var slider = ModuleRegistry.Sliders.Find(s => s.Name == name);
            if (slider != null) { DrawSlider(slider); return; }

            GUILayout.Label($"  {name} (не найдено)", _lockedStyle);
        }

        private static void DrawButton(ModuleRegistry.ButtonDef btn)
        {
            if (GUILayout.Button(btn.Name, _actionButtonStyle, GUILayout.Height(32f)))
            {
                btn.OnClick?.Invoke();
            }
            CustomSpace(4f);
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
                if (newVal != current) cb.Setter(newVal);
            }
            GUILayout.EndHorizontal();
            CustomSpace(2f);
        }

        private static void DrawSlider(ModuleRegistry.SliderDef sl)
        {
            if (!_sliderCache.ContainsKey(sl.Name))
                _sliderCache[sl.Name] = sl.DefaultValue;

            float val = _sliderCache[sl.Name];

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{sl.Name}: {val:0.00}", _labelStyle, GUILayout.Width(260f));
            float newVal = GUILayout.HorizontalSlider(val, sl.MinValue, sl.MaxValue, _sliderStyle, _sliderThumbStyle);

            if (Mathf.Abs(newVal - val) > 0.001f)
            {
                _sliderCache[sl.Name] = newVal;
                sl.OnChanged(newVal);
            }

            GUILayout.EndHorizontal();
            CustomSpace(4f);
        }

        private static void ResetAllSettings()
        {
            foreach (var cb in ModuleRegistry.Checkboxes)
            {
                cb.Setter(cb.DefaultValue);
            }

            foreach (var sl in ModuleRegistry.Sliders)
            {
                _sliderCache[sl.Name] = sl.DefaultValue;
                sl.OnChanged(sl.DefaultValue);
            }

            HUDManager.CreateToast("ALL SETTINGS RESET", Color.yellow, 2f);
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
            return new RectOffset { left = left, right = right, top = top, bottom = bottom };
        }

        private static void EnsureStyles()
        {
            if (_stylesReady) return;

            Color bgCol = new Color(0.08f, 0.08f, 0.10f, 0.96f);
            Color tabBg = new Color(0.13f, 0.13f, 0.16f, 1f);
            Color activeTabBg = new Color(0.0f, 0.47f, 0.84f, 1f);
            Color buttonBg = new Color(0.16f, 0.16f, 0.20f, 1f);

            _bgTexture = MakeTex(2, 2, bgCol);
            _tabBgTexture = MakeTex(2, 2, tabBg);
            _activeTabBgTexture = MakeTex(2, 2, activeTabBg);
            _buttonBgTexture = MakeTex(2, 2, buttonBg);
            _sliderBarTexture = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.25f, 1f));
            _sliderThumbTexture = MakeTex(2, 2, activeTabBg);

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _bgTexture;
            _windowStyle.onNormal.background = _bgTexture;
            _windowStyle.padding = MakeOffset(14, 14, 14, 14);

            // Крупный заголовок окна
            _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _headerStyle.normal.textColor = new Color(0.0f, 0.6f, 1f);

            // Крупный заголовок карточки/группы
            _cardHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _cardHeaderStyle.normal.textColor = new Color(0.0f, 0.7f, 0.9f);

            // Текст надписей и слайдеров
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            _lockedStyle = new GUIStyle(_labelStyle);
            _lockedStyle.normal.textColor = new Color(0.4f, 0.4f, 0.4f);

            // Крупный чекбокс
            _checkboxStyle = new GUIStyle(GUI.skin.toggle) { fontSize = 14 };
            _checkboxStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            _checkboxStyle.onNormal.textColor = new Color(0.2f, 0.9f, 0.4f);
            _checkboxStyle.padding = MakeOffset(24, 0, 0, 0);

            // Вкладки
            _tabButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
            _tabButtonStyle.normal.background = _tabBgTexture;
            _tabButtonStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            _activeTabButtonStyle = new GUIStyle(_tabButtonStyle);
            _activeTabButtonStyle.normal.background = _activeTabBgTexture;
            _activeTabButtonStyle.normal.textColor = Color.white;

            // Кнопки
            _actionButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
            _actionButtonStyle.normal.background = _buttonBgTexture;
            _actionButtonStyle.normal.textColor = Color.white;

            // Слайдеры
            _sliderStyle = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 8f };
            _sliderStyle.normal.background = _sliderBarTexture;

            _sliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = 12f, fixedHeight = 18f };
            _sliderThumbStyle.normal.background = _sliderThumbTexture;

            _spacerStyle = new GUIStyle();
            _spacerStyle.fixedHeight = 0f;
            _spacerStyle.stretchHeight = false;

            _stylesReady = true;
        }
    }
}
