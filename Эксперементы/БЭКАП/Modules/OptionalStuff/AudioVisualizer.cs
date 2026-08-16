using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace jsb_new
{
    public static class AudioVisualizer
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("AudioVisualizer");
            set => ModuleRegistry.SetActive("AudioVisualizer", value);
        }

        private const int TotalBands = 48;
        public static float BarWidth = 14f;
        public static float GlowWidth = 20f;
        public static float MaxHeight = 320f;
        public static float BarSpacing = 3f;
        public static float LineThickness = 2f;
        public static float BarAlpha = 0.35f;
        public static float GlowAlpha = 0.15f;
        public static float VisualizerHue = 0.89f;
        public static float VerticalOffset = 0f;
        public static float TotalWidth = 1150f;

        public static float VisualGain = 3.5f;
        public static float AttackSpeed = 40f;
        public static float DecaySpeed = 8.5f;
        public static float PeakDecaySpeed = 1.4f;
        public static float SpatialSmoothing = 0.03f;
        public static float EqScale = 1.8f;
        public static float EqPower = 1.7f;

        public static bool ShowPeakLines = true;
        public static bool ShowGlow = true;

        private static Il2CppStructArray<float> _rawSpectrumL = new Il2CppStructArray<float>(512);
        private static Il2CppStructArray<float> _rawSpectrumR = new Il2CppStructArray<float>(512);

        private static float[] _bandsL = new float[TotalBands];
        private static float[] _bandsR = new float[TotalBands];
        private static float[] _smoothedBandsL = new float[TotalBands];
        private static float[] _smoothedBandsR = new float[TotalBands];
        private static float[] _peakHeightsL = new float[TotalBands];
        private static float[] _peakHeightsR = new float[TotalBands];

        private static GameObject _canvasObject = null!;
        private static GameObject _barsContainer = null!;

        private static GameObject[] _uiBarsTop = null!;
        private static GameObject[] _uiBarsBottom = null!;
        private static GameObject[] _glowBarsTop = null!;
        private static GameObject[] _glowBarsBottom = null!;
        private static GameObject[] _peakLinesTop = null!;
        private static GameObject[] _peakLinesBottom = null!;

        private static GameObject _centralLine = null!;
        private static AudioSource _musicSource = null!;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_KeyManager_Update_AudioVisualizer));

            ModuleRegistry.RegisterCheckbox("Enable Audio Visualizer",
                                            () => Enabled,
                                            (newValue) => SetEnabledState(newValue)
            );
        }

        public static void SetEnabledState(bool newValue)
        {
            Enabled = newValue;
            DebugStrings.Log($"AudioVisualizer enabled: {newValue}");
            if (!newValue) DestroyVisuals();
        }

        public static bool IsPlayerOnLevel()
        {
            if (GameScene.instance == null || GameScene.instance.logicManager == null) return false;
            try
            {
                if (GameScene.instance.logicManager.getFirst(Il2CppType.Of<ActorNormalLevelLogic>()) != null) return true;
                if (GameScene.instance.logicManager.getFirst(Il2CppType.Of<ActorMultiplayerLevelLogic>()) != null) return true;
            }
            catch { }
            return false;
        }

        private static AudioSource FindMusicSource()
        {
            if (_musicSource != null && _musicSource.isPlaying && _musicSource.clip != null) return _musicSource;
            _musicSource = null!;
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
                        _musicSource = src;
                        return _musicSource;
                    }
                    if (src.clip != null && src.clip.length > longestClip)
                    {
                        longestClip = src.clip.length;
                        candidate = src;
                    }
                }
            }
            _musicSource = candidate;
            return _musicSource;
        }

        private static void CopySpectrum(Il2CppStructArray<float> from, Il2CppStructArray<float> to)
        {
            for (int i = 0; i < from.Length; i++) to[i] = from[i];
        }

        private static void AnalyzeAudio()
        {
            bool gotData = false;
            bool isStereo = false;

            for (int i = 0; i < 512; i++) { _rawSpectrumL[i] = 0f; _rawSpectrumR[i] = 0f; }

            AudioSource src = FindMusicSource();
            if (src != null && src.isPlaying)
            {
                try
                {
                    isStereo = (src.clip != null && src.clip.channels > 1);
                    src.GetSpectrumData(_rawSpectrumL, 0, FFTWindow.Hanning);
                    if (isStereo) { try { src.GetSpectrumData(_rawSpectrumR, 1, FFTWindow.Hanning); } catch { CopySpectrum(_rawSpectrumL, _rawSpectrumR); } }
                    else CopySpectrum(_rawSpectrumL, _rawSpectrumR);
                    if (_rawSpectrumL[0] > 0f || _rawSpectrumL[1] > 0f) gotData = true;
                }
                catch { gotData = false; }
            }

            if (!gotData)
            {
                try
                {
                    AudioListener.GetSpectrumData(_rawSpectrumL, 0, FFTWindow.Hanning);
                    try { AudioListener.GetSpectrumData(_rawSpectrumR, 1, FFTWindow.Hanning); } catch { CopySpectrum(_rawSpectrumL, _rawSpectrumR); }
                    if (_rawSpectrumL[0] > 0f || _rawSpectrumL[1] > 0f) gotData = true;
                }
                catch { }
            }

            float minFreq = 1.0f;
            float maxFreq = 512f;

            for (int i = 0; i < TotalBands; i++)
            {
                float lowBound = minFreq * Mathf.Pow(maxFreq / minFreq, (float)i / TotalBands);
                float highBound = minFreq * Mathf.Pow(maxFreq / minFreq, (float)(i + 1) / TotalBands);

                int startBin = Mathf.FloorToInt(lowBound);
                int endBin = Mathf.CeilToInt(highBound);
                if (endBin <= startBin) endBin = startBin + 1;

                float maxL = 0f;
                float maxR = 0f;

                for (int j = startBin; j < endBin && j < 512; j++)
                {
                    if (_rawSpectrumL[j] > maxL) maxL = _rawSpectrumL[j];
                    if (_rawSpectrumR[j] > maxR) maxR = _rawSpectrumR[j];
                }

                float freqFactor = (float)i / TotalBands;
                float eqMultiplier = 1f + Mathf.Pow(freqFactor, EqPower) * EqScale;

                float targetHeightL = maxL * VisualGain * eqMultiplier * MaxHeight;
                float targetHeightR = maxR * VisualGain * eqMultiplier * MaxHeight;

                _bandsL[i] = SoftClip(targetHeightL, MaxHeight * 0.75f);
                _bandsR[i] = SoftClip(targetHeightR, MaxHeight * 0.75f);
            }

            SmoothArray(_bandsL);
            SmoothArray(_bandsR);
        }

        private static void SmoothArray(float[] targetArray)
        {
            float centerWeight = 1.0f - 2f * SpatialSmoothing;
            float[] temp = new float[TotalBands];
            for (int i = 0; i < TotalBands; i++)
            {
                float sum = 0f, totalWeight = 0f;
                for (int offset = -1; offset <= 1; offset++)
                {
                    int idx = i + offset;
                    if (idx >= 0 && idx < TotalBands)
                    {
                        float w = (offset == 0) ? centerWeight : SpatialSmoothing;
                        sum += targetArray[idx] * w;
                        totalWeight += w;
                    }
                }
                temp[i] = sum / totalWeight;
            }
            Array.Copy(temp, targetArray, TotalBands);
        }

        private static float SoftClip(float value, float threshold)
        {
            if (value <= threshold) return value;
            float excess = value - threshold;
            float clipped = threshold + (MaxHeight - threshold) * (1f - Mathf.Exp(-excess / (MaxHeight - threshold)));
            return Mathf.Clamp(clipped, 0f, MaxHeight);
        }

        private static void UpdateVisualizer()
        {
            if (_canvasObject == null || _barsContainer == null) return;

            AnalyzeAudio();
            float dt = Mathf.Min(Time.deltaTime, 0.1f);

            if (_centralLine != null)
            {
                var lineRect = _centralLine.GetComponent<RectTransform>();
                if (lineRect != null) { lineRect.sizeDelta = new Vector2(0f, LineThickness); lineRect.anchoredPosition = new Vector2(0f, VerticalOffset); }
                var lineImg = _centralLine.GetComponent<Image>();
                if (lineImg != null) {
                    Color lineColor = Color.HSVToRGB(VisualizerHue, 0.9f, 1.0f);
                    lineColor.a = BarAlpha * 0.8f;
                    lineImg.color = lineColor;
                }
            }

            float spacing = TotalWidth / (TotalBands - 1);
            float startX = -TotalWidth / 2f;

            for (int i = 0; i < TotalBands; i++)
            {
                float x = startX + i * spacing;

                float targetL = Mathf.Clamp(_bandsL[i], 0f, MaxHeight);
                _smoothedBandsL[i] = Mathf.Lerp(_smoothedBandsL[i], targetL, (targetL > _smoothedBandsL[i] ? AttackSpeed : DecaySpeed) * dt);
                _peakHeightsL[i] = targetL > _peakHeightsL[i] ? targetL : Mathf.Lerp(_peakHeightsL[i], targetL, PeakDecaySpeed * dt);

                float targetR = Mathf.Clamp(_bandsR[i], 0f, MaxHeight);
                _smoothedBandsR[i] = Mathf.Lerp(_smoothedBandsR[i], targetR, (targetR > _smoothedBandsR[i] ? AttackSpeed : DecaySpeed) * dt);
                _peakHeightsR[i] = targetR > _peakHeightsR[i] ? targetR : Mathf.Lerp(_peakHeightsR[i], targetR, PeakDecaySpeed * dt);

                if (_uiBarsTop[i] != null)
                {
                    _uiBarsTop[i].GetComponent<RectTransform>().sizeDelta = new Vector2(BarWidth - BarSpacing, _smoothedBandsL[i]);
                    _uiBarsTop[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(x, VerticalOffset + (_smoothedBandsL[i] / 2f));
                }
                if (_uiBarsBottom[i] != null)
                {
                    _uiBarsBottom[i].GetComponent<RectTransform>().sizeDelta = new Vector2(BarWidth - BarSpacing, _smoothedBandsR[i]);
                    _uiBarsBottom[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(x, VerticalOffset - (_smoothedBandsR[i] / 2f));
                }

                if (ShowGlow)
                {
                    if (_glowBarsTop[i] != null)
                    {
                        _glowBarsTop[i].GetComponent<RectTransform>().sizeDelta = new Vector2(GlowWidth - BarSpacing, _smoothedBandsL[i]);
                        _glowBarsTop[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(x, VerticalOffset + (_smoothedBandsL[i] / 2f));
                    }
                    if (_glowBarsBottom[i] != null)
                    {
                        _glowBarsBottom[i].GetComponent<RectTransform>().sizeDelta = new Vector2(GlowWidth - BarSpacing, _smoothedBandsR[i]);
                        _glowBarsBottom[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(x, VerticalOffset - (_smoothedBandsR[i] / 2f));
                    }
                }

                if (ShowPeakLines)
                {
                    if (_peakLinesTop[i] != null)
                        _peakLinesTop[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(x, VerticalOffset + _peakHeightsL[i]);
                    if (_peakLinesBottom[i] != null)
                        _peakLinesBottom[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(x, VerticalOffset - _peakHeightsR[i]);
                }
            }

            float shakeX = 0f, shakeY = 0f;
            if (CameraFlash.mainCamera != null && CameraFlash.mainCamera.actorForTransform != null)
            {
                shakeX = CameraFlash.mainCamera.actorForTransform.px;
                shakeY = CameraFlash.mainCamera.actorForTransform.py;
            }
            _canvasObject.transform.position = new Vector3(0f, 0f, 1.51f);
            var containerRect = _barsContainer.GetComponent<RectTransform>();
            if (containerRect != null) containerRect.anchoredPosition = new Vector2(shakeX, -shakeY);
        }

        public static void CreateVisuals()
        {
            if (_canvasObject != null) return;

            _canvasObject = new GameObject("AudioVisualizerCanvas");
            _canvasObject.layer = 0;
            UnityEngine.Object.DontDestroyOnLoad(_canvasObject);

            Canvas canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = -100;

            var rect = _canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1280f, 720f);

            _barsContainer = new GameObject("BarsContainer");
            _barsContainer.layer = 0;
            _barsContainer.transform.SetParent(_canvasObject.transform, false);
            var containerRect = _barsContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.sizeDelta = Vector2.zero;
            containerRect.anchoredPosition = Vector2.zero;

            _centralLine = new GameObject("CentralLine");
            _centralLine.layer = 0;
            _centralLine.transform.SetParent(_barsContainer.transform, false);
            var lineRect = _centralLine.AddComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0f, 0.5f);
            lineRect.anchorMax = new Vector2(1f, 0.5f);
            lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.anchoredPosition = new Vector2(0f, VerticalOffset);
            var lineImage = _centralLine.AddComponent<Image>();
            lineImage.raycastTarget = false;

            _uiBarsTop = new GameObject[TotalBands];
            _uiBarsBottom = new GameObject[TotalBands];
            _glowBarsTop = new GameObject[TotalBands];
            _glowBarsBottom = new GameObject[TotalBands];
            _peakLinesTop = new GameObject[TotalBands];
            _peakLinesBottom = new GameObject[TotalBands];

            float spacing = TotalWidth / (TotalBands - 1);
            float startX = -TotalWidth / 2f;

            Color baseColor = Color.HSVToRGB(VisualizerHue, 0.85f, 1.0f);
            Color glowColor = baseColor;
            glowColor.a = GlowAlpha;
            Color peakColor = Color.HSVToRGB(VisualizerHue, 0.9f, 1.0f);
            peakColor.a = 0.9f;

            for (int i = 0; i < TotalBands; i++)
            {
                float x = startX + i * spacing;

                if (ShowGlow)
                {
                    _glowBarsTop[i] = CreateBarObj($"GlowTop_{i}", GlowWidth, glowColor);
                    _glowBarsBottom[i] = CreateBarObj($"GlowBottom_{i}", GlowWidth, glowColor);
                }

                _uiBarsTop[i] = CreateBarObj($"BarTop_{i}", BarWidth, baseColor, BarAlpha);
                _uiBarsBottom[i] = CreateBarObj($"BarBottom_{i}", BarWidth, baseColor, BarAlpha);

                if (ShowPeakLines)
                {
                    _peakLinesTop[i] = CreatePeakObj($"PeakTop_{i}", peakColor);
                    _peakLinesBottom[i] = CreatePeakObj($"PeakBottom_{i}", peakColor);
                }
            }

            DebugStrings.Log("AudioVisualizer: Пиковое считывание частот включено.");
        }

        private static GameObject CreateBarObj(string name, float width, Color color, float alpha = -1f)
        {
            GameObject obj = new GameObject(name);
            obj.layer = 0;
            obj.transform.SetParent(_barsContainer.transform, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            var img = obj.AddComponent<Image>();
            if (alpha >= 0f) color.a = alpha;
            img.color = color;
            img.raycastTarget = false;
            return obj;
        }

        private static GameObject CreatePeakObj(string name, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.layer = 0;
            obj.transform.SetParent(_barsContainer.transform, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(BarWidth * 0.5f, 2f);
            var img = obj.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return obj;
        }

        public static void DestroyVisuals()
        {
            if (_canvasObject != null)
            {
                UnityEngine.Object.Destroy(_canvasObject);
                _canvasObject = null!;
                _barsContainer = null!;
                _centralLine = null!;
                _uiBarsTop = null!; _uiBarsBottom = null!;
                _glowBarsTop = null!; _glowBarsBottom = null!;
                _peakLinesTop = null!; _peakLinesBottom = null!;
                _musicSource = null!;
            }
        }

        [HarmonyPatch(typeof(KeyManager), "update")]
        private static class Patch_KeyManager_Update_AudioVisualizer
        {
            private static int _menuLogCounter = 0;
            static void Postfix()
            {
                if (!Enabled) { DestroyVisuals(); return; }
                if (IsPlayerOnLevel())
                {
                    _menuLogCounter = 0;
                    CreateVisuals();
                    UpdateVisualizer();
                }
                else
                {
                    DestroyVisuals();
                    _menuLogCounter++;
                    if (_menuLogCounter >= 180) { _menuLogCounter = 0; DebugStrings.Log("AudioVisualizer enabled, but player is not on a level"); }
                }
            }
        }
    }
}
