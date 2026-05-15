using PlayProbe;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SdkTestController : MonoBehaviour
{
    private void Start()
    {
        PlayProbeManager.Instance.Survey.Register("level_1")
            .AddRating("How would you rate this level?", "level_1_rating").AddMultipleChoice(
                "Which part did you like the most?", "level_1_favorite_part",
                new[] { "Enemies", "Graphics", "Sound", "Gameplay" })
            .AddYesNo("Did you find any bugs?", "level_1_found_bugs")
            .AddText("Any additional feedback?", "level_1_additional_feedback", false);
        
        PlayProbeManager.Instance.Survey.Register("level_2")
            .AddRating("How would you rate this level?", "level_2_rating").AddMultipleChoice(
                "Which part did you like the most?", "level_2_favorite_part",
                new[] { "Enemies", "Graphics", "Sound", "Gameplay" })
            .AddYesNo("Did you find any bugs?", "level_2_found_bugs")
            .AddText("Any additional feedback?", "level_2_additional_feedback", false);
        PlayProbeManager.Instance.StartSession();
    }
    
    #if UNITY_EDITOR
    [CustomEditor(typeof(SdkTestController))]
    private class SdkTestControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            SdkTestController controller = (SdkTestController)target;
            if (GUILayout.Button("Show Level 1 survey"))
            {
                PlayProbeManager.Instance.ShowSurvey("level_1");
            }
            
            if (GUILayout.Button("Show Level 2 survey"))
            {
                PlayProbeManager.Instance.ShowSurvey("level_2");
            }
        }
    }
    #endif
}