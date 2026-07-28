using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace jsb_new
{
    public static class HitboxRenderer
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("HitboxRenderer");
            set
            {
                if (ModuleRegistry.IsActive("HitboxRenderer") == value) return;
                ModuleRegistry.SetActive("HitboxRenderer", value);
                VersionInfo.DEBUG_FORCE_ALL_COLLISIONS_VISIBLE = value;
                DebugStrings.Log($"Hitboxes: {(value ? "ON" : "OFF")}");

                if (!value)
                    Cleanup();
            }
        }

        private static readonly Color DrawColor = Color.yellow;
        private static readonly Vector2[] CirclePoints;
        private const int CircleSegments = 18;

        private static Mesh? _lineMesh;
        private static Material? _lineMaterial;
        private static readonly List<Line> _lines = new(25000);
        private static Vector3[] _vertexBuffer = new Vector3[16384];
        private static Color32[] _colorBuffer = new Color32[16384];
        private static int[] _indexBuffer = new int[0];
        private static bool _resourcesReady = false;

        static HitboxRenderer()
        {
            CirclePoints = new Vector2[CircleSegments];
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * 360f / CircleSegments * Mathf.Deg2Rad;
                CirclePoints[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
        }

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            try
            {
                var circleMethod = typeof(CollisionCircleComponent).GetMethod("drawCircle");
                if (circleMethod != null)
                    harmony.Patch(circleMethod, new HarmonyMethod(typeof(HitboxRenderer), nameof(OnDrawCircle)));

                var circleVelMethod = typeof(CollisionCircleComponent).GetMethod("drawCircleWithVelocity");
                if (circleVelMethod != null)
                    harmony.Patch(circleVelMethod, new HarmonyMethod(typeof(HitboxRenderer), nameof(OnDrawCircleWithVelocity)));

                var rectMethod = typeof(CollisionRectangleComponent).GetMethod("drawRectangle");
                if (rectMethod != null)
                    harmony.Patch(rectMethod, new HarmonyMethod(typeof(HitboxRenderer), nameof(OnDrawRectangle)));

                var manageColMethod = AccessTools.Method(typeof(DisplayObjectRendererTk2d), "ManageCol");
                if (manageColMethod != null)
                    harmony.Patch(manageColMethod, new HarmonyMethod(typeof(HitboxRenderer), nameof(OnManageCol)));

                ModuleRegistry.RegisterCheckbox("Optional Stuff", "Hitboxes",
                                                () => Enabled,
                                                (newValue) => { Enabled = newValue; },
                                                order: 30
                );

                HUDManager.CreateHUD(
                    key: "HitboxRenderer",
                    textGetter: () => "HITBOXES VISIBLE",
                                     baseColor: Color.white,
                                     pulseColor: Color.yellow,
                                     activeGetter: () => Enabled, // <-- сюда прокидывается актуальное состояние автоматически
                                     height: 35f
                );

                DebugStrings.Log("[HitboxRenderer] Initialized.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[HitboxRenderer] Init failed: {ex.Message}");
            }
        }

        public static void Cleanup()
        {
            if (_lineMesh != null) { UnityEngine.Object.DestroyImmediate(_lineMesh); _lineMesh = null; }
            if (_lineMaterial != null) { UnityEngine.Object.DestroyImmediate(_lineMaterial); _lineMaterial = null; }

            _lines.Clear();
            _vertexBuffer = new Vector3[16384];
            _colorBuffer = new Color32[16384];
            _indexBuffer = new int[0];
            _resourcesReady = false;
        }

        public static void Update()
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                Enabled = !Enabled;
            }

            if (!Enabled)
            {
                if (_lines.Count > 0) _lines.Clear();
                return;
            }

            DrawPlayerHitboxes();
            RenderLines();
        }

        private static void DrawPlayerHitboxes()
        {
            var mainGame = MainGame.instance;
            if (mainGame == null) return;
            var scene = mainGame.gameSceneManager?.gameScene;
            if (scene == null) return;

            var actorList = scene.heroManager?.actorList;
            if (actorList == null) return;

            for (int i = 0; i < actorList.Count; i++)
            {
                var actor = actorList[i];
                if (actor == null) continue;

                var hero = actor.TryCast<Hero>();
                if (hero == null) continue;

                var enemyComp = hero.circleColComponentEnemy;
                if (enemyComp?.circleCol != null && enemyComp.enabled)
                    DrawCircleComponent(enemyComp, enemyComp.circleCol);

                var itemComp = hero.circleColComponentItem;
                if (itemComp?.circleCol != null && itemComp.enabled)
                    DrawCircleComponent(itemComp, itemComp.circleCol);
            }
        }

        private static void DrawCircleComponent(CollisionCircleComponent comp, Circle c)
        {
            var cam = CameraFlash.mainCamera;
            if (cam == null || comp.actor == null) return;

            var actor = comp.actor;
            float flashX = actor.px + c.x;
            float flashY = (actor.py + c.y) * -1f;
            float offset = (cam.px - (cam.actorForTransform?.px ?? 0f)) * 2f;

            Point p = cam.getCoordInWorld(flashX + 640f, flashY + 360f);
            AddCircle(p.x - offset, p.y, c.radius);
        }

        private static void RenderLines()
        {
            if (_lines.Count == 0) return;

            if (!_resourcesReady)
                InitResources();

            if (_lineMesh == null || _lineMaterial == null) return;

            int vertexCount = _lines.Count * 2;
            EnsureCapacity(vertexCount);

            for (int i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                int idx = i * 2;
                _vertexBuffer[idx] = line.A;
                _vertexBuffer[idx + 1] = line.B;
                _colorBuffer[idx] = line.Color;
                _colorBuffer[idx + 1] = line.Color;
            }

            _lineMesh.Clear(keepVertexLayout: true);
            _lineMesh.SetVertices(_vertexBuffer, 0, vertexCount);
            _lineMesh.SetColors(_colorBuffer, 0, vertexCount);
            _lineMesh.SetIndices(_indexBuffer, 0, vertexCount, MeshTopology.Lines, 0, false);

            var cam = Camera.main ?? UnityEngine.Object.FindObjectOfType<Camera>();
            if (cam != null)
                Graphics.DrawMesh(_lineMesh, Vector3.zero, Quaternion.identity, _lineMaterial, 0, cam);

            _lines.Clear();
        }

        private static void InitResources()
        {
            try
            {
                _lineMesh = new Mesh
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f))
                };
                _lineMesh.MarkDynamic();

                _indexBuffer = new int[50000];
                for (int i = 0; i < _indexBuffer.Length; i++) _indexBuffer[i] = i;

                var shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
                if (shader == null) throw new Exception("Shader not found");

                _lineMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _lineMaterial.SetInt("_ZWrite", 0);
                _lineMaterial.SetInt("_ZTest", 8);
                _lineMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;

                _resourcesReady = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[HitboxRenderer] Init resources failed: {ex.Message}");
                _resourcesReady = false;
            }
        }

        private static void EnsureCapacity(int size)
        {
            if (_vertexBuffer.Length >= size) return;
            int newSize = Mathf.NextPowerOfTwo(Mathf.Max(size, 16384));
            Array.Resize(ref _vertexBuffer, newSize);
            Array.Resize(ref _colorBuffer, newSize);
        }

        private static void AddLine(Vector3 a, Vector3 b)
        {
            if (_lines.Count >= 25000) return;
            _lines.Add(new Line { A = a, B = b, Color = DrawColor });
        }

        private static void AddCircle(float cx, float cy, float radius)
        {
            if (radius <= 0f) return;

            Vector3 prev = new Vector3(cx + CirclePoints[0].x * radius, cy + CirclePoints[0].y * radius, 0f);
            for (int i = 1; i <= CircleSegments; i++)
            {
                int idx = i % CircleSegments;
                Vector3 next = new Vector3(cx + CirclePoints[idx].x * radius, cy + CirclePoints[idx].y * radius, 0f);
                AddLine(prev, next);
                prev = next;
            }
        }

        private static void AddCapsule(Vector3 a, Vector3 b, float radius)
        {
            if (radius <= 0f) return;
            AddCircle(a.x, a.y, radius);
            AddCircle(b.x, b.y, radius);

            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.001f) return;
            dir /= len;
            Vector3 perp = new Vector3(-dir.y * radius, dir.x * radius, 0f);

            AddLine(a + perp, b + perp);
            AddLine(a - perp, b - perp);
        }

        public static bool OnDrawCircle(CollisionCircleComponent __instance, Circle c)
        {
            if (!Enabled || c == null) return false;
            try
            {
                var cam = CameraFlash.mainCamera;
                if (cam == null) return false;
                float localY = c.y * -1f;
                Point p = cam.getCoordInWorld(c.x + 640f, localY + 360f);
                float offset = 0f;
                if (cam.actorForTransform != null)
                    offset = (cam.px - cam.actorForTransform.px) * 2f;
                AddCircle(p.x - offset, p.y, c.radius);
            }
            catch (Exception ex) { MelonLogger.Error($"[Hitbox] OnDrawCircle error: {ex.Message}"); }
            return false;
        }

        public static bool OnDrawCircleWithVelocity(CollisionCircleComponent __instance, Circle c, float vx, float vy)
        {
            if (!Enabled || c == null) return false;
            try
            {
                var cam = CameraFlash.mainCamera;
                if (cam == null) return false;
                float localY = c.y * -1f;
                float localVY = vy * -1f;

                Point p1Raw = cam.getCoordInWorld(c.x + 640f, localY + 360f);
                float p1x = p1Raw.x;
                float p1y = p1Raw.y;

                Point p2Raw = cam.getCoordInWorld(c.x + vx + 640f, localY + localVY + 360f);
                float p2x = p2Raw.x;
                float p2y = p2Raw.y;

                float offset = 0f;
                if (cam.actorForTransform != null)
                    offset = (cam.px - cam.actorForTransform.px) * 2f;

                Vector3 a = new Vector3(p1x - offset, p1y, 0f);
                Vector3 b = new Vector3(p2x - offset, p2y, 0f);
                AddCapsule(a, b, c.radius);
            }
            catch (Exception ex) { MelonLogger.Error($"[Hitbox] OnDrawCircleWithVelocity error: {ex.Message}"); }
            return false;
        }

        public static bool OnDrawRectangle(float x, float y, float scaleX, float scaleY, float rot)
        {
            if (!Enabled) return false;
            try
            {
                var cam = CameraFlash.mainCamera;
                if (cam == null) return false;
                float localY = y * -1f;
                Point p = cam.getCoordInWorld(x + 640f, localY + 360f);
                float offset = 0f;
                if (cam.actorForTransform != null)
                    offset = (cam.px - cam.actorForTransform.px) * 2f;

                float wx = p.x - offset;
                float wy = p.y;
                float halfW = 25f * scaleX;
                float halfH = 25f * scaleY;

                Vector2[] corners = new Vector2[4];
                corners[0] = RotatePoint(-halfW, -halfH, rot);
                corners[1] = RotatePoint(halfW, -halfH, rot);
                corners[2] = RotatePoint(halfW, halfH, rot);
                corners[3] = RotatePoint(-halfW, halfH, rot);

                for (int i = 0; i < 4; i++)
                {
                    corners[i].x += wx;
                    corners[i].y += wy;
                }

                AddLine(new Vector3(corners[0].x, corners[0].y, 0f), new Vector3(corners[1].x, corners[1].y, 0f));
                AddLine(new Vector3(corners[1].x, corners[1].y, 0f), new Vector3(corners[2].x, corners[2].y, 0f));
                AddLine(new Vector3(corners[2].x, corners[2].y, 0f), new Vector3(corners[3].x, corners[3].y, 0f));
                AddLine(new Vector3(corners[3].x, corners[3].y, 0f), new Vector3(corners[0].x, corners[0].y, 0f));
            }
            catch (Exception ex) { MelonLogger.Error($"[Hitbox] OnDrawRectangle error: {ex.Message}"); }
            return false;
        }

        public static bool OnManageCol(DisplayObjectRendererTk2d __instance, DisplayObject d)
        {
            if (!Enabled || __instance == null || d == null || d.colType != 2) return true;
            try
            {
                var rectLines = __instance.rectLines;
                if (rectLines == null) return true;

                rectLines.calculate(d.transform.concatenedMatrix);

                AddLine(new Vector3(rectLines.l1.x1, rectLines.l1.y1, 0f), new Vector3(rectLines.l1.x2, rectLines.l1.y2, 0f));
                AddLine(new Vector3(rectLines.l2.x1, rectLines.l2.y1, 0f), new Vector3(rectLines.l2.x2, rectLines.l2.y2, 0f));
                AddLine(new Vector3(rectLines.l3.x1, rectLines.l3.y1, 0f), new Vector3(rectLines.l3.x2, rectLines.l3.y2, 0f));
                AddLine(new Vector3(rectLines.l4.x1, rectLines.l4.y1, 0f), new Vector3(rectLines.l4.x2, rectLines.l4.y2, 0f));
            }
            catch (Exception ex) { MelonLogger.Error($"[Hitbox] OnManageCol error: {ex.Message}"); }
            return true;
        }

        private static Vector2 RotatePoint(float x, float y, float rot)
        {
            float rad = rot * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(cos * x - sin * y, sin * x + cos * y);
        }

        private struct Line
        {
            public Vector3 A;
            public Vector3 B;
            public Color32 Color;
        }
    }
}
