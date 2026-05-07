using PlayProbe.Data;
using PlayProbe.Interfaces;
using TMPro;
using UnityEngine;

namespace PlayProbe
{
    public class TextQuestion : MonoBehaviour , IPlayProbeQuestionElement
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TextMeshProUGUI questionText;
        private SurveyQuestionSchema _schema;

        public void InitQuestion(SurveyQuestionSchema questionSchema)
        {
            questionText.SetText(questionSchema.label);
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
                value_text = inputField.text
            };
        }

        public bool IsAnswerSelected()
        {
            return inputField.text != null && inputField.text.Length > 0;
        }
    }
}
