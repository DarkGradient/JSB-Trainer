using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using Il2CppInterop.Runtime;

namespace jsb_new
{
    public class Aberration : MonoBehaviour
    {
        public static Aberration Instance { get; private set; }

        private Material _material;
        private bool _enabled = true;

        public static void Initialize()
        {
            // Создаём компонент на главной камере
            var cam = Camera.main ?? FindObjectOfType<Camera>();
            if (cam == null)
            {
                DebugStrings.Log("Aberration: camera not found");
                return;
            }

            Instance = cam.gameObject.AddComponent<Aberration>();
            DebugStrings.Log("Aberration: strong chromatic aberration initialized");
        }

        private void Awake()
        {
            CreateMaterial();
        }

        private void CreateMaterial()
        {
            var shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
            _material = new Material(shader);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!_enabled || _material == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            // === СИЛЬНАЯ ХРОМАТИЧЕСКАЯ АБЕРРАЦИЯ ===
            _material.SetFloat("_Strength", 42f);   // очень заметно
            _material.SetFloat("_Dist", 1.2f);

            Graphics.Blit(source, destination, _material);
        }

        private void OnDestroy()
        {
            if (_material != null)
                DestroyImmediate(_material);
        }
    }
}
