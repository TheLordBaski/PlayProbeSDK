using PlayProbe;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Play-mode test harness for the PlayProbe Unity SDK.
//
// Setup:
//   1) Put a PlayProbeManager in the scene (Tools > PlayProbe > Setup) with a valid shareToken.
//   2) For standalone editor testing, set isStandaloneTest = true on the PlayProbeConfig.
//   3) For the full feature set also enable: enablePositionHeatmap, enableCrashReporting,
//      enableInstantFeedback (otherwise those features no-op with a warning).
//   4) Drop this component on any GameObject and enter Play mode.
//
// On Play it registers surveys (covering every question type), starts a session, spawns a moving
// tracked "player" so FPS + position analytics get live data, and draws an on-screen control panel
// (Game view) to exercise each feature. Controls use OnGUI so they work regardless of the project's
// Input System settings (no legacy UnityEngine.Input calls that would throw under the new system).
public class SdkTestController : MonoBehaviour
{
    [Header("Auto-driven telemetry")]
    [Tooltip("Orbit a tracked 'player' object so FPS + position analytics receive live data.")]
    public bool autoMovePlayer = true;
    public float moveRadius = 6f;
    public float moveSpeed = 0.5f;

    [Tooltip("Periodically fire random custom events to simulate live gameplay telemetry.")]
    public bool autoFireEvents = true;
    public float autoEventInterval = 5f;

    private Transform _player;
    private Transform _enemy;
    private float _nextAutoEventTime;
    private int _autoEventCounter;
    private int _feedbackCounter;

    private GUIStyle _titleStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _statusStyle;

    private void Start()
    {
        if (PlayProbeManager.Instance == null)
        {
            Debug.LogError("[SdkTest] No PlayProbeManager in the scene. Add one via Tools > PlayProbe > Setup before entering Play mode.");
            enabled = false;
            return;
        }

        RegisterSurveys();
        SetupTrackedObjects();
        PlayProbeManager.Instance.StartSession();
        _nextAutoEventTime = Time.time + autoEventInterval;

        Debug.Log("[SdkTest] Session starting. Use the on-screen panel in the Game view to test SDK features.");
    }

    private void RegisterSurveys()
    {
        PlayProbeSurvey survey = PlayProbeManager.Instance.Survey;

        // level_1 / level_2 cover rating, multiple_choice, yes_no, text.
        survey.Register("level_1")
            .AddRating("How would you rate this level?", "level_1_rating")
            .AddMultipleChoice("Which part did you like the most?", "level_1_favorite_part",
                new[] { "Enemies", "Graphics", "Sound", "Gameplay" })
            .AddYesNo("Did you find any bugs?", "level_1_found_bugs")
            .AddText("Any additional feedback?", "level_1_additional_feedback", false);

        survey.Register("level_2")
            .AddRating("How would you rate this level?", "level_2_rating")
            .AddMultipleChoice("Which part did you like the most?", "level_2_favorite_part",
                new[] { "Enemies", "Graphics", "Sound", "Gameplay" })
            .AddYesNo("Did you find any bugs?", "level_2_found_bugs")
            .AddText("Any additional feedback?", "level_2_additional_feedback", false);

        // boss_fight covers the remaining question type (emoji_scale).
        survey.Register("boss_fight")
            .AddEmojiScale("How did the boss fight feel?", "boss_fight_feel")
            .AddYesNo("Was the boss too hard?", "boss_fight_too_hard")
            .AddText("How could we improve it?", "boss_fight_improve", false);
    }

    private void SetupTrackedObjects()
    {
        _player = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
        _player.name = "PlayProbeTestPlayer";

        _enemy = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        _enemy.name = "PlayProbeTestEnemy";
        _enemy.position = new Vector3(moveRadius, 0f, 0f);

        PlayProbeAnalytics analytics = PlayProbeManager.Instance.Analytics;
        analytics.SetTrackedTransform(_player);
        analytics.RegisterTrackedObject("enemy", _enemy);
    }

    private void Update()
    {
        PlayProbeManager manager = PlayProbeManager.Instance;
        if (manager == null)
        {
            return;
        }

        if (autoMovePlayer && _player != null)
        {
            float angle = Time.time * moveSpeed;
            _player.position = new Vector3(Mathf.Cos(angle) * moveRadius, 0f, Mathf.Sin(angle) * moveRadius);
        }

        if (autoFireEvents && manager.IsSessionActive && Time.time >= _nextAutoEventTime)
        {
            _nextAutoEventTime = Time.time + Mathf.Max(1f, autoEventInterval);
            FireRandomAutoEvent();
        }
    }

    #region Feature actions (shared by the on-screen panel and the inspector buttons)

    private void SendNumericEvent()
    {
        PlayProbeManager.Instance.Events.LogEvent("score_gained", Random.Range(10, 500));
    }

    private void SendTextEvent()
    {
        string[] weapons = { "plasma_rifle", "shotgun", "sword", "bow" };
        PlayProbeManager.Instance.Events.LogEvent("weapon_selected", weapons[Random.Range(0, weapons.Length)]);
    }

    private void SendFlagEvent()
    {
        PlayProbeManager.Instance.Events.LogEvent("checkpoint_reached");
    }

    private void FireRandomAutoEvent()
    {
        _autoEventCounter++;
        switch (_autoEventCounter % 3)
        {
            case 0:
                SendNumericEvent();
                break;
            case 1:
                SendTextEvent();
                break;
            default:
                SendFlagEvent();
                break;
        }
    }

