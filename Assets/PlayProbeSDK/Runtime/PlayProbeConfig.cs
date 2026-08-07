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

        [Header("Privacy")]
        [Tooltip("When on, the SDK collects and sends nothing until you call " +
                 "PlayProbeManager.Instance.SetConsent(true). Turn this on if your players are in the EU/UK " +
                 "or anywhere else consent is needed before analytics. You are the data controller for data " +
                 "collected inside your game, so showing the prompt is your responsibility.")]
        public bool requireConsent = false;

        [Tooltip("With Require Consent on, show PlayProbe's built-in consent dialog when the player " +
                 "has not answered yet. Turn this OFF if you show your own prompt and call SetConsent() " +
                 "yourself, or the player sees two dialogs.\n\n" +
                 "Switching this on is YOU choosing to show that prompt — you are the data controller. " +
                 "Read the wording in PlayProbeUiTheme (consentTitle / consentBody) and edit it to match " +
                 "your privacy policy before you ship.")]
        public bool useBuiltInConsentDialog = true;

        [Tooltip("Shown by the Instant Feedback popup and any consent UI you build. Point this at YOUR " +
                 "privacy policy — the one that tells players you use PlayProbe.")]
        public string privacyPolicyUrl = "";

        [Tooltip("Short line shown in the Instant Feedback popup, so players know what a report sends. " +
                 "Leave empty to use the built-in English default; override it to translate.")]
        [TextArea(2, 4)]
        public string feedbackPrivacyNotice = "";

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
