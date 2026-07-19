#pragma warning disable CS8618 // Поля, не допускающие значения NULL, не инициализированы
#pragma warning disable CS8600 // Преобразование null-литерала
#pragma warning disable CS8602 // Разыменование возможной пустой ссылки
#pragma warning disable CS8603 // Возможный возврат null
#pragma warning disable CS8604 // Возможный аргумент-ссылка null

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using Il2CppInterop.Runtime;

namespace jsb_new
{
    // Сетка из 9 позиций экрана
    public enum HUDPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        Center,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    public static class HUDManager
    {
        // Свойство для левой плашки (Level Info)
        public static bool LevelInfoEnabled
        {
            get => ModuleRegistry.IsActive("LevelInfoHUD");
            set => ModuleRegistry.SetActive("LevelInfoHUD", value);
        }

        // --- ДЕКЛАРАЦИЯ ОБЪЕКТА ПЛАШКИ ---
        public class RightPlateDef
        {
            public string Key = "";
            public Func<string> TextGetter = default!;
            public Color BaseColor;
            public Color PulseColor;
            public Func<bool> ActiveGetter = default!;
            public float Height;
            public int Order;
            public int FontSize;
            public HUDPosition Position; // <--- Позиция плашки на экране
            public float CurrentAlpha = 0f;
        }

        // Список всех зарегистрированных плашек
        public static readonly List<RightPlateDef> RightPlates = new List<RightPlateDef>();

        // Метод регистрации плашки из любого внешнего файла мода
        public static void CreateHUD(string key, Func<string> textGetter, Color baseColor, Color pulseColor, Func<bool> activeGetter, float height = 35f, int order = 0, int fontSize = 18, HUDPosition position = HUDPosition.TopRight)
        {
            RightPlates.Add(new RightPlateDef
            {
                Key = key,
                TextGetter = textGetter,
                BaseColor = baseColor,
                PulseColor = pulseColor,
                ActiveGetter = activeGetter,
                Height = height,
                Order = order,
                FontSize = fontSize,
                Position = position, // <--- Записываем позицию
                CurrentAlpha = 0f
            });
        }

        // --- ПЕРЕМЕННЫЕ И СТИЛИ ---
        private static float _levelInfoAlpha = 0f;

        private static GUIStyle? _labelStyle;
        private static GUIStyle? _boxStyle;
        private static GUIStyle? _notifStyle;

        private static string _lastDumpedSongId = "";
        private static AudioSource? _cachedSource;

        // --- СТРУКТУРА ОПОВЕЩЕНИЙ ---
        public struct TrainerNotification
        {
            public string Text;
            public Color TextColor;
            public float Duration;
            public float Elapsed;
            public HUDPosition Position; // <--- Позиция всплывающего тоста
            public float Alpha;          // Плавная прозрачность конкретного тоста
        }

        private static readonly Queue<TrainerNotification> _notificationQueue = new Queue<TrainerNotification>();
        private static readonly List<TrainerNotification> _activeNotifications = new List<TrainerNotification>();

        // --- ДИНАМИЧЕСКИЙ МЕНЕДЖЕР СТЕКОВ ---
        private static readonly Dictionary<HUDPosition, List<string>> _hudStacks = new Dictionary<HUDPosition, List<string>>();

        static HUDManager()
        {
            // Инициализируем стеки для всех 9 позиций экрана
            foreach (HUDPosition pos in Enum.GetValues(typeof(HUDPosition)))
            {
                _hudStacks[pos] = new List<string>();
            }
        }

        private static void ReportHudActive(string name, HUDPosition position, bool isVisible)
        {
            var stack = _hudStacks[position];
            if (isVisible)
            {
                if (!stack.Contains(name))
                {
                    // Сортируем при добавлении на основе приоритета order
                    stack.Add(name);
                    stack.Sort((a, b) => {
                        var pA = RightPlates.Find(p => p.Key == a);
                        var pB = RightPlates.Find(p => p.Key == b);
                        int oA = pA != null ? pA.Order : 0;
                        int oB = pB != null ? pB.Order : 0;
                        return oA.CompareTo(oB);
                    });
                }
            }
            else
            {
                stack.Remove(name);
            }
        }

