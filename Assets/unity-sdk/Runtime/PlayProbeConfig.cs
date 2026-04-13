using UnityEngine;

namespace PlayProbe
{
    [CreateAssetMenu(fileName = "PlayProbeConfig", menuName = "PlayProbe/Configuration")]
    public class PlayProbeConfig : ScriptableObject
    {
        public const string SDKVersion = "0.1.0";
        [Header("Connection")]
        public const string ApiEndpoint = "https://api.playprobe.io";
        public string shareToken;
        public string gameId;

        [Header("Session")]
        public bool enableFpsTracking = true;
        public bool enablePositionHeatmap = false;
        public float positionLogInterval = 5f;
        public bool enableCrashReporting = true;

        [Header("Survey")]
        public bool allowSurveyDismiss = true;
        public bool pauseTimeDuringSurvey = true;
    }
}
