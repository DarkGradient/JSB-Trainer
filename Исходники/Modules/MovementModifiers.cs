// Mouse Control, Noclip, AutoDash, Dash Cooldown
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace jsb_new
{
    // --- 1. MOUSE CONTROL ---
    public static class MouseControl
    {
        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroControl_Update));
            ModuleRegistry.RegisterCheckbox("Mouse Control", () => ModuleRegistry.IsActive("MouseControl"), (v) => ModuleRegistry.SetActive("MouseControl", v));
        }

        [HarmonyPatch(typeof(HeroControl), "update")]
        private static class Patch_HeroControl_Update
        {
            static void Postfix(HeroControl __instance)
            {
                if (!ModuleRegistry.IsActive("MouseControl") || HeroControl.PAUSE_CONTROL) return;
                Hero hero = __instance.hero;
                if (hero == null || hero.heroInputLocal == null || !hero.heroInputLocal.enabled) return;

                Point mousePos = KeyManager.mousePos;
                if (mousePos == null || CameraFlash.mainCamera == null) return;

                float zoom = CameraFlash.mainCamera.zoomScale > 0.001f ? CameraFlash.mainCamera.zoomScale : 1f;
                Vector2 gamePos = ResolutionManager.screenToGamePos(mousePos);

                hero.px = CameraFlash.mainCamera.px + (gamePos.x - 640f) / zoom;
                hero.py = CameraFlash.mainCamera.py + (gamePos.y - 360f) / zoom;
                if (hero.physicComponent != null) { hero.physicComponent.vx = 0f; hero.physicComponent.vy = 0f; }
            }
        }
    }

    // --- 2. NOCLIP ---
    public static class Noclip
    {
        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(HeroCollisionWithEnemy), "onCollision"), prefix: new HarmonyMethod(typeof(Noclip), nameof(OnCollision)));
            ModuleRegistry.RegisterCheckbox("Noclip", () => ModuleRegistry.IsActive("Noclip"), (v) => ModuleRegistry.SetActive("Noclip", v));

            HUDManager.CreateHUD("Noclip", () => "NOCLIP ON", Color.white, Color.red, () => ModuleRegistry.IsActive("Noclip"));
        }

        public static void Update()
        {
            if (Input.GetKeyDown(KeyCode.N)) ModuleRegistry.SetActive("Noclip", !ModuleRegistry.IsActive("Noclip"));
        }

        public static bool OnCollision() => !ModuleRegistry.IsActive("Noclip");
    }

    // --- 3. AUTO DASH ---
    public static class AutoDash
    {
        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(HeroInputLocal), "listenToController"), postfix: new HarmonyMethod(typeof(AutoDash), nameof(OnListen)));
            ModuleRegistry.RegisterCheckbox("Auto Dash", () => ModuleRegistry.IsActive("Auto Dash"), (v) => ModuleRegistry.SetActive("Auto Dash", v));
        }

        private static void OnListen(HeroInputLocal __instance)
        {
            if (!ModuleRegistry.IsActive("Auto Dash") || __instance?.metaPlayer == null) return;
            if (__instance.metaPlayer.isActionDown(ModelControlActionEnum.DASH))
            {
                __instance.hero.controlComponent.isDashing = true;
            }
        }
    }

    // --- 4. DASH COOLDOWN ---
    public static class DashCooldown
    {
        public static float DashCooldownValue = 10f;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(HeroDashComponent), "update"), postfix: new HarmonyMethod(typeof(DashCooldown), nameof(Postfix_Update)));
            ModuleRegistry.RegisterSlider("Dash Cooldown", 0f, 1f, 1.0f, (val) => DashCooldownValue = val * 10f);
        }

        public static void Postfix_Update(HeroDashComponent __instance)
        {
            if (__instance.dashCooldown != null) __instance.dashCooldown.waitMax = DashCooldownValue;
        }
    }
}
