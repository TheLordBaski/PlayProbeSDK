// Copyright PlayProbe.io 2026. All rights reserved

using System;
using System.Collections.Generic;

namespace PlayProbe.Data
{
    [Serializable]
    public class SurveySchemaItem
    {
        public string trigger_key;
        public List<SurveyQuestionSchema> questions;
    }
}