        // Вычисляет динамическую Y-координату на основе стека конкретной позиции экрана
        public static float GetHudY(string name)
        {
            var plate = RightPlates.Find(p => p.Key == name);
            if (plate == null) return 15f;

            var stack = _hudStacks[plate.Position];
            int index = stack.IndexOf(name);
            if (index == -1) return 15f;

            float pad = 15f;
            float spacing = 10f;

            // 1. Позиции вверху (TopLeft, TopCenter, TopRight) -> Стек растет вниз (+)
            if (plate.Position == HUDPosition.TopLeft || plate.Position == HUDPosition.TopCenter || plate.Position == HUDPosition.TopRight)
            {
                float currentY = pad;
                for (int i = 0; i < index; i++)
                {
                    var prevPlate = RightPlates.Find(p => p.Key == stack[i]);
                    float h = prevPlate != null ? GetPlateSize(prevPlate).y : 35f;
                    currentY += h + spacing;
                }
                return currentY;
            }

            // 2. Позиции внизу (BottomLeft, BottomCenter, BottomRight) -> Стек растет вверх (-)
            if (plate.Position == HUDPosition.BottomLeft || plate.Position == HUDPosition.BottomCenter || plate.Position == HUDPosition.BottomRight)
            {
                float currentY = Screen.height - pad;
                for (int i = 0; i <= index; i++)
                {
                    var prevPlate = RightPlates.Find(p => p.Key == stack[i]);
                    float h = prevPlate != null ? GetPlateSize(prevPlate).y : 35f;
                    currentY -= h;
                    if (i < index) currentY -= spacing;
                }
                return currentY;
            }

            // 3. Позиции посередине (MiddleLeft, Center, MiddleRight) -> Авто-центрирование всего стека по вертикали!
            {
                float totalHeight = 0f;
                for (int i = 0; i < stack.Count; i++)
                {
                    var prevPlate = RightPlates.Find(p => p.Key == stack[i]);
                    totalHeight += prevPlate != null ? GetPlateSize(prevPlate).y : 35f;
                    if (i < stack.Count - 1) totalHeight += spacing;
                }

                float startY = (Screen.height - totalHeight) / 2f;
                float currentY = startY;
                for (int i = 0; i < index; i++)
                {
                    var prevPlate = RightPlates.Find(p => p.Key == stack[i]);
                    float h = prevPlate != null ? GetPlateSize(prevPlate).y : 35f;
                    currentY += h + spacing;
                }
                return currentY;
            }
        }

        // === ЧЕСТНЫЙ МАТЕМАТИЧЕСКИЙ АВТОКЛАЙОУТ (Unity GUI Engine) ===
        private static Vector2 GetPlateSize(RightPlateDef plate)
        {
            string hudText = plate.TextGetter != null ? plate.TextGetter() : "";

            int fontSize = plate.FontSize > 0 ? plate.FontSize : 18;

            GUIStyle measureStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold
            };

            Vector2 textSize = measureStyle.CalcSize(new GUIContent(hudText));

            float paddingX = 25f;
            float paddingY = 10f;

            bool isMultiLine = hudText.Contains("\n");
            float minW = isMultiLine ? (fontSize * 10f) : (fontSize * 8f);
            float minH = isMultiLine ? (fontSize * 3f) : (fontSize * 2f);

            float finalWidth = Mathf.Max(minW, textSize.x + paddingX);
            float finalHeight = Mathf.Max(minH, textSize.y + paddingY);

            return new Vector2(finalWidth, finalHeight);
        }

