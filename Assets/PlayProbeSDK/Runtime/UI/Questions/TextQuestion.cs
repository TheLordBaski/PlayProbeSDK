// Copyright PlayProbe.io 2026. All rights reserved

using PlayProbe.Data;
using PlayProbe.Interfaces;
using TMPro;
using UnityEngine;

namespace PlayProbe
{
    /// <summary>
    /// An open-ended survey question: a multi-line text box, plus the optional tag chooser so the
    /// tester can label what their answer is about ("Combat", "Performance", …).
    ///
    /// Tags are only meaningful on free-text answers — the backend ignores <c>tag_ids</c> on every
    /// other question type — so this is the only question element that carries a
    /// <see cref="PlayProbeTagSelector"/>.
    ///
    /// Rendered from <c>Resources/PlayProbeTextQuestion</c>.
    /// </summary>
    public class TextQuestion : MonoBehaviour, IPlayProbeQuestionElement
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TextMeshProUGUI questionText;

        [Tooltip("Optional. Renders the global answer-tag vocabulary under the text box. " +
                 "Hides itself when the test has no tags configured.")]
        [SerializeField] private PlayProbeTagSelector tagSelector;

        private SurveyQuestionSchema _schema;

        /// <inheritdoc />
        public void InitQuestion(SurveyQuestionSchema questionSchema)
        {
            if (questionSchema == null)
            {
                Debug.LogWarning("[PlayProbe] TextQuestion.InitQuestion got a null schema.");
                return;
            }

            _schema = questionSchema;

            if (questionText != null)
            {
                questionText.SetText(questionSchema.label ?? string.Empty);
            }

            if (tagSelector != null)
            {
                tagSelector.SetHeading(PlayProbeUiTheme.Default.surveyTagsLabel);
                tagSelector.Build();
            }
        }

        /// <inheritdoc />
        public SurveyResponse GetAnswerData()
        {
            if (!IsAnswerSelected())
            {
                return new SurveyResponse();
            }

            return new SurveyResponse
            {
                question_id = _schema.id,
                value_text = inputField.text.Trim(),
                tag_ids = tagSelector != null ? tagSelector.SelectedTagIds : null,
            };
        }

        /// <inheritdoc />
        public bool IsAnswerSelected()
        {
            return _schema != null && inputField != null && !string.IsNullOrWhiteSpace(inputField.text);
        }
    }
}
