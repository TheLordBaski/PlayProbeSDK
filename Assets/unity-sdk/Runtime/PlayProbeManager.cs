// Copyright PlayProbe.io 2026. All rights reserved

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlayProbe.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayProbe
{
    [DisallowMultipleComponent]
    public class PlayProbeManager : MonoBehaviour
    {
        public static PlayProbeManager Instance { get; private set; }

        [SerializeField] private PlayProbeConfig config;

        private PlayProbeRuntimeConfig _runtimeConfig;

        public bool IsSessionActive { get; private set; }

        public PlayProbeSurvey Survey { get; private set; }

        public PlayProbeAnalytics Analytics { get; private set; }

        internal PlayProbeEvents Events { get; private set; }

        public List<SurveySchemaItem> surveySchemaItems;

        private DateTime _sessionStartUtc;

        #region Monobehaviour

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

                Survey = new PlayProbeSurvey(_runtimeConfig);
                Events = new PlayProbeEvents(_runtimeConfig);
                Analytics = new PlayProbeAnalytics(config);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayProbe] Initialization failed: {exception.Message}");
                IsSessionActive = false;
            }
        }

        private void OnApplicationQuit()
        {
            EndSession();
        }

        #endregion


        #region Public methods

        //TODO: Add Application.logMessageReceived += HandleLogMessageReceived;
        public void StartSession()
        {
            if (string.IsNullOrWhiteSpace(_runtimeConfig.ShareToken))
            {
                Debug.LogWarning("[PlayProbe] ShareToken is empty. Session start skipped.");
                IsSessionActive = false;
                return;
            }

            if (_runtimeConfig.IsStandaloneTest)
            {
                StartStandaloneSession();
            }
            else
            {
                ShowHandOffTokenScreen();
            }
        }

        public void EndSession()
        {
            if (!IsSessionActive)
            {
                Debug.Log("[PlayProbe] The session is not active");
                return;
            }

            if (string.IsNullOrWhiteSpace(_runtimeConfig.SessionId))
            {
                Debug.LogWarning("[PlayProbe] Session ID is empty. Session end skipped.");
                return;
            }

            Analytics?.StopTracking();
            Events?.FlushBufferedEvents();
            Events?.StopFlushLoop();

            double durationSeconds = Math.Max(0d, (DateTime.UtcNow - _sessionStartUtc).TotalSeconds);

            PlayProbeSdkSessionEndRequest endRequestPayload = new()
            {
                session_id = _runtimeConfig.SessionId,
                session_token = _runtimeConfig.SessionToken,
                duration_seconds = durationSeconds,
                avg_fps = Analytics != null ? Analytics.AverageFps : 0d,
                min_fps = Analytics != null ? Analytics.MinFps : 0d,
                survey_responses = Survey.GetSurveyResponses()
            };

            IsSessionActive = false;

            EndSessionAsync(endRequestPayload);
        }

        #endregion

        #region Private methods

        private void BuildRuntimeConfig()
        {
            _runtimeConfig = new PlayProbeRuntimeConfig
            {
                AllowSurveyDismiss = config.allowSurveyDismiss,
                EnableCrashReporting = config.enableCrashReporting,
                EnableFpsTracking = config.enableFpsTracking,
                EnablePositionHeatmap = config.enablePositionHeatmap,
                PauseTimeDuringSurvey = config.pauseTimeDuringSurvey,
                ShareToken = config.shareToken,
                PositionLogInterval = config.positionLogInterval,
                IsStandaloneTest = config.isStandaloneTest,
            };
        }

        internal string GetEndpointAddressForFunction(string edgeFunction)
        {
            return _runtimeConfig != null ? $"{PlayProbeRuntimeConfig.ApiEndpoint}{edgeFunction}" : null;
        }

        // Maps Unity's RuntimePlatform (e.g. "WindowsPlayer", "IPhonePlayer") to the vocabulary the
        // sdk-start-session edge function accepts: Windows, macOS, Linux, Android, iOS, WebGL, Editor, WindowsEditor.
        // Application.platform.ToString() does not match that set, so raw values were rejected with a 400.
        // Platforms with no backend equivalent (consoles, other) fall back to "Editor" so start never fails.
        private static string GetNormalizedPlatform()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                    return "Windows";
                case RuntimePlatform.WindowsEditor:
                    return "WindowsEditor";
                case RuntimePlatform.OSXPlayer:
                    return "macOS";
                case RuntimePlatform.LinuxPlayer:
                    return "Linux";
                case RuntimePlatform.Android:
                    return "Android";
                case RuntimePlatform.IPhonePlayer:
                    return "iOS";
                case RuntimePlatform.WebGLPlayer:
                    return "WebGL";
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.LinuxEditor:
                default:
                    return "Editor";
            }
        }

        // Response bodies contain the session_id (the SDK's only capability token), so they are logged
        // only in the editor and development builds, never in shipped players.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVerbose(string message)
        {
            Debug.Log($"[PlayProbe] {message}");
        }

        private void ShowHandOffTokenScreen()
        {
            PlayProbeTokenInputController startScreen =
                Resources.Load<PlayProbeTokenInputController>("PlayProbeStartSessionScreen");
            if (startScreen == null)
            {
                IsSessionActive = false;
                Debug.LogWarning("[PlayProbe] Could not load session start screen prefab.");
                return;
            }

            Instantiate(startScreen);
        }

        internal async Task<bool> CheckHandOffStatus(string handOffToken)
        {
            PlayProbeCheckTokenRequest payloadRequest = new()
            {
                share_token = _runtimeConfig.ShareToken,
                handoff_token = handOffToken
            };

            string payloadJson;
            try
            {
                payloadJson = JsonUtility.ToJson(payloadRequest);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayProbe] Could not build check-token payload: {ex.Message}");
                return false;
            }

            using (UnityWebRequest request =
                   PlayProbeHttp.CreatePostRequest(GetEndpointAddressForFunction("sdk-check-function"), payloadJson))
            {
                await request.SendWebRequest();
                long statusCode = request.responseCode;
                string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                if (request.result is UnityWebRequest.Result.ConnectionError
                    or UnityWebRequest.Result.ProtocolError)
                {
                    string requestError = request.error;
                    Debug.LogWarning($"[PlayProbe] Check token request error: {requestError}");
                    return false;
                }

                if (statusCode != 200)
                {
                    Debug.LogWarning(
                        $"[PlayProbe] Check token request failed with status code {statusCode} and response: {responseBody}");
                    return false;
                }

                try
                {
                    PlayProbeCheckTokenResponse responseData =
                        JsonUtility.FromJson<PlayProbeCheckTokenResponse>(responseBody);
                    return responseData.isTokenCorrect;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayProbe] Could not parse Check token response: {ex.Message}");
                }
            }

            return false;
        }

        private async void EndSessionAsync(PlayProbeSdkSessionEndRequest payloadRequest)
        {
            string payloadJson;
            try
            {
                payloadJson = JsonUtility.ToJson(payloadRequest);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayProbe] Could not build check-token payload: {ex.Message}");
                return;
            }

            using (UnityWebRequest request =
                   PlayProbeHttp.CreatePostRequest(GetEndpointAddressForFunction("sdk-session-end"), payloadJson))
            {
                await request.SendWebRequest();
                long statusCode = request.responseCode;
                string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                if (request.result is UnityWebRequest.Result.ConnectionError
                    or UnityWebRequest.Result.ProtocolError)
                {
                    string requestError = request.error;
                    Debug.LogWarning($"[PlayProbe] Check token request error: {requestError}");
                    return;
                }

                if (statusCode != 200)
                {
                    Debug.LogWarning(
                        $"[PlayProbe] Check token request failed with status code {statusCode} and response: {responseBody}");
                    return;
                }

                try
                {
                    Debug.Log("[PlayProbe] Session ended successfully.");
                    LogVerbose(responseBody);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayProbe] Could not parse Check token response: {ex.Message}");
                }
            }
        }

        internal void StartHandOffSession(string handOffToken)
        {
            List<SurveySchemaItem> surveySchema = Survey.GetRegisteredSurveySchema();

            PlayProbeSdkSessionStartRequest startRequestPayload = new()
            {
                share_token = _runtimeConfig.ShareToken,
                handoff_token = handOffToken,
                sdk_version = PlayProbeRuntimeConfig.SdkVersion,
                unity_version = Application.unityVersion,
                platform = GetNormalizedPlatform(),
                screen_width = Screen.width,
                screen_height = Screen.height,
                survey_schema = surveySchema
            };
            StartSessionAsync(startRequestPayload);
        }

        private void StartStandaloneSession()
        {
            List<SurveySchemaItem> surveySchema = Survey.GetRegisteredSurveySchema();

            PlayProbeSdkSessionStartRequest startRequestPayload = new()
            {
                share_token = _runtimeConfig.ShareToken,
                sdk_version = PlayProbeRuntimeConfig.SdkVersion,
                unity_version = Application.unityVersion,
                platform = GetNormalizedPlatform(),
                screen_width = Screen.width,
                screen_height = Screen.height,
                survey_schema = surveySchema
            };
            StartSessionAsync(startRequestPayload);
        }

        private async void StartSessionAsync(PlayProbeSdkSessionStartRequest startRequestPayload)
        {
            try
            {
                string payloadJson;
                try
                {
                    payloadJson = JsonUtility.ToJson(startRequestPayload);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayProbe] Could not build start-session payload: {ex.Message}");
                    IsSessionActive = false;
                    return;
                }

                using (UnityWebRequest request =
                       PlayProbeHttp.CreatePostRequest(GetEndpointAddressForFunction("sdk-start-session"), payloadJson))
                {
                    await request.SendWebRequest();
                    long statusCode = request.responseCode;
                    string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                    LogVerbose(responseBody);
                    if (request.result is UnityWebRequest.Result.ConnectionError
                        or UnityWebRequest.Result.ProtocolError)
                    {
                        string requestError = request.error;
                        Debug.LogWarning($"[PlayProbe] Start session request error: {requestError}");
                        IsSessionActive = false;
                    }
                    else if (statusCode != 200)
                    {
                        Debug.LogWarning(
                            $"[PlayProbe] Start session request failed with status code {statusCode} and response: {responseBody}");
                        IsSessionActive = false;
                    }
                    else
                    {
                        try
                        {
                            PlayProbeSdkSessionStartResponse startResponse =
                                JsonUtility.FromJson<PlayProbeSdkSessionStartResponse>(responseBody);
                            _runtimeConfig.SessionId = startResponse.session_id;
                            _runtimeConfig.SessionToken = startResponse.session_token;
                            surveySchemaItems = startResponse.survey_triggers.ToList();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[PlayProbe] Could not parse start-session response: {ex.Message}");
                            IsSessionActive = false;
                            return;
                        }

                        Debug.Log("[PlayProbe] Session started successfully.");
                        IsSessionActive = true;
                        _sessionStartUtc = DateTime.UtcNow;
                        Analytics?.StartTracking();
                        Events?.StartFlushLoop();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayProbe] Could not start session properly: {ex.Message}");
                IsSessionActive = false;
            }
        }

        #endregion

        internal async void SubmitSurveyResponses(List<SurveyResponse> responses)
        {
            try
            {
                PlayProbeSurveySubmitRequest requestPayload = new()
                {
                    session_id = _runtimeConfig.SessionId,
                    session_token = _runtimeConfig.SessionToken,
                    survey_responses = responses
                };
                string payloadJson;
                try
                {
                    payloadJson = JsonUtility.ToJson(requestPayload);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayProbe] Could not build mid-survey payload: {ex.Message}");
                    IsSessionActive = false;
                    return;
                }

                using (UnityWebRequest request =
                       PlayProbeHttp.CreatePostRequest(GetEndpointAddressForFunction("sdk-mid-survey"), payloadJson))
                {
                    await request.SendWebRequest();
                    long statusCode = request.responseCode;
                    string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                    if (request.result is UnityWebRequest.Result.ConnectionError
                        or UnityWebRequest.Result.ProtocolError)
                    {
                        string requestError = request.error;
                        Debug.LogWarning($"[PlayProbe] Survey submit request error: {requestError}");
                    }
                    else if (statusCode != 200)
                    {
                        Debug.LogWarning(
                            $"[PlayProbe] Survey submit request failed with status code {statusCode} and response: {responseBody}");
                    }
                    else
                    {
                        Debug.Log("[PlayProbe] Survey submitted successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayProbe] Could not start session properly: {ex.Message}");
                IsSessionActive = false;
            }
        }

        public async void ShowSurvey(string trigger)
        {
            //spawn survey canvas
            try
            {
                ResourceRequest handle = Resources.LoadAsync<GameObject>("PlayProbeSurveyCanvas");
                await handle;

                if (!handle.isDone || handle.asset == null)
                {
                    Debug.LogWarning($"[PlayProbe] Could not load survey prefab from Resources");
                    return;
                }

                if (!(handle.asset is GameObject prefab))
                {
                    Debug.LogWarning($"[PlayProbe] Resource 'PlayProbeSurveyCanvas' is not a GameObject prefab.");
                    return;
                }

                GameObject questionObject = Instantiate(prefab);
                PlayProbeSurveyCanvas playProbeQuestionElement =
                    questionObject.GetComponent<PlayProbeSurveyCanvas>();

                if (playProbeQuestionElement == null)
                {
                    Debug.LogWarning(
                        $"[PlayProbe] Prefab does not contain a component PlayProbeSurveyCanvas.");
                    Destroy(questionObject);
                    return;
                }

                SurveySchemaItem questionSchema = surveySchemaItems.FirstOrDefault(item => item.trigger_key == trigger);
                if (questionSchema != null) playProbeQuestionElement.Initialize(questionSchema.questions);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"[PlayProbe] Prefab 'PlayProbeSurveyCanvas' does not contain a component implementing IQuestionElement. {e}");
            }
            //show canvas
            //pause game if needed
        }
    }

    internal class PlayProbeRuntimeConfig
    {
        public const string ApiEndpoint = "https://api.playprobe.io/";
        public const string SdkVersion = "0.1.0";
        public string ShareToken { get; set; }
        public string SessionId { get; set; }
        public string SessionToken { get; set; }
        public bool IsStandaloneTest { get; set; }

        public string HandOffToken { get; set; }

        public bool AllowSurveyDismiss { get; set; }
        public float PositionLogInterval { get; set; }
        public bool EnableCrashReporting { get; set; }
        public bool EnableFpsTracking { get; set; }
        public bool EnablePositionHeatmap { get; set; }
        public bool PauseTimeDuringSurvey { get; set; }
    }
}