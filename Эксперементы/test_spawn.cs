using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public static class test_spawn
{
    private static GameObject _testCanvasObject;
    private static GameObject _imageObj;
    private static RectTransform _imgRect;
    private static RawImage _rawImage;
    private static Texture2D _testTexture;

    private enum AnimStep
    {
        None,
        FadeIn,
        MoveLeft,
        MoveRight,
        MoveUp,
        MoveDown,
        CenterAndRotate,
        FadeOut,
        Done
    }

    private static AnimStep _currentStep = AnimStep.None;
    private static float _stepTimer = 0f;

    private const float MoveExtentX = 540f;
    private const float MoveExtentY = 260f;

    private const float FadeDuration = 0.5f;
    private const float MoveDuration = 0.8f;
    private const float RotateDuration = 1.0f;

    private static Vector2 _stepStartPos;
    private static Vector2 _stepTargetPos;
    private static float _stepStartRot;
    private static float _stepTargetRot;
    private static float _stepStartAlpha;
    private static float _stepTargetAlpha;

    public static void SpawnTestImage()
    {
        if (_testCanvasObject != null)
        {
            jsb_new.DebugStrings.Log("test_spawn: already spawned, skipping");
            return;
        }

        byte[] imageBytes = LoadEmbeddedBytes("jsb_new.resources.test_image.png");
        if (imageBytes == null)
        {
            MelonLoader.MelonLogger.Error("[test_spawn] Не удалось прочитать embedded resource.");
            return;
        }

        try
        {
            _testTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool loaded = ImageConversion.LoadImage(_testTexture, imageBytes);
            if (!loaded)
            {
                MelonLoader.MelonLogger.Error("[test_spawn] LoadImage вернул false.");
                return;
            }
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Error($"[test_spawn] Исключение при LoadImage: {ex}");
            return;
        }

        BuildCanvas();
        StartAnimation();
    }

    private static byte[] LoadEmbeddedBytes(string resourceName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                MelonLoader.MelonLogger.Error($"[test_spawn] Ресурс '{resourceName}' не найден.");
                return null;
            }
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Error($"[test_spawn] Ошибка чтения ресурса: {ex}");
            return null;
        }
    }

    private static void BuildCanvas()
    {
        _testCanvasObject = new GameObject("TestImageCanvas");
        _testCanvasObject.layer = 0;
        UnityEngine.Object.DontDestroyOnLoad(_testCanvasObject);

        Canvas canvas = _testCanvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        _testCanvasObject.transform.position = new Vector3(0f, 0f, 1.5f);
        _testCanvasObject.transform.localScale = Vector3.one;

        var rect = _testCanvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1280f, 720f);

        _imageObj = new GameObject("TestImage");
        _imageObj.layer = 0;
        _imageObj.transform.SetParent(_testCanvasObject.transform, false);

        _imgRect = _imageObj.AddComponent<RectTransform>();
        _imgRect.anchorMin = new Vector2(0.5f, 0.5f);
        _imgRect.anchorMax = new Vector2(0.5f, 0.5f);
        _imgRect.sizeDelta = new Vector2(200f, 200f);
        _imgRect.anchoredPosition = Vector2.zero;
        _imgRect.localRotation = Quaternion.identity;

        _rawImage = _imageObj.AddComponent<RawImage>();
        _rawImage.texture = _testTexture;
        _rawImage.color = new Color(1f, 1f, 1f, 0f);

        jsb_new.DebugStrings.Log("test_spawn: canvas with image spawned");
    }

    private static void StartAnimation()
    {
        _currentStep = AnimStep.FadeIn;
        _stepTimer = 0f;
        _stepStartAlpha = 0f;
        _stepTargetAlpha = 1f;
    }

    public static void Update()
    {
        if (Input.GetKeyDown(KeyCode.U))
        {
            SpawnTestImage();
        }
        if (Input.GetKeyDown(KeyCode.I))
        {
            DespawnTestImage();
        }

        if (_currentStep == AnimStep.None || _currentStep == AnimStep.Done || _imgRect == null)
            return;

        _stepTimer += Time.deltaTime;

        switch (_currentStep)
        {
            case AnimStep.FadeIn:
            {
                float t = Mathf.Clamp01(_stepTimer / FadeDuration);
                SetAlpha(Mathf.Lerp(_stepStartAlpha, _stepTargetAlpha, t));
                if (t >= 1f) BeginMove(new Vector2(-MoveExtentX, 0f));
                break;
            }
            case AnimStep.MoveLeft:
            {
                float t = Mathf.Clamp01(_stepTimer / MoveDuration);
                _imgRect.anchoredPosition = Vector2.Lerp(_stepStartPos, _stepTargetPos, SmoothStep(t));
                if (t >= 1f) BeginMove(new Vector2(MoveExtentX, 0f), AnimStep.MoveRight);
                break;
            }
            case AnimStep.MoveRight:
            {
                float t = Mathf.Clamp01(_stepTimer / MoveDuration);
                _imgRect.anchoredPosition = Vector2.Lerp(_stepStartPos, _stepTargetPos, SmoothStep(t));
                if (t >= 1f) BeginMove(new Vector2(0f, MoveExtentY), AnimStep.MoveUp);
                break;
            }
            case AnimStep.MoveUp:
            {
                float t = Mathf.Clamp01(_stepTimer / MoveDuration);
                _imgRect.anchoredPosition = Vector2.Lerp(_stepStartPos, _stepTargetPos, SmoothStep(t));
                if (t >= 1f) BeginMove(new Vector2(0f, -MoveExtentY), AnimStep.MoveDown);
                break;
            }
            case AnimStep.MoveDown:
            {
                float t = Mathf.Clamp01(_stepTimer / MoveDuration);
                _imgRect.anchoredPosition = Vector2.Lerp(_stepStartPos, _stepTargetPos, SmoothStep(t));
                if (t >= 1f) BeginCenterAndRotate();
                break;
            }
            case AnimStep.CenterAndRotate:
            {
                float t = Mathf.Clamp01(_stepTimer / RotateDuration);
                float eased = SmoothStep(t);
                _imgRect.anchoredPosition = Vector2.Lerp(_stepStartPos, Vector2.zero, eased);
                float rot = Mathf.Lerp(_stepStartRot, _stepTargetRot, eased);
                _imgRect.localRotation = Quaternion.Euler(0f, 0f, rot);
                if (t >= 1f) BeginFadeOut();
                break;
            }
            case AnimStep.FadeOut:
            {
                float t = Mathf.Clamp01(_stepTimer / FadeDuration);
                SetAlpha(Mathf.Lerp(_stepStartAlpha, _stepTargetAlpha, t));
                if (t >= 1f)
                {
                    _currentStep = AnimStep.Done;
                    DespawnTestImage();
                }
                break;
            }
        }
    }

    private static void BeginMove(Vector2 target, AnimStep nextStep = AnimStep.MoveLeft)
    {
        _stepStartPos = _imgRect.anchoredPosition;
        _stepTargetPos = target;
        _stepTimer = 0f;
        _currentStep = nextStep;
    }

    private static void BeginCenterAndRotate()
    {
        _stepStartPos = _imgRect.anchoredPosition;
        _stepStartRot = 0f;
        _stepTargetRot = 360f;
        _stepTimer = 0f;
        _currentStep = AnimStep.CenterAndRotate;
    }

    private static void BeginFadeOut()
    {
        _stepStartAlpha = 1f;
        _stepTargetAlpha = 0f;
        _stepTimer = 0f;
        _currentStep = AnimStep.FadeOut;
    }

    private static void SetAlpha(float alpha)
    {
        if (_rawImage == null) return;
        Color c = _rawImage.color;
        c.a = alpha;
        _rawImage.color = c;
    }

    private static float SmoothStep(float t) => t * t * (3f - 2f * t);

    public static void DespawnTestImage()
    {
        if (_testCanvasObject != null)
        {
            UnityEngine.Object.Destroy(_testCanvasObject);
            _testCanvasObject = null;
            _imageObj = null;
            _imgRect = null;
            _rawImage = null;
        }
        if (_testTexture != null)
        {
            UnityEngine.Object.Destroy(_testTexture);
            _testTexture = null;
        }
        _currentStep = AnimStep.None;
    }
}
