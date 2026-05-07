using System;
using System.Collections.Generic;

namespace PlayProbe.Data
{
    [Serializable]
    public class PlayProbeSurveySubmitRequest
    {
        public string session_id;
        public List<SurveyResponse> survey_responses;
    }
}