using System;
using System.Collections.Generic;
using PlayProbe.Data;

namespace PlayProbe
{
    public class PlayProbeSurvey
    {
        private PlayProbeRuntimeConfig _config;

        private readonly List<SurveySchemaItem> _registrations = new();
        
        internal PlayProbeSurvey(PlayProbeRuntimeConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Call this function to create a new survey. The triggerKey is used to identify the survey and should be
        /// unique for each survey you want to create. You can use the same triggerKey to update an existing survey.
        /// After calling this function, you can use the returned SurveyBuilder to add questions to the survey.
        /// </summary>
        /// <param name="triggerKey">Survey identifier. Should be unique for each survey.</param>
        /// <returns>Return Survey builder, used to add questions to survey</returns>
        public SurveyBuilder Register(string triggerKey)
        {
            string resolvedTriggerKey = string.IsNullOrWhiteSpace(triggerKey) ? "default" : triggerKey.Trim();
            SurveySchemaItem registration = FindOrCreateRegistration(resolvedTriggerKey);
            return new SurveyBuilder(registration);
        }
        
        
        private SurveySchemaItem FindOrCreateRegistration(string triggerKey)
        {
            foreach (SurveySchemaItem registration in _registrations)
            {
                if (registration != null && string.Equals(registration.trigger_key, triggerKey, StringComparison.Ordinal))
                {
                    return registration;
                }
            }

            SurveySchemaItem created = new SurveySchemaItem
            {
                trigger_key = triggerKey
            };

            _registrations.Add(created);
            return created;
        }

        internal List<SurveySchemaItem> GetRegisteredSurveySchema()
        {
            return _registrations;
        }

        public List<SurveyResponse> GetSurveyResponses()
        {
            //TODO: Implement this function to return survey responses for the current session. For now, it returns an empty list.
            return new List<SurveyResponse>();
        }
    }
}