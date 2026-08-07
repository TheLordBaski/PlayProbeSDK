// Copyright PlayProbe.io 2026. All rights reserved

using PlayProbe.Data;

namespace PlayProbe.Interfaces
{
    /// <summary>
    /// One question inside a survey overlay. <see cref="PlayProbeSurveyCanvas"/> spawns the prefab
    /// registered for a question type, finds this interface on it, and drives it through these three
    /// calls.
    ///
    /// Implement it to add a question style of your own: put your component on a prefab in a
    /// <c>Resources</c> folder and it will be treated exactly like the built-in ones. Note the backend
    /// only stores the five known <c>question_type</c> values, so a custom element still has to produce
    /// a response that fits one of the <see cref="SurveyResponse"/> value fields.
    /// </summary>
    public interface IPlayProbeQuestionElement
    {
        /// <summary>
        /// Populates the element from the backend schema. Called once, immediately after the prefab is
        /// instantiated and before it is shown.
        /// </summary>
        /// <param name="questionSchema">
        /// The question as the backend returned it. <c>id</c> is the value that must end up in
        /// <see cref="SurveyResponse.question_id"/>.
        /// </param>
        void InitQuestion(SurveyQuestionSchema questionSchema);

        /// <summary>
        /// The player's answer, ready to submit. Only called when
        /// <see cref="IsAnswerSelected"/> is true.
        /// </summary>
        SurveyResponse GetAnswerData();

        /// <summary>
        /// Whether the player has answered. Required questions block submission until this is true;
        /// unanswered optional questions are left out of the submission entirely.
        /// </summary>
        bool IsAnswerSelected();
    }
}
