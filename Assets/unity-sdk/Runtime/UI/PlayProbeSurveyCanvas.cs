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
        
        
        //TODO: remove, just for testing
        private void Start()
        {
            PlayProbeSurvey survey = new PlayProbeSurvey(null);
            survey.Register("level_1")
                .AddRating("How would you rate this level?", "level_1_rating")
                .AddMultipleChoice(
                    "Which part did you like the most?", "level_1_favorite_part",
                    new[] { "Enemies", "Graphics", "Sound", "Gameplay" })
                .AddYesNo("Did you find any bugs?", "level_1_found_bugs")
                .AddText("Any additional feedback?", "level_1_additional_feedback")
                .AddEmojiScale("How did this level make you feel?", "level_1_emotion");
            Initialize(survey.GetRegisteredSurveySchema()[0].questions);
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
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"[PlayProbe] Prefab '{resourcePath}' does not contain a component implementing IQuestionElement.");
            }
        }
    }
}