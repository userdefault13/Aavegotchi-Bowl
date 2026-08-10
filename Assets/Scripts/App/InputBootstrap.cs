using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroBowl.App
{
    /// <summary>
    /// DualShock4 HID on macOS can emit multi‑MB state floods per frame and trip
    /// InputSystem's default event budget. Lift the cap early so pad input stays usable.
    /// </summary>
    public static class InputBootstrap
    {
        static bool configured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ConfigureOnLoad() => EnsureConfigured();

        public static void EnsureConfigured()
        {
            if (configured) return;
            configured = true;

            // 0 = unlimited (Unity's recommended escape hatch for noisy HID pads).
            InputSystem.settings.maxEventBytesPerUpdate = 0;
            InputSystem.settings.maxQueuedEventsPerUpdate = 1000;
        }
    }
}
