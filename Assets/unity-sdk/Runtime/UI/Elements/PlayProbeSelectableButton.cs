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
    }
}
