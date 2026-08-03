// Copyright PlayProbe.io 2026. All rights reserved

using System;
using System.Collections;
using System.Collections.Generic;
using PlayProbe.Data;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace PlayProbe
{
    // Instant Feedback subsystem: captures a screenshot + runtime context and submits a feedback
    // report to the sdk-feedback edge function. UI (corner button + popup) is provided as separate
    // prefabs; this class is what those prefabs (or custom dev code) call into.
    //
    // Typical flow from the built-in popup:
    //   PlayProbeManager.Instance.OpenFeedback();               // captures shot, pauses, spawns popup
    //   ... user fills the form, popup calls ...
    //   PlayProbeManager.Instance.Feedback.Submit(title, desc, category, attachScreenshot);
    // Or fully custom UI: gather your own title/description and call Submit(...) directly.
    public class PlayProbeFeedback
    {
        private readonly PlayProbeRuntimeConfig _runtimeConfig;
        private readonly PlayProbeConfig _config;
        private readonly WaitForEndOfFrame _endOfFrame = new WaitForEndOfFrame();

        private PlayProbeDeviceInfo _device;

        private byte[] _pendingScreenshotJpg;
        private Texture2D _pendingScreenshotTexture;

        private bool _isPaused;
        private float _previousTimeScale = 1f;

        private GameObject _canvasInstance;

        internal PlayProbeFeedback(PlayProbeRuntimeConfig runtimeConfig, PlayProbeConfig config)
        {
            _runtimeConfig = runtimeConfig;
            _config = config;
        }

        /// <summary>The most recent captured screenshot (for the popup preview). May be null.</summary>
        public Texture2D PendingScreenshot => _pendingScreenshotTexture;

        /// <summary>Whether screenshots are allowed at all (config).</summary>
        public bool AllowScreenshot => _config != null && _config.feedbackAllowScreenshot;

        /// <summary>Whether the "attach screenshot" toggle should start on (config).</summary>
        public bool ScreenshotDefaultOn => _config != null && _config.feedbackScreenshotDefaultOn;

        /// <summary>
        /// Opens the feedback popup: captures the current frame, optionally pauses the game, and
        /// spawns the PlayProbeFeedbackCanvas prefab from Resources. Safe to call from a button or
        /// a keyboard shortcut. No-op (with a warning) when no session is active.
        /// </summary>
        public void Open()
        {
            PlayProbeManager manager = PlayProbeManager.Instance;
            if (manager == null || !manager.IsSessionActive)
            {
                Debug.LogWarning("[PlayProbe] OpenFeedback skipped: no active session.");
                return;
            }

            if (_canvasInstance != null)
            {
                // Already open.
                return;
            }

            manager.StartCoroutine(CaptureThenOpenRoutine());
        }

        /// <summary>
        /// Submits a feedback report. Called by the popup UI, or directly by custom code. Uses the
        /// screenshot captured by Open() when present, otherwise captures one on demand (if allowed
        /// and requested). Closes the popup and unpauses the game afterwards.
        /// </summary>
        public void Submit(string title, string description, string category = null, bool attachScreenshot = true)
        {
            PlayProbeManager manager = PlayProbeManager.Instance;
            if (manager == null || !manager.IsSessionActive)
            {
                Debug.LogWarning("[PlayProbe] Submit feedback skipped: no active session.");
                return;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                Debug.LogWarning("[PlayProbe] Submit feedback skipped: description is empty.");
                return;
            }

            manager.StartCoroutine(SubmitRoutine(title, description, category, attachScreenshot));
        }

        /// <summary>Closes the popup and unpauses without submitting.</summary>
        public void Cancel()
        {
            Cleanup();
        }

        private IEnumerator CaptureThenOpenRoutine()
        {
            yield return CaptureScreenshotRoutine();
            Pause();
            ShowCanvas();
        }

        private IEnumerator SubmitRoutine(string title, string description, string category, bool attachScreenshot)
        {
            bool wantShot = attachScreenshot && AllowScreenshot;

            if (wantShot && _pendingScreenshotJpg == null)
            {
                yield return CaptureScreenshotRoutine();
            }

            byte[] jpg = wantShot ? _pendingScreenshotJpg : null;
            PlayProbeFeedbackRequest request = BuildRequest(title, description, category);

            SendFeedbackAsync(request, jpg);

            Cleanup();
        }

        private IEnumerator CaptureScreenshotRoutine()
        {
            DisposePendingScreenshot();

            if (!AllowScreenshot)
            {
                yield break;
            }

            yield return _endOfFrame;

            try
            {
                Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                _pendingScreenshotTexture = shot;
                _pendingScreenshotJpg = EncodeMaybeDownscale(shot);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayProbe] Screenshot capture failed: {exception.Message}");
                _pendingScreenshotJpg = null;
            }
        }

        private byte[] EncodeMaybeDownscale(Texture2D source)
        {
            if (source == null)
            {
                return null;
            }

            int maxWidth = _config.feedbackScreenshotMaxWidth > 0 ? _config.feedbackScreenshotMaxWidth : 1920;

            if (source.width <= maxWidth)
            {
                return source.EncodeToJPG(75);
            }

            float scale = (float)maxWidth / source.width;
            int width = maxWidth;
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            byte[] jpg;
            Texture2D scaled = null;

            try
            {
                Graphics.Blit(source, renderTexture);
                RenderTexture.active = renderTexture;
                scaled = new Texture2D(width, height, TextureFormat.RGB24, false);
                scaled.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                scaled.Apply();
                jpg = scaled.EncodeToJPG(75);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
                if (scaled != null)
                {
                    UnityEngine.Object.Destroy(scaled);
                }
            }

            return jpg;
        }

        private PlayProbeFeedbackRequest BuildRequest(string title, string description, string category)
        {
            PlayProbeManager manager = PlayProbeManager.Instance;
            Scene scene = SceneManager.GetActiveScene();
            float instantFps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;

            return new PlayProbeFeedbackRequest
            {
                category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
                title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
                description = description.Trim(),
                scene_name = scene.name,
                scene_build_index = scene.buildIndex,
                world_position = GetWorldPosition(),
                playtime_seconds = manager != null ? manager.PlaytimeSeconds : 0d,
                game_time_scale = _isPaused ? _previousTimeScale : Time.timeScale,
                fps = instantFps,
                context = BuildContext(instantFps),
                device = _device ??= BuildDevice(),
                screen_width = Screen.width,
                screen_height = Screen.height,
                sdk_version = PlayProbeRuntimeConfig.SdkVersion,
                client_timestamp = DateTime.UtcNow.ToString("o"),
            };
        }

        private PlayProbeFeedbackContext BuildContext(float instantFps)
        {
            PlayProbeManager manager = PlayProbeManager.Instance;
            float avgFps = manager != null && manager.Analytics != null ? manager.Analytics.AverageFps : instantFps;
            float memoryMb = (float)(GC.GetTotalMemory(false) / (1024.0 * 1024.0));

            int qualityLevel = QualitySettings.GetQualityLevel();
            string[] qualityNames = QualitySettings.names;
            string qualityName = qualityNames != null && qualityLevel >= 0 && qualityLevel < qualityNames.Length
                ? qualityNames[qualityLevel]
                : null;

            return new PlayProbeFeedbackContext
            {
                avg_fps = avgFps,
                memory_mb = memoryMb,
                quality_level = qualityLevel,
                quality_name = qualityName,
                target_frame_rate = Application.targetFrameRate,
                vsync = QualitySettings.vSyncCount,
            };
        }

        private PlayProbeDeviceInfo BuildDevice()
        {
            return new PlayProbeDeviceInfo
            {
                cpu = SystemInfo.processorType,
                cpu_cores = SystemInfo.processorCount,
                cpu_mhz = SystemInfo.processorFrequency,
                gpu = SystemInfo.graphicsDeviceName,
                gpu_mem_mb = SystemInfo.graphicsMemorySize,
                ram_mb = SystemInfo.systemMemorySize,
                os = SystemInfo.operatingSystem,
                device_model = SystemInfo.deviceModel,
                device_type = SystemInfo.deviceType.ToString(),
            };
        }

        private PlayProbeVec3 GetWorldPosition()
        {
            Transform tracked = null;
            PlayProbeManager manager = PlayProbeManager.Instance;

            if (manager != null && manager.Analytics != null)
            {
                tracked = manager.Analytics.PrimaryTrackedTransform;
            }

            if (tracked == null && Camera.main != null)
            {
                tracked = Camera.main.transform;
            }

            if (tracked == null)
            {
                return new PlayProbeVec3();
            }

            Vector3 position = tracked.position;
            return new PlayProbeVec3
            {
                x = position.x,
                y = position.y,
                z = position.z,
                ry = tracked.eulerAngles.y,
            };
        }

        private async void SendFeedbackAsync(PlayProbeFeedbackRequest request, byte[] screenshotJpg)
        {
            PlayProbeManager manager = PlayProbeManager.Instance;
            if (manager == null)
            {
                return;
            }

            string payloadJson;
            try
            {
                payloadJson = JsonUtility.ToJson(request);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayProbe] Could not build feedback payload: {exception.Message}");
                return;
            }

            List<IMultipartFormSection> sections = new()
            {
                new MultipartFormDataSection("session_id", _runtimeConfig.SessionId),
                new MultipartFormDataSection("session_token", _runtimeConfig.SessionToken),
                new MultipartFormDataSection("payload", payloadJson),
            };

            if (screenshotJpg != null && screenshotJpg.Length > 0)
            {
                sections.Add(new MultipartFormFileSection("screenshot", screenshotJpg, "screenshot.jpg", "image/jpeg"));
            }

            try
            {
                using (UnityWebRequest webRequest =
                       PlayProbeHttp.CreateMultipartPostRequest(manager.GetEndpointAddressForFunction("sdk-feedback"), sections))
                {
                    await webRequest.SendWebRequest();

                    long statusCode = webRequest.responseCode;
                    string responseBody = webRequest.downloadHandler != null ? webRequest.downloadHandler.text : string.Empty;

                    if (webRequest.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                    {
                        Debug.LogWarning($"[PlayProbe] Feedback request error: {webRequest.error}");
                        return;
                    }

                    if (statusCode != 200)
                    {
                        Debug.LogWarning($"[PlayProbe] Feedback request failed with status {statusCode}: {responseBody}");
                        return;
                    }

                    Debug.Log("[PlayProbe] Feedback submitted successfully.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayProbe] Could not submit feedback: {exception.Message}");
            }
        }

        private void ShowCanvas()
        {
            GameObject prefab = Resources.Load<GameObject>("PlayProbeFeedbackCanvas");
            if (prefab == null)
            {
                Debug.Log("[PlayProbe] No PlayProbeFeedbackCanvas prefab found. Call PlayProbeManager.Instance.Feedback.Submit(...) from your own UI, or add the prefab to a Resources folder.");
                return;
            }

            _canvasInstance = UnityEngine.Object.Instantiate(prefab);
        }

        private void Pause()
        {
            if (_config == null || !_config.pauseGameDuringFeedback || _isPaused)
            {
                return;
            }

            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _isPaused = true;
        }

        private void Unpause()
        {
            if (!_isPaused)
            {
                return;
            }

            Time.timeScale = _previousTimeScale;
            _isPaused = false;
        }

        private void Cleanup()
        {
            Unpause();

            if (_canvasInstance != null)
            {
                UnityEngine.Object.Destroy(_canvasInstance);
                _canvasInstance = null;
            }

            DisposePendingScreenshot();
        }

        private void DisposePendingScreenshot()
        {
            if (_pendingScreenshotTexture != null)
            {
                UnityEngine.Object.Destroy(_pendingScreenshotTexture);
                _pendingScreenshotTexture = null;
            }

            _pendingScreenshotJpg = null;
        }
    }
}
