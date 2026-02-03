#if INJECTION_SHIM
using System;
namespace ForegroundBotRunner
{
    // Minimal placeholder so the injected net48 DLL loads without referencing net8.0-only projects.
    public static class ShimEntry
    {
        // Loader will reflect for some known method; keep a no-op safe entry.
        public static void Initialize()
        {
            // Intentionally blank – real logic lives in net8.0 build inside hosting environment.
        }
    }
}
#endif
