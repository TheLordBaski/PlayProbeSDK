// Copyright PlayProbe.io 2026. All rights reserved

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PlayProbe.Editor
{
    /// <summary>
    /// One window that gets a project from "package installed" to "session recording": create the
    /// config, fill in the share token, drop a manager into the scene, and generate the UI prefabs.
    /// </summary>
    public class PlayProbeSetupWindow : EditorWindow
    {
        private const string ConfigDirectory = "Assets/Resources";
        private const string ConfigPath = "Assets/Resources/PlayProbeConfig.asset";
        private const string ThemePath = "Assets/Resources/PlayProbeUiTheme.asset";
        private const string FeedbackCanvasPath = "Assets/unity-sdk/Resources/PlayProbeFeedbackCanvas.prefab";

        private PlayProbeConfig _config;
        private SerializedObject _serializedConfig;
        private Vector2 _scroll;

        [MenuItem("Tools/PlayProbe/Setup", priority = 0)]
        public static void Open()
        {
            PlayProbeSetupWindow window = GetWindow<PlayProbeSetupWindow>("PlayProbe Setup");
            window.minSize = new Vector2(460f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadConfig();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("PlayProbe SDK Configuration", EditorStyles.boldLabel);

            if (_config == null || _serializedConfig == null)
            {
                EditorGUILayout.HelpBox(
                    "No config asset yet. The runtime loads it from Assets/Resources/PlayProbeConfig.asset.",
                    MessageType.Info);

                if (GUILayout.Button("Create PlayProbeConfig Asset", GUILayout.Height(32f)))
                {
                    CreateConfigAsset();
                }

                EditorGUILayout.EndScrollView();
                return;
            }

            _serializedConfig.Update();

            DrawConnectionSection();
            DrawSessionSection();
            DrawSurveySection();
            DrawFeedbackSection();
            DrawPrivacySection();
            DrawUiSection();
            DrawActions();

            _serializedConfig.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();
        }

        #region Sections

        private void DrawConnectionSection()
        {
            EditorGUILayout.LabelField("Connection", EditorStyles.boldLabel);
            DrawProperty("shareToken", "Share Token");
            DrawProperty("isStandaloneTest", "Standalone Test Mode");

            SerializedProperty shareToken = _serializedConfig.FindProperty("shareToken");
            if (shareToken != null && string.IsNullOrWhiteSpace(shareToken.stringValue))
            {
                EditorGUILayout.HelpBox(
                    "Without a share token the SDK stays inactive. Copy it from the test's page in the " +
                    "PlayProbe dashboard.",
                    MessageType.Warning);
            }

            SerializedProperty standalone = _serializedConfig.FindProperty("isStandaloneTest");
            if (standalone != null)
            {
                EditorGUILayout.HelpBox(
                    standalone.boolValue
                        ? "Standalone: the session starts as soon as you call StartSession(). Good for " +
                          "editor testing and public builds."
                        : "Handoff: the tester is shown a code entry screen and types the code from their " +
                          "dashboard session page, which ties the session to that specific tester.",
                    MessageType.None);
            }

            EditorGUILayout.Space();
        }

        private void DrawSessionSection()
        {
            EditorGUILayout.LabelField("Session", EditorStyles.boldLabel);
            DrawProperty("enableFpsTracking", "Enable FPS Tracking");
            DrawProperty("enablePositionHeatmap", "Enable Position Heatmap");
            DrawProperty("positionLogInterval", "Position Log Interval");
            DrawProperty("enableCrashReporting", "Enable Crash Reporting");

            SerializedProperty crash = _serializedConfig.FindProperty("enableCrashReporting");
            if (crash != null && crash.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Crash reporting uploads every Debug.LogError and uncaught exception, message and " +
                    "stack trace included. Make sure your error messages do not contain player names, " +
                    "emails, or anything else personal.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();
        }

        private void DrawSurveySection()
        {
            EditorGUILayout.LabelField("Survey", EditorStyles.boldLabel);
            DrawProperty("allowSurveyDismiss", "Allow Survey Dismiss");
            DrawProperty("pauseTimeDuringSurvey", "Pause Time During Survey");
            EditorGUILayout.Space();
        }

        private void DrawFeedbackSection()
        {
            EditorGUILayout.LabelField("Instant Feedback", EditorStyles.boldLabel);
            DrawProperty("enableInstantFeedback", "Enable Instant Feedback");

            SerializedProperty enabled = _serializedConfig.FindProperty("enableInstantFeedback");
            bool isOn = enabled != null && enabled.boolValue;

            using (new EditorGUI.DisabledScope(!isOn))
            {
                DrawProperty("feedbackButtonCorner", "Button Corner");
                DrawProperty("pauseGameDuringFeedback", "Pause Game While Open");
                DrawProperty("feedbackAllowScreenshot", "Allow Screenshots");
                DrawProperty("feedbackScreenshotDefaultOn", "Screenshot On By Default");
                DrawProperty("feedbackScreenshotMaxWidth", "Screenshot Max Width");
            }

            if (isOn && !File.Exists(FeedbackCanvasPath))
            {
                EditorGUILayout.HelpBox(
                    "The feedback popup prefab is missing, so OpenFeedback() will do nothing. Generate " +
                    "it with Tools > PlayProbe > UI > Create Missing Prefabs.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();
        }

        private void DrawPrivacySection()
        {
            EditorGUILayout.LabelField("Privacy", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Inside your game YOU are the data controller and PlayProbe is your processor. You must " +
                "name PlayProbe in your own privacy policy, and obtain consent where your players' laws " +
                "require it. See section 11 of the SDK documentation for copy-paste policy text.",
                MessageType.Info);

            DrawProperty("requireConsent", "Require Consent Before Collecting");

            SerializedProperty requireConsent = _serializedConfig.FindProperty("requireConsent");
            bool consentRequired = requireConsent != null && requireConsent.boolValue;

            using (new EditorGUI.DisabledScope(!consentRequired))
            {
                DrawProperty("useBuiltInConsentDialog", "Use Built-In Consent Dialog");
            }

            SerializedProperty useBuiltIn = _serializedConfig.FindProperty("useBuiltInConsentDialog");
            if (consentRequired && useBuiltIn != null)
            {
                EditorGUILayout.HelpBox(
                    useBuiltIn.boolValue
                        ? "StartSession() will show PlayProbe's consent dialog when the player has not " +
                          "answered yet, and start the session by itself once they agree. Edit its " +
                          "wording in PlayProbeUiTheme (consentTitle / consentBody) to match your " +
                          "privacy policy before shipping."
                        : "StartSession() will wait silently. Show your own prompt and call " +
                          "PlayProbeManager.Instance.SetConsent(true), or nothing is ever collected.",
                    useBuiltIn.boolValue ? MessageType.None : MessageType.Warning);
            }

            DrawProperty("privacyPolicyUrl", "Your Privacy Policy URL");
            DrawProperty("feedbackPrivacyNotice", "Feedback Notice Override");

            if (!consentRequired)
            {
                EditorGUILayout.HelpBox(
                    "Consent is not required. The SDK starts collecting as soon as StartSession() is " +
                    "called. If you ship to the EU or UK, turn this on and call " +
                    "PlayProbeManager.Instance.SetConsent(true) after your own consent prompt.",
                    MessageType.Warning);
            }

            SerializedProperty policyUrl = _serializedConfig.FindProperty("privacyPolicyUrl");
            if (policyUrl != null && string.IsNullOrWhiteSpace(policyUrl.stringValue))
            {
                EditorGUILayout.HelpBox(
                    "No privacy policy URL set, so the 'Privacy policy' link hides itself in the feedback " +
                    "popup and the consent dialog.",
                    MessageType.None);
            }

            EditorGUILayout.Space();
        }

        private void DrawUiSection()
        {
            EditorGUILayout.LabelField("User Interface", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Every PlayProbe screen is generated from a theme asset. Create the theme, restyle it, " +
                "then rebuild the prefabs — no prefab editing needed.",
                MessageType.Info);

            PlayProbeUiTheme theme = AssetDatabase.LoadAssetAtPath<PlayProbeUiTheme>(ThemePath);

            if (theme == null)
            {
                if (GUILayout.Button("Create UI Theme Asset", GUILayout.Height(24f)))
                {
                    EditorApplication.ExecuteMenuItem("Tools/PlayProbe/UI/Create Theme Asset");
                }
            }
            else if (GUILayout.Button("Select UI Theme Asset", GUILayout.Height(24f)))
            {
                Selection.activeObject = theme;
                EditorGUIUtility.PingObject(theme);
            }

            if (GUILayout.Button("Create Missing UI Prefabs", GUILayout.Height(24f)))
            {
                EditorApplication.ExecuteMenuItem("Tools/PlayProbe/UI/Create Missing Prefabs");
            }

            if (GUILayout.Button("Rebuild All UI Prefabs", GUILayout.Height(24f)))
            {
                EditorApplication.ExecuteMenuItem("Tools/PlayProbe/UI/Rebuild All Prefabs (overwrite)");
            }

            EditorGUILayout.Space();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Scene", EditorStyles.boldLabel);

            if (GUILayout.Button("Create PlayProbeManager In Active Scene", GUILayout.Height(28f)))
            {
                CreateManagerInScene();
            }

            if (GUILayout.Button("Select Config Asset", GUILayout.Height(24f)))
            {
                Selection.activeObject = _config;
                EditorGUIUtility.PingObject(_config);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Open PlayProbe Dashboard", GUILayout.Height(24f)))
            {
                Application.OpenURL("https://playprobe.io/dashboard");
            }
        }

        #endregion

        #region Asset handling

        private void DrawProperty(string propertyName, string label)
        {
            SerializedProperty property = _serializedConfig.FindProperty(propertyName);

            if (property == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label));
        }

        private void LoadConfig()
        {
            _config = AssetDatabase.LoadAssetAtPath<PlayProbeConfig>(ConfigPath);
            _serializedConfig = _config != null ? new SerializedObject(_config) : null;
        }

        private void CreateConfigAsset()
        {
            Directory.CreateDirectory(ConfigDirectory);

            _config = CreateInstance<PlayProbeConfig>();
            AssetDatabase.CreateAsset(_config, ConfigPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _serializedConfig = new SerializedObject(_config);
            Selection.activeObject = _config;
            EditorGUIUtility.PingObject(_config);
        }

        private void CreateManagerInScene()
        {
#if UNITY_2023_1_OR_NEWER
            PlayProbeManager existing = FindFirstObjectByType<PlayProbeManager>();
#else
            PlayProbeManager existing = FindObjectOfType<PlayProbeManager>();
#endif

            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            GameObject managerObject = new GameObject("PlayProbeManager");
            Undo.RegisterCreatedObjectUndo(managerObject, "Create PlayProbeManager");

            PlayProbeManager manager = managerObject.AddComponent<PlayProbeManager>();

            // The config field is private and serialized. Assigning it here is the difference between
            // the manager working out of the box and the developer hitting "no config assigned" at
            // runtime with no obvious cause.
            if (_config != null)
            {
                SerializedObject serializedManager = new SerializedObject(manager);
                SerializedProperty configProperty = serializedManager.FindProperty("config");

                if (configProperty != null)
                {
                    configProperty.objectReferenceValue = _config;
                    serializedManager.ApplyModifiedProperties();
                }
            }

            EditorSceneManager.MarkSceneDirty(managerObject.scene);
            Selection.activeGameObject = managerObject;
        }

        #endregion
    }
}
