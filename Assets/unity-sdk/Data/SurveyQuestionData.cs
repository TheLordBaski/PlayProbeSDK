// Copyright PlayProbe.io 2026. All rights reserved

using System;

namespace PlayProbe.Data
{
    [Serializable]
    public class SurveyQuestionData
    {
        public string question_id;
        public string sdk_question_id;
        public string question_type;
        public string label;
        public string[] options;
        public bool required;
        public int order_index;
    }
}