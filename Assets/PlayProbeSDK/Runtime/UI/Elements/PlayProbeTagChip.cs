// Copyright PlayProbe.io 2026. All rights reserved

using PlayProbe.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayProbe
{
    /// <summary>
    /// One selectable tag pill inside a <see cref="PlayProbeTagSelector"/> — "Combat", "UI/UX",
    /// "Performance". Clicking toggles it.
    ///
    /// You normally do not create these yourself: the selector spawns one per entry of
    /// <see cref="PlayProbeManager.AnswerTags"/>. Swap the prefab on the selector to restyle them.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class PlayProbeTagChip : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Image background;

        [Tooltip("Optional ring image that brightens when the chip is picked. It is a separate Image " +
                 "using the pill outline sprite, because uGUI has no stroke and its Outline effect " +
                 "smears rather than strokes on a rounded shape.")]
        [SerializeField] private Image border;

        private PlayProbeTagSelector _owner;
        private bool _isSelected;

        /// <summary>The tag this chip stands for. Null until the selector binds it.</summary>
        public AnswerTag Tag { get; private set; }

        /// <summary>Whether the player has this tag turned on.</summary>
        public bool IsSelected => _isSelected;

        /// <summary>
        /// Binds the chip to a tag and its owning selector. Called by
        /// <see cref="PlayProbeTagSelector"/> right after the chip is spawned.
        /// </summary>
        public void Bind(AnswerTag tag, PlayProbeTagSelector owner)
        {
            Tag = tag;
            _owner = owner;

            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (label != null)
            {
                label.SetText(tag != null ? tag.label : string.Empty);
            }

            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);

            SetSelected(false);
        }

        /// <summary>
        /// Sets the visual and logical state without going through the owner. Use
        /// <see cref="PlayProbeTagSelector.SetSelected"/> if you want the selector's limits enforced.
        /// </summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            ApplyVisualState();
        }

        /// <summary>Greys the chip out — used when the selection limit has been reached.</summary>
        public void SetInteractable(bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }

            ApplyVisualState();
        }

        private void HandleClick()
        {
            if (_owner == null)
            {
                SetSelected(!_isSelected);
                return;
            }

            _owner.Toggle(this);
        }

        private void ApplyVisualState()
        {
            PlayProbeUiTheme theme = PlayProbeUiTheme.Default;
            bool dimmed = button != null && !button.interactable;

            if (background != null)
            {
                Color fill = _isSelected
                    // A tinted wash rather than solid primary: a row of solid brand-coloured pills
                    // reads as "everything is selected" at a glance.
                    ? new Color(theme.primary.r, theme.primary.g, theme.primary.b, 0.22f)
                    : theme.surfaceRaised;

                background.color = dimmed ? WithAlpha(fill, fill.a * 0.4f) : fill;
            }

            if (border != null)
            {
                Color stroke = _isSelected ? theme.primary : theme.border;
                border.color = dimmed ? WithAlpha(stroke, stroke.a * 0.4f) : stroke;
            }

            if (label != null)
            {
                Color text = _isSelected ? theme.textPrimary : theme.textMuted;
                label.color = dimmed ? WithAlpha(text, text.a * 0.5f) : text;
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
