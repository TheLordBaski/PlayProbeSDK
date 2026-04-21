// Copyright PlayProbe.io 2026. All rights reserved

using TMPro;
using UnityEngine;

namespace PlayProbe
{
    public class RatingQuestion : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI question;
        
        [SerializeField] private PlayProbeSelectableButton[] ratingButtons;


        private PlayProbeSelectableButton _selectedAnswer;
        private void Start()
        {
            foreach (PlayProbeSelectableButton ratingButton in ratingButtons)
            {
                ratingButton.button.onClick.AddListener(() => OnAnswerSelected(ratingButton));
            }
        }
        
        private void OnAnswerSelected(PlayProbeSelectableButton button)
        {
            bool gotSelected = false;
            _selectedAnswer = button;
            foreach (PlayProbeSelectableButton ratingButton in ratingButtons)
            {
                if (!gotSelected)
                {
                    ratingButton.SelectButton();
                }
                else
                {
                    ratingButton.DeselectButton();
                }

                if (_selectedAnswer == ratingButton)
                {
                    gotSelected = true;
                }
            }
        }
        
    }
}
