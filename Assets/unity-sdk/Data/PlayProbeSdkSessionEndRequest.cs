// Copyright PlayProbe.io 2026. All rights reserved

using System;
using System.Collections.Generic;

namespace PlayProbe.Data
{
    [Serializable]
    public class PlayProbeSdkSessionEndRequest
    {
        public string session_id;
        public double duration_seconds;
        public double avg_fps;
        public double min_fps;
        public List<SurveyResponse> survey_responses;
    }
}