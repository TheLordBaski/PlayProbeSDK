// Copyright PlayProbe.io 2026. All rights reserved

using System;
using System.Collections.Generic;

namespace PlayProbe.Data
{
    [Serializable]
    public class PlayProbeEventPayload
    {

        public string session_id;
        public string session_token;
        public List<PlayProbeEvent> events;
    }
}