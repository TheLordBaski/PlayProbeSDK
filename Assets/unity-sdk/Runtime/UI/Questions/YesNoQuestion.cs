// Copyright PlayProbe.io 2026. All rights reserved

using PlayProbe.Data;
using PlayProbe.Interfaces;
using TMPro;
using UnityEngine;

namespace PlayProbe
{
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

        public void InitQuestion(SurveyQuestionSchema questionSchema)
        {
            title.SetText(questionSchema.label);
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
                value_choice = _selectedAnswer == yesButton ? "Yes" : "No"
            };
        }

        public bool IsAnswerSelected()
        {
            return _selectedAnswer != null;
        }
    }
}
