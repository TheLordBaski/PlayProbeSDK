using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using PlayProbe.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayProbe
{
    public class PlayProbeSurveyOld
    {
        private readonly PlayProbeConfig _config;
        private readonly PlayProbeEvents _events;

        private readonly List<SurveySchemaItem> _registrations = new List<SurveySchemaItem>();
        private readonly Dictionary<string, List<SurveyQuestionData>> _loadedTriggers = new Dictionary<string, List<SurveyQuestionData>>();
        private readonly Dictionary<string, string> _questionMap = new Dictionary<string, string>();
        private readonly List<SurveyResponse> _bufferedResponses = new List<SurveyResponse>();
        private readonly Queue<QueuedSurveyRequest> _queuedSurveys = new Queue<QueuedSurveyRequest>();

        private SurveyOverlay _activeOverlay;
        private bool _surveyOpen;
        private bool _pauseApplied;
        private float _previousTimeScale = 1f;

        public PlayProbeSurveyOld(PlayProbeConfig config, PlayProbeEvents events)
        {
            _config = config;
            _events = events;
        }

        public SurveyBuilder Register(string triggerKey)
        {
            string resolvedTriggerKey = string.IsNullOrWhiteSpace(triggerKey) ? "default" : triggerKey.Trim();
            SurveySchemaItem registration = FindOrCreateRegistration(resolvedTriggerKey);
            return new SurveyBuilder(registration);
        }

        public List<SurveySchemaItem> GetRegisteredSchema()
        {
            List<SurveySchemaItem> schema = new List<SurveySchemaItem>();

            for (int registrationIndex = 0; registrationIndex < _registrations.Count; registrationIndex++)
            {
                SurveySchemaItem registration = _registrations[registrationIndex];

                if (registration == null || string.IsNullOrWhiteSpace(registration.trigger_key))
                {
                    continue;
                }

                List<SurveyQuestionSchema> questions = new List<SurveyQuestionSchema>();

                for (int questionIndex = 0; questionIndex < registration.questions.Count; questionIndex++)
                {
                    SurveyQuestionSchema question = registration.questions[questionIndex];

                    if (question == null || string.IsNullOrWhiteSpace(question.sdk_question_id))
                    {
                        continue;
                    }

                    questions.Add(new SurveyQuestionSchema
                    {
                        sdk_question_id = question.sdk_question_id,
                        question_type = string.IsNullOrWhiteSpace(question.question_type) ? "text" : question.question_type,
                        label = question.label,
                        options = question.options != null ? (string[])question.options.Clone() : Array.Empty<string>(),
                        required = question.required,
                        order_index = question.order_index
                    });
                }

                questions.Sort((left, right) => left.order_index.CompareTo(right.order_index));

                schema.Add(new SurveySchemaItem
                {
                    trigger_key = registration.trigger_key,
                    questions = questions
                });
            }

            return schema;
        }

        public void LoadTriggers(Dictionary<string, List<SurveyQuestionData>> triggers)
        {
            _loadedTriggers.Clear();
            _questionMap.Clear();

            if (triggers == null)
            {
                return;
            }

            foreach (KeyValuePair<string, List<SurveyQuestionData>> pair in triggers)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                {
                    continue;
                }

                List<SurveyQuestionData> questions = new List<SurveyQuestionData>();

                for (int index = 0; index < pair.Value.Count; index++)
                {
                    SurveyQuestionData source = pair.Value[index];

                    if (source == null)
                    {
                        continue;
                    }

                    SurveyQuestionData cloned = new SurveyQuestionData
                    {
                        question_id = source.question_id,
                        sdk_question_id = source.sdk_question_id,
                        question_type = string.IsNullOrWhiteSpace(source.question_type) ? "text" : source.question_type,
                        label = source.label,
                        options = source.options != null ? (string[])source.options.Clone() : Array.Empty<string>(),
                        required = source.required,
                        order_index = source.order_index
                    };

                    questions.Add(cloned);

                    if (!string.IsNullOrWhiteSpace(cloned.sdk_question_id) && !string.IsNullOrWhiteSpace(cloned.question_id))
                    {
                        _questionMap[cloned.sdk_question_id] = cloned.question_id;
                    }
                }

                questions.Sort((left, right) => left.order_index.CompareTo(right.order_index));
                _loadedTriggers[pair.Key] = questions;
            }
        }

        public void LoadTriggers(IList<SurveyTrigger> triggers)
        {
            Dictionary<string, List<SurveyQuestionData>> mapped = new Dictionary<string, List<SurveyQuestionData>>();

            if (triggers != null)
            {
                for (int triggerIndex = 0; triggerIndex < triggers.Count; triggerIndex++)
                {
                    SurveyTrigger trigger = triggers[triggerIndex];

                    if (trigger == null || string.IsNullOrWhiteSpace(trigger.trigger_key) || trigger.questions == null)
                    {
                        continue;
                    }

                    List<SurveyQuestionData> questions = new List<SurveyQuestionData>();

                    for (int questionIndex = 0; questionIndex < trigger.questions.Count; questionIndex++)
                    {
                        SurveyQuestionData question = trigger.questions[questionIndex];

                        if (question == null)
                        {
                            continue;
                        }

                        questions.Add(new SurveyQuestionData
                        {
                            question_id = question.question_id,
                            sdk_question_id = question.sdk_question_id,
                            question_type = question.question_type,
                            label = question.label,
                            options = question.options != null ? (string[])question.options.Clone() : Array.Empty<string>(),
                            required = question.required,
                            order_index = question.order_index
                        });
                    }

                    mapped[trigger.trigger_key] = questions;
                }
            }

            LoadTriggers(mapped);
        }

        public void ShowSurvey(string triggerKey, Action onComplete = null, Action onSkipped = null)
        {
            if (string.IsNullOrWhiteSpace(triggerKey))
            {
                Debug.LogWarning("[PlayProbe] Survey trigger not found: " + triggerKey);
                return;
            }

            if (!_loadedTriggers.TryGetValue(triggerKey, out List<SurveyQuestionData> loadedQuestions) || loadedQuestions == null || loadedQuestions.Count == 0)
            {
                Debug.LogWarning("[PlayProbe] Survey trigger not found: " + triggerKey);
                return;
            }

            if (_surveyOpen || _activeOverlay != null)
            {
                _queuedSurveys.Enqueue(new QueuedSurveyRequest
                {
                    trigger_key = triggerKey,
                    onComplete = onComplete,
                    onSkipped = onSkipped
                });

                return;
            }

            ShowSurveyInternal(triggerKey, loadedQuestions, onComplete, onSkipped);
        }

        public List<SurveyResponse> GetBufferedResponses()
        {
            List<SurveyResponse> copy = new List<SurveyResponse>(_bufferedResponses.Count);

            for (int index = 0; index < _bufferedResponses.Count; index++)
            {
                SurveyResponse response = _bufferedResponses[index];

                if (response == null)
                {
                    continue;
                }

                copy.Add(new SurveyResponse
                {
                    question_id = response.question_id,
                    value_text = response.value_text,
                    value_number = response.value_number,
                    value_choice = response.value_choice
                });
            }

            return copy;
        }

        private SurveySchemaItem FindOrCreateRegistration(string triggerKey)
        {
            for (int index = 0; index < _registrations.Count; index++)
            {
                SurveySchemaItem registration = _registrations[index];

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

        private void ShowSurveyInternal(string triggerKey, List<SurveyQuestionData> loadedQuestions, Action onComplete, Action onSkipped)
        {
            SurveyOverlay overlay = CreateOverlayInstance();

            if (overlay == null)
            {
                Debug.LogWarning("[PlayProbe] Could not instantiate survey overlay.");
                onSkipped?.Invoke();
                TryShowNextQueuedSurvey();
                return;
            }

            _activeOverlay = overlay;
            _surveyOpen = true;

            ApplyTimePauseIfEnabled();

            List<SurveyQuestion> overlayQuestions = BuildOverlayQuestions(loadedQuestions);

            _events?.LogEvent("survey_shown", new Dictionary<string, object>
            {
                { "trigger_key", triggerKey }
            });

            overlay.Initialize(
                overlayQuestions,
                answers => HandleSurveySubmitted(triggerKey, loadedQuestions, answers, onComplete),
                () => HandleSurveySkipped(triggerKey, onSkipped)
            );
        }

        private void HandleSurveySubmitted(string triggerKey, List<SurveyQuestionData> loadedQuestions, Dictionary<string, object> answers, Action onComplete)
        {
            try
            {
                List<SurveyResponse> mappedResponses = BuildResponses(loadedQuestions, answers);

                if (mappedResponses.Count > 0)
                {
                    _bufferedResponses.AddRange(mappedResponses);
                    SubmitSurveyImmediately(triggerKey, mappedResponses);
                }

                _events?.LogEvent("survey_submitted", new Dictionary<string, object>
                {
                    { "trigger_key", triggerKey },
                    { "responses_count", mappedResponses.Count }
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] Survey submit handling failed: " + ex.Message);
            }
            finally
            {
                RestoreTimeScale();
                _activeOverlay = null;
                _surveyOpen = false;

                onComplete?.Invoke();
                TryShowNextQueuedSurvey();
            }
        }

        private void HandleSurveySkipped(string triggerKey, Action onSkipped)
        {
            try
            {
                _events?.LogEvent("survey_skipped", new Dictionary<string, object>
                {
                    { "trigger_key", triggerKey }
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] Survey skip handling failed: " + ex.Message);
            }
            finally
            {
                RestoreTimeScale();
                _activeOverlay = null;
                _surveyOpen = false;

                onSkipped?.Invoke();
                TryShowNextQueuedSurvey();
            }
        }

        private void TryShowNextQueuedSurvey()
        {
            if (_surveyOpen || _activeOverlay != null)
            {
                return;
            }

            while (_queuedSurveys.Count > 0)
            {
                QueuedSurveyRequest queued = _queuedSurveys.Dequeue();

                if (queued == null || string.IsNullOrWhiteSpace(queued.trigger_key))
                {
                    continue;
                }

                if (!_loadedTriggers.TryGetValue(queued.trigger_key, out List<SurveyQuestionData> loadedQuestions) ||
                    loadedQuestions == null ||
                    loadedQuestions.Count == 0)
                {
                    Debug.LogWarning("[PlayProbe] Survey trigger not found: " + queued.trigger_key);
                    queued.onSkipped?.Invoke();
                    continue;
                }

                ShowSurveyInternal(queued.trigger_key, loadedQuestions, queued.onComplete, queued.onSkipped);
                break;
            }
        }

        private List<SurveyQuestion> BuildOverlayQuestions(List<SurveyQuestionData> loadedQuestions)
        {
            List<SurveyQuestion> questions = new List<SurveyQuestion>();

            if (loadedQuestions == null)
            {
                return questions;
            }

            for (int index = 0; index < loadedQuestions.Count; index++)
            {
                SurveyQuestionData question = loadedQuestions[index];

                if (question == null)
                {
                    continue;
                }

                string sdkQuestionId = string.IsNullOrWhiteSpace(question.sdk_question_id)
                    ? question.question_id
                    : question.sdk_question_id;

                if (string.IsNullOrWhiteSpace(sdkQuestionId))
                {
                    continue;
                }

                questions.Add(new SurveyQuestion
                {
                    id = sdkQuestionId,
                    question_type = string.IsNullOrWhiteSpace(question.question_type) ? "text" : question.question_type,
                    label = question.label,
                    options = question.options != null ? (string[])question.options.Clone() : Array.Empty<string>(),
                    required = question.required,
                    order_index = question.order_index
                });
            }

            questions.Sort((left, right) => left.order_index.CompareTo(right.order_index));
            return questions;
        }

        private List<SurveyResponse> BuildResponses(List<SurveyQuestionData> loadedQuestions, Dictionary<string, object> answers)
        {
            List<SurveyResponse> responses = new List<SurveyResponse>();

            if (answers == null || answers.Count == 0)
            {
                return responses;
            }

            Dictionary<string, SurveyQuestionData> questionBySdkId = new Dictionary<string, SurveyQuestionData>();

            if (loadedQuestions != null)
            {
                for (int index = 0; index < loadedQuestions.Count; index++)
                {
                    SurveyQuestionData question = loadedQuestions[index];

                    if (question == null || string.IsNullOrWhiteSpace(question.sdk_question_id))
                    {
                        continue;
                    }

                    questionBySdkId[question.sdk_question_id] = question;
                }
            }

            foreach (KeyValuePair<string, object> answer in answers)
            {
                if (string.IsNullOrWhiteSpace(answer.Key))
                {
                    continue;
                }

                questionBySdkId.TryGetValue(answer.Key, out SurveyQuestionData questionData);
                string questionId = ResolveQuestionUuid(answer.Key, questionData);

                if (string.IsNullOrWhiteSpace(questionId))
                {
                    continue;
                }

                string questionType = questionData != null && !string.IsNullOrWhiteSpace(questionData.question_type)
                    ? questionData.question_type.ToLowerInvariant()
                    : "text";

                SurveyResponse response = new SurveyResponse
                {
                    question_id = questionId
                };

                object rawValue = answer.Value;

                switch (questionType)
                {
                    case "rating":
                        if (TryGetNumericValue(rawValue, out float numericRating))
                        {
                            response.value_number = numericRating;
                        }
                        else
                        {
                            response.value_text = ConvertToInvariantString(rawValue);
                        }

                        break;

                    case "yes_no":
                    case "multiple_choice":
                    case "emoji_scale":
                        response.value_choice = ConvertToInvariantString(rawValue);
                        break;

                    case "text":
                        response.value_text = ConvertToInvariantString(rawValue);
                        break;

                    default:
                        if (TryGetNumericValue(rawValue, out float numericValue))
                        {
                            response.value_number = numericValue;
                        }
                        else
                        {
                            response.value_text = ConvertToInvariantString(rawValue);
                        }

                        break;
                }

                responses.Add(response);
            }

            return responses;
        }

        private string ResolveQuestionUuid(string sdkQuestionId, SurveyQuestionData questionData)
        {
            if (questionData != null && !string.IsNullOrWhiteSpace(questionData.question_id))
            {
                return questionData.question_id;
            }

            if (!string.IsNullOrWhiteSpace(sdkQuestionId) && _questionMap.TryGetValue(sdkQuestionId, out string mappedQuestionId))
            {
                return mappedQuestionId;
            }

            return sdkQuestionId;
        }

        private void SubmitSurveyImmediately(string triggerKey, List<SurveyResponse> responses)
        {
            if (responses == null || responses.Count == 0)
            {
                return;
            }

            PlayProbeManagerOld managerOld = PlayProbeManagerOld.Instance;

            if (managerOld == null)
            {
                Debug.LogWarning("[PlayProbe] Survey submit skipped because manager is unavailable.");
                return;
            }

            string sessionId = managerOld.SessionId;

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                Debug.LogWarning("[PlayProbe] Survey submit skipped because session is not active.");
                return;
            }

            try
            {
                SurveySubmitPayload payload = new SurveySubmitPayload
                {
                    session_id = sessionId,
                    trigger_key = triggerKey,
                    responses = responses
                };

                string payloadJson = JsonUtility.ToJson(payload);
                managerOld.StartCoroutine(PostSurveySubmit(payloadJson));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] Survey submit request creation failed: " + ex.Message);
            }
        }

        private IEnumerator PostSurveySubmit(string payloadJson)
        {
            string url = "";//BuildUrl("/sdk-survey-submit");

            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                try
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
                    request.uploadHandler = new UploadHandlerRaw(bytes);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.timeout = 10;
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[PlayProbe] Failed to submit survey responses: " + ex.Message);
                    yield break;
                }

                UnityWebRequestAsyncOperation operation;

                try
                {
                    operation = request.SendWebRequest();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[PlayProbe] Failed to submit survey responses: " + ex.Message);
                    yield break;
                }

                yield return operation;

                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.ProtocolError)
                {
                    string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                    Debug.LogWarning("[PlayProbe] Failed to submit survey responses: HTTP " + request.responseCode + " " + request.error + ": " + responseBody);
                }
            }
        }

        private SurveyOverlay CreateOverlayInstance()
        {
            if (_activeOverlay != null)
            {
                UnityEngine.Object.Destroy(_activeOverlay.gameObject);
                _activeOverlay = null;
            }

            SurveyOverlay typedOverlayPrefab = Resources.Load<SurveyOverlay>("PlayProbeSurveyOverlay");

            if (typedOverlayPrefab != null)
            {
                return UnityEngine.Object.Instantiate(typedOverlayPrefab);
            }

            GameObject overlayPrefabObject = Resources.Load<GameObject>("PlayProbeSurveyOverlay");

            if (overlayPrefabObject != null)
            {
                GameObject instance = UnityEngine.Object.Instantiate(overlayPrefabObject);
                SurveyOverlay overlay = instance.GetComponent<SurveyOverlay>();

                if (overlay == null)
                {
                    overlay = instance.AddComponent<SurveyOverlay>();
                }

                return overlay;
            }

            SurveyOverlay legacyTypedPrefab = Resources.Load<SurveyOverlay>("SurveyOverlay");

            if (legacyTypedPrefab != null)
            {
                return UnityEngine.Object.Instantiate(legacyTypedPrefab);
            }

            GameObject legacyPrefabObject = Resources.Load<GameObject>("SurveyOverlay");

            if (legacyPrefabObject != null)
            {
                GameObject instance = UnityEngine.Object.Instantiate(legacyPrefabObject);
                SurveyOverlay overlay = instance.GetComponent<SurveyOverlay>();

                if (overlay == null)
                {
                    overlay = instance.AddComponent<SurveyOverlay>();
                }

                return overlay;
            }

            GameObject fallback = new GameObject("PlayProbeSurveyOverlay");
            return fallback.AddComponent<SurveyOverlay>();
        }

        private void ApplyTimePauseIfEnabled()
        {
            if (_pauseApplied || !ResolvePauseDuringSurveyEnabled())
            {
                return;
            }

            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _pauseApplied = true;
        }

        private void RestoreTimeScale()
        {
            if (!_pauseApplied)
            {
                return;
            }

            Time.timeScale = _previousTimeScale;
            _pauseApplied = false;
        }

        private bool ResolvePauseDuringSurveyEnabled()
        {
            PlayProbeManagerOld managerOld = PlayProbeManagerOld.Instance;

            if (managerOld != null)
            {
                if (TryReadBooleanMember(managerOld, "pauseGameDuringSurvey", out bool pauseGameDuringSurvey))
                {
                    return pauseGameDuringSurvey;
                }

                if (TryReadBooleanMember(managerOld, "pauseTimeDuringSurvey", out bool pauseTimeDuringSurvey))
                {
                    return pauseTimeDuringSurvey;
                }
            }

            return _config == null || _config.pauseTimeDuringSurvey;
        }

        private static bool TryReadBooleanMember(object source, string memberName, out bool value)
        {
            value = false;

            if (source == null || string.IsNullOrWhiteSpace(memberName))
            {
                return false;
            }

            Type sourceType = source.GetType();

            FieldInfo field = sourceType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field != null && field.FieldType == typeof(bool))
            {
                value = (bool)field.GetValue(source);
                return true;
            }

            PropertyInfo property = sourceType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (property != null && property.PropertyType == typeof(bool) && property.CanRead)
            {
                value = (bool)property.GetValue(source, null);
                return true;
            }

            return false;
        }

        private static bool TryGetNumericValue(object value, out float result)
        {
            if (value is float floatValue)
            {
                result = floatValue;
                return true;
            }

            if (value is int intValue)
            {
                result = intValue;
                return true;
            }

            if (value is long longValue)
            {
                result = longValue;
                return true;
            }

            if (value is double doubleValue)
            {
                result = (float)doubleValue;
                return true;
            }

            if (value is string stringValue && float.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out float parsed))
            {
                result = parsed;
                return true;
            }

            result = 0f;
            return false;
        }

        private static string ConvertToInvariantString(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }

        
    }
}
