// Copyright PlayProbe.io 2026. All rights reserved

using System.Collections.Generic;
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

        // multipart/form-data POST. UnityWebRequest.Post computes the boundary and sets the
        // Content-Type header automatically, so do NOT set Content-Type manually here.
        public static UnityWebRequest CreateMultipartPostRequest(string url, List<IMultipartFormSection> sections)
        {
            UnityWebRequest request = UnityWebRequest.Post(url, sections);
            request.timeout = 15;
            return request;
        }
    }
}