        // Калькулятор динамического авто-размера для всплывающих баннеров (Toasts) [4]
        private static Vector2 GetNotifSize(string text)
        {
            if (_notifStyle == null)
            {
                _notifStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            Vector2 size = _notifStyle.CalcSize(new GUIContent(text));
            float paddingX = 30f;
            float paddingY = 15f;
            return new Vector2(Mathf.Max(250f, size.x + paddingX), Mathf.Max(40f, size.y + paddingY));
        }

        // --- ИНИЦИАЛИЗАЦИЯ И СЛЕДИТЕЛИ ---
        public static void Initialize()
        {
            ModuleRegistry.RegisterCheckbox(
                category: "Optional Stuff",
                name: "Show Level Info HUD",
                getter: () => LevelInfoEnabled,
                                            setter: (newValue) => { LevelInfoEnabled = newValue; },
                                            isLocked: null,
                                            order: 15
            );

            CreateToast("WELCOME TO JS&B. ALL GOOD.", Color.green, 8.0f);
        }

        // Изменен сигнатура: добавлен необязательный параметр положения тоста на экране (по умолчанию снизу по центру)
        public static void CreateToast(string text, Color color, float duration = 2.5f, HUDPosition position = HUDPosition.BottomCenter)
        {
            _notificationQueue.Enqueue(new TrainerNotification {
                Text = text,
                TextColor = color,
                Duration = duration,
                Elapsed = 0f,
                Position = position,
                Alpha = 0f
            });
        }

        public static void Update()
        {
            // 1. Плавно двигаем альфу у всех зарегистрированных плашек
            foreach (var plate in RightPlates)
            {
                float targetAlpha = (plate.ActiveGetter != null && plate.ActiveGetter()) ? 1f : 0f;
                plate.CurrentAlpha = Mathf.MoveTowards(plate.CurrentAlpha, targetAlpha, Time.unscaledDeltaTime * 4f);

                // Сообщаем в менеджер стека конкретной позиции экрана
                ReportHudActive(plate.Key, plate.Position, plate.CurrentAlpha > 0.001f);
            }

            // 2. Плавная анимация появления левой плашки
            _levelInfoAlpha = Mathf.MoveTowards(_levelInfoAlpha, LevelInfoEnabled ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        }

        // --- РЕНДЕР (OnGUI) ---
        public static void OnGUI()
        {
            if (_boxStyle == null)
            {
                _boxStyle = new GUIStyle();
                _boxStyle.normal.background = Texture2D.whiteTexture;
            }

            DrawLeftHUD();
            DrawRightHUD();
            DrawNotifications();
        }

        // 1. Отрисовка левой плашки (Song Name, Time, Seed) - БЕЗ BPM!
        private static void DrawLeftHUD()
        {
            if (_levelInfoAlpha <= 0.001f) return;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft
                };
            }

            string songName = "None";
            string timeStr = "00:00";
            string seedStr = "Unknown";

            ModelStoryCheckpoint currentSong = GetCurrentModelSong();
            if (currentSong != null)
            {
                songName = currentSong.name;
                timeStr = GetFormattedTime();
                seedStr = GetRoomSeed();

                CheckAndDump(currentSong);
            }

            _labelStyle.normal.textColor = new Color(1f, 1f, 1f, _levelInfoAlpha * 0.9f);
            string hudText = $"Song: {songName}\nTime: {timeStr}\nSeed: {seedStr}";

            float x = 15f; float y = 15f; float width = 300f; float height = 75f;

            Color shadowColor = new Color(0f, 0f, 0f, _levelInfoAlpha * 0.5f);
            Color originalGuiColor = GUI.color;
            GUI.color = shadowColor;
            GUI.Box(new Rect(x - 5f, y - 5f, width, height), "", _boxStyle);
            GUI.color = originalGuiColor;

            GUI.Label(new Rect(x, y, width - 10f, height - 10f), hudText, _labelStyle);
        }

