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
