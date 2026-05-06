using PlayProbe.Data;

namespace PlayProbe.Interfaces
{
    internal interface IPlayProbeQuestionElement
    {
        void InitQuestion(SurveyQuestionSchema questionSchema);

        void GetAnswerData(SurveyResponse response);
        
        bool IsAnswerSelected();
    }
}