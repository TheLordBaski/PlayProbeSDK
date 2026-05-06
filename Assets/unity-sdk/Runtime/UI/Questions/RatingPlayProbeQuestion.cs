// Copyright PlayProbe.io 2026. All rights reserved

using PlayProbe.Data;
using PlayProbe.Interfaces;
using TMPro;
using UnityEngine;

namespace PlayProbe
{
    public class RatingPlayProbeQuestion : MonoBehaviour, IPlayProbeQuestionElement
    {
        [SerializeField] private TextMeshProUGUI question;
        
        [SerializeField] private PlayProbeSelectableButton[] ratingButtons;

        [SerializeField] private bool isEmojiRating;

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
            if (isEmojiRating)
            {
                button.SelectButton();
                _selectedAnswer?.DeselectButton();;
                _selectedAnswer = button;
                return;
            }
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

        public void InitQuestion(SurveyQuestionSchema questionSchema)
        {
            question.SetText(questionSchema.label ?? string.Empty);
        }

        public void GetAnswerData(SurveyResponse response)
        {
            throw new System.NotImplementedException();
        }

        public bool IsAnswerSelected()
        {
            throw new System.NotImplementedException();
        }
    }
}
