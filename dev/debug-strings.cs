using MelonLoader;

namespace jsb_new
{
    // Централизованный вывод отладочных сообщений.
    // Раньше это были разбросанные по всем модулям MelonLogger.Msg/Warning —
    // теперь любой "некритичный" лог (тумблеры настроек, подтверждения
    // инициализации подмодулей, шумные события в Update) идёт сюда.
    // MelonLogger.Error по-прежнему остаётся в местах вызова как есть —
    // критические ошибки должны быть видны всегда, без переключателя.
    public static class DebugStrings
    {
        // Переключи на true, когда нужен подробный лог всех подмодулей.
        public static bool Enabled = true;

        public static void Log(string message)
        {
            if (Enabled)
                MelonLogger.Msg($"[DEBUG] {message}");
        }
    }
}