    private void SubmitTestFeedback(bool attachScreenshot)
    {
        _feedbackCounter++;
        string[] categories = { "bug", "suggestion", "praise", "other" };
        // Attach a rotating sample of the delivered global tag vocabulary so feedback_tags gets exercised.
        string[] tagIds = SampleTagIds(1 + (_feedbackCounter % 2));
        PlayProbeManager.Instance.SubmitFeedback(
            $"Test feedback #{_feedbackCounter}",
            $"Automated harness feedback #{_feedbackCounter}. Player at the tracked world position; check scene/fps/device/tags fields.",
            categories[_feedbackCounter % categories.Length],
            attachScreenshot,
            tagIds);
    }

    // Picks up to `count` tag ids from the vocabulary delivered at session start (empty if none).
    private static string[] SampleTagIds(int count)
    {
        var tags = PlayProbeManager.Instance != null ? PlayProbeManager.Instance.AnswerTags : null;
        if (tags == null || tags.Count == 0 || count <= 0)
        {
            return System.Array.Empty<string>();
        }

        int take = Mathf.Min(count, tags.Count);
        string[] ids = new string[take];
        for (int i = 0; i < take; i++)
        {
            ids[i] = tags[i].id;
        }

        return ids;
    }

    private void ThrowTestException()
    {
        // Captured as an "exception" event by crash reporting when enableCrashReporting = true.
        Debug.LogException(new System.InvalidOperationException("[SdkTest] Simulated exception for crash reporting."));
    }

    private void RestartSession()
    {
        PlayProbeManager manager = PlayProbeManager.Instance;
        if (manager.IsSessionActive)
        {
            manager.EndSession();
        }

        manager.StartSession();
        _nextAutoEventTime = Time.time + autoEventInterval;
    }

    #endregion

    #region On-screen control panel

    private void OnGUI()
    {
        EnsureStyles();

        PlayProbeManager manager = PlayProbeManager.Instance;

        GUILayout.BeginArea(new Rect(10, 10, 290, Screen.height - 20), GUI.skin.box);

        GUILayout.Label("PlayProbe SDK Test Harness", _titleStyle);

        if (manager == null)
        {
            GUILayout.Label("No PlayProbeManager in scene.", _statusStyle);
            GUILayout.EndArea();
            return;
        }

        bool active = manager.IsSessionActive;
        float avgFps = manager.Analytics != null ? manager.Analytics.AverageFps : 0f;
        GUILayout.Label($"Session: {(active ? "ACTIVE" : "inactive")}", _statusStyle);
        GUILayout.Label($"Avg FPS: {avgFps:F1}   Feedback: {(manager.Feedback != null ? "on" : "off")}", _statusStyle);

        GUILayout.Space(6);
        GUILayout.Label("Mid-game surveys", _sectionStyle);
        if (GUILayout.Button("Show Level 1 survey")) manager.ShowSurvey("level_1");
        if (GUILayout.Button("Show Level 2 survey")) manager.ShowSurvey("level_2");
        if (GUILayout.Button("Show Boss Fight survey (emoji)")) manager.ShowSurvey("boss_fight");

        GUILayout.Space(6);
        GUILayout.Label("Custom events", _sectionStyle);
        if (GUILayout.Button("Numeric event (score_gained)")) SendNumericEvent();
        if (GUILayout.Button("Text event (weapon_selected)")) SendTextEvent();
        if (GUILayout.Button("Flag event (checkpoint_reached)")) SendFlagEvent();
        autoFireEvents = GUILayout.Toggle(autoFireEvents, " Auto-fire events");

        GUILayout.Space(6);
        GUILayout.Label("Analytics", _sectionStyle);
        autoMovePlayer = GUILayout.Toggle(autoMovePlayer, " Auto-move tracked player");

        GUILayout.Space(6);
        GUILayout.Label("Instant feedback", _sectionStyle);
        if (GUILayout.Button("Open feedback popup")) manager.OpenFeedback();
        if (GUILayout.Button("Submit feedback (+ screenshot)")) SubmitTestFeedback(true);
        if (GUILayout.Button("Submit feedback (no screenshot)")) SubmitTestFeedback(false);

        GUILayout.Space(6);
        GUILayout.Label("Reliability", _sectionStyle);
        if (GUILayout.Button("Throw test exception")) ThrowTestException();

        GUILayout.Space(6);
        GUILayout.Label("Session", _sectionStyle);
        if (GUILayout.Button("End session")) manager.EndSession();
        if (GUILayout.Button("Restart session")) RestartSession();

        GUILayout.EndArea();
    }

    private void EnsureStyles()
    {
        if (_titleStyle != null)
        {
            return;
        }

        _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        _sectionStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        _statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
    }

    #endregion

#if UNITY_EDITOR
    [CustomEditor(typeof(SdkTestController))]
    private class SdkTestControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play mode to test the SDK. All controls are also available as an on-screen panel in the Game view.",
                    MessageType.Info);
                return;
            }

            SdkTestController controller = (SdkTestController)target;

            EditorGUILayout.Space();
            if (GUILayout.Button("Show Level 1 survey")) PlayProbeManager.Instance.ShowSurvey("level_1");
            if (GUILayout.Button("Show Level 2 survey")) PlayProbeManager.Instance.ShowSurvey("level_2");
            if (GUILayout.Button("Show Boss Fight survey")) PlayProbeManager.Instance.ShowSurvey("boss_fight");
            if (GUILayout.Button("Submit test feedback (+ screenshot)")) controller.SubmitTestFeedback(true);
            if (GUILayout.Button("Throw test exception")) controller.ThrowTestException();
            if (GUILayout.Button("End session")) PlayProbeManager.Instance.EndSession();
        }
    }
#endif
}
