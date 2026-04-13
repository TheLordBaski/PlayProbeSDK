// Copyright PlayProbe.io 2026. All rights reserved

using System;

namespace PlayProbe.Data
{
    [Serializable]
    public class SurveyQuestionSchema
    {
        public string sdk_question_id;
        /// <summary>
        /// "rating" | "yes_no" | "text" | "multiple_choice" | "emoji_scale";
        /// </summary>
        public string question_type;
        public string label;
        public string[] options;
        public bool required;
        public int order_index;
    }
}