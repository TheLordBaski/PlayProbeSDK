using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlayProbe.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayProbe
{
    public class PlayProbeEvents
    {
        private const float FLushInterval = 30f;
        private const int FlushThreshold = 20;
        private const int MaxRetries = 3;

        private PlayProbeRuntimeConfig _runtimeConfig;


        private readonly object _bufferLock = new object();

        private readonly List<PlayProbeEvent> _eventBuffer = new();
        private Coroutine _flushCoroutine;

        private bool _isFlushing;
        private int _retryCount;
        private bool _logHandlerRegistered;


        internal PlayProbeEvents(PlayProbeRuntimeConfig runtimeConfig)
        {
            _runtimeConfig = runtimeConfig;
            RegisterCrashHandlerIfNeeded();
        }

        private void RegisterCrashHandlerIfNeeded()
        {
            if (_runtimeConfig == null || !_runtimeConfig.EnableCrashReporting || _logHandlerRegistered)
            {
                return;
            }

            Application.logMessageReceived += HandleUnityLog;
            _logHandlerRegistered = true;
        }

        private void HandleUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error)
            {
                return;
            }

            LogExceptionInternal(new Exception(condition), stackTrace);
        }

        private void LogExceptionInternal(Exception exception, string stackTrace)
        {
            string exceptionType = exception != null ? exception.GetType().Name : "Exception";
            string exceptionMessage = exception != null ? exception.Message : string.Empty;

            PlayProbeEvent payload = new()
            {
                event_type = "exception",
                event_name = exceptionType,
                value_text = exceptionMessage,
                value_json = string.IsNullOrWhiteSpace(stackTrace) ? string.Empty : stackTrace,
                timestamp = DateTime.UtcNow.ToString("o")
            };

            Enqueue(payload);
        }

        private void Enqueue(PlayProbeEvent payload)
        {
            if (payload == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(payload.timestamp))
            {
                payload.timestamp = DateTime.UtcNow.ToString("o");
            }

            bool shouldFlush;

            lock (_bufferLock)
            {
                _eventBuffer.Add(payload);
                shouldFlush = _eventBuffer.Count >= FlushThreshold;
            }

            if (shouldFlush)
            {
                _ = FlushAsync();
            }
        }

        private async Task FlushAsync()
        {
            List<PlayProbeEvent> batch;

            lock (_bufferLock)
            {
                if (_isFlushing || _eventBuffer.Count == 0)
                {
                    return;
                }

                _isFlushing = true;
                batch = new List<PlayProbeEvent>(_eventBuffer);
            }

            PlayProbeEventPayload payload = new()
            {
                session_id = _runtimeConfig.SessionId,
                events = batch
            };
            string payloadJson;
            try
            {
                payloadJson = JsonUtility.ToJson(payload);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayProbe] Failed to serialize event payload: {ex}");
                return;
            }

            try
            {
                using (UnityWebRequest request =
                       PlayProbeHttp.CreatePostRequest(
                           PlayProbeManager.Instance.GetEndpointAddressForFunction("sdk_events"), payloadJson))
                {
                    await request.SendWebRequest();
                    long statusCode = request.responseCode;
                    string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                    if (request.result is UnityWebRequest.Result.ConnectionError
                        or UnityWebRequest.Result.ProtocolError)
                    {
                        string requestError = request.error;
                        Debug.LogWarning($"[PlayProbe] Event request error: {requestError}");
                        return;
                    }

                    if (statusCode != 200)
                    {
                        Debug.LogWarning(
                            $"[PlayProbe]  Event request failed with status code {statusCode} and response: {responseBody}");
                        return;
                    }

                    lock (_bufferLock)
                    {
                        int removeCount = Mathf.Min(batch.Count, _eventBuffer.Count);
                        _eventBuffer.RemoveRange(0, removeCount);
                        _retryCount = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                bool shouldDrop;
                int droppedCount = 0;

                lock (_bufferLock)
                {
                    _retryCount++;
                    shouldDrop = _retryCount >= MaxRetries;

                    if (shouldDrop)
                    {
                        droppedCount = _eventBuffer.Count;
                        _eventBuffer.Clear();
                        _retryCount = 0;
                    }
                }

                Debug.LogWarning("[PlayProbe] Failed to flush sdk events: " + ex.Message);

                if (shouldDrop)
                {
                    Debug.LogWarning("[PlayProbe] Dropped " + droppedCount + " buffered sdk events after 3 retries.");
                }
            }
            finally
            {
                lock (_bufferLock)
                {
                    _isFlushing = false;
                }
            }
        }
        
        internal void LogFps(float fps)
        {
            PlayProbeEvent payload = new ()
            {
                event_type = "fps",
                event_name = "fps_sample",
                value_num = fps,
                timestamp = DateTime.UtcNow.ToString("o")
            };

            Enqueue(payload);
        }
        
        public void LogPosition(Vector3 position, string name, string tag = null)
        {
            PlayProbePositionPayload positionData = new()
            {
                x = position.x,
                y = position.y,
                z = position.z,
                tag = tag
            };

            PlayProbeEvent payload = new()
            {
                event_type = "position",
                event_name = name,
                value_json = JsonUtility.ToJson(positionData),
                timestamp = DateTime.UtcNow.ToString("o")
            };

            Enqueue(payload);
        }
    }
}