// Copyright PlayProbe.io 2026. All rights reserved

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PlayProbe
{
    /// <summary>
    /// Shared plumbing for the built-in PlayProbe screens: canvas layering and making sure the scene
    /// can actually receive clicks. Internal — games do not need to call any of this.
    /// </summary>
    internal static class PlayProbeUi
    {
        // Canvas sort orders. Deliberately high so PlayProbe UI draws over game HUDs, and ordered so
        // that a screen which interrupts another (a toast over a survey) wins.
        internal const int SortOrderFeedbackButton = 4900;
        internal const int SortOrderSurvey = 5000;
        internal const int SortOrderFeedback = 5100;
        internal const int SortOrderConsent = 5200;
        internal const int SortOrderTokenScreen = 5300;
        internal const int SortOrderToast = 5400;

        // Set once we have created our own EventSystem, so we never create a second one.
        private static GameObject _ownedEventSystem;

        /// <summary>
        /// Makes sure the scene has an EventSystem, creating one if the game does not ship its own.
        /// Without this, a game with no uGUI of its own would show PlayProbe's UI but ignore every
        /// click on it.
        /// </summary>
        internal static void EnsureEventSystem()
        {
            if (EventSystem.current != null || _ownedEventSystem != null)
            {
                return;
            }

#if UNITY_2023_1_OR_NEWER
            EventSystem existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
#else
            EventSystem existing = UnityEngine.Object.FindObjectOfType<EventSystem>();
#endif
            if (existing != null)
            {
                return;
            }

            GameObject host = new GameObject("PlayProbeEventSystem", typeof(EventSystem));
            UnityEngine.Object.DontDestroyOnLoad(host);
            AttachInputModule(host);
            _ownedEventSystem = host;
        }

        // The correct input module depends on which input backend the project enabled. Referencing
        // InputSystemUIInputModule directly would make the SDK's assembly definition depend on
        // com.unity.inputsystem, which most projects do not have — so it is resolved by name and only
        // when the new backend is actually compiled in.
        private static void AttachInputModule(GameObject host)
        {
#if ENABLE_INPUT_SYSTEM
            Type inputSystemModule = Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

            if (inputSystemModule != null)
            {
                host.AddComponent(inputSystemModule);
                return;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            host.AddComponent<StandaloneInputModule>();
            return;
#else
            Debug.LogWarning(
                "[PlayProbe] Could not add an input module to the EventSystem. PlayProbe's UI will " +
                "render but not respond to clicks. Add an EventSystem to your scene manually.");
#endif
        }

        /// <summary>
        /// Applies the standard modal-overlay setup to a canvas: screen-space overlay, the given sort
        /// order, and a scaler that matches the theme's reference resolution.
        /// </summary>
        internal static void ConfigureOverlayCanvas(Canvas canvas, int sortOrder, PlayProbeUiTheme theme)
        {
            if (canvas == null)
            {
                return;
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortOrder;

            UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null && theme != null)
            {
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = theme.referenceResolution;
                scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        /// <summary>
        /// Opens a URL in the player's browser, refusing anything that is not http(s). Guards against a
        /// mistyped or hostile config value turning a "Privacy policy" button into an arbitrary
        /// <c>Application.OpenURL</c> — which on desktop can launch local files and custom schemes.
        /// </summary>
        internal static void OpenExternalUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            string trimmed = url.Trim();

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                Debug.LogWarning($"[PlayProbe] Refusing to open '{trimmed}': only http and https URLs are allowed.");
                return;
            }

            Application.OpenURL(parsed.AbsoluteUri);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic(){
            _ownedEventSystem = null;
        }
    }
}
