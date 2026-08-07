// Copyright PlayProbe.io 2026. All rights reserved

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlayProbe
{
    /// <summary>
    /// The small floating button that lets a player open the Instant Feedback popup from anywhere in
    /// the game. The SDK spawns it from <c>Resources/PlayProbeFeedbackButton</c> when a session starts
    /// with Instant Feedback enabled, and destroys it when the session ends or consent is withdrawn.
    ///
    /// It parks itself in the corner named by <c>feedbackButtonCorner</c> in
    /// <see cref="PlayProbeConfig"/>, hides while the popup is open, and expands to show its label on
    /// hover.
    ///
    /// Don't want a floating button? Turn the prefab off (delete it from Resources) and call
    /// <see cref="PlayProbeManager.OpenFeedback"/> from your own pause menu instead.
    /// </summary>
    public class PlayProbeFeedbackButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private RectTransform panel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI label;

        [Header("Placement")]
        [Tooltip("Distance from the two screen edges of the chosen corner, in reference pixels.")]
        [SerializeField] private Vector2 margin = new Vector2(28f, 28f);

        [Header("Label")]
        [Tooltip("Text revealed when the pointer is over the button. Empty keeps it icon-only.")]
        [SerializeField] private string hoverLabel = "Feedback";

        [Tooltip("Seconds the label takes to expand and collapse.")]
        [SerializeField] private float expandSeconds = 0.15f;

        [Header("Opacity")]
        [SerializeField] private float idleAlpha = 0.55f;
        [SerializeField] private float hoverAlpha = 1f;

        private float _collapsedWidth;
        private float _expandedWidth;
        private float _progress;
        private bool _isHovered;

        private void Start()
        {
            PlayProbeUi.EnsureEventSystem();
            PlayProbeUi.ConfigureOverlayCanvas(GetComponent<Canvas>(), PlayProbeUi.SortOrderFeedbackButton,
                PlayProbeUiTheme.Default);

            if (button != null)
            {
                button.onClick.AddListener(Open);
            }

            if (label != null)
            {
                label.SetText(hoverLabel ?? string.Empty);
            }

            MeasureWidths();
            ApplyCorner();
            ApplyExpansion(0f);
        }

        private void Update()
        {
            // Hide while the popup is up, so the button does not float over its own dialog.
            bool popupOpen = PlayProbeManager.Instance != null &&
                             PlayProbeManager.Instance.Feedback != null &&
                             PlayProbeManager.Instance.Feedback.IsOpen;

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = !popupOpen;
            }

            float target = popupOpen ? 0f : (_isHovered ? 1f : 0f);
            float step = expandSeconds > 0f ? Time.unscaledDeltaTime / expandSeconds : 1f;
            float next = Mathf.MoveTowards(_progress, target, step);

            if (!Mathf.Approximately(next, _progress) || popupOpen)
            {
                _progress = next;
                ApplyExpansion(_progress);
            }

            if (canvasGroup != null)
            {
                float visibility = popupOpen ? 0f : Mathf.Lerp(idleAlpha, hoverAlpha, _progress);
                canvasGroup.alpha = visibility;
            }
        }

        /// <summary>Opens the feedback popup. Wired to the button; safe to call from your own UI.</summary>
        public void Open()
        {
            if (PlayProbeManager.Instance != null)
            {
                PlayProbeManager.Instance.OpenFeedback();
            }
        }

        /// <inheritdoc />
        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
        }

        /// <inheritdoc />
        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
        }

        // The collapsed state is a square the height of the panel; the expanded state adds room for the
        // label. Measured once at Start from the values the prefab was built with.
        private void MeasureWidths()
        {
            if (panel == null)
            {
                return;
            }

            _expandedWidth = panel.sizeDelta.x;
            _collapsedWidth = panel.sizeDelta.y;

            if (string.IsNullOrWhiteSpace(hoverLabel) || _expandedWidth <= _collapsedWidth)
            {
                _expandedWidth = _collapsedWidth;
            }
        }

        private void ApplyExpansion(float t)
        {
            if (panel == null)
            {
                return;
            }

            // Ease-out so the label springs open and settles rather than sliding linearly.
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            Vector2 size = panel.sizeDelta;
            size.x = Mathf.Lerp(_collapsedWidth, _expandedWidth, eased);
            panel.sizeDelta = size;

            if (label != null)
            {
                Color color = label.color;
                color.a = eased;
                label.color = color;
            }
        }

        private void ApplyCorner()
        {
            if (panel == null)
            {
                return;
            }

            FeedbackButtonCorner corner = PlayProbeManager.Instance != null
                ? PlayProbeManager.Instance.FeedbackButtonCorner
                : FeedbackButtonCorner.BottomRight;

            switch (corner)
            {
                case FeedbackButtonCorner.BottomLeft:
                    SetAnchors(panel, new Vector2(0f, 0f), new Vector2(margin.x, margin.y));
                    break;
                default:
                    SetAnchors(panel, new Vector2(1f, 0f), new Vector2(-margin.x, margin.y));
                    break;
            }
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchor, Vector2 offset)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = offset;
        }
    }
}
