// Copyright PlayProbe.io 2026. All rights reserved

using System;

namespace PlayProbe.Data
{
    // Serialized to JSON and sent as the multipart "payload" field of sdk-feedback.
    // session_id / session_token travel as separate multipart form fields, not here.
    [Serializable]
    internal class PlayProbeFeedbackRequest
    {
        public string category;
        public string title;
        public string description;
        public string scene_name;
        public int scene_build_index;
        public PlayProbeVec3 world_position;
        public double playtime_seconds;
        public float game_time_scale;
        public float fps;
        public PlayProbeFeedbackContext context;
        public PlayProbeDeviceInfo device;
        public int screen_width;
        public int screen_height;
        public string sdk_version;
        public string client_timestamp;
    }
}
