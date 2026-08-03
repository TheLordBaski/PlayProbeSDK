using UnityEngine;

namespace PlayProbe
{
    public enum FeedbackButtonCorner
    {
        BottomRight,
        BottomLeft,
    }

    [CreateAssetMenu(fileName = "PlayProbeConfig", menuName = "PlayProbe/Configuration")]
    public class PlayProbeConfig : ScriptableObject
    {
        [Header("Connection")]
        public string shareToken;
        public bool isStandaloneTest;

        [Header("Session")]
        public bool enableFpsTracking = true;
        public bool enablePositionHeatmap = false;
        public float positionLogInterval = 5f;
        public bool enableCrashReporting = true;

        [Header("Survey")]
        public bool allowSurveyDismiss = true;
        public bool pauseTimeDuringSurvey = true;

        [Header("Instant Feedback")]
        public bool enableInstantFeedback = false;
        public FeedbackButtonCorner feedbackButtonCorner = FeedbackButtonCorner.BottomRight;
        public bool pauseGameDuringFeedback = true;
        public bool feedbackAllowScreenshot = true;
        public bool feedbackScreenshotDefaultOn = true;
        [Tooltip("Screenshots wider than this are downscaled before upload (keeps the JPG small).")]
        public int feedbackScreenshotMaxWidth = 1920;
    }
}
