// Copyright PlayProbe.io 2026. All rights reserved

using PlayProbe.Data;
using PlayProbe.Interfaces;
using TMPro;
using UnityEngine;

namespace PlayProbe
{
    /// <summary>
    /// A two-button question. The answer is submitted as the literal choice text "Yes" or "No", which
    /// is what the results dashboard groups on — so do not translate those two strings without also
    /// changing how the dashboard reads them.
    ///
    /// Rendered from <c>Resources/PlayProbeYesNoQuestion</c>.
    /// </summary>
    public class YesNoQuestion : MonoBehaviour, IPlayProbeQuestionElement
    {
        [SerializeField] private TextMeshProUGUI title;
        
        [SerializeField]
        private PlayProbeSelectableButton yesButton;
        
        
        [SerializeField]
        private PlayProbeSelectableButton noButton;


        private PlayProbeSelectableButton _selectedAnswer;
        private SurveyQuestionSchema _schema;

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

        /// <inheritdoc />
        public void InitQuestion(SurveyQuestionSchema questionSchema)
        {
            _schema = questionSchema;

            if (title != null)
            {
                title.SetText(questionSchema.label ?? string.Empty);
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
                value_choice = _selectedAnswer == yesButton ? "Yes" : "No"
            };
        }

        /// <inheritdoc />
        public bool IsAnswerSelected()
        {
            return _schema != null && _selectedAnswer != null;
        }
    }
}
