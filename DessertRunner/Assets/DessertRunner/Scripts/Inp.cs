using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DessertRunner
{
    /// <summary>Keyboard input that works with the old Input Manager AND the new Input System package.</summary>
    public static class Inp
    {
        public static bool Left, Right, Jump, Slide, Pause, Submit, Back;

        public static void Poll()
        {
            Left = Right = Jump = Slide = Pause = Submit = Back = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            Left   = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
            Right  = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
            Jump   = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Space);
            Slide  = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
            Pause  = Input.GetKeyDown(KeyCode.Escape);
            Submit = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
            Back   = Input.GetKeyDown(KeyCode.Escape);
#elif ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return;
            Left   = k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame;
            Right  = k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame;
            Jump   = k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame;
            Slide  = k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame;
            Pause  = k.escapeKey.wasPressedThisFrame;
            Submit = k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame;
            Back   = k.escapeKey.wasPressedThisFrame;
#endif
        }
    }
}
