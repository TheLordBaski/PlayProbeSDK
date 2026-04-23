// Copyright PlayProbe.io 2026. All rights reserved

using System;

namespace PlayProbe.Data
{
    [Serializable]
    public class PlayProbeSdkSessionStartResponse
    {
        public string session_id;
        public PlayProbeQuestionMapEntry[] question_map;
        public SurveySchemaItem[] survey_triggers;
    }
}