using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace HangingWoman
{
    /// <summary>Keys for the recording and cutscene tools, on either input backend.</summary>
    public static class GameInput
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        static bool Down(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        public static bool RecordPressed() => Down(Key.R);
        public static bool MarkerPressed() => Down(Key.M);
        public static bool PlayCutscenePressed() => Down(Key.P);
        public static bool SkipPressed() => Down(Key.Space) || Down(Key.Enter);
        /// <summary>Scroll in wheel notches (positive = away from you).</summary>
        public static float Scroll() => Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
        public static bool RecordPressed() => Input.GetKeyDown(KeyCode.R);
        public static bool MarkerPressed() => Input.GetKeyDown(KeyCode.M);
        public static bool PlayCutscenePressed() => Input.GetKeyDown(KeyCode.P);
        public static bool SkipPressed() => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
        public static float Scroll() => Input.mouseScrollDelta.y;
#endif
    }
}
