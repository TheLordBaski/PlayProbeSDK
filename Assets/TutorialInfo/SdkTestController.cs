using PlayProbe;
using UnityEngine;

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
        PlayProbeManager.Instance.StartSession();
    }
}