using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace jsb_new
{
    public static class AutoCollision
    {
        public static bool Enabled = false;

        // Неоново-розовый цвет для отрисовки полигонов опасности
        private static readonly Color32 RadarColor = new Color32(255, 0, 128, 255);

        public static void Update()
        {
            if (!Enabled) return;

            try
            {
                // Находим все базовые спрайты 2D Toolkit на сцене
                var sprites = UnityEngine.Object.FindObjectsOfType<tk2dBaseSprite>();
                if (sprites == null) return;

                for (int s = 0; s < sprites.Length; s++)
                {
                    var sprite = sprites[s];
                    if (sprite == null || !sprite.enabled || !sprite.gameObject.activeInHierarchy)
                        continue;

                    Color col = sprite.color;
                    // Проверяем, розовый ли это объект (высокий красный и синий, низкий зеленый)
                    bool isPink = (col.r > 0.7f && col.g < 0.3f && col.b > 0.3f);
                    if (!isPink)
                        continue;

                    var filter = sprite.GetComponent<MeshFilter>();
                    if (filter == null)
                        continue;

                    var mesh = filter.sharedMesh;
                    if (mesh == null)
                        continue;

                    Vector3[] vertices = mesh.vertices;
                    int[] triangles = mesh.triangles;
                    if (vertices == null || triangles == null)
                        continue;

                    Matrix4x4 localToWorld = sprite.transform.localToWorldMatrix;

                    // Обходим треугольники меша и рисуем их ребра напрямую в мире
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        if (i + 2 >= triangles.Length) break;

                        // Переводим локальные вершины спрайта в мировые координаты Unity
                        Vector3 v1 = localToWorld.MultiplyPoint3x4(vertices[triangles[i]]);
                        Vector3 v2 = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                        Vector3 v3 = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 2]]);

                        // Рисуем полигональный каркас реальной формы розового объекта
                        HitboxRenderer.AddLine(v1, v2, RadarColor);
                        HitboxRenderer.AddLine(v2, v3, RadarColor);
                        HitboxRenderer.AddLine(v3, v1, RadarColor);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AutoCollision] Update error: {ex.Message}");
            }
        }
    }
}
