using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace jsb_new
{
    public static class MouseControl
    {
        public static bool MouseMoveEnabled
        {
            get => ModuleRegistry.IsActive("MouseControl");
            set => ModuleRegistry.SetActive("MouseControl", value);
        }

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroInputLocal_Update));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroControl_Update));

            ModuleRegistry.RegisterCheckbox("Gamemodes", "Mouse Control",
                                            () => MouseMoveEnabled,
                                            (newValue) =>
                                            {
                                                MouseMoveEnabled = newValue;
                                                DebugStrings.Log($"MouseControl changed to: {newValue}");
                                            },
                                            isLocked: () => ModuleRegistry.IsActive("OrangeSoul") || ModuleRegistry.IsActive("PurpleSoul") || ModuleRegistry.IsActive("OneHit"),
                                            order: 40
            );
        }

        [HarmonyPatch(typeof(HeroInputLocal), "update")]
        private static class Patch_HeroInputLocal_Update
        {
            static void Postfix(HeroInputLocal __instance)
            {
                if (!MouseMoveEnabled || !__instance.enabled || ModuleRegistry.IsActive("OrangeSoul") || ModuleRegistry.IsActive("PurpleSoul"))
                    return;

                Hero hero = __instance.hero;
                if (hero == null || hero.controlComponent == null)
                    return;

                MetaPlayer mp = __instance.metaPlayer;
                if (mp == null && PlayerManager.instance != null)
                {
                    mp = PlayerManager.instance.getFromModel(hero.modelPlayer);
                }

                if (mp == null || !mp.isLocalMainPlayer())
                    return;

                Point mousePos = KeyManager.mousePos;
                if (mousePos == null)
                    return;

                float cameraX = 0f;
                float cameraY = 0f;
                float zoom = 1f;

                if (CameraFlash.mainCamera != null)
                {
                    cameraX = CameraFlash.mainCamera.px;
                    cameraY = CameraFlash.mainCamera.py;
                    if (CameraFlash.mainCamera.zoomScale > 0.001f)
                    {
                        zoom = CameraFlash.mainCamera.zoomScale;
                    }
                }

                Vector2 gamePos = ResolutionManager.screenToGamePos(mousePos);

                float targetX = cameraX + (gamePos.x - 640f) / zoom;
                float targetY = cameraY + (gamePos.y - 360f) / zoom;

                float dx = targetX - hero.px;
                float dy = targetY - hero.py;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > 1f)
                {
                    hero.controlComponent.moveInput.x = dx / dist;
                    hero.controlComponent.moveInput.y = dy / dist;
                }
                else
                {
                    hero.controlComponent.moveInput.x = 0f;
                    hero.controlComponent.moveInput.y = 0f;
                }
            }
        }

        [HarmonyPatch(typeof(HeroControl), "update")]
        private static class Patch_HeroControl_Update
        {
            static void Postfix(HeroControl __instance)
            {
                if (!MouseMoveEnabled || HeroControl.PAUSE_CONTROL || ModuleRegistry.IsActive("OrangeSoul") || ModuleRegistry.IsActive("PurpleSoul"))
                    return;

                Hero hero = __instance.hero;
                if (hero == null)
                    return;

                if (hero.heroInputLocal == null || !hero.heroInputLocal.enabled)
                    return;

                MetaPlayer mp = hero.metaPlayer;
                if (mp == null && PlayerManager.instance != null)
                {
                    mp = PlayerManager.instance.getFromModel(hero.modelPlayer);
                }

                if (mp == null || !mp.isLocalMainPlayer())
                    return;

                Point mousePos = KeyManager.mousePos;
                if (mousePos == null)
                    return;

                float cameraX = 0f;
                float cameraY = 0f;
                float zoom = 1f;

                if (CameraFlash.mainCamera != null)
                {
                    cameraX = CameraFlash.mainCamera.px;
                    cameraY = CameraFlash.mainCamera.py;
                    if (CameraFlash.mainCamera.zoomScale > 0.001f)
                    {
                        zoom = CameraFlash.mainCamera.zoomScale;
                    }
                }

                Vector2 gamePos = ResolutionManager.screenToGamePos(mousePos);

                float targetX = cameraX + (gamePos.x - 640f) / zoom;
                float targetY = cameraY + (gamePos.y - 360f) / zoom;

                hero.px = targetX;
                hero.py = targetY;

                if (hero.physicComponent != null)
                {
                    hero.physicComponent.vx = 0f;
                    hero.physicComponent.vy = 0f;
                }
            }
        }
    }
}
