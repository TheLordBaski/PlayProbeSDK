using PlayProbe.Data;

namespace PlayProbe.Interfaces
{
    internal interface IPlayProbeQuestionElement
    {
        void InitQuestion(SurveyQuestionSchema questionSchema);

        SurveyResponse GetAnswerData();
        
        bool IsAnswerSelected();
    }
}