// Copyright PlayProbe.io 2026. All rights reserved

using System;

namespace PlayProbe.Data
{
    [Serializable]
    public class SurveyResponse
    {
        public string question_id;
        public string value_text;
        public double value_number;
        public string value_choice;
        // Optional tag ids the tester attached (open-ended answers only). Ignored server-side for
        // non-text questions and for ids not in the active vocabulary.
        public string[] tag_ids;
    }
}