// Copyright PlayProbe.io 2026. All rights reserved

using System;
using System.Collections.Generic;
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
            PlayProbeSdkSessionEndRequest endRequestPayload = new()
            {
                session_id = _runtimeConfig.SessionId,
                //TODO: load this data from analytics
                duration_seconds = UnityEngine.Random.Range(10f,15f),
                avg_fps = 57.0,
                min_fps = 25,
                survey_responses = Survey.GetSurveyResponses()
            };

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

        private string GetEndpointAddressForFunction(string edgeFunction)
        {
            return _runtimeConfig != null ? $"{PlayProbeRuntimeConfig.ApiEndpoint}{edgeFunction}" : null;
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
                    Debug.Log(responseBody);
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
                platform = Application.platform.ToString(),
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
                platform = Application.platform.ToString(),
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
                    Debug.Log(payloadJson);
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
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[PlayProbe] Could not parse start-session response: {ex.Message}");
                            IsSessionActive = false;
                            return;
                        }

                        Debug.Log("[PlayProbe] Session started successfully.");
                        IsSessionActive = true;
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
    }

    internal class PlayProbeRuntimeConfig
    {
        public const string ApiEndpoint = "https://api.playprobe.io/";
        public const string SdkVersion = "0.1.0";
        public string ShareToken { get; set; }
        public string SessionId { get; set; }
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