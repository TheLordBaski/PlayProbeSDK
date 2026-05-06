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

        public void InitQuestion(SurveyQuestionSchema questionSchema)
        {
            questionText.SetText(questionSchema.label);
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
