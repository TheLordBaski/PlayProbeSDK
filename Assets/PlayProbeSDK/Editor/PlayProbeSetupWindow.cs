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
        private static string FeedbackCanvasPath => PlayProbePackagePaths.PrefabPath("PlayProbeFeedbackCanvas");

        private PlayProbeConfig _config;
        private SerializedObject _serializedConfig;
        private Vector2 _scroll;

        // A share token is a UUID from crypto.randomUUID(): 36 characters, 8-4-4-4-12.
        private const int ShareTokenLength = 36;

        // Long enough that pasting a token does not fire a request per keystroke, short enough that
        // it still feels like it happened on paste.
        private const double AutoCheckDelaySeconds = 0.4;

        // Token check state. _verifiedToken is what the result actually describes, so editing the
        // field clears a stale "all good" instead of leaving it reassuring the wrong token.
        private PlayProbeTokenVerifier.Result _verifyResult;
        private string _verifiedToken;
        private bool _isVerifying;

        // Auto-check bookkeeping. _lastSeenToken detects an edit; _pendingToken is one waiting out
        // the debounce.
        private string _lastSeenToken;
        private string _pendingToken;
        private double _pendingSince;

        // A finished check lands here instead of being applied straight away. IMGUI runs OnGUI once
        // to lay out and again to repaint, and throws "Mismatched LayoutGroup" if the two passes
        // emit a different number of controls — which is exactly what happens when a network
        // callback swaps a "Checking..." box for a result box between them. Everything that changes
        // the shape of the UI is drained during the Layout event, so both passes always agree.
        private PlayProbeTokenVerifier.Result _incomingResult;
        private string _incomingResultToken;

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

            // A domain reload kills any check that was in flight along with its callback, so never
            // come back up stuck on "Checking...".
            _isVerifying = false;
            _pendingToken = null;
        }

        /// <summary>
        /// Runs ten times a second whether or not the window has focus. OnGUI only runs when
        /// something asks it to, so while a check is queued or in flight this is what keeps asking —
        /// the work itself happens in OnGUI's Layout pass.
        /// </summary>
        private void OnInspectorUpdate()
        {
            if (_isVerifying || _pendingToken != null || _incomingResult != null)
            {
                Repaint();
            }
        }

        /// <summary>
        /// Applies every state change that would alter the control layout. Called only on the Layout
        /// event, so the Repaint pass that follows sees identical state.
        /// </summary>
        private void ApplyDeferredTokenState()
        {
            if (_incomingResult != null)
            {
                _isVerifying = false;
                _verifiedToken = _incomingResultToken;
                _verifyResult = _incomingResult;
                _incomingResult = null;
                _incomingResultToken = null;
            }

            if (_pendingToken != null
                && !_isVerifying
                && EditorApplication.timeSinceStartup - _pendingSince >= AutoCheckDelaySeconds)
            {
                string token = _pendingToken;
                _pendingToken = null;
                BeginTokenCheck(token);
            }
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.Layout)
            {
                ApplyDeferredTokenState();
            }

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

            DrawTokenCheck(shareToken);

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

        /// <summary>
        /// The "is this token going to work" check. The backend refuses SDK sessions for tests that
        /// are closed, have SDK mode off, or belong to a Free account; all three are invisible from
        /// inside Unity until a session silently fails to start, so ask once here instead.
        /// </summary>
        private void DrawTokenCheck(SerializedProperty shareToken)
        {
            string rawToken = shareToken != null ? shareToken.stringValue ?? string.Empty : string.Empty;
            string token = rawToken.Trim();

            // Layout only, for the same reason as ApplyDeferredTokenState: both of these add or
            // remove a HelpBox, and the Repaint pass has to agree with the Layout pass about how
            // many controls there are.
            if (Event.current.type == EventType.Layout)
            {
                NoticeTokenEdit(rawToken, token);

                // A result only describes the token it was fetched for.
                if (_verifyResult != null && !string.Equals(_verifiedToken, token, System.StringComparison.Ordinal))
                {
                    _verifyResult = null;
                }
            }

            DrawTokenLengthHint(rawToken, token);

            // Both waiting states are worth showing. Without them the window sits silent for up to a
            // second after a paste and the check looks like it never fired.
            if (_pendingToken != null)
            {
                EditorGUILayout.HelpBox("Token complete — checking it in a moment...", MessageType.None);
            }
            else if (_isVerifying)
            {
                EditorGUILayout.HelpBox("Checking this token with playprobe.io...", MessageType.None);
            }

            // The button is now the "I just changed something on the dashboard, ask again" path
            // rather than the only way to run the check.
            using (new EditorGUI.DisabledScope(token.Length == 0 || _isVerifying || _pendingToken != null))
            {
                string label = _verifyResult != null ? "Check Again" : "Check Token";

                if (GUILayout.Button(label, GUILayout.Height(24f)))
                {
                    BeginTokenCheck(token);
                }
            }

            if (_verifyResult == null)
            {
                EditorGUILayout.Space();
                return;
            }

            switch (_verifyResult.Outcome)
            {
                case PlayProbeTokenVerifier.Outcome.Ready:
                    EditorGUILayout.HelpBox(
                        $"Ready. Connected to \"{_verifyResult.TestName}\" — Pro plan, SDK mode on, test open.",
                        MessageType.Info);
                    break;

                case PlayProbeTokenVerifier.Outcome.Blocked:
                    EditorGUILayout.HelpBox(
                        $"Token is valid, and it belongs to \"{_verifyResult.TestName}\". Sessions will still be "
                        + "refused:\n\n• " + string.Join("\n• ", _verifyResult.Problems),
                        MessageType.Warning);
                    break;

                case PlayProbeTokenVerifier.Outcome.InvalidToken:
                    EditorGUILayout.HelpBox(string.Join("\n", _verifyResult.Problems), MessageType.Error);
                    break;

                default:
                    // Unreachable: says nothing about the token, so it must not read like a rejection.
                    EditorGUILayout.HelpBox(
                        string.Join("\n", _verifyResult.Problems)
                        + "\n\nThis is a problem with the check itself, not necessarily with your token.",
                        MessageType.Warning);
                    break;
            }

            if (_verifyResult.NeedsUpgrade && GUILayout.Button("Upgrade to Pro on playprobe.io", GUILayout.Height(24f)))
            {
                Application.OpenURL(_verifyResult.UpgradeUrl);
            }

            EditorGUILayout.Space();
        }

        /// <summary>
        /// Watches the token field for edits and queues an automatic check once the value reaches
        /// the length of a real token. Debounced rather than immediate, so pasting a token — which
        /// arrives as one change, but typing one does not — costs a single request.
        /// </summary>
        private void NoticeTokenEdit(string rawToken, string token)
        {
            if (string.Equals(rawToken, _lastSeenToken, System.StringComparison.Ordinal))
            {
                return;
            }

            _lastSeenToken = rawToken;
            _pendingToken = null;

            // Anything other than an exactly-token-length value is a half-typed or mangled token;
            // asking the backend about it would only ever come back "no test has this token", which
            // the length hint already says without a round trip.
            if (token.Length != ShareTokenLength)
            {
                return;
            }

            // Already know the answer for this exact value.
            if (string.Equals(token, _verifiedToken, System.StringComparison.Ordinal))
            {
                return;
            }

            _pendingToken = token;
            _pendingSince = EditorApplication.timeSinceStartup;
        }

        /// <summary>
        /// Says what is wrong with the shape of the token before the network is involved. "Too
        /// short" is a far more useful answer to a half-pasted token than "no test has this token".
        /// </summary>
        private void DrawTokenLengthHint(string rawToken, string token)
        {
            if (token.Length == 0)
            {
                // The empty-token warning above this already covers it.
                return;
            }

            if (rawToken.Length != token.Length)
            {
                EditorGUILayout.HelpBox(
                    "The token has whitespace around it. It is trimmed before use, so this still works — " +
                    "but it usually means the copy picked up a stray character.",
                    MessageType.None);
            }

            if (token.Length < ShareTokenLength)
            {
                EditorGUILayout.HelpBox(
                    $"Token is too short — {token.Length} of {ShareTokenLength} characters. " +
                    "It is checked automatically once it is complete.",
                    MessageType.None);
                return;
            }

            if (token.Length > ShareTokenLength)
            {
                EditorGUILayout.HelpBox(
                    $"Token is too long — {token.Length} characters, expected {ShareTokenLength}. " +
                    "Check for a doubled paste.",
                    MessageType.Warning);
                return;
            }

            // Right length, wrong shape. Worth a nudge, but not worth blocking the check: the
            // backend is the authority on what a valid token looks like, not this window.
            if (!System.Guid.TryParseExact(token, "D", out _))
            {
                EditorGUILayout.HelpBox(
                    "That is the right length but not the usual share token shape " +
                    "(8-4-4-4-12 hexadecimal). Checking it anyway.",
                    MessageType.None);
            }
        }

        private void BeginTokenCheck(string token)
        {
            _isVerifying = true;
            _verifyResult = null;

            // Park the result rather than applying it: the callback arrives from the editor's update
            // loop, which can land between OnGUI's Layout and Repaint passes.
            PlayProbeTokenVerifier.Verify(token, result =>
            {
                _incomingResultToken = token;
                _incomingResult = result;
                Repaint();
            });
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
