using System;
using System.Collections.Generic;
using PlayProbe.Data;

namespace PlayProbe
{
    /// <summary>
    /// Where mid-game surveys are declared. Register each one against a trigger key before the session
    /// starts, then show it when that moment arrives.
    ///
    /// <code>
    /// PlayProbeManager.Instance.Survey.Register("after_level_1")
    ///     .AddRating("How would you rate this level?", "lvl1_rating")
    ///     .AddText("Anything else?", "lvl1_notes", required: false);
    ///
    /// // later
    /// PlayProbeManager.Instance.ShowSurvey("after_level_1");
    /// </code>
    ///
    /// Registration has to happen before <see cref="PlayProbeManager.StartSession"/>: the schema is
    /// sent with the start request, and the backend replies with the ids answers are recorded against.
    /// </summary>
    public class PlayProbeSurvey
    {
        private readonly List<SurveySchemaItem> _registrations = new();
        
        internal PlayProbeSurvey()
        {
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
    }
}