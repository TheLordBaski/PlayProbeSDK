# PlayProbe Unity SDK Documentation

How to integrate and use the PlayProbe Unity SDK (`Assets/unity-sdk`, package `io.playprobe.sdk`).

> This document reflects the current runtime (`PlayProbeManager` and its subsystems). Older drafts
> described a Supabase-REST / `Initialize` / `PlayProbeSession` API that no longer exists — ignore any
> copy that mentions `supabaseUrl`/`supabaseAnonKey`, `PreloadSurveys`, or `SurveyOverlay`.

## 1. Overview

PlayProbe provides:
- Session lifecycle (standalone or dashboard **handoff** sessions)
- Passive analytics (FPS summary, optional position heatmap)
- Buffered custom + system events (batched upload)
- Mid-game surveys (register schema, display in-game, submit responses)

Runtime entry point: `PlayProbeManager` (singleton, `DontDestroyOnLoad`).
Backend: edge functions under `https://api.playprobe.io/`.

## 2. Prerequisites
- Unity 2021.3+
- A PlayProbe test with a **share token** (from the dashboard). SDK/mid-game surveys require the test to have SDK mode enabled.

## 3. Setup

1. Create the config asset at `Assets/Resources/PlayProbeConfig.asset` via `Tools > PlayProbe > Setup`, or `Assets > Create > PlayProbe > Configuration`.
2. Add a `PlayProbeManager` component to a bootstrap GameObject in your first scene.

### PlayProbeConfig fields
Connection:
- `shareToken` — the test's share token.
- `isStandaloneTest` — `true`: start immediately using the share token. `false`: show the handoff-token screen (tester pastes a code from the dashboard session page).

Session:
- `enableFpsTracking` — sample FPS during the session (summary sent on session end).
- `enablePositionHeatmap` — periodically log tracked-object positions.
- `positionLogInterval` — seconds between position samples.
- `enableCrashReporting` — capture Unity `Error`/`Exception` logs as events.

Survey:
- `allowSurveyDismiss` — show the survey skip button.
- `pauseTimeDuringSurvey` — (intended) pause gameplay while a survey is shown.

## 4. Quick start

```csharp
using PlayProbe;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    private void Start()
    {
        // 1) Register mid-game surveys (schema only) before starting the session.
        PlayProbeManager.Instance.Survey.Register("after_level_1")
            .AddRating("How would you rate this level?", "lvl1_rating")
            .AddMultipleChoice("Favorite part?", "lvl1_fav",
                new[] { "Enemies", "Graphics", "Sound", "Gameplay" })
            .AddYesNo("Hit any bugs?", "lvl1_bugs")
            .AddText("Anything else?", "lvl1_notes", required: false);

        // 2) (Optional) analytics targets.
        // PlayProbeManager.Instance.Analytics.SetTrackedTransform(player.transform);

        // 3) Start the session.
        PlayProbeManager.Instance.StartSession();
    }

    // Later, at a gameplay milestone:
    public void OnLevel1Complete() => PlayProbeManager.Instance.ShowSurvey("after_level_1");
}
```

The session ends automatically on `OnApplicationQuit`, or call `PlayProbeManager.Instance.EndSession()`.

## 5. API reference

### PlayProbeManager
- `static PlayProbeManager Instance`
- `bool IsSessionActive`
- `PlayProbeSurvey Survey`, `PlayProbeAnalytics Analytics`, `PlayProbeEvents Events`
- `void StartSession()` — standalone (share token) or handoff (token screen) per `isStandaloneTest`.
- `void EndSession()` — stops tracking, flushes events, posts duration + FPS summary.
- `void ShowSurvey(string triggerKey)` — display a registered mid-game survey.

### PlayProbeSurvey
- `SurveyBuilder Register(string triggerKey)` — create/extend a survey. Chain question builders:
  - `AddRating(label, sdkQuestionId, required = true)` — 1–5.
  - `AddEmojiScale(label, sdkQuestionId, required = true)` — 1–5 emoji.
  - `AddYesNo(label, sdkQuestionId, required = true)`
  - `AddMultipleChoice(label, sdkQuestionId, string[] options, required = true)`
  - `AddText(label, sdkQuestionId, required = false)`
- `sdkQuestionId` must be unique per test and stable across builds (it maps to the backend question).

Register surveys **before** `StartSession()` — the schema is sent with the start request and the backend returns the concrete question IDs used at submit time.

### PlayProbeAnalytics
- `void SetTrackedTransform(Transform t)` — primary tracked object (e.g. player).
- `void RegisterTrackedObject(string tag, Transform t)` — additional tagged objects.
- `float AverageFps`, `float MinFps`, `bool HasFpsSamples`.
- FPS is sampled every second; positions are logged every `positionLogInterval` seconds when `enablePositionHeatmap` is on.

### PlayProbeEvents
Custom gameplay events (server `event_type` "custom"), buffered and uploaded in batches (threshold 20 / every 30s, 3 retries then drop). No-op with a warning when no session is active.
- `void LogEvent(string eventName)`
- `void LogEvent(string eventName, float value)`
- `void LogEvent(string eventName, string valueText)`
- `void LogPosition(Vector3 position, string name, string tag = null)`

```csharp
PlayProbeManager.Instance.Events.LogEvent("level_complete", 3f);
PlayProbeManager.Instance.Events.LogEvent("difficulty_selected", "hard");
```

Crash reporting (when `enableCrashReporting`) auto-captures `Error`/`Exception` logs as `exception` events.

## 6. Session modes

- **Standalone** (`isStandaloneTest = true`): posts to `sdk-start-session` with the share token and starts immediately.
- **Handoff** (`isStandaloneTest = false`): loads `Resources/PlayProbeStartSessionScreen`; the tester enters the code shown on the dashboard session page. The code is validated via `sdk-check-function`, then the session starts via `sdk-start-session`.

## 7. Backend endpoints (reference)
`sdk-check-function`, `sdk-start-session`, `sdk-session-end`, `sdk-events`, `sdk-mid-survey` — all under `https://api.playprobe.io/`. Payload field names are snake_case and must match the edge-function contracts.

## 8. Reliability
- The SDK never throws into gameplay; failures log `[PlayProbe]` warnings.
- Events are buffered with timed + threshold flushes and bounded retries.
- Null/missing config degrades gracefully (session simply does not start).

## 9. Troubleshooting
- **Session does not start** — check `shareToken`, that the test is open with SDK mode enabled, and the Unity console for `[PlayProbe]` warnings.
- **Survey does not show** — ensure the `triggerKey` passed to `ShowSurvey` matches a `Register(...)` call made before `StartSession()`.
- **Position heatmap empty** — set `enablePositionHeatmap = true` and call `SetTrackedTransform`/`RegisterTrackedObject` with non-null transforms.
- **Custom events missing** — `LogEvent` only records while a session is active.
