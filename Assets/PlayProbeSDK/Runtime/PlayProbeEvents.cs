using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlayProbe.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayProbe
{
    /// <summary>
    /// Your own gameplay events, plus the Unity errors crash reporting picks up.
    ///
    /// Events are buffered and uploaded in batches — when 20 accumulate, every 30 seconds, and at
    /// session end — so calling this on a hot path is cheap. A batch that fails is retried three times
    /// before being dropped, and the buffer is capped so an offline player cannot grow it without
    /// limit.
    ///
    /// <code>
    /// PlayProbeManager.Instance.Events.LogEvent("boss_defeated", 42f);
    /// </code>
    /// </summary>
    public class PlayProbeEvents
    {
        private const float FLushInterval = 30f;
        private const int FlushThreshold = 20;
        private const int MaxRetries = 3;

        // Hard ceiling on the buffer. Reached only when uploads are failing faster than the retry
        // budget clears them — a long offline stretch, say. Past this the oldest events are dropped, so
        // a player with no connection never has the SDK grow its memory use without limit.
        private const int MaxBufferedEvents = 500;

        private PlayProbeRuntimeConfig _runtimeConfig;


        private readonly object _bufferLock = new object();

        private readonly List<PlayProbeEvent> _eventBuffer = new();
        private Coroutine _flushCoroutine;

        private bool _isFlushing;
        private int _retryCount;
        private bool _logHandlerRegistered;

        // sdk-events requires value_json to be a JSON object (or null); a raw stack-trace string is
        // rejected and fails the whole batch, so exception stacks are wrapped in an object.
        [Serializable]
        private class ExceptionEventDetail
        {
            public string stack_trace;
        }


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
                value_json = string.IsNullOrWhiteSpace(stackTrace)
                    ? string.Empty
                    : JsonUtility.ToJson(new ExceptionEventDetail { stack_trace = stackTrace }),
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

            // Single choke point for every buffered event. The crash handler is registered in the
            // constructor (before any consent prompt can have run), so without this check exceptions
            // raised pre-consent would sit in the buffer and be uploaded the moment consent is given.
            PlayProbeManager manager = PlayProbeManager.Instance;
            if (manager != null && !manager.IsCollectionAllowed)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(payload.timestamp))
            {
                payload.timestamp = DateTime.UtcNow.ToString("o");
            }

            bool shouldFlush;

            int overflow;

            lock (_bufferLock)
            {
                _eventBuffer.Add(payload);
                overflow = _eventBuffer.Count - MaxBufferedEvents;

                if (overflow > 0)
                {
                    _eventBuffer.RemoveRange(0, overflow);
                }

                shouldFlush = _eventBuffer.Count >= FlushThreshold;
            }

            if (overflow > 0)
            {
                Debug.LogWarning(
                    $"[PlayProbe] Event buffer is full; dropped the {overflow} oldest event(s). Uploads are probably failing — check the console for earlier request warnings.");
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

                // Without a session id there is nothing to attribute the events to and sdk-events would
                // reject the whole batch. Keep them buffered — the session may still be starting.
                if (string.IsNullOrEmpty(_runtimeConfig?.SessionId))
                {
                    return;
                }

                _isFlushing = true;
                batch = new List<PlayProbeEvent>(_eventBuffer);
            }

            // Every exit from here on must reach the finally that clears _isFlushing. The serialization
            // failure below used to return before the try was entered, leaving the flag stuck true for
            // the rest of the run — after which no event was ever uploaded again.
            try
            {
                PlayProbeEventPayload payload = new()
                {
                    session_id = _runtimeConfig.SessionId,
                    session_token = _runtimeConfig.SessionToken,
                    events = batch
                };

                string payloadJson;

                try
                {
                    payloadJson = JsonUtility.ToJson(payload);
                }
                catch (Exception ex)
                {
                    // A payload that cannot be serialized never will. Drop the batch instead of
                    // retrying it forever and blocking everything queued behind it.
                    Debug.LogWarning(
                        $"[PlayProbe] Dropping {batch.Count} events that could not be serialized: {ex.Message}");

                    lock (_bufferLock)
                    {
                        _eventBuffer.RemoveRange(0, Mathf.Min(batch.Count, _eventBuffer.Count));
                    }

                    return;
                }

                using (UnityWebRequest request =
                       PlayProbeHttp.CreatePostRequest(
                           PlayProbeManager.Instance.GetEndpointAddressForFunction("sdk-events"), payloadJson))
                {
                    await request.SendWebRequest();
                    long statusCode = request.responseCode;
                    string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                    // UnityWebRequest reports transport and HTTP failures through `result`, it does not
                    // throw — so these two branches, not the catch below, are the ones that actually
                    // fire when the backend is unreachable. They used to leave the retry counter alone,
                    // which meant a down backend buffered events forever and the buffer grew without
                    // bound for the whole session.
                    if (request.result is UnityWebRequest.Result.ConnectionError
                        or UnityWebRequest.Result.ProtocolError)
                    {
                        RegisterFailedAttempt($"Event request error: {request.error}");
                        return;
                    }

                    if (statusCode != 200)
                    {
                        RegisterFailedAttempt(
                            $"Event request failed with status code {statusCode} and response: {responseBody}");
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
                RegisterFailedAttempt("Failed to flush sdk events: " + ex.Message);
            }
            finally
            {
                lock (_bufferLock)
                {
                    _isFlushing = false;
                }
            }
        }

        // Counts a failed upload and throws the buffer away once the attempts are exhausted, so a
        // backend the player cannot reach costs a bounded amount of memory rather than a growing one.
        private void RegisterFailedAttempt(string reason)
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

            Debug.LogWarning($"[PlayProbe] {reason}");

            if (shouldDrop)
            {
                Debug.LogWarning(
                    $"[PlayProbe] Dropped {droppedCount} buffered sdk events after {MaxRetries} attempts.");
            }
        }

        internal void StartFlushLoop()
        {
            PlayProbeManager manager = PlayProbeManager.Instance;

            if (manager == null || _flushCoroutine != null)
            {
                return;
            }

            _flushCoroutine = manager.StartCoroutine(FlushLoop());
        }

        internal void StopFlushLoop()
        {
            PlayProbeManager manager = PlayProbeManager.Instance;

            if (manager != null && _flushCoroutine != null)
            {
                manager.StopCoroutine(_flushCoroutine);
            }

            _flushCoroutine = null;
        }

        // Fire-and-forget flush of whatever is currently buffered (e.g. on session end).
        internal void FlushBufferedEvents()
        {
            _ = FlushAsync();
        }

        // Throws away everything buffered without sending it. Used when a player withdraws consent:
        // anything still in the buffer must not reach the backend.
        internal void DiscardBufferedEvents()
        {
            lock (_bufferLock)
            {
                _eventBuffer.Clear();
            }

            _retryCount = 0;
        }

        private IEnumerator FlushLoop()
        {
            WaitForSeconds interval = new WaitForSeconds(FLushInterval);

            while (true)
            {
                yield return interval;
                _ = FlushAsync();
            }
        }

        /// <summary>
        /// Records a custom gameplay event (server event_type "custom") with no value.
        /// Buffered and uploaded in batches. Ignored with a warning when no session is active.
        /// </summary>
        public void LogEvent(string eventName)
        {
            LogCustomInternal(eventName, null, null);
        }

        /// <summary>
        /// Records a custom gameplay event with a numeric value (e.g. score, time, damage).
        /// </summary>
        public void LogEvent(string eventName, float value)
        {
            LogCustomInternal(eventName, value, null);
        }

        /// <summary>
        /// Records a custom gameplay event with a text value (e.g. a chosen difficulty, an item id).
        /// </summary>
        public void LogEvent(string eventName, string valueText)
        {
            LogCustomInternal(eventName, null, valueText);
        }

        private void LogCustomInternal(string eventName, float? value, string valueText)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                Debug.LogWarning("[PlayProbe] LogEvent skipped: eventName is empty.");
                return;
            }

            PlayProbeManager manager = PlayProbeManager.Instance;
            if (manager == null || !manager.IsSessionActive)
            {
                Debug.LogWarning($"[PlayProbe] LogEvent('{eventName}') skipped: no active session.");
                return;
            }

            PlayProbeEvent payload = new()
            {
                event_type = "custom",
                event_name = eventName.Trim(),
                value_num = value ?? 0d,
                value_text = string.IsNullOrEmpty(valueText) ? null : valueText,
                timestamp = DateTime.UtcNow.ToString("o")
            };

            Enqueue(payload);
        }

        internal void LogFps(float fps)
        {
            PlayProbeEvent payload = new()
            {
                event_type = "fps",
                event_name = "fps_sample",
                value_num = fps,
                timestamp = DateTime.UtcNow.ToString("o")
            };

            Enqueue(payload);
        }

        /// <summary>
        /// Logs a single point in the world — a death, a chest opened, wherever the player gave up.
        /// One-off counterpart to the periodic sampling <c>enablePositionHeatmap</c> does.
        /// </summary>
        /// <param name="position">Where it happened, in world space.</param>
        /// <param name="name">What happened. Shows up as the event name.</param>
        /// <param name="tag">Optional category, so the dashboard can filter by kind of point.</param>
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

        public void OnDestroy()
        {
            Application.logMessageReceived -= HandleUnityLog;
        }
    }
}