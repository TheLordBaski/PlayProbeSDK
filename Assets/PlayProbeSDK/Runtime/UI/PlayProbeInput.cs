// Copyright PlayProbe.io 2026. All rights reserved

using System;
using System.Reflection;
using UnityEngine;

namespace PlayProbe
{
    /// <summary>
    /// Keyboard checks that work under either input backend, without the SDK taking a hard dependency
    /// on <c>com.unity.inputsystem</c>.
    ///
    /// A project using the new Input System exclusively throws an <c>InvalidOperationException</c> from
    /// <c>UnityEngine.Input</c>, and a project on the legacy manager has no <c>Keyboard.current</c> —
    /// so the SDK's UI cannot just pick one. But naming <c>Unity.InputSystem</c> in the assembly
    /// definition would stop the SDK compiling in every project that does not have that package
    /// installed, which is most of them. So the new backend is reached by reflection, resolved once and
    /// cached.
    ///
    /// The cost is a handful of cached <c>MethodInfo</c> invocations per frame, and only while one of
    /// PlayProbe's own modal screens is open. Everything here degrades to "not pressed" rather than
    /// throwing into the game's update loop.
    /// </summary>
    internal static class PlayProbeInput
    {
        private const string KeyboardTypeName = "UnityEngine.InputSystem.Keyboard, Unity.InputSystem";

        private static bool _resolved;
        private static PropertyInfo _keyboardCurrent;
        private static PropertyInfo _escapeKey;
        private static PropertyInfo _backspaceKey;
        private static PropertyInfo _wasPressedThisFrame;

        /// <summary>True on the frame Escape goes down. Used to dismiss PlayProbe's modal screens.</summary>
        internal static bool WasCancelPressedThisFrame()
        {
            if (TryReadNewInputSystemKey(_escapeKey, out bool pressed))
            {
                return pressed;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        /// <summary>True on the frame Backspace goes down.</summary>
        internal static bool WasBackspacePressedThisFrame()
        {
            if (TryReadNewInputSystemKey(_backspaceKey, out bool pressed))
            {
                return pressed;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Backspace);
#else
            return false;
#endif
        }

        /// <summary>
        /// The system clipboard, or an empty string when it cannot be read. <c>GUIUtility</c> throws on
        /// some platforms (notably WebGL) rather than returning null.
        /// </summary>
        internal static string ReadClipboard()
        {
            try
            {
                return GUIUtility.systemCopyBuffer ?? string.Empty;
            }
            catch (Exception exception)
            {
                Debug.Log($"[PlayProbe] Clipboard is unavailable on this platform: {exception.Message}");
                return string.Empty;
            }
        }

        // Returns false when the new Input System is not present or no keyboard is connected, which is
        // the signal to fall back to the legacy manager.
        private static bool TryReadNewInputSystemKey(PropertyInfo keyProperty, out bool pressed)
        {
            pressed = false;

            EnsureResolved();

            if (keyProperty == null || _keyboardCurrent == null || _wasPressedThisFrame == null)
            {
                return false;
            }

            try
            {
                object keyboard = _keyboardCurrent.GetValue(null);
                if (keyboard == null)
                {
                    return false;
                }

                object key = keyProperty.GetValue(keyboard);
                if (key == null)
                {
                    return false;
                }

                pressed = (bool)_wasPressedThisFrame.GetValue(key);
                return true;
            }
            catch (Exception exception)
            {
                // Whatever went wrong, stop trying: fall back to the legacy path for the rest of the run
                // rather than throwing the same exception every frame.
                Debug.LogWarning($"[PlayProbe] Input System probe failed, using legacy input: {exception.Message}");
                _keyboardCurrent = null;
                return false;
            }
        }

        private static void EnsureResolved()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;

            Type keyboardType = Type.GetType(KeyboardTypeName);
            if (keyboardType == null)
            {
                return;
            }

            _keyboardCurrent = keyboardType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            _escapeKey = keyboardType.GetProperty("escapeKey", BindingFlags.Public | BindingFlags.Instance);
            _backspaceKey = keyboardType.GetProperty("backspaceKey", BindingFlags.Public | BindingFlags.Instance);

            // ButtonControl.wasPressedThisFrame — read off the key instance's own type so this keeps
            // working if the control class is ever moved.
            Type buttonControlType = Type.GetType("UnityEngine.InputSystem.Controls.ButtonControl, Unity.InputSystem");
            _wasPressedThisFrame = buttonControlType?.GetProperty(
                "wasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance);
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic(){
            _resolved = false;
            _keyboardCurrent = null;
            _escapeKey = null;
            _backspaceKey = null;
            _wasPressedThisFrame = null;
        }
    }
}
