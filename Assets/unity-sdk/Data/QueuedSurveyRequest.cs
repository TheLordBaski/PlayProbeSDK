using System;

namespace PlayProbe.Data
{
    public class QueuedSurveyRequest
    {
        public string trigger_key;
        public Action onComplete;
        public Action onSkipped;
    }
}