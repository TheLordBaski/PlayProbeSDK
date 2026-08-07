using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlayProbe
{
    /// <summary>
    /// Passive telemetry: a frame-rate summary for the session, and an optional position heatmap.
    ///
    /// Both are sampled by the SDK on a timer — you only have to say what to watch:
    /// <code>
    /// PlayProbeManager.Instance.Analytics.SetTrackedTransform(player.transform);
    /// PlayProbeManager.Instance.Analytics.RegisterTrackedObject("boss", boss.transform);
    /// </code>
    /// </summary>
    public class PlayProbeAnalytics
    {
        private PlayProbeConfig _config;
        private Transform _trackedTransform;
        private readonly Dictionary<string, Transform> _trackedObjects = new Dictionary<string, Transform>();

        private Coroutine _fpsCoroutine;
        private Coroutine _positionCoroutine;

        private float _fpsAccumulator = 0f;
        private int _fpsSampleCount;
        private float _minFps = float.MaxValue;

        /// <summary>Mean of every FPS sample so far this session, or 0 before the first sample.</summary>
        public float AverageFps => _fpsSampleCount > 0 ? _fpsAccumulator / _fpsSampleCount : 0f;
        /// <summary>Worst FPS sample so far this session, or 0 before the first sample.</summary>
        public float MinFps => _minFps == float.MaxValue ? 0f : _minFps;
        /// <summary>Whether any FPS sample has been taken yet.</summary>
        public bool HasFpsSamples => _fpsSampleCount > 0;

        // Primary tracked object (if the game called SetTrackedTransform). Used by instant feedback
        // to record the player/camera world position at report time.
        internal Transform PrimaryTrackedTransform => _trackedTransform;

        internal PlayProbeAnalytics(PlayProbeConfig config)
        {
            _config = config;
        }

        internal void StartTracking()
        {
            PlayProbeManager manager = PlayProbeManager.Instance;

            if (manager == null)
            {
                Debug.LogWarning("[PlayProbe] StartTracking failed because PlayProbeManager.Instance is null.");
                return;
            }

            StopTracking();

            _fpsAccumulator = 0f;
            _fpsSampleCount = 0;
            _minFps = float.MaxValue;

            // enableFpsTracking was copied into the runtime config and then never consulted, so a
            // developer who switched FPS tracking off still got a sampling coroutine and fps events.
            if (_config == null || _config.enableFpsTracking)
            {
                _fpsCoroutine = manager.StartCoroutine(TrackFps());
            }

            if (_config != null && _config.enablePositionHeatmap)
            {
                _positionCoroutine = manager.StartCoroutine(TrackPositions());
            }
        }

        internal void StopTracking()
        {
            PlayProbeManager manager = PlayProbeManager.Instance;

            if (manager == null)
            {
                _fpsCoroutine = null;
                _positionCoroutine = null;
                return;
            }

            if (_fpsCoroutine != null)
            {
                manager.StopCoroutine(_fpsCoroutine);
                _fpsCoroutine = null;
            }

            if (_positionCoroutine != null)
            {
                manager.StopCoroutine(_positionCoroutine);
                _positionCoroutine = null;
            }
        }

        /// <summary>
        /// Sets the primary subject of position tracking — usually the player. Instant Feedback also
        /// records this transform's position when a report is filed, falling back to the main camera
        /// when nothing is set.
        /// </summary>
        /// <param name="t">The transform to follow. Pass null to stop following anything.</param>
        public void SetTrackedTransform(Transform t)
        {
            _trackedTransform = t;
        }

        /// <summary>
        /// Watches an additional transform under a label, so the heatmap can separate categories of
        /// thing — enemies, pickups, checkpoints.
        /// </summary>
        /// <param name="tag">The label positions are logged under. Ignored when empty.</param>
        /// <param name="t">The transform to follow. Pass null to stop watching this tag.</param>
        public void RegisterTrackedObject(string tag, Transform t)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            if (t == null)
            {
                _trackedObjects.Remove(tag);
                return;
            }

            _trackedObjects[tag] = t;
        }

        private IEnumerator TrackFps()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);

                float delta = Time.unscaledDeltaTime;

                if (delta <= 0f)
                {
                    continue;
                }

                float currentFps = 1f / delta;
                _fpsAccumulator += currentFps;
                _fpsSampleCount++;

                if (currentFps < _minFps)
                {
                    _minFps = currentFps;
                }

                if (_fpsSampleCount % 10 == 0)
                {
                    PlayProbeManager manager = PlayProbeManager.Instance;

                    if (manager != null && manager.Events != null)
                    {
                        manager.Events.LogFps(currentFps);
                    }
                }
            }
            yield break;
        }

        private IEnumerator TrackPositions()
        {
            while (true)
            {
                float interval = _config != null ? _config.positionLogInterval : 5f;

                if (interval <= 0f)
                {
                    interval = 1f;
                }

                yield return new WaitForSeconds(interval);

                PlayProbeManager manager = PlayProbeManager.Instance;

                if (manager == null || manager.Events == null)
                {
                    continue;
                }

                if (_trackedTransform != null)
                {
                    manager.Events.LogPosition(_trackedTransform.position, _trackedTransform.name);
                }

                foreach (KeyValuePair<string, Transform> tracked in _trackedObjects)
                {
                    if (tracked.Value == null)
                    {
                        continue;
                    }

                    manager.Events.LogPosition(tracked.Value.position, tracked.Value.name, tracked.Key);
                }
            }
            yield break;
        }
    }
}
