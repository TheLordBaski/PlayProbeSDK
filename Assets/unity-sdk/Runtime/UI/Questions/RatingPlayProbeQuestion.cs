// Copyright PlayProbe.io 2026. All rights reserved

using System.Collections.Generic;
using PlayProbe.Data;
using PlayProbe.Interfaces;
using TMPro;
using UnityEngine;

namespace PlayProbe
{
    public class RatingPlayProbeQuestion : MonoBehaviour, IPlayProbeQuestionElement
    {
        [SerializeField] private TextMeshProUGUI question;
        
        [SerializeField] private List<PlayProbeSelectableButton> ratingButtons;

        [SerializeField] private bool isEmojiRating;

        private PlayProbeSelectableButton _selectedAnswer;
        private SurveyQuestionSchema _schema;

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
            _schema = questionSchema;
        }

        public SurveyResponse GetAnswerData()
        {
            if (!IsAnswerSelected())
            {
                return new SurveyResponse();
            }

            return new SurveyResponse()
            {
                question_id = _schema.id,
                value_number =  ratingButtons.IndexOf(_selectedAnswer) + 1
            };
        }

        public bool IsAnswerSelected()
        {
            return _selectedAnswer  != null;
        }
    }
}
