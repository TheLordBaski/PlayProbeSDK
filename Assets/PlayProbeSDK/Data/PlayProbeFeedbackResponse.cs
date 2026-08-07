// Copyright PlayProbe.io 2026. All rights reserved

using System;

namespace PlayProbe.Data
{
    [Serializable]
    internal class PlayProbeFeedbackResponse
    {
        public bool ok;
        public string feedback_id;
        public bool screenshot_uploaded;
    }
}
