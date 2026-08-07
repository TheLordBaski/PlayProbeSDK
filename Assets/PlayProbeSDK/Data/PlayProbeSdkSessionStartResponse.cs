// Copyright PlayProbe.io 2026. All rights reserved

using System;

namespace PlayProbe.Data
{
    [Serializable]
    internal class PlayProbeSdkSessionStartResponse
    {
        public string session_id;
        public string session_token;
        public SurveySchemaItem[] survey_triggers;
        // Global tag vocabulary the tester can attach to open-ended answers + Instant Feedback.
        public AnswerTag[] answer_tags;
    }
}