// Copyright PlayProbe.io 2026. All rights reserved

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlayProbe.Data;
using PlayProbe.Interfaces;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayProbe
{
    /// <summary>
    /// The mid-game survey overlay. Spawned by
    /// <see cref="PlayProbeManager.ShowSurvey(string)"/> from
    /// <c>Resources/PlayProbeSurveyCanvas</c>, it builds one question element per question in the
    /// trigger's schema, collects the answers, and submits them.
    ///
    /// Behaviour follows <see cref="PlayProbeConfig"/>: <c>allowSurveyDismiss</c> decides whether the
    /// skip button exists, and <c>pauseTimeDuringSurvey</c> decides whether the game freezes while the
    /// survey is up.
    ///
    /// Regenerate the prefab with <c>Tools &gt; PlayProbe &gt; UI &gt; Rebuild Prefabs</c> after
    /// editing <see cref="PlayProbeUiTheme"/>.
    /// </summary>
    public class PlayProbeSurveyCanvas : MonoBehaviour
    {
        // Question type id (from the backend schema) -> the prefab that renders it.
        private static readonly Dictionary<string, string> QuestionPrefabs = new()
        {
            { "rating", "PlayProbeRatingQuestion" },
            { "yes_no", "PlayProbeYesNoQuestion" },
            { "multiple_choice", "PlayProbeMultipleOptions" },
            { "text", "PlayProbeTextQuestion" },
            { "emoji_scale", "PlayProbeEmojiQuestion" },
        };

        [Header("Layout")]
        [Tooltip("Question elements are spawned as children of this transform.")]
        [SerializeField] private Transform container;

        [Header("Actions")]
        [SerializeField] private Button submitButton;
        [SerializeField] private TextMeshProUGUI submitButtonLabel;
        [SerializeField] private Button skipButton;
        [SerializeField] private TextMeshProUGUI skipButtonLabel;
        [SerializeField] private TextMeshProUGUI errorLabel;

        // Element plus the schema it was built from, so required-ness survives past spawn time.
        private sealed class QuestionEntry
        {
            internal IPlayProbeQuestionElement Element;
            internal SurveyQuestionSchema Schema;
        }

        private readonly List<QuestionEntry> _questions = new();

        private bool _isBuilt;
        private bool _hasSubmitted;
        private bool _didPause;
        private float _previousTimeScale = 1f;

        private void Awake()
        {
            PlayProbeUi.EnsureEventSystem();
            PlayProbeUi.ConfigureOverlayCanvas(GetComponent<Canvas>(), PlayProbeUi.SortOrderSurvey,
                PlayProbeUiTheme.Default);
        }

        private void Start()
        {
            PlayProbeUiTheme theme = PlayProbeUiTheme.Default;
            SetText(submitButtonLabel, theme.surveySubmitLabel);
            SetText(skipButtonLabel, theme.surveySkipLabel);
            ClearError();

            if (submitButton != null)
            {
                submitButton.onClick.AddListener(OnSubmit);
                // Nothing to submit until the questions have finished loading.
                submitButton.interactable = _isBuilt;
            }

            bool allowDismiss = PlayProbeManager.Instance == null || PlayProbeManager.Instance.AllowSurveyDismiss;

            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(allowDismiss);
                skipButton.onClick.AddListener(OnSkip);
            }

            ApplyPause();
        }

        private void Update()
        {
            bool allowDismiss = PlayProbeManager.Instance == null || PlayProbeManager.Instance.AllowSurveyDismiss;

            if (allowDismiss && PlayProbeInput.WasCancelPressedThisFrame())
            {
                OnSkip();
            }
        }

        private void OnDestroy()
        {
            RestorePause();
        }

        /// <summary>
        /// Builds the question elements for a trigger's schema. Called by
        /// <see cref="PlayProbeManager.ShowSurvey(string)"/> right after the canvas is instantiated.
        /// </summary>
        /// <param name="questions">The trigger's questions, in any order — they are sorted here.</param>
        public void Initialize(List<SurveyQuestionSchema> questions)
        {
            _ = BuildAsync(questions);
        }

        // Questions are loaded one at a time and awaited in order. Loading them in parallel (the
        // previous behaviour) meant the display order depended on which Resources request finished
        // first, so the same survey could come out shuffled between runs.
        private async Task BuildAsync(List<SurveyQuestionSchema> questions)
        {
            if (questions == null || container == null)
            {
                MarkBuilt();
                return;
            }

            List<SurveyQuestionSchema> ordered = new List<SurveyQuestionSchema>(questions);
            ordered.Sort((a, b) => a.order_index.CompareTo(b.order_index));

            foreach (SurveyQuestionSchema schema in ordered)
            {
                if (schema == null || !QuestionPrefabs.TryGetValue(schema.question_type ?? string.Empty,
                        out string resourcePath))
                {
                    Debug.LogWarning(
                        $"[PlayProbe] Skipping question of unknown type '{schema?.question_type}'.");
                    continue;
                }

                await SpawnQuestionAsync(resourcePath, schema);

                // ShowSurvey can be interrupted by a scene load or a skip while prefabs are loading.
                if (this == null || container == null)
                {
                    return;
                }
            }

            MarkBuilt();
        }

        private async Task SpawnQuestionAsync(string resourcePath, SurveyQuestionSchema schema)
        {
            try
            {
                ResourceRequest handle = Resources.LoadAsync<GameObject>(resourcePath);
                await handle;

                if (handle.asset is not GameObject prefab)
                {
                    Debug.LogWarning($"[PlayProbe] Could not load question prefab '{resourcePath}'.");
                    return;
                }

                GameObject instance = Instantiate(prefab, container);
                IPlayProbeQuestionElement element = instance.GetComponent<IPlayProbeQuestionElement>();

                if (element == null)
                {
                    Debug.LogWarning(
                        $"[PlayProbe] Prefab '{resourcePath}' has no component implementing IPlayProbeQuestionElement.");
                    Destroy(instance);
                    return;
                }

                element.InitQuestion(schema);
                _questions.Add(new QuestionEntry { Element = element, Schema = schema });
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayProbe] Failed to spawn question '{resourcePath}': {exception.Message}");
            }
        }

        private void MarkBuilt()
        {
            _isBuilt = true;

            if (submitButton != null)
            {
                submitButton.interactable = true;
            }
        }

        #region Actions

        private void OnSkip()
        {
            if (_hasSubmitted)
            {
                return;
            }

            _hasSubmitted = true;
            Destroy(gameObject);
        }

        private void OnSubmit()
        {
            if (_hasSubmitted || !_isBuilt)
            {
                return;
            }

            // Only questions the developer marked required block submission. The old behaviour demanded
            // every question, which made optional free-text boxes mandatory in practice.
            foreach (QuestionEntry entry in _questions)
            {
                if (entry.Schema != null && entry.Schema.required && !entry.Element.IsAnswerSelected())
                {
                    ShowError(PlayProbeUiTheme.Default.surveyIncompleteError);
                    return;
                }
            }

            List<SurveyResponse> responses = new();

            foreach (QuestionEntry entry in _questions)
            {
                if (!entry.Element.IsAnswerSelected())
                {
                    // Skipped optional question: sending a blank response would create an empty answer
                    // row in the results rather than no answer at all.
                    continue;
                }

                SurveyResponse response = entry.Element.GetAnswerData();
                if (response != null && !string.IsNullOrEmpty(response.question_id))
                {
                    responses.Add(response);
                }
            }

            _hasSubmitted = true;

            if (submitButton != null)
            {
                submitButton.interactable = false;
            }

            if (responses.Count > 0 && PlayProbeManager.Instance != null)
            {
                PlayProbeManager.Instance.SubmitSurveyResponses(responses);
                PlayProbeToast.Show(PlayProbeUiTheme.Default.surveySentMessage);
            }

            Destroy(gameObject);
        }

        #endregion

        #region Pause

        private void ApplyPause()
        {
            if (PlayProbeManager.Instance == null || !PlayProbeManager.Instance.PauseTimeDuringSurvey || _didPause)
            {
                return;
            }

            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _didPause = true;
        }

        private void RestorePause()
        {
            if (!_didPause)
            {
                return;
            }

            Time.timeScale = _previousTimeScale;
            _didPause = false;
        }

        #endregion

        #region Helpers

        private void ShowError(string message)
        {
            if (errorLabel == null)
            {
                Debug.Log($"[PlayProbe] {message}");
                return;
            }

            errorLabel.SetText(message);
            errorLabel.gameObject.SetActive(true);
        }

        private void ClearError()
        {
            if (errorLabel != null)
            {
                errorLabel.gameObject.SetActive(false);
            }
        }

        private static void SetText(TextMeshProUGUI label, string value)
        {
            if (label != null)
            {
                label.SetText(value ?? string.Empty);
            }
        }

        #endregion
    }
}
