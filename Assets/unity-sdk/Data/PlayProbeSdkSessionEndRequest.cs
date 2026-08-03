// Copyright PlayProbe.io 2026. All rights reserved

using System;

namespace PlayProbe.Data
{
    [Serializable]
    public class PlayProbeSdkSessionEndRequest
    {
        public string session_id;
        public string session_token;
        public double duration_seconds;
        public double avg_fps;
        public double min_fps;
    }
}