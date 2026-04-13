using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayProbe
{
    [Serializable]
    internal class PlayProbeTestLookupRow
    {
        public string id;
    }

    [Serializable]
    internal class PlayProbeSessionCreateRow
    {
        public string id;
    }

    [Serializable]
    internal class PlayProbeSessionArrayWrapper<T>
    {
        public T[] items;
    }

    [Serializable]
    internal class PlayProbeSessionCreatePayload
    {
        public string test_id;
        public string session_token;
        public string status;
        public string play_started_at;
    }

    [Serializable]
    internal class PlayProbeSdkSessionPayload
    {
        public string session_id;
        public string sdk_version;
        public string unity_version;
        public string platform;
        public int screen_width;
        public int screen_height;
    }

    [Serializable]
    internal class PlayProbeSessionCompletePayload
    {
        public string status;
        public string completed_at;
        public double play_duration_seconds;
    }

    [Serializable]
    internal class PlayProbeSdkSessionFpsPayload
    {
        public float avg_fps;
        public float min_fps;
    }

    public class PlayProbeSession
    {
        private const string SdkVersion = "0.1.0";

        private readonly PlayProbeConfig _config;
        private readonly PlayProbeEvents _events;
        private readonly PlayProbeAnalytics _analytics;

        private readonly object _bufferLock = new object();
        private readonly List<SdkEventPayloadOld> _bufferedEvents = new List<SdkEventPayloadOld>();

        public string SessionId { get; private set; }
        public string SessionToken { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime StartTime { get; private set; }

        public PlayProbeSession(PlayProbeConfig config, PlayProbeEvents events, PlayProbeAnalytics analytics = null)
        {
            _config = config;
            _events = events;
            _analytics = analytics;
        }

        public async Task<bool> StartSession(string shareToken)
        {
            try
            {
                if (_config == null)
                {
                    Debug.LogWarning("[PlayProbe] StartSession skipped because PlayProbeConfig is missing.");
                    return false;
                }

                string resolvedShareToken = string.IsNullOrWhiteSpace(shareToken) ? _config.shareToken : shareToken;

                if (string.IsNullOrWhiteSpace(resolvedShareToken))
                {
                    Debug.LogWarning("[PlayProbe] StartSession failed because share token is empty.");
                    return false;
                }

                string testId;

                try
                {
                    string encodedShareToken = Uri.EscapeDataString(resolvedShareToken);
                    string lookupEndpoint = "/rest/v1/tests?share_token=eq." + encodedShareToken + "&select=id&limit=1";
                    string lookupResponse = await SendRequestAsync(UnityWebRequest.kHttpVerbGET, lookupEndpoint, null, false);
                    PlayProbeTestLookupRow[] tests = ParseArray<PlayProbeTestLookupRow>(lookupResponse);

                    if (tests.Length == 0 || string.IsNullOrWhiteSpace(tests[0].id))
                    {
                        Debug.LogWarning("[PlayProbe] StartSession failed because no test matched the share token.");
                        return false;
                    }

                    testId = tests[0].id;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[PlayProbe] StartSession failed during test lookup: " + ex.Message);
                    return false;
                }

                SessionToken = Guid.NewGuid().ToString();
                DateTime now = DateTime.UtcNow;

                try
                {
                    PlayProbeSessionCreatePayload createSessionPayload = new PlayProbeSessionCreatePayload
                    {
                        test_id = testId,
                        session_token = SessionToken,
                        status = "playing",
                        play_started_at = now.ToString("o")
                    };

                    string createSessionJson = JsonUtility.ToJson(createSessionPayload);
                    string createSessionResponse = await SendRequestAsync(
                        UnityWebRequest.kHttpVerbPOST,
                        "/rest/v1/sessions",
                        createSessionJson,
                        true
                    );

                    PlayProbeSessionCreateRow[] createdSessions = ParseArray<PlayProbeSessionCreateRow>(createSessionResponse);

                    if (createdSessions.Length == 0 || string.IsNullOrWhiteSpace(createdSessions[0].id))
                    {
                        Debug.LogWarning("[PlayProbe] StartSession failed because sessions insert did not return an id.");
                        return false;
                    }

                    SessionId = createdSessions[0].id;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[PlayProbe] StartSession failed creating session row: " + ex.Message);
                    return false;
                }

                try
                {
                    PlayProbeSdkSessionPayload sdkSessionPayload = new PlayProbeSdkSessionPayload
                    {
                        session_id = SessionId,
                        sdk_version = SdkVersion,
                        unity_version = Application.unityVersion,
                        platform = Application.platform.ToString(),
                        screen_width = Screen.width,
                        screen_height = Screen.height
                    };

                    string sdkSessionJson = JsonUtility.ToJson(sdkSessionPayload);
                    await SendRequestAsync(UnityWebRequest.kHttpVerbPOST, "/rest/v1/sdk_sessions", sdkSessionJson, false);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[PlayProbe] StartSession failed creating sdk_sessions row: " + ex.Message);
                    return false;
                }

                IsActive = true;
                StartTime = now;

                if (_events != null)
                {
                    _events.SessionId = SessionId;
                }

                BufferInternalEvent("session_started", "{\"session_id\":\"" + SessionId + "\"}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] StartSession unexpected error: " + ex.Message);
                return false;
            }
        }

        public async Task EndSession()
        {
            if (!IsActive || string.IsNullOrWhiteSpace(SessionId))
            {
                return;
            }

            DateTime completedAt = DateTime.UtcNow;
            double durationSeconds = (completedAt - StartTime).TotalSeconds;

            try
            {
                PlayProbeSessionCompletePayload completePayload = new PlayProbeSessionCompletePayload
                {
                    status = "complete",
                    completed_at = completedAt.ToString("o"),
                    play_duration_seconds = durationSeconds
                };

                string endpoint = "/rest/v1/sessions?id=eq." + Uri.EscapeDataString(SessionId);
                string json = JsonUtility.ToJson(completePayload);
                await SendRequestAsync("PATCH", endpoint, json, false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] EndSession failed updating session status: " + ex.Message);
            }

            try
            {
                if (_config != null && _config.enableFpsTracking && _analytics != null && _analytics.HasFpsSamples)
                {
                    PlayProbeSdkSessionFpsPayload fpsPayload = new PlayProbeSdkSessionFpsPayload
                    {
                        avg_fps = _analytics.AverageFps,
                        min_fps = _analytics.MinFps
                    };

                    string endpoint = "/rest/v1/sdk_sessions?session_id=eq." + Uri.EscapeDataString(SessionId);
                    string json = JsonUtility.ToJson(fpsPayload);
                    await SendRequestAsync("PATCH", endpoint, json, false);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] EndSession failed updating FPS stats: " + ex.Message);
            }

            try
            {
                List<SdkEventPayloadOld> eventsToFlush;

                lock (_bufferLock)
                {
                    eventsToFlush = new List<SdkEventPayloadOld>(_bufferedEvents);
                    _bufferedEvents.Clear();
                }

                await FlushEvents(eventsToFlush);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] EndSession failed flushing buffered SDK events: " + ex.Message);
            }

            try
            {
                if (_events != null)
                {
                    await _events.FlushPendingEvents();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] EndSession failed flushing PlayProbe events: " + ex.Message);
            }

            IsActive = false;
        }

        public async Task FlushEvents(List<SdkEventPayloadOld> events)
        {
            try
            {
                if (!IsActive || string.IsNullOrWhiteSpace(SessionId) || events == null || events.Count == 0)
                {
                    return;
                }

                foreach (SdkEventPayloadOld sdkEvent in events)
                {
                    if (sdkEvent == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (string.IsNullOrWhiteSpace(sdkEvent.session_id))
                        {
                            sdkEvent.session_id = SessionId;
                        }

                        if (string.IsNullOrWhiteSpace(sdkEvent.timestamp))
                        {
                            sdkEvent.timestamp = DateTime.UtcNow.ToString("o");
                        }

                        string json = JsonUtility.ToJson(sdkEvent);
                        await SendRequestAsync(UnityWebRequest.kHttpVerbPOST, "/rest/v1/sdk_events", json, false);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[PlayProbe] FlushEvents failed for one event: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] FlushEvents failed: " + ex.Message);
            }
        }

        private void BufferInternalEvent(string eventName, string payloadJson)
        {
            try
            {
                SdkEventPayloadOld sdkEvent = new SdkEventPayloadOld
                {
                    session_id = SessionId,
                    event_type = ResolveSessionEventType(eventName),
                    event_name = eventName,
                    value_json = payloadJson,
                    timestamp = DateTime.UtcNow.ToString("o")
                };

                lock (_bufferLock)
                {
                    _bufferedEvents.Add(sdkEvent);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayProbe] Failed to buffer internal event: " + ex.Message);
            }
        }

        private async Task<string> SendRequestAsync(string method, string endpoint, string jsonBody, bool preferReturnRepresentation)
        {
            if (_config == null)
            {
                throw new InvalidOperationException("PlayProbeConfig is missing.");
            }

            string baseUrl = PlayProbeConfig.ApiEndpoint.TrimEnd('/');
            string normalizedEndpoint = endpoint.StartsWith("/", StringComparison.Ordinal) ? endpoint : "/" + endpoint;
            string url = baseUrl + normalizedEndpoint;

            using (UnityWebRequest request = new UnityWebRequest(url, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                if (preferReturnRepresentation)
                {
                    request.SetRequestHeader("Prefer", "return=representation");
                }

                if (!string.Equals(method, UnityWebRequest.kHttpVerbGET, StringComparison.OrdinalIgnoreCase))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(jsonBody) ? "{}" : jsonBody);
                    request.uploadHandler = new UploadHandlerRaw(bytes);
                }

                TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                operation.completed += _ => tcs.TrySetResult(true);
                await tcs.Task;

                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.ProtocolError)
                {
                    string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                    throw new InvalidOperationException($"HTTP {(int)request.responseCode} {request.error}: {responseBody}");
                }

                return request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            }
        }

        private static T[] ParseArray<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "[]")
            {
                return Array.Empty<T>();
            }

            string wrapped = "{\"items\":" + json + "}";
            PlayProbeSessionArrayWrapper<T> parsed = JsonUtility.FromJson<PlayProbeSessionArrayWrapper<T>>(wrapped);
            return parsed != null && parsed.items != null ? parsed.items : Array.Empty<T>();
        }

        private static string ResolveSessionEventType(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                return "custom";
            }

            string normalized = eventName.ToLowerInvariant();

            if (normalized.Contains("start"))
            {
                return "session_start";
            }

            if (normalized.Contains("end") || normalized.Contains("complete"))
            {
                return "session_end";
            }

            return "custom";
        }
    }
}
