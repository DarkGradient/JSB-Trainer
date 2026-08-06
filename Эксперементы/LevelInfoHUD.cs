using System;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using Il2CppInterop.Runtime;

namespace jsb_new
{
    public static class LevelInfoHUD
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("LevelInfoHUD");
            set => ModuleRegistry.SetActive("LevelInfoHUD", value);
        }

        private static float _hudAlpha = 0f;
        private static GUIStyle? _labelStyle;
        private static GUIStyle? _boxStyle;
        private static AudioSource? _cachedSource;
        private static string _lastDumpedSongId = "";

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            ModuleRegistry.RegisterCheckbox(
                category: "Optional Stuff",
                name: "Show Level Info HUD",
                getter: () => Enabled,
                                            setter: (newValue) => { Enabled = newValue; },
                                            isLocked: null,
                                            order: 15
            );
        }

        public static void Update()
        {
            float targetAlpha = Enabled ? 1f : 0f;
            _hudAlpha = Mathf.MoveTowards(_hudAlpha, targetAlpha, Time.unscaledDeltaTime * 4f);
        }

        public static void OnGUI()
        {
            if (_hudAlpha <= 0.001f)
                return;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft
                };
            }

            if (_boxStyle == null)
            {
                _boxStyle = new GUIStyle();
                _boxStyle.normal.background = Texture2D.whiteTexture;
            }

            // Получаем данные
            string songName = GetCurrentSongName();
            // string bpmStr = BPMProvider.CurrentBPM.ToString("F0");
            string timeStr = GetFormattedTime();
            string seedStr = GetRoomSeed();

            ModelStoryCheckpoint currentSong = GetCurrentModelSong();
            if (currentSong != null)
            {
                CheckAndDump(currentSong);
            }

            _labelStyle.normal.textColor = new Color(1f, 1f, 1f, _hudAlpha * 0.9f);

            string hudText = $"Song: {songName}\nTime: {timeStr}\nSeed: {seedStr}";

            float x = 15f;
            float y = 15f;
            float width = 300f;
            float height = 95f;

            // Отрисовка подложки без выделения новой памяти (GC Alloc = 0)
            Color shadowColor = new Color(0f, 0f, 0f, _hudAlpha * 0.5f);
            Color originalGuiColor = GUI.color;
            GUI.color = shadowColor;
            GUI.Box(new Rect(x - 5f, y - 5f, width, height), "", _boxStyle);
            GUI.color = originalGuiColor;

            GUI.Label(new Rect(x, y, width - 10f, height - 10f), hudText, _labelStyle);
        }

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

        private static string GetCurrentSongName()
        {
            var song = GetCurrentModelSong();
            return song != null ? song.name : "None";
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
            try
            {
                return MyMath.roomSeed.ToString();
            }
            catch
            {
                return "Unknown";
            }
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
