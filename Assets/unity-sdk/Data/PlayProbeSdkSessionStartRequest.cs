// Copyright PlayProbe.io 2026. All rights reserved

using System;
using System.Collections.Generic;

namespace PlayProbe.Data
{
    [Serializable]
    public class PlayProbeSdkSessionStartRequest
    {
        public string share_token;
        public string handoff_token;
        public string sdk_version;
        public string unity_version;
        public string platform;
        public int screen_width;
        public int screen_height;
        public List<SurveySchemaItem> survey_schema;
    }
}
