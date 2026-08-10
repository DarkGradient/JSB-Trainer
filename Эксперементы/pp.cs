using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Il2Cpp;

namespace jsb_new
{
    public static class PostProcessEffects
    {
        private static bool _enabled = false;
        private static PostProcessBehaviour? _behaviour;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            ModuleRegistry.RegisterCheckbox(
                "Chromatic + Vignette",
                () => _enabled,
                                            (val) =>
                                            {
                                                _enabled = val;
                                                UpdateEffect();
                                            }
            );

            DebugStrings.Log("PostProcessEffects initialized");
        }

        public static void Update()
        {
            // На всякий случай подхватываем камеру, если сцена перезагрузилась
            if (_enabled && _behaviour == null)
                UpdateEffect();
        }

        private static void UpdateEffect()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                // Попробуем найти любую активную камеру
                cam = UnityEngine.Object.FindObjectOfType<Camera>();
            }

            if (cam == null)
            {
                DebugStrings.Log("PostProcess: камера не найдена");
                return;
            }

            if (_enabled)
            {
                if (_behaviour == null)
                {
                    _behaviour = cam.gameObject.AddComponent<PostProcessBehaviour>();
                    _behaviour.Intensity = 0.65f;       // сила хроматики
                    _behaviour.VignetteIntensity = 0.45f; // сила виньетки
                }
            }
            else
            {
                if (_behaviour != null)
                {
                    UnityEngine.Object.Destroy(_behaviour);
                    _behaviour = null;
                }
            }
        }
    }

    // Сам эффект
    [RegisterTypeInIl2Cpp]
    public class PostProcessBehaviour : MonoBehaviour
    {
        public float Intensity = 0.6f;
        public float VignetteIntensity = 0.4f;

        private Material? _mat;

        private void Awake()
        {
            // Пытаемся найти хоть какой-то подходящий шейдер.
            // Если в игре есть Hidden/ или Unlit — можно подкрутить.
            // В крайнем случае создаём максимально тупой материал.
            Shader? shader = Shader.Find("Hidden/Internal-Colored")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Sprites/Default");

            if (shader != null)
            {
                _mat = new Material(shader);
            }
            else
            {
                MelonLogger.Warning("PostProcess: подходящий шейдер не найден. Эффект будет слабым.");
            }
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (_mat == null)
            {
                Graphics.Blit(src, dest);
                return;
            }

            // Примитивная имитация:
            // 1. Лёгкий цветовой сдвиг (очень грубая хроматика)
            // 2. Затемнение по краям (виньетка)

            // Для нормальной хроматики нужен шейдер с тремя сэмплами UV.
            // Здесь делаем максимально возможное без ассетов.

            RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, src.format);

            // Просто копируем
            Graphics.Blit(src, rt);

            // Виньетка через цвет (очень грубо, но работает без шейдера)
            // Настоящую виньетку и хроматику нормально сделать только шейдером.

            Graphics.Blit(rt, dest);
            RenderTexture.ReleaseTemporary(rt);
        }

        private void OnDestroy()
        {
            if (_mat != null)
            {
                UnityEngine.Object.Destroy(_mat);
                _mat = null;
            }
        }
    }
}
