// Copyright PlayProbe.io 2026. All rights reserved

using System;

namespace PlayProbe.Data
{
    [Serializable]
    public class SurveyResponse
    {
        public string question_id;
        public string value_text;
        public float? value_number;
        public string value_choice;
    }
}