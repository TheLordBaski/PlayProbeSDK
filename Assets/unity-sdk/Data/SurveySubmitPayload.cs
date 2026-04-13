// Copyright PlayProbe.io 2026. All rights reserved

using System;
using System.Collections.Generic;

namespace PlayProbe.Data
{
    [Serializable]
    public class SurveySubmitPayload
    {
        public string session_id;
        public string trigger_key;
        public List<SurveyResponse> responses;
    }
}