// Copyright PlayProbe.io 2026. All rights reserved

using System.Collections.Generic;
using PlayProbe.Data;
using PlayProbe.Interfaces;
using TMPro;
using UnityEngine;

namespace PlayProbe
{
    /// <summary>
    /// A 1-5 scale, in either of two looks driven by <c>isEmojiRating</c>:
    /// a star-style bar where picking 4 lights up 1 through 4, or an emoji row where exactly one face
    /// is chosen. The submitted value is the 1-based index either way.
    ///
    /// Rendered from <c>Resources/PlayProbeRatingQuestion</c> and
    /// <c>Resources/PlayProbeEmojiQuestion</c>.
    /// </summary>
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

        /// <inheritdoc />
        public void InitQuestion(SurveyQuestionSchema questionSchema)
        {
            _schema = questionSchema;

            if (question != null)
            {
                question.SetText(questionSchema.label ?? string.Empty);
            }
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public bool IsAnswerSelected()
        {
            return _schema != null && _selectedAnswer != null;
        }
    }
}
