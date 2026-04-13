using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PlayProbe.Editor
{
    public class PlayProbeSetupWindow : EditorWindow
    {
        private const string ConfigDirectory = "Assets/Resources";
        private const string ConfigPath = "Assets/Resources/PlayProbeConfig.asset";

        private PlayProbeConfig _config;
        private SerializedObject _serializedConfig;

        [MenuItem("Tools/PlayProbe/Setup")]
        public static void Open()
        {
            PlayProbeSetupWindow window = GetWindow<PlayProbeSetupWindow>("PlayProbe Setup");
            window.minSize = new Vector2(450f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadConfig();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("PlayProbe SDK Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Create or edit PlayProbeConfig in Assets/Resources so the runtime can load it automatically.", MessageType.Info);
            EditorGUILayout.Space();

            if (_config == null || _serializedConfig == null)
            {
                if (GUILayout.Button("Create PlayProbeConfig Asset", GUILayout.Height(32f)))
                {
                    CreateConfigAsset();
                }

                return;
            }

            _serializedConfig.Update();

            EditorGUILayout.LabelField("Connection", EditorStyles.boldLabel);
            //DrawProperty("supabaseUrl", "Supabase URL");
            //DrawProperty("supabaseAnonKey", "Supabase Anon Key");
            DrawProperty("shareToken", "Share Token");
            DrawProperty("gameId", "Game ID");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Session", EditorStyles.boldLabel);
            DrawProperty("enableFpsTracking", "Enable FPS Tracking");
            DrawProperty("enablePositionHeatmap", "Enable Position Heatmap");
            DrawProperty("positionLogInterval", "Position Log Interval");
            DrawProperty("enableCrashReporting", "Enable Crash Reporting");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Survey", EditorStyles.boldLabel);
            DrawProperty("allowSurveyDismiss", "Allow Survey Dismiss");
            DrawProperty("pauseTimeDuringSurvey", "Pause Time During Survey");

            EditorGUILayout.Space();

            if (GUILayout.Button("Save Config", GUILayout.Height(30f)))
            {
                SaveConfig();
            }

            if (GUILayout.Button("Select Config Asset", GUILayout.Height(24f)))
            {
                Selection.activeObject = _config;
                EditorGUIUtility.PingObject(_config);
            }

            if (GUILayout.Button("Create PlayProbeManager In Active Scene", GUILayout.Height(24f)))
            {
                CreateManagerInScene();
            }
            
            if(GUILayout.Button("Open PlayProbe Dashboard", GUILayout.Height(24f)))
            {
                Application.OpenURL("https://playprobe.io/dashboard");
            }
            
            if(GUILayout.Button("Test start session", GUILayout.Height(24f)))
            {
                PlayProbeManager.StartSession(_config);
            }
            
            if(GUILayout.Button("Test end session", GUILayout.Height(24f)))
            {
                PlayProbeManager.EndSession(_config);
            }
        }

        private void DrawProperty(string propertyName, string label)
        {
            SerializedProperty property = _serializedConfig.FindProperty(propertyName);

            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label));
            }
        }

        private void LoadConfig()
        {
            _config = AssetDatabase.LoadAssetAtPath<PlayProbeConfig>(ConfigPath);
            _serializedConfig = _config != null ? new SerializedObject(_config) : null;
        }

        private void CreateConfigAsset()
        {
            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
            }

            _config = CreateInstance<PlayProbeConfig>();
            AssetDatabase.CreateAsset(_config, ConfigPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _serializedConfig = new SerializedObject(_config);
            Selection.activeObject = _config;
            EditorGUIUtility.PingObject(_config);
        }

        private void SaveConfig()
        {
            _serializedConfig.ApplyModifiedProperties();
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateManagerInScene()
        {
            PlayProbeManager existingManager = FindObjectOfType<PlayProbeManager>();

            if (existingManager != null)
            {
                Selection.activeGameObject = existingManager.gameObject;
                EditorGUIUtility.PingObject(existingManager.gameObject);
                return;
            }

            GameObject managerObject = new GameObject("PlayProbeManager");
            Undo.RegisterCreatedObjectUndo(managerObject, "Create PlayProbeManager");
            managerObject.AddComponent<PlayProbeManager>();
            EditorSceneManager.MarkSceneDirty(managerObject.scene);
            Selection.activeGameObject = managerObject;
        }

        
    }
}
