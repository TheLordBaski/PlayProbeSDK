using UnityEngine;

namespace PlayProbe
{
    public class YesNoQuestion : MonoBehaviour
    {
        [SerializeField]
        private PlayProbeSelectableButton yesButton;
        
        
        [SerializeField]
        private PlayProbeSelectableButton noButton;


        private PlayProbeSelectableButton _selectedAnswer;
        private void Start()
        {
             yesButton.button.onClick.AddListener(() => OnAnswerSelected(yesButton));
             noButton.button.onClick.AddListener(() => OnAnswerSelected(noButton));
        }

        private void OnAnswerSelected(PlayProbeSelectableButton button)
        {
            if(_selectedAnswer != null)
            {
                _selectedAnswer.DeselectButton();
            }
            _selectedAnswer = button;
            _selectedAnswer.SelectButton();
        }
    }
}
