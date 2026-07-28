using System;
using System.Collections.Generic;
using System.Reflection;
using Il2Cpp;
using UnityEngine;
using Il2CppInterop.Runtime;

namespace jsb_new
{
    public static class BPMProvider
    {
        private static AudioSource? _cachedSource;
        private static string _cachedSongId = "";
        private static float _cachedBpm = 120f;

        // База данных оригинального темпа (BPM) всех уровней Just Shapes & Beats
        private static readonly Dictionary<string, float> BpmDictionary = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            // Обучение и Пролог
            { "CORRUPTED", 136f },
            { "CHRONOS", 136f },
            { "MILKY WAYS", 183f },
            { "LOGIC GATEKEEPER", 163f },
            { "LONG LIVE THE NEW FRESH", 127f },

            // Вулкан (Вход и босс)
            { "THE ART OF WAR", 174f },
            { "TERMINATION SHOCK", 126f },
            { "SEVCON", 133f },
            { "CASCADE", 129f },
            { "BARRACUDA", 130f },

            // Вулкан (Уровни)
            { "DUBWOOFER SUBSTEP", 109f },
            { "CHEAT CODES", 128f },
            { "CLASH", 192f },
            { "LYCANTHROPY", 125f },

            // Индустриальная зона
            { "COOL FRIENDS", 115f },
            { "THE LUNAR WHALE", 109f },
            { "SPECTRA", 130f },
            { "UNLOCKED", 128f },
            { "CLOSE TO ME", 124f },
            { "INTO THE ZONE", 122f },
            { "VINDICATE ME", 100f },
            { "TRY THIS", 130f },
            { "FINAL BOSS", 140f },
            { "ANNIHILATE (ORIGINAL MIX)", 140f },
            { "TILL IT'S OVER", 110f },

            // Рыцарь Shovel Knight
            { "STRIKE THE EARTH!", 140f },
            { "IN THE HALLS OF THE USURPER", 130f },
            { "LA DANSE MACABRE", 130f },
            { "FLOWERS OF ANTIMONY", 130f },

            // Каверна (Затерянная глава)
            { "AIRBORNE ROBOTS", 145f },
            { "INTERLACED", 130f },
            { "LAST TILE 海底撈月", 140f },
            { "BORN SURVIVOR", 130f },
            { "SPIDER DANCE", 130f },

            // Экстра-уровни (Плейлист)
            { "WICKED", 100f },
            { "FIRST CRUSH", 128f },
            { "YOKUMAN STAGE", 130f },
            { "PAPER DOLLS", 130f },
            { "COMMANDO STEVE", 174f },
            { "HOUSTON", 128f },
            { "HYPE", 128f },
            { "TOKYO SKIES", 130f },
            { "DANCE OF THE INCOGNIZANT", 130f },
            { "FOX", 130f },
            { "CORE", 130f },
            { "CRYSTAL TOKYO", 174f },
            { "ON THE RUN", 128f },
            { "MORTAL KOMBAT", 140f },
            { "CREATURES OV DECEPTION", 130f },
            { "DEADLOCKED", 130f },
            { "GRANITE", 130f },
            { "LIGHTSPEED", 140f },
            { "KATANA BLASTER", 140f }
        };

        // Текущий BPM песни.
        public static float CurrentBPM
        {
            get
            {
                var song = GetCurrentModelSong();
                if (song == null) return 120f;

                // Если песня сменилась — обновляем кэш
                if (_cachedSongId != song.id)
                {
                    _cachedSongId = song.id;

                    // Очищаем название от лишних пробелов по краям (как в " Chronos")
                    string cleanName = song.name != null ? song.name.Trim() : "";

                    // Сначала ищем в нашем словаре оригинальных треков
                    if (BpmDictionary.TryGetValue(cleanName, out float dictBpm))
                    {
                        _cachedBpm = dictBpm;
                    }
                    else
                    {
                        // Если в словаре нет, на всякий случай пробуем рефлексию (вдруг кастомный уровень)
                        float fallbackBpm = GetSongBPM(song);
                        _cachedBpm = fallbackBpm > 1f ? fallbackBpm : 120f;
                    }
                }
                return _cachedBpm;
            }
        }

        // Возвращает точную текущую долю (Beat) трека (например, 120.45)
        public static float CurrentBeat
        {
            get
            {
                var src = FindMusicSource();
                if (src == null) return 0f;

                // Beat = секунды * (BPM / 60)
                return src.time * (CurrentBPM / 60f);
            }
        }

        // Возвращает плавный импульс удара от 1.0f (в пик удара доли) до 0.0f (затухание)
        public static float GetPulse(float division = 1f, float speed = 5f)
        {
            float beat = CurrentBeat;
            if (beat <= 0f) return 0f;

            float progress = (beat % division) / division;
            return Mathf.Max(0f, 1f - (progress * speed));
        }

        // --- Внутренние сканеры игры ---

        private static float GetSongBPM(ModelStoryCheckpoint modelSong)
        {
            if (modelSong == null) return 0f;

            float bpm = FindBpmInObject(modelSong);
            if (bpm > 1f) return bpm;

            if (modelSong.metaSong != null)
            {
                bpm = FindBpmInObject(modelSong.metaSong);
                if (bpm > 1f) return bpm;
            }

            if (modelSong.modelSoundTrack != null)
            {
                bpm = FindBpmInObject(modelSong.modelSoundTrack);
                if (bpm > 1f) return bpm;
            }

            return 0f;
        }

        private static float FindBpmInObject(object obj)
        {
            if (obj == null) return 0f;
            try
            {
                var type = obj.GetType();

                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (field.Name.IndexOf("bpm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        field.Name.IndexOf("tempo", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var val = field.GetValue(obj);
                        if (val != null && float.TryParse(val.ToString(), out float res))
                            return res;
                    }
                }

                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (prop.Name.IndexOf("bpm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        prop.Name.IndexOf("tempo", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var val = prop.GetValue(obj);
                        if (val != null && float.TryParse(val.ToString(), out float res))
                            return res;
                    }
                }
            }
            catch {}
            return 0f;
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
