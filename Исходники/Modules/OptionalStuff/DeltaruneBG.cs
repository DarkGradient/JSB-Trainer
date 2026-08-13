using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace jsb_new
{
    public static class DeltaruneGrid
    {
        public const string FEATURE_NAME = "Deltarune Grid BG";

        // ===== Настройки (меняются слайдерами) =====
        // Дефолты подобраны под вид diagonal-grid фона из battle-меню Deltarune:
        // крупные бледные клетки, холодный почти-белый цвет, медленный диагональный скролл.
        public static float CellSize = 96f;
        public static float LineThickness = 2.5f;
        public static int GridLinesX = 16;
        public static int GridLinesY = 12;

        public static float SpeedX1 = -18f;   // сетка 1: влево-вверх, медленно
        public static float SpeedY1 = 12f;
        public static float SpeedX2 = 15f;    // сетка 2: вправо-вниз, медленно (встречная диагональ)
        public static float SpeedY2 = -10f;

        public static float Alpha1 = 0.30f;
        public static float Alpha2 = 0.20f;
        public static float ColorR = 0.55f;
        public static float ColorG = 0.18f;
        public static float ColorB = 0.80f;
        public static int SortingOrder = -20;
        public static float PosZ = 1.5f;

        // ===== Внутреннее =====
        private static GameObject? _canvasObject;
        private static RectTransform? _container1;
        private static RectTransform? _container2;
        private static float _offsetX1, _offsetY1;
        private static float _offsetX2, _offsetY2;
        private static bool _needRebuild;

        public static bool Enabled
        {
            get => ModuleRegistry.IsActive(FEATURE_NAME);
            set => ModuleRegistry.SetActive(FEATURE_NAME, value);
        }

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            ModuleRegistry.RegisterCheckbox(
                FEATURE_NAME,
                () => Enabled,
                                            (v) =>
                                            {
                                                Enabled = v;
                                                if (v)
                                                {
                                                    CreateGrid();
                                                    HUDManager.CreateToast("DELTARUNE GRID: ON", new Color(0.6f, 0.2f, 0.8f), 1.5f);
                                                }
                                                else
                                                {
                                                    DestroyGrid();
                                                    HUDManager.CreateToast("DELTARUNE GRID: OFF", Color.gray, 1.5f);
                                                }
                                            }
            );

            ModuleRegistry.RegisterSlider("DG Cell Size", 20f, 150f, 96f, v => { CellSize = v; _needRebuild = true; });
            ModuleRegistry.RegisterSlider("DG Line Thickness", 0.5f, 8f, 2.5f, v => { LineThickness = v; _needRebuild = true; });
            ModuleRegistry.RegisterSlider("DG Lines X", 8f, 48f, 16f, v => { GridLinesX = Mathf.RoundToInt(v); _needRebuild = true; });
            ModuleRegistry.RegisterSlider("DG Lines Y", 6f, 32f, 12f, v => { GridLinesY = Mathf.RoundToInt(v); _needRebuild = true; });

            ModuleRegistry.RegisterSlider("DG Speed X1", -80f, 80f, -18f, v => SpeedX1 = v);
            ModuleRegistry.RegisterSlider("DG Speed Y1", -80f, 80f, 12f, v => SpeedY1 = v);
            ModuleRegistry.RegisterSlider("DG Speed X2", -80f, 80f, 15f, v => SpeedX2 = v);
            ModuleRegistry.RegisterSlider("DG Speed Y2", -80f, 80f, -10f, v => SpeedY2 = v);

            ModuleRegistry.RegisterSlider("DG Alpha 1", 0f, 1f, 0.30f, v => { Alpha1 = v; ApplyColors(); });
            ModuleRegistry.RegisterSlider("DG Alpha 2", 0f, 1f, 0.20f, v => { Alpha2 = v; ApplyColors(); });
            ModuleRegistry.RegisterSlider("DG Color R", 0f, 1f, 0.55f, v => { ColorR = v; ApplyColors(); });
            ModuleRegistry.RegisterSlider("DG Color G", 0f, 1f, 0.18f, v => { ColorG = v; ApplyColors(); });
            ModuleRegistry.RegisterSlider("DG Color B", 0f, 1f, 0.80f, v => { ColorB = v; ApplyColors(); });

            ModuleRegistry.RegisterSlider("DG Sorting Order", -50f, 10f, -20f, v =>
            {
                SortingOrder = Mathf.RoundToInt(v);
                if (_canvasObject != null)
                {
                    var c = _canvasObject.GetComponent<Canvas>();
                    if (c != null) c.sortingOrder = SortingOrder;
                }
            });

            ModuleRegistry.RegisterSlider("DG Pos Z", 0f, 5f, 1.5f, v =>
            {
                PosZ = v;
                if (_canvasObject != null)
                {
                    var p = _canvasObject.transform.position;
                    _canvasObject.transform.position = new Vector3(p.x, p.y, PosZ);
                }
            });

            DebugStrings.Log("[DeltaruneGrid] Initialized (fixed to world, shake-compensated only)");
        }

        public static void Update()
        {
            if (!Enabled)
            {
                if (_canvasObject != null)
                    DestroyGrid();
                return;
            }

            if (_needRebuild)
            {
                DestroyGrid();
                CreateGrid();
                _needRebuild = false;
            }

            if (_canvasObject == null)
                CreateGrid();

            if (_container1 == null || _container2 == null || _canvasObject == null)
                return;

            // Как в PurpleSoul.UpdateShakeAndZoom(): канвас зафиксирован в мировых координатах
            // и НЕ следует за позицией/зумом камеры. Компенсируем только shake (тряску камеры),
            // а не саму позицию камеры — это то, что раньше "уносило" сетку в сторону при её движении.
            _canvasObject.transform.position = new Vector3(0f, 0f, PosZ);
            _canvasObject.transform.localScale = Vector3.one;

            float shakeX = 0f;
            float shakeY = 0f;

            if (CameraFlash.mainCamera != null && CameraFlash.mainCamera.actorForTransform != null)
            {
                shakeX = CameraFlash.mainCamera.actorForTransform.px;
                shakeY = CameraFlash.mainCamera.actorForTransform.py;
            }

            // ===== Скролл сеток: чистое время + компенсация шейка =====
            _offsetX1 += SpeedX1 * Time.deltaTime;
            _offsetY1 += SpeedY1 * Time.deltaTime;
            float wx1 = Mod(_offsetX1, CellSize);
            float wy1 = Mod(_offsetY1, CellSize);
            _container1.anchoredPosition = new Vector2(wx1 + shakeX, -wy1 - shakeY);

            _offsetX2 += SpeedX2 * Time.deltaTime;
            _offsetY2 += SpeedY2 * Time.deltaTime;
            float wx2 = Mod(_offsetX2, CellSize);
            float wy2 = Mod(_offsetY2, CellSize);
            _container2.anchoredPosition = new Vector2(wx2 + shakeX, -wy2 - shakeY);
        }

        private static float Mod(float a, float b)
        {
            if (b == 0f) return 0f;
            float r = a % b;
            return r < 0f ? r + b : r;
        }

        private static void CreateGrid()
        {
            if (_canvasObject != null) return;

            _canvasObject = new GameObject("DeltaruneGridCanvas");
            _canvasObject.layer = 0;
            UnityEngine.Object.DontDestroyOnLoad(_canvasObject);

            Canvas canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = SortingOrder;

            _canvasObject.transform.position = new Vector3(0f, 0f, PosZ);
            _canvasObject.transform.localScale = Vector3.one;

            var canvasRect = _canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1280f, 720f);

            Color c1 = new Color(ColorR, ColorG, ColorB, Alpha1);
            Color c2 = new Color(ColorR * 0.8f, ColorG * 0.8f, ColorB * 0.9f, Alpha2);

            _container1 = BuildGridContainer("Grid1", c1);
            _container2 = BuildGridContainer("Grid2", c2);

            DebugStrings.Log("[DeltaruneGrid] Dual grid created");
        }

        private static RectTransform BuildGridContainer(string name, Color color)
        {
            GameObject container = new GameObject(name);
            container.layer = 0;
            container.transform.SetParent(_canvasObject!.transform, false);

            var rect = container.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1280f + CellSize * 2f, 720f + CellSize * 2f);
            rect.anchoredPosition = Vector2.zero;

            float totalW = CellSize * GridLinesX;
            float startX = -totalW * 0.5f;
            for (int i = 0; i <= GridLinesX; i++)
            {
                MakeLine(container.transform, $"V_{i}",
                         new Vector2(startX + i * CellSize, 0f),
                         new Vector2(LineThickness, 720f + CellSize * 2f),
                         color);
            }

            float totalH = CellSize * GridLinesY;
            float startY = -totalH * 0.5f;
            for (int i = 0; i <= GridLinesY; i++)
            {
                MakeLine(container.transform, $"H_{i}",
                         new Vector2(0f, startY + i * CellSize),
                         new Vector2(1280f + CellSize * 2f, LineThickness),
                         color);
            }

            return rect;
        }

        private static void MakeLine(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            GameObject line = new GameObject(name);
            line.layer = 0;
            line.transform.SetParent(parent, false);

            var r = line.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;

            var img = line.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private static void ApplyColors()
        {
            if (_container1 == null || _container2 == null) return;

            Color c1 = new Color(ColorR, ColorG, ColorB, Alpha1);
            Color c2 = new Color(ColorR * 0.8f, ColorG * 0.8f, ColorB * 0.9f, Alpha2);

            SetChildrenColor(_container1, c1);
            SetChildrenColor(_container2, c2);
        }

        private static void SetChildrenColor(RectTransform root, Color color)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var img = root.GetChild(i).GetComponent<Image>();
                if (img != null) img.color = color;
            }
        }

        private static void DestroyGrid()
        {
            if (_canvasObject != null)
            {
                UnityEngine.Object.Destroy(_canvasObject);
                _canvasObject = null;
                _container1 = null;
                _container2 = null;
            }
            _offsetX1 = _offsetY1 = _offsetX2 = _offsetY2 = 0f;
        }
    }
}
