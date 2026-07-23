using System;
using System.Collections.Generic;

namespace PlayProbe.Data
{
    [Serializable]
    public class PlayProbeSurveySubmitRequest
    {
        public string session_id;
        public string session_token;
        public List<SurveyResponse> survey_responses;
    }
}