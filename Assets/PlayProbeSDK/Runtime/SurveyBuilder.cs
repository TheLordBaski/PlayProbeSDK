using System;
using System.Collections.Generic;
using PlayProbe.Data;
using UnityEngine;

namespace PlayProbe
{
    public class SurveyBuilder
    {
        private readonly SurveySchemaItem _registration;

        internal SurveyBuilder(SurveySchemaItem registration)
        {
            _registration = registration;
        }

        public SurveyBuilder AddRating(string label, string sdkQuestionId, bool required = true)
        {
            return AddQuestion(label, sdkQuestionId, "rating", null, required);
        }

        public SurveyBuilder AddYesNo(string label, string sdkQuestionId, bool required = true)
        {
            return AddQuestion(label, sdkQuestionId, "yes_no", null , required);
        }

        public SurveyBuilder AddText(string label, string sdkQuestionId, bool required = false)
        {
            return AddQuestion(label, sdkQuestionId, "text", null, required);
        }

        public SurveyBuilder AddMultipleChoice(string label, string sdkQuestionId, string[] options, bool required = true)
        {
            string[] resolvedOptions = options != null ? (string[])options.Clone() : Array.Empty<string>();
            return AddQuestion(label, sdkQuestionId, "multiple_choice", resolvedOptions, required);
        }

        public SurveyBuilder AddEmojiScale(string label, string sdkQuestionId, bool required = true)
        {
            return AddQuestion(label, sdkQuestionId, "emoji_scale", null, required);
        }

        private SurveyBuilder AddQuestion(string label, string sdkQuestionId, string questionType, string[] options, bool required)
        {
            if (_registration == null)
            {
                Debug.LogWarning("[PlayProbe] Survey builder not properly initialized.");
                return this;
            }

            if (string.IsNullOrWhiteSpace(sdkQuestionId))
            {
                Debug.LogWarning("[PlayProbe] Register question skipped because sdkQuestionId is empty.");
                return this;
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                label = "Question";
            }

            if (_registration.questions == null)
            {
                _registration.questions = new List<SurveyQuestionSchema>();
            }

            _registration.questions.Add(new SurveyQuestionSchema
            {
                sdk_question_id = sdkQuestionId,
                question_type = string.IsNullOrWhiteSpace(questionType) ? "text" : questionType,
                label = label,
                options = options ?? Array.Empty<string>(),
                required = required,
                order_index = _registration.questions.Count
            });

            return this;
        }
    }
}