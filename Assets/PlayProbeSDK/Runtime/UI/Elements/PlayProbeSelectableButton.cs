// Copyright PlayProbe.io 2026. All rights reserved

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayProbe
{
    /// <summary>
    /// A button that remembers whether it is the chosen one. Used wherever the player picks from a
    /// set: rating scales, yes/no, multiple choice, and the feedback categories.
    ///
    /// Selection is purely visual here — the owning question element decides what "selected" means
    /// (one of many, or a filled-up-to-here rating scale).
    /// </summary>
    public class PlayProbeSelectableButton : MonoBehaviour
    {
        /// <summary>The underlying uGUI button. Add your click listeners here.</summary>
        [SerializeField] public Button button;

        [SerializeField] private TextMeshProUGUI buttonText;

        [Tooltip("Fill used while selected. Leave at the default and the PlayProbe theme colour is used.")]
        [SerializeField] private Color selectedColor = new Color32(0x79, 0x3C, 0xDD, 0xFF);

        [Tooltip("Optional ring image that brightens with the selection. A separate Image using the " +
                 "outline sprite — uGUI's Outline effect smears rather than strokes on a rounded shape.")]
        [SerializeField] private Image border;

        [Tooltip("Optional background image, dimmed by Hide().")]
        [SerializeField] private Image background;

        private Color _unselectedColor;
        private bool _isSelected;
        private bool _capturedRestingColor;

        /// <summary>Whether this button is currently the chosen one.</summary>
        public bool IsSelected => _isSelected;

        // Awake, not Start: a question element can select a button in the same frame it spawns it, and
        // Start would not have run yet — the resting colour would then be captured as the *selected*
        // colour, and deselecting would leave the button stuck highlighted.
        private void Awake()
        {
            CaptureRestingColor();
        }

        private void CaptureRestingColor()
        {
            if (_capturedRestingColor || button == null)
            {
                return;
            }

            _unselectedColor = button.colors.normalColor;
            _capturedRestingColor = true;
        }

        /// <summary>Marks the button as chosen.</summary>
        public void SelectButton()
        {
            if (_isSelected)
            {
                return;
            }

            CaptureRestingColor();
            _isSelected = true;
            ApplyColor(selectedColor);
        }

        /// <summary>Returns the button to its resting look.</summary>
        public void DeselectButton()
        {
            if (!_isSelected)
            {
                return;
            }

            _isSelected = false;
            ApplyColor(_unselectedColor);
        }

        /// <summary>Sets the label text. Safe when the prefab has no label.</summary>
        public void SetLabel(string label)
        {
            if (buttonText == null)
            {
                Debug.LogWarning("[PlayProbe] PlayProbeSelectableButton is missing its button text reference.");
                return;
            }

            buttonText.SetText(label ?? string.Empty);
        }

        /// <summary>The current label text, or an empty string when there is no label.</summary>
        public string GetLabel()
        {
            return buttonText != null ? buttonText.text : string.Empty;
        }

        /// <summary>
        /// Makes the button invisible and unclickable while keeping its slot in the layout. Multiple
        /// choice uses this to pad the last row when the option count is odd, so the remaining option
        /// keeps its column width instead of stretching across the row.
        /// </summary>
        public void Hide()
        {
            if (button != null)
            {
                button.gameObject.SetActive(false);
            }

            // These were GetComponent lookups that threw a NullReferenceException on any prefab without
            // an Outline or an Image — which is every prefab that is not the multiple-choice option.
            if (border != null)
            {
                border.enabled = false;
            }

            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (background != null)
            {
                background.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        private void ApplyColor(Color color)
        {
            if (button == null)
            {
                return;
            }

            ColorBlock block = button.colors;
            block.normalColor = color;
            block.selectedColor = color;
            button.colors = block;

            if (border != null)
            {
                border.color = _isSelected
                    ? PlayProbeUiTheme.Default.primary
                    : PlayProbeUiTheme.Default.border;
            }
        }
    }
}
