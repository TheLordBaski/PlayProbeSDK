// Copyright PlayProbe.io 2026. All rights reserved

using System;
using System.Collections.Generic;

namespace PlayProbe.Data
{
    [Serializable]
    public class SdkEventPayload
    {

        public string session_id;
        public List<SdkEvent> events;
    }
}