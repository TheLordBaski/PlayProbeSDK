// Copyright PlayProbe.io 2026. All rights reserved

using System;

namespace PlayProbe.Data
{
    [Serializable]
    internal class PlayProbeEvent
    {
        public string event_type;
        public string event_name;
        public double value_num;
        public string value_text;
        public string value_json;
        public string timestamp;
    }
}