// Copyright PlayProbe.io 2026. All rights reserved

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayProbe
{
    public class PlayProbeSelectableButton : MonoBehaviour
    {

        [SerializeField] public Button button;
        [SerializeField] private TextMeshProUGUI buttonText;
        [SerializeField] private Color selectedColor;
        
        private Color _unselectedColor;
        
        private bool _isSelected;
        
        private void Start()
        {
            _unselectedColor = button.colors.normalColor;
        }

        public void SelectButton()
        {
            if (_isSelected)
            {
                return;
            }
            _isSelected = true;
            ColorBlock block = button.colors;
            block.normalColor = selectedColor;
            block.selectedColor = selectedColor;
            button.colors = block;
        }
        
        public void DeselectButton()
        {
            if (!_isSelected)
            {
                return;
            }
            _isSelected = false;
            ColorBlock block = button.colors;
            block.normalColor = _unselectedColor;
            block.selectedColor = _unselectedColor;
            button.colors = block;
        }

        public void SetLabel(string label)
        {
            if (buttonText == null)
            {
                Debug.LogWarning("[PlayProbe] PlayProbeSelectableButton is missing button text reference.");
                return;
            }

            buttonText.SetText(label ?? string.Empty);
        }

        public void Hide()
        {
            button.gameObject.SetActive(false);
            GetComponent<Outline>().enabled = false;
            GetComponent<Image>().color = new Color(0, 0, 0, 0);
        }
    }
}
