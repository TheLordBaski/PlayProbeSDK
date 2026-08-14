# PlayProbe SDK for Unity

Playtesting analytics, in-game surveys and instant feedback — reporting to your
[PlayProbe](https://playprobe.io) dashboard.

Package: `io.playprobe.sdk` · Unity 6000.0+ · Full guide: [`documentation.md`](documentation.md)

## What it does

| | |
| --- | --- |
| **Sessions** | Start and end a playtest session, standalone or handed off from a dashboard session page. |
| **Analytics** | Average and minimum FPS, plus an optional position heatmap for any transforms you register. |
| **Events** | `LogEvent("boss_defeated", 42f)` — buffered and uploaded in batches. |
| **Crash capture** | Unity errors and uncaught exceptions, with stack traces. |
| **Surveys** | Register questions in code, show them at a gameplay moment, submit the answers. |
| **Instant feedback** | A floating button that opens a report form with a screenshot attached. |
| **Consent** | An opt-in gate that sends nothing at all until the player agrees. |

## Install

Add the package through the Package Manager (`Window > Package Manager > + > Add package from git
URL…`), or point it at a local copy of the `PlayProbeSDK` folder.

It depends on `com.unity.ugui`, which every Unity project already has.

## Set up

1. **`Tools > PlayProbe > Setup`** → *Create PlayProbeConfig Asset*.
2. Paste your **share token** from the test's page in the dashboard.
3. **Create Missing UI Prefabs** — generates the survey, feedback and code-entry screens.
4. **Create PlayProbeManager In Active Scene**.

## Use

```csharp
using PlayProbe;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    private void Start()
    {
        // Register surveys before the session starts — the schema is sent with the start request.
        PlayProbeManager.Instance.Survey.Register("after_level_1")
            .AddRating("How would you rate this level?", "lvl1_rating")
            .AddYesNo("Hit any bugs?", "lvl1_bugs")
            .AddText("Anything else?", "lvl1_notes", required: false);

        PlayProbeManager.Instance.Analytics.SetTrackedTransform(player.transform);
        PlayProbeManager.Instance.StartSession();
    }

    public void OnLevel1Complete()
    {
        PlayProbeManager.Instance.Events.LogEvent("level_complete", 1f);
        PlayProbeManager.Instance.ShowSurvey("after_level_1");
    }
}
```

The session ends by itself on quit, or call `EndSession()`.

## Before you ship

Inside your game **you are the data controller and PlayProbe is your processor**. Name PlayProbe in
your own privacy policy, set `privacyPolicyUrl` in the config, and decide whether you need
`requireConsent`. There is copy-paste policy text and a full checklist in
[section 11 of the documentation](documentation.md#11-privacy-and-gdpr).

## Restyling

Every screen is generated from one asset. `Tools > PlayProbe > UI > Create Theme Asset`, edit the
colours and strings, then `Rebuild All Prefabs`. Nothing else needs to change — and translating the
UI is the same operation.

## Support

[playprobe.io](https://playprobe.io) · support@playprobe.io
