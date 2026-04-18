// Copyright PlayProbe.io 2026. All rights reserved

using System.Text;
using UnityEngine.Networking;

namespace PlayProbe
{
    public class PlayProbeHttp
    {
        public static UnityWebRequest CreatePostRequest(string url, string payloadJson)
        {
            byte[] body = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);

            UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 10
            };

            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }
    }
}