using System;
using System.Collections.Generic;
using System.Linq;
using PlayProbe.Data;
using PlayProbe.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace PlayProbe
{
    internal class PlayProbeSurveyCanvas : MonoBehaviour
    {
        [SerializeField] private Transform container;

        [SerializeField] private Button submitButton;
        [SerializeField] private Button skipButton;
        
        private List<IPlayProbeQuestionElement> _questionElements = new();
        
        private void Start()
        {
            submitButton.onClick.AddListener(OnSubmit);
            skipButton.onClick.AddListener(OnSkip);
        }


        internal void Initialize(List<SurveyQuestionSchema> surveySchemaItem)
        {
            surveySchemaItem = surveySchemaItem.OrderBy(x => x.order_index).ToList();

            foreach (SurveyQuestionSchema questionSchema in surveySchemaItem)
            {
                CreateQuestion(questionSchema);
            }
        }

        private void CreateQuestion(SurveyQuestionSchema questionSchema)
        {
            switch (questionSchema.question_type)
            {
                case "rating":
                    SpawnQuestion("PlayProbeRatingQuestion", questionSchema);
                    return;
                case "yes_no":
                    SpawnQuestion("PlayProbeYesNoQuestion", questionSchema);
                    return;
                case "multiple_choice":
                    SpawnQuestion("PlayProbeMultipleOptions", questionSchema);
                    return;
                case "text":
                    SpawnQuestion("PlayProbeTextQuestion", questionSchema);
                    return;
                case "emoji_scale":
                    SpawnQuestion("PlayProbeEmojiQuestion", questionSchema);
                    return;
            }
        }


        private async void SpawnQuestion(string resourcePath, SurveyQuestionSchema questionSchema)
        {
            try
            {
                ResourceRequest handle = Resources.LoadAsync<GameObject>(resourcePath);
                await handle;

                if (!handle.isDone || handle.asset == null)
                {
                    Debug.LogWarning($"[PlayProbe] Could not load question prefab from Resources path '{resourcePath}'.");
                    return;
                }

                if (!(handle.asset is GameObject prefab))
                {
                    Debug.LogWarning($"[PlayProbe] Resource '{resourcePath}' is not a GameObject prefab.");
                    return;
                }

                GameObject questionObject = Instantiate(prefab, container);
                IPlayProbeQuestionElement playProbeQuestionElement =
                    questionObject.GetComponent<IPlayProbeQuestionElement>();

                if (playProbeQuestionElement == null)
                {
                    Debug.LogWarning(
                        $"[PlayProbe] Prefab '{resourcePath}' does not contain a component implementing IQuestionElement.");
                    Destroy(questionObject);
                    return;
                }

                playProbeQuestionElement.InitQuestion(questionSchema);
                _questionElements.Add(playProbeQuestionElement);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"[PlayProbe] Prefab '{resourcePath}' does not contain a component implementing IQuestionElement. {e}");
            }
        }
        
        
        private void OnSkip()
        {
            
        }

        private void OnSubmit()
        {
            foreach(IPlayProbeQuestionElement questionElement in _questionElements)
            {
                if (!questionElement.IsAnswerSelected())
                {
                    Debug.Log("[PlayProbe] Not all questions have been answered.");
                    return;
                }
            }
            List<SurveyResponse> responses = new();
            foreach (IPlayProbeQuestionElement questionElement in _questionElements)
            {
                responses.Add(questionElement.GetAnswerData());
            }
            PlayProbeManager.Instance.SubmitSurveyResponses(responses);
        }
    }
}