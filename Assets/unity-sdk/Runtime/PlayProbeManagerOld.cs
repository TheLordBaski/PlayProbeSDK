// Copyright PlayProbe.io 2026. All rights reserved

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using PlayProbe.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayProbe
{
    [DisallowMultipleComponent]
    public class PlayProbeManagerOld : MonoBehaviour
    {
        [Header("PlayProbe Configuration")]
        [SerializeField] private string testToken;

        [Header("Analytics")]
        [SerializeField] private bool enableFpsTracking = true;
        [SerializeField] private bool enableCrashReporting = true;
        [SerializeField] private bool enablePositionHeatmap;
        [SerializeField] private float positionLogInterval = 5f;

        public static PlayProbeManagerOld Instance { get; private set; }

        // Kept for compatibility with older SDK callers.
        public PlayProbeSession Session { get; private set; }
        public PlayProbeEvents Events { get; private set; }
        public PlayProbeSurveyOld SurveyOld { get; private set; }
        public PlayProbeAnalytics Analytics { get; private set; }

        public string SessionId { get; private set; }
        public bool IsSessionActive { get; private set; }

        private const string API_BASE = "https://api.playprobe.io/";
        private const string SDK_VERSION = "0.1.0";
        private readonly Dictionary<string, string> _questionMap = new Dictionary<string, string>();
        private DateTime _sessionStartTime;

        private Coroutine _flushLoopCoroutine;
        private bool _isEndingSession;
        private bool _crashHandlerRegistered;
        private PlayProbeConfig _runtimeConfig;

        private void Awake()
        {
            try
            {
                if (Instance != null && Instance != this)
                {
                    Destroy(gameObject);
                    return;
                }

                Instance = this;
                DontDestroyOnLoad(gameObject);

                BuildRuntimeConfig();
                PlayProbeHttpOld.Configure(_runtimeConfig);

                Events = new PlayProbeEvents(_runtimeConfig);
                SurveyOld = new PlayProbeSurveyOld(_runtimeConfig, Events);
                Analytics = new PlayProbeAnalytics(_runtimeConfig, Events);
                Session = null;

                if (enableCrashReporting)
                {
                    Application.logMessageReceived += HandleLogMessageReceived;
                    _crashHandlerRegistered = true;
                }

                StartCoroutine(StartSessionAsync());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] Initialization failed: " + ex.Message);
                IsSessionActive = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            if (_flushLoopCoroutine != null)
            {
                StopCoroutine(_flushLoopCoroutine);
                _flushLoopCoroutine = null;
            }

            if (_crashHandlerRegistered)
            {
                Application.logMessageReceived -= HandleLogMessageReceived;
                _crashHandlerRegistered = false;
            }

            Analytics?.StopTracking();
            Events?.StopAutoFlush();

            if (_runtimeConfig != null)
            {
                Destroy(_runtimeConfig);
                _runtimeConfig = null;
            }

            Instance = null;
        }

        private IEnumerator StartSessionAsync()
        {
            if (string.IsNullOrWhiteSpace(testToken))
            {
                Debug.LogWarning("[PlayProbe] testToken is empty. Session start skipped.");
                IsSessionActive = false;
                yield break;
            }

            List<SurveySchemaItem> surveySchema = new List<SurveySchemaItem>();

            try
            {
                if (SurveyOld != null)
                {
                    surveySchema = SurveyOld.GetRegisteredSchema();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] Failed to read survey schema: " + ex.Message);
            }

            PlayProbeSdkSessionStartRequest payload = new PlayProbeSdkSessionStartRequest
            {
                share_token = testToken,
                sdk_version = SDK_VERSION,
                unity_version = Application.unityVersion,
                platform = Application.platform.ToString(),
                screen_width = Screen.width,
                screen_height = Screen.height,
                survey_schema = surveySchema
            };

            string payloadJson;

            try
            {
                payloadJson = JsonUtility.ToJson(payload);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] Could not build start-session payload: " + ex.Message);
                IsSessionActive = false;
                yield break;
            }

            long statusCode = 0;
            string responseBody = string.Empty;
            string requestError = string.Empty;

            yield return PostJsonRequestCoroutine(
                "/sdk-session-start",
                payloadJson,
                (code, body, error) =>
                {
                    statusCode = code;
                    responseBody = body;
                    requestError = error;
                }
            );

            if (!string.IsNullOrWhiteSpace(requestError) || statusCode != 200)
            {
                string resolvedError = !string.IsNullOrWhiteSpace(requestError)
                    ? requestError
                    : "HTTP " + statusCode;

                Debug.LogWarning("[PlayProbe] Could not start session: " + resolvedError);
                IsSessionActive = false;
                yield break;
            }

            string parseError;

            if (!TryApplyStartSessionResponse(responseBody, out parseError))
            {
                Debug.LogWarning("[PlayProbe] Could not start session: " + parseError);
                IsSessionActive = false;
                yield break;
            }

            IsSessionActive = true;
            _sessionStartTime = DateTime.UtcNow;

            if (enableFpsTracking)
            {
                Analytics?.StartTracking(_runtimeConfig);
            }

            if (_flushLoopCoroutine != null)
            {
                StopCoroutine(_flushLoopCoroutine);
            }

            _flushLoopCoroutine = StartCoroutine(EventFlushLoop());

            Debug.Log("[PlayProbe] Session started: " + SessionId);
        }

        public void EndSession()
        {
            if (!IsSessionActive || _isEndingSession)
            {
                return;
            }

            StartCoroutine(EndSessionAsync());
        }

        private IEnumerator EndSessionAsync()
        {
            if (!IsSessionActive || _isEndingSession)
            {
                yield break;
            }

            _isEndingSession = true;
            IsSessionActive = false;

            StopSessionTracking();

            yield return FlushEvents();
            yield return SendSessionEndRequestAsync();

            Debug.Log("[PlayProbe] Session ended.");

            SessionId = null;

            if (Events != null)
            {
                Events.SessionId = null;
            }

            _isEndingSession = false;
        }

        private void OnApplicationQuit()
        {
            EndSessionSynchronously();
        }

        private void EndSessionSynchronously()
        {
            if (!IsSessionActive || _isEndingSession)
            {
                return;
            }

            _isEndingSession = true;
            IsSessionActive = false;

            StopSessionTracking();

            try
            {
                // Best effort only: flush uses coroutine-backed transport and may not complete during quit.
                Events?.FlushPendingEvents();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] Failed to flush events during quit: " + ex.Message);
            }

            try
            {
                PlayProbeSdkSessionEndRequest payload = BuildSessionEndPayload();
                string payloadJson = JsonUtility.ToJson(payload);

                long statusCode;
                string responseBody;
                string requestError;

                PostJsonRequestBlocking("/sdk-session-end", payloadJson, out statusCode, out responseBody, out requestError);

                if (!string.IsNullOrWhiteSpace(requestError) || statusCode < 200 || statusCode >= 300)
                {
                    string resolvedError = !string.IsNullOrWhiteSpace(requestError)
                        ? requestError
                        : "HTTP " + statusCode;

                    Debug.LogWarning("[PlayProbe] Could not end session: " + resolvedError);
                }
                else
                {
                    Debug.Log("[PlayProbe] Session ended.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] Could not end session: " + ex.Message);
            }
            finally
            {
                SessionId = null;

                if (Events != null)
                {
                    Events.SessionId = null;
                }

                _isEndingSession = false;
            }
        }

        private IEnumerator SendSessionEndRequestAsync()
        {
            PlayProbeSdkSessionEndRequest payload;

            try
            {
                payload = BuildSessionEndPayload();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] Could not build end-session payload: " + ex.Message);
                yield break;
            }

            string payloadJson;

            try
            {
                payloadJson = JsonUtility.ToJson(payload);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] Could not serialize end-session payload: " + ex.Message);
                yield break;
            }

            long statusCode = 0;
            string responseBody = string.Empty;
            string requestError = string.Empty;

            yield return PostJsonRequestCoroutine(
                "/sdk-session-end",
                payloadJson,
                (code, body, error) =>
                {
                    statusCode = code;
                    responseBody = body;
                    requestError = error;
                }
            );

            if (!string.IsNullOrWhiteSpace(requestError) || statusCode < 200 || statusCode >= 300)
            {
                string resolvedError = !string.IsNullOrWhiteSpace(requestError)
                    ? requestError
                    : "HTTP " + statusCode;

                Debug.LogWarning("[PlayProbe] Could not end session: " + resolvedError);
            }
        }

        private IEnumerator FlushEvents()
        {
            if (Events == null)
            {
                yield break;
            }

            Task flushTask;

            try
            {
                flushTask = Events.FlushPendingEvents();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] Failed to flush events: " + ex.Message);
                yield break;
            }

            if (flushTask == null)
            {
                yield break;
            }

            while (!flushTask.IsCompleted)
            {
                yield return null;
            }

            if (flushTask.IsFaulted)
            {
                Exception root = flushTask.Exception != null ? flushTask.Exception.GetBaseException() : null;
                string message = root != null ? root.Message : "Unknown flush failure.";
                Debug.LogWarning("[PlayProbe] Failed to flush events: " + message);
            }
        }

        private IEnumerator EventFlushLoop()
        {
            WaitForSeconds wait = new WaitForSeconds(30f);

            while (IsSessionActive)
            {
                yield return wait;
                yield return FlushEvents();
            }
        }

        private void StopSessionTracking()
        {
            if (_flushLoopCoroutine != null)
            {
                StopCoroutine(_flushLoopCoroutine);
                _flushLoopCoroutine = null;
            }

            Analytics?.StopTracking();
        }

        private PlayProbeSdkSessionEndRequest BuildSessionEndPayload()
        {
            List<SurveyResponse> surveyResponses = new List<SurveyResponse>();

            try
            {
                if (SurveyOld != null)
                {
                    surveyResponses = SurveyOld.GetBufferedResponses();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] Failed to collect buffered survey responses: " + ex.Message);
            }

            double durationSeconds = 0d;

            if (_sessionStartTime != default(DateTime))
            {
                durationSeconds = Math.Max(0d, (DateTime.UtcNow - _sessionStartTime).TotalSeconds);
            }

            return new PlayProbeSdkSessionEndRequest
            {
                session_id = SessionId,
                duration_seconds = durationSeconds,
                avg_fps = Analytics != null ? Analytics.AverageFps : 0f,
                min_fps = Analytics != null ? Analytics.MinFps : 0f,
                survey_responses = surveyResponses
            };
        }

        private bool TryApplyStartSessionResponse(string responseJson, out string error)
        {
            error = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(responseJson))
                {
                    error = "empty start-session response.";
                    return false;
                }

                PlayProbeSdkSessionStartResponse response = JsonUtility.FromJson<PlayProbeSdkSessionStartResponse>(responseJson);

                if (response == null)
                {
                    error = "invalid start-session response json.";
                    return false;
                }

                string resolvedSessionId = response.session_id;

                if (string.IsNullOrWhiteSpace(resolvedSessionId))
                {
                    error = "session_id missing from start-session response.";
                    return false;
                }

                SessionId = resolvedSessionId;

                if (Events != null)
                {
                    Events.SessionId = SessionId;
                }

                _questionMap.Clear();

                if (response != null && response.question_map != null)
                {
                    for (int i = 0; i < response.question_map.Length; i++)
                    {
                        AddQuestionMapEntry(response.question_map[i]);
                    }
                }

                List<SurveyTrigger> surveyTriggers = new List<SurveyTrigger>();

                if (response != null && response.survey_triggers != null)
                {
                    surveyTriggers.AddRange(response.survey_triggers);
                }

                ApplyQuestionMapToTriggers(surveyTriggers);

                try
                {
                    SurveyOld?.LoadTriggers(surveyTriggers);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[PlayProbe] Failed to load survey triggers: " + ex.Message);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private void AddQuestionMapEntry(PlayProbeQuestionMapEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            string key = entry.sdk_question_id;
            string value = FirstNonEmpty(entry.question_id, entry.form_question_id);

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                _questionMap[key] = value;
            }
        }

        private void ApplyQuestionMapToTriggers(List<SurveyTrigger> surveyTriggers)
        {
            if (surveyTriggers == null || surveyTriggers.Count == 0 || _questionMap.Count == 0)
            {
                return;
            }

            for (int triggerIndex = 0; triggerIndex < surveyTriggers.Count; triggerIndex++)
            {
                SurveyTrigger trigger = surveyTriggers[triggerIndex];

                if (trigger == null || trigger.questions == null)
                {
                    continue;
                }

                for (int questionIndex = 0; questionIndex < trigger.questions.Count; questionIndex++)
                {
                    SurveyQuestionData question = trigger.questions[questionIndex];

                    if (question == null || !string.IsNullOrWhiteSpace(question.question_id) || string.IsNullOrWhiteSpace(question.sdk_question_id))
                    {
                        continue;
                    }

                    if (_questionMap.TryGetValue(question.sdk_question_id, out string mappedQuestionId))
                    {
                        question.question_id = mappedQuestionId;
                    }
                }
            }
        }

        private void BuildRuntimeConfig()
        {
            _runtimeConfig = ScriptableObject.CreateInstance<PlayProbeConfig>();
            _runtimeConfig.enableFpsTracking = enableFpsTracking;
            _runtimeConfig.enablePositionHeatmap = enablePositionHeatmap;
            _runtimeConfig.positionLogInterval = positionLogInterval;

            // Manager owns crash registration now to avoid duplicate handlers.
            _runtimeConfig.enableCrashReporting = false;

            // Keep survey behavior defaults even when not using a config asset.
            _runtimeConfig.allowSurveyDismiss = true;
            _runtimeConfig.pauseTimeDuringSurvey = true;
        }

        private void HandleLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error)
            {
                return;
            }

            try
            {
                string errorMessage = string.IsNullOrWhiteSpace(stackTrace)
                    ? condition
                    : condition + "\n" + stackTrace;

                Events?.LogException(new Exception(errorMessage));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] Failed to report crash: " + ex.Message);
            }
        }

        private IEnumerator PostJsonRequestCoroutine(string endpoint, string payloadJson, Action<long, string, string> onComplete)
        {
            string url = BuildUrl(endpoint);

            using (UnityWebRequest request = CreatePostRequest(url, payloadJson))
            {
                UnityWebRequestAsyncOperation operation;

                try
                {
                    operation = request.SendWebRequest();
                }
                catch (Exception ex)
                {
                    onComplete?.Invoke(0, string.Empty, ex.Message);
                    yield break;
                }

                yield return operation;

                long statusCode = request.responseCode;
                string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                string requestError = string.Empty;

                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.ProtocolError)
                {
                    requestError = "HTTP " + statusCode + " " + request.error + ": " + responseBody;
                }

                onComplete?.Invoke(statusCode, responseBody, requestError);
            }
        }

        private void PostJsonRequestBlocking(string endpoint, string payloadJson, out long statusCode, out string responseBody, out string requestError)
        {
            statusCode = 0;
            responseBody = string.Empty;
            requestError = string.Empty;

            string url = BuildUrl(endpoint);

            using (UnityWebRequest request = CreatePostRequest(url, payloadJson))
            {
                try
                {
                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                    }

                    statusCode = request.responseCode;
                    responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

                    if (request.result == UnityWebRequest.Result.ConnectionError ||
                        request.result == UnityWebRequest.Result.ProtocolError)
                    {
                        requestError = "HTTP " + statusCode + " " + request.error + ": " + responseBody;
                    }
                }
                catch (Exception ex)
                {
                    requestError = ex.Message;
                }
            }
        }

        private static UnityWebRequest CreatePostRequest(string url, string payloadJson)
        {
            byte[] body = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);

            UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 10
            };

            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        private static string BuildUrl(string endpoint)
        {
            string normalizedBase = API_BASE.TrimEnd('/');
            string normalizedEndpoint = endpoint.StartsWith("/", StringComparison.Ordinal) ? endpoint : "/" + endpoint;
            return normalizedBase + normalizedEndpoint;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return values[i];
                }
            }

            return string.Empty;
        }
        
        public static async void StartSession(PlayProbeConfig config)
        {

            List<SurveySchemaItem> surveyItems = new List<SurveySchemaItem>();
            surveyItems.AddRange(new  List<SurveySchemaItem>
            {
                new SurveySchemaItem()                {
                   trigger_key = "test_scene_1",
                   questions = new List<SurveyQuestionSchema>() {
                       new SurveyQuestionSchema() { 
                           sdk_question_id = "some_unique_id",
                           question_type = "rating",
                           label = "How would you rate this scene?",
                           options = null,
                           required = true,
                           order_index = 0
                       }
                   }
                },
            });
            PlayProbeSdkSessionStartRequest startSessionRequest = new PlayProbeSdkSessionStartRequest()
            {
                share_token = "b7beacf2-1438-4743-9af8-895518ae25b4",
                handoff_token = "DNSRL2VW",
                sdk_version = PlayProbeConfig.SDKVersion,
                unity_version = Application.unityVersion,
                platform = "Windows",//Application.platform.ToString(),
                screen_width = Screen.width,
                screen_height = Screen.height,
                survey_schema = surveyItems
            };
            Debug.Log("Application platform: " + Application.platform.ToString());

            string payloadJson = JsonUtility.ToJson(startSessionRequest);
            
            const string url = "api.playprobe.io/";
            using (UnityWebRequest request = CreatePostRequest(url+"sdk-start-session", payloadJson))
            {
                 await request.SendWebRequest();
                 long statusCode = request.responseCode;
                 string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                 string requestError = string.Empty;
                 Debug.Log($"Status code: {statusCode}");
                 Debug.Log($"Response body: {responseBody}");
            
                 if (request.result == UnityWebRequest.Result.ConnectionError ||
                     request.result == UnityWebRequest.Result.ProtocolError)
                 {
                     requestError = "HTTP " + statusCode + " " + request.error + ": " + responseBody;
                 }
                 Debug.Log($"Request error: {requestError}");
             }

            return;
            SdkEventPayload eventPayload = new SdkEventPayload()
            {
                session_id = "4c48971e-3573-4224-af00-014b82f334fa",
                events = new List<SdkEvent>()
                {
                    new SdkEvent()
                    {
                        event_type = "fps",
                        event_name = "avg_fps",
                        value_num = 58.3,
                        value_text = "",
                        value_json = "",
                        timestamp = DateTime.UtcNow.ToString("o")
                    },
                    new SdkEvent()
                    {
                        event_type = "custom",
                        event_name = "item_collected",
                        value_num = 50,
                        value_text = "gold_coin",
                        value_json = JsonUtility.ToJson(new PlayProbeDictionaryEntry()
                        {
                            key = "item_collected",
                            value = "Not what you expect"
                        }),
                        timestamp = DateTime.UtcNow.ToString("o")
                    },
                    new SdkEvent()
                    {
                        event_type = "custom",
                        event_name = "item_collected",
                        value_num = 50,
                        value_text = "gold_coin",
                        value_json = JsonUtility.ToJson(new PlayProbeDictionaryEntry()
                        {
                            key = "item_collected",
                            value = "Not what you expect"
                        }),
                        timestamp = DateTime.UtcNow.ToString("o")
                    }
                }
            }; 
            
            payloadJson = JsonUtility.ToJson(eventPayload);
            Debug.Log(payloadJson);
            
            using (UnityWebRequest request = CreatePostRequest(url+"sdk-events", payloadJson))
            {
                await request.SendWebRequest();
                long statusCode = request.responseCode;
                string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                string requestError = string.Empty;
                Debug.Log($"Status code: {statusCode}");
                Debug.Log($"Response body: {responseBody}");

                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.ProtocolError)
                {
                    requestError = "HTTP " + statusCode + " " + request.error + ": " + responseBody;
                }
                Debug.Log($"Request error: {requestError}");
            }
        }

        public static async void EndSession(PlayProbeConfig config)
        {
            PlayProbeSdkSessionEndRequest endRequestPayload = new PlayProbeSdkSessionEndRequest()
            {
                session_id = config.shareToken, //is session id but for test
                duration_seconds = 123.45,
                avg_fps = 58.3f,
                min_fps = 30.1f,
                survey_responses = new List<SurveyResponse>()
            };
            
            string payloadJson = JsonUtility.ToJson(endRequestPayload);
            const string url = "api.playprobe.io/";
            using (UnityWebRequest request = CreatePostRequest(url+"sdk-session-end", payloadJson))
            {
                await request.SendWebRequest();
                long statusCode = request.responseCode;
                string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                string requestError = string.Empty;
                Debug.Log($"Status code: {statusCode}");
                Debug.Log($"Response body: {responseBody}");
            
                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.ProtocolError)
                {
                    requestError = "HTTP " + statusCode + " " + request.error + ": " + responseBody;
                }
                Debug.Log($"Request error: {requestError}");
            }
        }
    }
}
