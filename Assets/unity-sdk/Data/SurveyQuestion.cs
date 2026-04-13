// Copyright PlayProbe.io 2026. All rights reserved

using System;

namespace PlayProbe.Data
{
    [Serializable]
    public class SurveyQuestion
    {
        public string id;
        public string question_type;
        public string label;
        public string[] options;
        public bool required;
        public int order_index;
    }
}