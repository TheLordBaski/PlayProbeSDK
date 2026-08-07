// Copyright PlayProbe.io 2026. All rights reserved

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayProbe
{
    /// <summary>
    /// A short confirmation message that slides in, holds, and fades away — "Thanks, your feedback was
    /// sent." The SDK shows one after a survey or feedback submission so the player gets an
    /// acknowledgement instead of the popup just vanishing.
    ///
    /// Show one from your own code with:
    /// <code>
    /// PlayProbeToast.Show("Saved!");
    /// PlayProbeToast.Show("Could not reach the server.", isError: true);
    /// </code>
    ///
    /// The prefab lives at <c>Resources/PlayProbeToast</c>. If it is missing, <see cref="Show"/> logs
    /// the message and returns rather than failing.
    /// </summary>
    public class PlayProbeToast : MonoBehaviour
    {
        private const string ResourcePath = "PlayProbeToast";

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Image accentBar;

        [Tooltip("Seconds the message stays fully visible, excluding the fades.")]
        [SerializeField] private float holdSeconds = 2.5f;

        [Tooltip("Seconds each of the fade-in and fade-out takes.")]
        [SerializeField] private float fadeSeconds = 0.2f;

        [Tooltip("How far the toast rises while fading in, in reference pixels.")]
        [SerializeField] private float riseDistance = 24f;

        private static PlayProbeToast _current;

        /// <summary>
        /// Shows a message. A second call replaces the message already on screen rather than stacking,
        /// so a burst of events cannot bury the game under toasts.
        /// </summary>
        /// <param name="message">The text to display. Empty messages are ignored.</param>
        /// <param name="isError">Colours the accent bar with the theme's danger colour.</param>
        public static void Show(string message, bool isError = false)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (_current != null)
            {
                _current.Restart(message, isError);
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                // Not fatal: the confirmation is a nicety, not part of the data path.
                Debug.Log($"[PlayProbe] {message}");
                return;
            }

            PlayProbeUi.EnsureEventSystem();

            GameObject instance = Instantiate(prefab);
            DontDestroyOnLoad(instance);

            PlayProbeToast toast = instance.GetComponent<PlayProbeToast>();
            if (toast == null)
            {
                Debug.LogWarning("[PlayProbe] The PlayProbeToast prefab has no PlayProbeToast component.");
                Destroy(instance);
                return;
            }

            _current = toast;
            toast.Restart(message, isError);
        }

        private Coroutine _routine;
        private RectTransform _rect;
        private Vector2 _restPosition;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            if (_rect.childCount > 0 && _rect.GetChild(0) is RectTransform panel)
            {
                _rect = panel;
            }

            _restPosition = _rect.anchoredPosition;
        }

        private void OnDestroy()
        {
            if (_current == this)
            {
                _current = null;
            }
        }

        private void Restart(string message, bool isError)
        {
            if (label != null)
            {
                label.SetText(message);
            }

            if (accentBar != null)
            {
                PlayProbeUiTheme theme = PlayProbeUiTheme.Default;
                accentBar.color = isError ? theme.danger : theme.success;
            }

            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            _routine = StartCoroutine(PlayRoutine());
        }

        // Every timing here uses unscaled time: the feedback popup pauses the game with
        // Time.timeScale = 0, and a toast that never advanced would sit on screen forever.
        private IEnumerator PlayRoutine()
        {
            yield return Fade(0f, 1f, riseDistance, 0f);

            float remaining = holdSeconds;
            while (remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            yield return Fade(1f, 0f, 0f, riseDistance * 0.5f);

            Destroy(gameObject);
        }

        private IEnumerator Fade(float fromAlpha, float toAlpha, float fromOffset, float toOffset)
        {
            float duration = Mathf.Max(0.0001f, fadeSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease-out cubic: fast at the start, settles gently. Matches the web app's motion.
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
                }

                if (_rect != null)
                {
                    _rect.anchoredPosition =
                        _restPosition + new Vector2(0f, Mathf.Lerp(fromOffset, toOffset, eased));
                }

                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = toAlpha;
            }
        }
    }
}
