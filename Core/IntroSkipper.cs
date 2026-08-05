using Il2Cpp;
using MelonLoader;

namespace jsb_new
{
    public static class IntroSkipper
    {
        private static bool _introSkipped = false;

        public static void Update()
        {
            if (MainGame.instance == null) return;

            if (!_introSkipped)
                SkipLongIntro();
        }

        private static void SkipLongIntro()
        {
            _introSkipped = true;
            try
            {
                MainGame.hardKillGameOnNextStableMoment(new Callback(new System.Action(UI_MainMenu02.loadAndCreateSkipIntro)));
                DebugStrings.Log("Intro skipped: success");
            }
            catch (System.Exception ex)
            {
                _introSkipped = false;
                MelonLogger.Error($"==== INTRO SKIPPED: FAILED - {ex.Message} ====");
            }
        }
    }
}
