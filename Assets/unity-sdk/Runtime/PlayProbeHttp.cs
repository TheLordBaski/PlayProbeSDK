using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayProbe
{
    public static class PlayProbeHttp
    {
        private static PlayProbeConfig _config;
        private static CoroutineRunner _runner;

        private sealed class CoroutineRunner : MonoBehaviour
        {
        }

        public static void Configure(PlayProbeConfig config)
        {
            _config = config;
        }

        public static async Task<string> PostAsync(string endpoint, string json, string authToken = null)
        {
            PlayProbeConfig config = ResolveConfig();
            string url = BuildUrl(PlayProbeConfig.ApiEndpoint, endpoint);
            byte[] body = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(json) ? "{}" : json);

            UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer()
            };

            ApplyHeaders(request, authToken);
            return await SendAsync(request);
        }

        public static async Task<string> GetAsync(string endpoint, string authToken = null)
        {
            PlayProbeConfig config = ResolveConfig();
            string url = BuildUrl(PlayProbeConfig.ApiEndpoint, endpoint);

            UnityWebRequest request = UnityWebRequest.Get(url);
            ApplyHeaders(request, authToken);
            return await SendAsync(request);
        }

        private static PlayProbeConfig ResolveConfig()
        {
            if (_config == null)
            {
                _config = Resources.Load<PlayProbeConfig>("PlayProbeConfig");
            }

            if (_config == null)
            {
                throw new InvalidOperationException("PlayProbeConfig was not found in Resources/PlayProbeConfig.");
            }

            return _config;
        }

        private static void ApplyHeaders(UnityWebRequest request, string authToken)
        {
            request.SetRequestHeader("Content-Type", "application/json");

            if (!string.IsNullOrWhiteSpace(authToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + authToken);
            }
        }

        private static async Task<string> SendAsync(UnityWebRequest request)
        {
            try
            {
                return await SendRequestWithCoroutineAsync(request);
            }
            finally
            {
                request.Dispose();
            }
        }

        private static Task<string> SendRequestWithCoroutineAsync(UnityWebRequest request)
        {
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
            Runner.StartCoroutine(SendRequestCoroutine(request, tcs));
            return tcs.Task;
        }

        private static IEnumerator SendRequestCoroutine(UnityWebRequest request, TaskCompletionSource<string> tcs)
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                string message = $"HTTP {(int)request.responseCode} {request.error}\n{body}";
                tcs.TrySetException(new InvalidOperationException(message));
                yield break;
            }

            string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            tcs.TrySetResult(responseText);
        }

        private static CoroutineRunner Runner
        {
            get
            {
                if (_runner != null)
                {
                    return _runner;
                }

                GameObject runnerObject = new GameObject("PlayProbeHttpRunner");
                UnityEngine.Object.DontDestroyOnLoad(runnerObject);
                _runner = runnerObject.AddComponent<CoroutineRunner>();
                return _runner;
            }
        }

        private static string BuildUrl(string baseUrl, string endpoint)
        {
            string normalizedBase = baseUrl.TrimEnd('/');
            string normalizedEndpoint = endpoint.StartsWith("/", StringComparison.Ordinal) ? endpoint : "/" + endpoint;
            return normalizedBase + normalizedEndpoint;
        }
    }
}
