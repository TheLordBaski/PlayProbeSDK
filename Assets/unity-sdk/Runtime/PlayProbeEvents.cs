using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PlayProbe
{
    [Serializable]
    public class SdkEventPayloadOld
    {
        
        public string session_id;
        public string event_type;
        public string event_name;
        public float? value_num;
        public string value_text;
        public string value_json;
        public string timestamp;
    }

    [Serializable]
    public class SdkEventPayload
    {

        public string session_id;
        public List<SdkEvent> events;
    }

    [Serializable]
    public class SdkEvent
    {
        public string event_type;
        public string event_name;
        public double value_num;
        public string value_text;
        public string value_json;
        public string timestamp;
    }

    [Serializable]
    internal class PlayProbePositionPayload
    {
        public float x;
        public float y;
        public float z;
        public string tag;
    }

    [Serializable]
    internal class PlayProbeDictionaryEntry
    {
        public string key;
        public string value;
    }

    [Serializable]
    internal class PlayProbeDictionaryPayload
    {
        public List<PlayProbeDictionaryEntry> entries = new List<PlayProbeDictionaryEntry>();
    }

    public class PlayProbeEvents
    {
        private const int FLUSH_THRESHOLD = 20;
        private const float FLUSH_INTERVAL = 30f;
        private const int MAX_RETRIES = 3;

        private readonly PlayProbeConfig _config;
        private readonly object _bufferLock = new object();
        private readonly List<SdkEventPayloadOld> _buffer = new List<SdkEventPayloadOld>();

        private MonoBehaviour _runner;
        private Coroutine _flushCoroutine;
        private bool _isFlushing;
        private int _retryCount;
        private bool _logHandlerRegistered;

        public string SessionId { get; set; }

        public PlayProbeEvents(PlayProbeConfig config)
        {
            _config = config;
            RegisterCrashHandlerIfNeeded();
        }

        public void StartAutoFlush(MonoBehaviour runner)
        {
            if (runner == null || _flushCoroutine != null)
            {
                return;
            }

            _runner = runner;
            _flushCoroutine = runner.StartCoroutine(FlushTimerLoop());
        }

        public void StopAutoFlush()
        {
            if (_runner != null && _flushCoroutine != null)
            {
                _runner.StopCoroutine(_flushCoroutine);
            }

            _flushCoroutine = null;
            _runner = null;

            if (_logHandlerRegistered)
            {
                Application.logMessageReceived -= HandleUnityLog;
                _logHandlerRegistered = false;
            }
        }

        public void LogEvent(string eventName, float? value = null)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                return;
            }

            SdkEventPayloadOld payloadOld = new SdkEventPayloadOld
            {
                session_id = SessionId,
                event_type = "custom",
                event_name = eventName,
                value_num = value,
                timestamp = DateTime.UtcNow.ToString("o")
            };

            Enqueue(payloadOld);
        }

        public void LogEvent(string eventName, Dictionary<string, object> data)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                return;
            }

            SdkEventPayloadOld payloadOld = new SdkEventPayloadOld
            {
                session_id = SessionId,
                event_type = "custom",
                event_name = eventName,
                value_json = SerializeDictionary(data),
                timestamp = DateTime.UtcNow.ToString("o")
            };

            Enqueue(payloadOld);
        }

        public void LogException(Exception exception)
        {
            LogExceptionInternal(exception, exception != null ? exception.StackTrace : string.Empty);
        }

        public void LogPosition(Vector3 position, string tag = null)
        {
            PlayProbePositionPayload positionData = new PlayProbePositionPayload
            {
                x = position.x,
                y = position.y,
                z = position.z,
                tag = tag
            };

            SdkEventPayloadOld payloadOld = new SdkEventPayloadOld
            {
                session_id = SessionId,
                event_type = "position",
                value_json = JsonUtility.ToJson(positionData),
                timestamp = DateTime.UtcNow.ToString("o")
            };

            Enqueue(payloadOld);
        }

        internal void LogFps(float fps)
        {
            SdkEventPayloadOld payloadOld = new SdkEventPayloadOld
            {
                session_id = SessionId,
                event_type = "fps",
                event_name = "fps_sample",
                value_num = fps,
                timestamp = DateTime.UtcNow.ToString("o")
            };

            Enqueue(payloadOld);
        }

        public async Task FlushAsync()
        {
            List<SdkEventPayloadOld> batch;

            lock (_bufferLock)
            {
                if (_isFlushing || _buffer.Count == 0)
                {
                    return;
                }

                _isFlushing = true;
                batch = new List<SdkEventPayloadOld>(_buffer);
            }

            try
            {
                string batchJson = SerializeBatch(batch);
                await PlayProbeHttp.PostAsync("/rest/v1/sdk_events", batchJson);

                lock (_bufferLock)
                {
                    int removeCount = Mathf.Min(batch.Count, _buffer.Count);
                    _buffer.RemoveRange(0, removeCount);
                    _retryCount = 0;
                }
            }
            catch (Exception ex)
            {
                bool shouldDrop;
                int droppedCount = 0;

                lock (_bufferLock)
                {
                    _retryCount++;
                    shouldDrop = _retryCount >= MAX_RETRIES;

                    if (shouldDrop)
                    {
                        droppedCount = _buffer.Count;
                        _buffer.Clear();
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

        public Task FlushPendingEvents()
        {
            return FlushAsync();
        }

        public int PendingCount
        {
            get
            {
                lock (_bufferLock)
                {
                    return _buffer.Count;
                }
            }
        }

        private IEnumerator FlushTimerLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(FLUSH_INTERVAL);

                Task flushTask = FlushAsync();
                while (!flushTask.IsCompleted)
                {
                    yield return null;
                }
            }
        }

        private void Enqueue(SdkEventPayloadOld payloadOld)
        {
            if (payloadOld == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(payloadOld.session_id))
            {
                payloadOld.session_id = SessionId;
            }

            if (string.IsNullOrWhiteSpace(payloadOld.timestamp))
            {
                payloadOld.timestamp = DateTime.UtcNow.ToString("o");
            }

            bool shouldFlush;

            lock (_bufferLock)
            {
                _buffer.Add(payloadOld);
                shouldFlush = _buffer.Count >= FLUSH_THRESHOLD;
            }

            if (shouldFlush)
            {
                _ = FlushAsync();
            }
        }

        private void RegisterCrashHandlerIfNeeded()
        {
            if (_config == null || !_config.enableCrashReporting || _logHandlerRegistered)
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

            SdkEventPayloadOld payloadOld = new SdkEventPayloadOld
            {
                session_id = SessionId,
                event_type = "exception",
                value_text = exceptionType + ": " + exceptionMessage,
                value_json = string.IsNullOrWhiteSpace(stackTrace) ? string.Empty : stackTrace,
                timestamp = DateTime.UtcNow.ToString("o")
            };

            Enqueue(payloadOld);
        }

        private static string SerializeDictionary(Dictionary<string, object> data)
        {
            PlayProbeDictionaryPayload payload = new PlayProbeDictionaryPayload();

            if (data != null)
            {
                foreach (KeyValuePair<string, object> pair in data)
                {
                    payload.entries.Add(new PlayProbeDictionaryEntry
                    {
                        key = pair.Key,
                        value = ConvertValueToString(pair.Value)
                    });
                }
            }

            return JsonUtility.ToJson(payload);
        }

        private static string ConvertValueToString(object value)
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

        private static string SerializeBatch(List<SdkEventPayloadOld> events)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append('[');

            for (int index = 0; index < events.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append(JsonUtility.ToJson(events[index]));
            }

            builder.Append(']');
            return builder.ToString();
        }
    }
}
