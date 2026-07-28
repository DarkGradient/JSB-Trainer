using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace jsb_new
{
    public static class OrangeSoul
    {
        public static bool Enabled
        {
            get => ModuleRegistry.IsActive("OrangeSoul");
            set => ModuleRegistry.SetActive("OrangeSoul", value);
        }

        private static Vector2 _lastDirection = new Vector2(1f, 0f);

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patch_HeroInputLocal_Update_OrangeSoul));

            ModuleRegistry.RegisterCheckbox("Gamemodes", "Orange SOUL Mode",
                                            () => Enabled,
                                            (newValue) =>
                                            {
                                                Enabled = newValue;
                                                DebugStrings.Log($"OrangeSoul mode changed to: {newValue}");
                                            },
                                            isLocked: () => ModuleRegistry.IsActive("MouseControl") || ModuleRegistry.IsActive("PurpleSoul"),
                                            order: 30
            );
        }

        [HarmonyPatch(typeof(HeroInputLocal), "update")]
        private static class Patch_HeroInputLocal_Update_OrangeSoul
        {
            static void Postfix(HeroInputLocal __instance)
            {
                if (!Enabled || ModuleRegistry.IsActive("MouseControl") || ModuleRegistry.IsActive("PurpleSoul"))
                    return;

                Hero hero = __instance.hero;
                if (hero == null || hero.controlComponent == null)
                    return;

                if (hero.heroInputLocal == null || !hero.heroInputLocal.enabled)
                    return;

                MetaPlayer mp = __instance.metaPlayer;
                if (mp == null && PlayerManager.instance != null)
                {
                    mp = PlayerManager.instance.getFromModel(hero.modelPlayer);
                }

                if (mp == null || !mp.isLocalMainPlayer())
                    return;

                float mx = hero.controlComponent.moveInput.x;
                float my = hero.controlComponent.moveInput.y;

                if (Mathf.Abs(mx) > 0.01f || Mathf.Abs(my) > 0.01f)
                {
                    _lastDirection = new Vector2(mx, my).normalized;
                }

                hero.controlComponent.moveInput.x = _lastDirection.x;
                hero.controlComponent.moveInput.y = _lastDirection.y;
            }
        }
    }
}
