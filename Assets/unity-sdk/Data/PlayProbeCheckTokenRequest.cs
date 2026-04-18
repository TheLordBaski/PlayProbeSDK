// Copyright PlayProbe.io 2026. All rights reserved

using System;

namespace PlayProbe.Data
{
    [Serializable]
    public class PlayProbeCheckTokenRequest
    {
        public string share_token;
        
        public string handoff_token;
    }
}