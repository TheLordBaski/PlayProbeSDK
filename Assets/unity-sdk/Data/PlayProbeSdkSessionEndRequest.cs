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
        public float avg_fps;
        public float min_fps;
        public List<SurveyResponse> survey_responses;
    }
}