        // 2. Универсальная автоматическая отрисовка всех зарегистрированных правых плашек на ЛЮБОЙ из 9 позиций
        private static void DrawRightHUD()
        {
            foreach (var plate in RightPlates)
            {
                if (plate.CurrentAlpha <= 0.001f)
                    continue;

                string hudText = plate.TextGetter != null ? plate.TextGetter() : "";

                Vector2 boxSize = GetPlateSize(plate);
                float width = boxSize.x;
                float height = boxSize.y;

                int fontSize = plate.FontSize > 0 ? plate.FontSize : 18;

                // // Авто-выравнивание текста в зависимости от стороны экрана
                TextAnchor anchor = TextAnchor.MiddleCenter;
                // if (plate.Position == HUDPosition.TopLeft || plate.Position == HUDPosition.MiddleLeft || plate.Position == HUDPosition.BottomLeft)
                // {
                //     anchor = TextAnchor.MiddleLeft;
                // }
                // else if (plate.Position == HUDPosition.TopRight || plate.Position == HUDPosition.MiddleRight || plate.Position == HUDPosition.BottomRight)
                // {
                //     anchor = TextAnchor.MiddleRight;
                // }

                GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = anchor
                };

                // Вычисляем X-координату на основе выравнивания
                float pad = 15f;
                float x = pad;
                if (plate.Position == HUDPosition.TopCenter || plate.Position == HUDPosition.Center || plate.Position == HUDPosition.BottomCenter)
                {
                    x = (Screen.width - width) / 2f;
                }
                else if (plate.Position == HUDPosition.TopRight || plate.Position == HUDPosition.MiddleRight || plate.Position == HUDPosition.BottomRight)
                {
                    x = Screen.width - width - pad;
                }

                // Вычисляем Y-координату с учетом динамического стека этой позиции
                float y = GetHudY(plate.Key);

                Rect boxRect = new Rect(x, y, width, height);

                Color shadowColor = new Color(0f, 0f, 0f, plate.CurrentAlpha * 0.5f);
                Color originalGuiColor = GUI.color;
                GUI.color = shadowColor;
                GUI.Box(boxRect, "", _boxStyle);
                GUI.color = originalGuiColor;

                float pulse = Mathf.PingPong(Time.unscaledTime * 2f, 1f);
                Color pulseColor = Color.Lerp(plate.BaseColor, plate.PulseColor, pulse);
                pulseColor.a = plate.CurrentAlpha;

                labelStyle.normal.textColor = pulseColor;

                // Небольшой отступ текста от края плашки при боковом выравнивании
                Rect textRect = boxRect;
                // if (anchor == TextAnchor.MiddleLeft) textRect.x += 10f;
                // if (anchor == TextAnchor.MiddleRight) textRect.x -= 10f;

                GUI.Label(textRect, hudText, labelStyle);
            }
        }

        // 3. Динамическая автоматическая отрисовка стека уведомлений (Toasts) на ЛЮБОЙ из 9 позиций
        private static void DrawNotifications()
        {
            // 1. Забираем новые уведомления из очереди в активный список
            while (_notificationQueue.Count > 0)
            {
                _activeNotifications.Add(_notificationQueue.Dequeue());
            }

            // 2. Группируем активные уведомления по их позициям для раздельного стекинга
            Dictionary<HUDPosition, List<int>> positionIndices = new Dictionary<HUDPosition, List<int>>();
            foreach (HUDPosition pos in Enum.GetValues(typeof(HUDPosition)))
            {
                positionIndices[pos] = new List<int>();
            }

            for (int i = 0; i < _activeNotifications.Count; i++)
            {
                positionIndices[_activeNotifications[i].Position].Add(i);
            }

            // 3. Отрисовываем каждое активное уведомление
            for (int i = 0; i < _activeNotifications.Count; i++)
            {
                var notif = _activeNotifications[i];
                notif.Elapsed += Time.unscaledDeltaTime;

                // Рассчитываем плавную прозрачность конкретного тоста
                float targetAlpha = 1f;
                if (notif.Elapsed >= notif.Duration - 0.5f)
                {
                    targetAlpha = 0f; // Угасание в конце жизни
                }
                notif.Alpha = Mathf.MoveTowards(notif.Alpha, targetAlpha, Time.unscaledDeltaTime * 4f);

                if (notif.Alpha > 0.001f)
                {
                    if (_notifStyle == null)
                    {
                        _notifStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 16,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleCenter
                        };
                    }

                    // Просим калькулятор рассчитать точный размер тоста под его текст
                    Vector2 size = GetNotifSize(notif.Text);
                    float w = size.x;
                    float h = size.y;

                    // Вычисляем X-координату тоста
                    float pad = 15f;
                    float x = pad;
                    if (notif.Position == HUDPosition.TopCenter || notif.Position == HUDPosition.Center || notif.Position == HUDPosition.BottomCenter)
                    {
                        x = (Screen.width - w) / 2f;
                    }
                    else if (notif.Position == HUDPosition.TopRight || notif.Position == HUDPosition.MiddleRight || notif.Position == HUDPosition.BottomRight)
                    {
                        x = Screen.width - w - pad;
                    }

                    // Вычисляем Y-координату тоста
                    float y = pad;
                    float spacing = 10f;

                    // Ищем порядковый номер этого тоста внутри стека его позиции
                    var stackIndices = positionIndices[notif.Position];
                    int stackIndex = stackIndices.IndexOf(i);

                    // Рассчитываем стек по вертикали
                    if (notif.Position == HUDPosition.TopLeft || notif.Position == HUDPosition.TopCenter || notif.Position == HUDPosition.TopRight)
                    {
                        // Стек растет вниз. Начинаем ниже активных плашек HUD этой же позиции
                        float platesHeight = 0f;
                        var plateStack = _hudStacks[notif.Position];
                        foreach (var key in plateStack)
                        {
                            var pl = RightPlates.Find(p => p.Key == key);
                            platesHeight += pl != null ? GetPlateSize(pl).y + spacing : 35f + spacing;
                        }

                        y = pad + platesHeight;
                        for (int s = 0; s < stackIndex; s++)
                        {
                            var prevNotif = _activeNotifications[stackIndices[s]];
                            y += GetNotifSize(prevNotif.Text).y + spacing;
                        }
                    }
                    else if (notif.Position == HUDPosition.BottomLeft || notif.Position == HUDPosition.BottomCenter || notif.Position == HUDPosition.BottomRight)
                    {
                        // Стек растет вверх
                        y = Screen.height - pad;
                        for (int s = 0; s <= stackIndex; s++)
                        {
                            var curNotif = _activeNotifications[stackIndices[s]];
                            y -= GetNotifSize(curNotif.Text).y;
                            if (s < stackIndex) y -= spacing;
                        }
                    }
                    else // Средние позиции по вертикали (MiddleLeft, Center, MiddleRight)
                    {
                        // Рассчитываем общую высоту стека уведомлений для авто-центрирования
                        float totalHeight = 0f;
                        for (int s = 0; s < stackIndices.Count; s++)
                        {
                            var curNotif = _activeNotifications[stackIndices[s]];
                            totalHeight += GetNotifSize(curNotif.Text).y;
                            if (s < stackIndices.Count - 1) totalHeight += spacing;
                        }

                        float startY = (Screen.height - totalHeight) / 2f;
                        y = startY;
                        for (int s = 0; s < stackIndex; s++)
                        {
                            var prevNotif = _activeNotifications[stackIndices[s]];
                            y += GetNotifSize(prevNotif.Text).y + spacing;
                        }
                    }

                    // Плавная анимация скольжения при появлении (slide offset)
                    float yOffset = Mathf.Lerp(0f, 10f, notif.Alpha);
                    if (notif.Position == HUDPosition.BottomLeft || notif.Position == HUDPosition.BottomCenter || notif.Position == HUDPosition.BottomRight)
                    {
                        y -= yOffset; // Едет вверх
                    }
                    else
                    {
                        y += yOffset; // Едет вниз
                    }

                    Rect rect = new Rect(x, y, w, h);

                    // Отрисовка темной подложки
                    Color boxCol = new Color(0f, 0f, 0f, notif.Alpha * 0.75f);
                    Color origColor = GUI.color;
                    GUI.color = boxCol;
                    GUI.Box(rect, "", _boxStyle);
                    GUI.color = origColor;

                    // Отрисовка текста тоста
                    Color txtCol = notif.TextColor;
                    txtCol.a = notif.Alpha;
                    _notifStyle.normal.textColor = txtCol;

                    GUI.Label(rect, notif.Text, _notifStyle);
                }

                _activeNotifications[i] = notif; // Сохраняем измененную структуру обратно
            }

            // Удаляем истекшие уведомления из активного списка
            _activeNotifications.RemoveAll(n => n.Elapsed >= n.Duration);
        }

        // --- РЕФЛЕКСИВНЫЕ ПОМОЩНИКИ КАРТЫ ---
        private static ModelStoryCheckpoint? GetCurrentModelSong()
        {
            try
            {
                if (GameScene.instance?.logicManager == null) return null;

                var normalType = Il2CppType.Of<ActorNormalLevelLogic>();
                var normalLogicObj = GameScene.instance.logicManager.getFirst(normalType);
                if (normalLogicObj != null)
                {
                    var normalLogic = normalLogicObj.Cast<ActorNormalLevelLogic>();
                    if (normalLogic != null) return normalLogic.modelSong;
                }

                var multiType = Il2CppType.Of<ActorMultiplayerLevelLogic>();
                var multiLogicObj = GameScene.instance.logicManager.getFirst(multiType);
                if (multiLogicObj != null)
                {
                    var multiLogic = multiLogicObj.Cast<ActorMultiplayerLevelLogic>();
                    if (multiLogic != null) return multiLogic.modelSong;
                }
            }
            catch { }
            return null;
        }

        private static void CheckAndDump(ModelStoryCheckpoint modelSong)
        {
            if (!DebugStrings.Enabled || modelSong == null || _lastDumpedSongId == modelSong.id)
                return;

            _lastDumpedSongId = modelSong.id;

            DumpFieldsAndProperties(modelSong, "ModelStoryCheckpoint (currentSong)");
            try { if (modelSong.metaSong != null) DumpFieldsAndProperties(modelSong.metaSong, "MetaSong"); } catch {}
            try { if (modelSong.modelSoundTrack != null) DumpFieldsAndProperties(modelSong.modelSoundTrack, "ModelSoundTrack"); } catch {}
        }

        private static void DumpFieldsAndProperties(object obj, string label)
        {
            if (obj == null) return;
            try
            {
                MelonLoader.MelonLogger.Msg($"[LevelInfoHUD] === СТРУКТУРА ОБЪЕКТА: {label} ({obj.GetType().Name}) ===");
                foreach (var field in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    MelonLoader.MelonLogger.Msg($"  Поле: {field.Name} (тип: {field.FieldType.Name})");
                }
                foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    MelonLoader.MelonLogger.Msg($"  Свойство: {prop.Name} (тип: {prop.PropertyType.Name})");
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"[LevelInfoHUD] Ошибка дампа {label}: {ex.Message}");
            }
        }

        private static string GetFormattedTime()
        {
            var src = FindMusicSource();
            if (src == null) return "00:00";

            int minutes = Mathf.FloorToInt(src.time / 60f);
            int seconds = Mathf.FloorToInt(src.time % 60f);
            return $"{minutes:D2}:{seconds:D2}";
        }

        private static string GetRoomSeed()
        {
            try { return MyMath.roomSeed.ToString(); }
            catch { return "Unknown"; }
        }

        private static AudioSource FindMusicSource()
        {
            if (_cachedSource != null && _cachedSource.isPlaying && _cachedSource.clip != null)
                return _cachedSource;

            var sources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
            if (sources == null) return null!;

            float longestClip = 0f;
            AudioSource candidate = null!;
            foreach (var src in sources)
            {
                if (src != null && src.isPlaying)
                {
                    if (src.gameObject.name.Contains("JukeBox") || src.gameObject.name.Contains("Jukebox"))
                    {
                        _cachedSource = src;
                        return src;
                    }
                    if (src.clip != null && src.clip.length > longestClip)
                    {
                        longestClip = src.clip.length;
                        candidate = src;
                    }
                }
            }
            _cachedSource = candidate;
            return candidate;
        }
    }
}
