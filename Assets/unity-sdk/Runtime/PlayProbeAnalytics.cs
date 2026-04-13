using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlayProbe
{
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

        public float AverageFps => _fpsSampleCount > 0 ? _fpsAccumulator / _fpsSampleCount : 0f;
        public float MinFps => _minFps == float.MaxValue ? 0f : _minFps;
        public bool HasFpsSamples => _fpsSampleCount > 0;

        public PlayProbeAnalytics(PlayProbeConfig config, PlayProbeEvents events)
        {
            _config = config;
        }

        public void StartTracking(PlayProbeConfig config)
        {
            if (config != null)
            {
                _config = config;
            }

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

            _fpsCoroutine = manager.StartCoroutine(TrackFps());

            if (_config != null && _config.enablePositionHeatmap)
            {
                _positionCoroutine = manager.StartCoroutine(TrackPositions());
            }
        }

        public void StopTracking()
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

        public void SetTrackedTransform(Transform t)
        {
            _trackedTransform = t;
        }

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
                    manager.Events.LogPosition(_trackedTransform.position);
                }

                foreach (KeyValuePair<string, Transform> tracked in _trackedObjects)
                {
                    if (tracked.Value == null)
                    {
                        continue;
                    }

                    manager.Events.LogPosition(tracked.Value.position, tracked.Key);
                }
            }
        }
    }
}
