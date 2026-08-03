using UnityEngine;

namespace PlayProbe
{
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
    }
}
