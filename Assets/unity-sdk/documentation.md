# PlayProbe Unity SDK Documentation

This document explains how to integrate and use the PlayProbe Unity SDK currently located in `Assets/unity-sdk`.

It covers:
- Initial setup
- Core classes and methods
- Recommended workflows
- Survey and analytics usage
- Troubleshooting

## 1. SDK Overview

PlayProbe SDK provides four main capabilities:
- Session lifecycle tracking (start/end game test sessions)
- Buffered event collection and upload
- Passive performance analytics (FPS, position)
- Mid-game survey delivery and response submission

The runtime entry point is `PlayProbeManager`.

## 2. Prerequisites

- Unity 2021.3+
- A Supabase project configured for PlayProbe tables
- A valid PlayProbe share token and test setup

## 3. Initial Setup

### 3.1 Add Config Asset

Create a config asset at:
- `Assets/Resources/PlayProbeConfig.asset`

You can do this from:
- `Tools > PlayProbe > Setup`

Or manually:
- `Assets > Create > PlayProbe > Configuration`

### 3.2 Fill PlayProbeConfig

`PlayProbeConfig` fields:

Connection:
- `supabaseUrl`: Supabase URL (example: `https://xxxx.supabase.co`)
- `supabaseAnonKey`: Supabase anon/public key
- `shareToken`: Optional default share token for session start
- `gameId`: Game identifier used by some SDK features/events

Session:
- `enableFpsTracking`: Enables FPS analytics summary patching at session end
- `enablePositionHeatmap`: Enables periodic position logging
- `positionLogInterval`: Seconds between position samples
- `enableCrashReporting`: Auto-captures Unity errors/exceptions into events

Survey:
- `allowSurveyDismiss`: Shows/hides survey skip button
- `pauseTimeDuringSurvey`: If true, survey display sets `Time.timeScale = 0` until close

### 3.3 Add PlayProbeManager to Scene

Add `PlayProbeManager` component to a bootstrap GameObject in your first scene.

`PlayProbeManager` is a singleton and marks itself `DontDestroyOnLoad`.

### 3.4 Optional: Survey Overlay Prefab

`PlayProbeSurvey` tries to load `Resources/SurveyOverlay` first.

If not found, it creates a fallback overlay dynamically at runtime.

Recommended path for a custom overlay prefab:
- `Assets/Resources/SurveyOverlay.prefab`

## 4. Quick Start Workflow

Typical flow:
1. App starts, scene contains `PlayProbeManager`
2. Call `PlayProbeManager.Instance.Initialize(shareToken)`
3. Optionally assign tracked transforms for analytics
4. Preload surveys for a test id
5. Trigger surveys at gameplay milestones
6. Session ends automatically on app quit (or call end manually)

Example bootstrap script:

```csharp
using System.Threading.Tasks;
using UnityEngine;
using PlayProbe;

public class PlayProbeBootstrap : MonoBehaviour
{
    [SerializeField] private string shareTokenOverride;
    [SerializeField] private string testIdForSurveyPreload;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform bossTransform;

    private async void Start()
    {
        PlayProbeManager manager = PlayProbeManager.Instance;

        if (manager == null)
        {
            Debug.LogWarning("PlayProbeManager is missing in scene.");
            return;
        }

        manager.Initialize(shareTokenOverride);

        manager.Analytics.SetTrackedTransform(playerTransform);
        manager.Analytics.RegisterTrackedObject("boss", bossTransform);

        if (!string.IsNullOrWhiteSpace(testIdForSurveyPreload))
        {
            await manager.Survey.PreloadSurveys(testIdForSurveyPreload);
        }
    }

    public void OnAfterLevel1()
    {
        PlayProbeManager.Instance?.Survey.ShowSurvey("after_level_1", () =>
        {
            Debug.Log("Survey completed or skipped.");
        });
    }
}
```

## 5. Core Class Reference

## 5.1 PlayProbeManager

Main runtime orchestrator.

Properties:
- `Instance`
- `Session`
- `Events`
- `Survey`
- `Analytics`

Methods:
- `Initialize(string shareToken)`

Behavior:
- Loads config from `Resources/PlayProbeConfig`
- Configures HTTP utility
- Creates events/session/survey/analytics subsystems
- Starts event auto-flush
- Starts session and analytics when initialized
- Flushes pending events on app pause
- Ends session on application quit

## 5.2 PlayProbeSession

Manages session lifecycle connected to PlayProbe backend.

Properties:
- `SessionId`
- `SessionToken`
- `IsActive`
- `StartTime`

Methods:
- `Task<bool> StartSession(string shareToken)`
- `Task EndSession()`
- `Task FlushEvents(List<SdkEventPayload> events)`

Start behavior:
1. Resolves `test_id` from `share_token`
2. Inserts into `/rest/v1/sessions`
3. Inserts SDK metadata into `/rest/v1/sdk_sessions`
4. Stores IDs and marks session active

End behavior:
1. Patches session status and duration in `/rest/v1/sessions`
2. Patches FPS summary in `/rest/v1/sdk_sessions` when enabled
3. Flushes buffered SDK events and pending event queue
4. Marks session inactive

All network operations are wrapped in try/catch and log warnings on failure.

## 5.3 PlayProbeEvents

Buffered event logger with automatic batch uploads.

Data model:
- `SdkEventPayload`
  - `session_id`
  - `event_type`
  - `event_name`
  - `value_num`
  - `value_text`
  - `value_json`
  - `timestamp`

Public methods:
- `LogEvent(string eventName, float? value = null)`
- `LogEvent(string eventName, Dictionary<string, object> data)`
- `LogException(System.Exception exception)`
- `LogPosition(Vector3 position, string tag = null)`
- `Task FlushAsync()`
- `Task FlushPendingEvents()`
- `void StartAutoFlush(MonoBehaviour runner)`
- `void StopAutoFlush()`

Internal method used by analytics:
- `LogFps(float fps)`

Batching behavior:
- Buffer threshold: 20 events
- Time-based flush interval: 30 seconds
- Retry policy: up to 3 failed attempts before dropping buffered batch

Crash reporting:
- If enabled in config, auto-subscribes to `Application.logMessageReceived`
- Logs `Error` and `Exception` as SDK events

## 5.4 PlayProbeAnalytics

Passive performance and position tracking via coroutines.

Properties:
- `AverageFps`
- `MinFps`
- `HasFpsSamples`

Methods:
- `StartTracking(PlayProbeConfig config)`
- `StopTracking()`
- `SetTrackedTransform(Transform t)`
- `RegisterTrackedObject(string tag, Transform t)`

Behavior:
- FPS coroutine samples every 1 second
- Every 10 samples, logs an FPS event (noise reduction)
- Position coroutine logs tracked positions at `positionLogInterval`
- Supports one primary tracked transform plus multiple tagged transforms

## 5.5 PlayProbeSurvey

Handles survey preloading, display, and response submission.

Data classes:
- `SurveyQuestion`
- `SurveyTrigger`

Methods:
- `Task PreloadSurveys(string testId)`
- `void ShowSurvey(string surveyKey, Action onComplete = null)`
- `Task SubmitSurveyResponses(string surveyKey, Dictionary<string, object> answers)` (internal)

Preload behavior:
- Fetches all `mid_game` questions from `/rest/v1/form_questions`
- Groups by `trigger_key`
- Caches in memory for offline/mid-game reliability

Display behavior:
- Looks up survey by `trigger_key`
- Instantiates overlay
- Optionally pauses gameplay (`Time.timeScale`) based on config
- Logs shown/submitted/skipped events

Response behavior:
- Inserts each answer row into `/rest/v1/responses`
- Maps answer values to:
  - `value_number` (rating/numeric)
  - `value_choice` (choice-based)
  - `value_text` (text/fallback)

## 5.6 SurveyOverlay

Dynamic UI controller for survey presentation.

Method:
- `Initialize(List<SurveyQuestion> questions, Action<Dictionary<string, object>> onSubmit, Action onSkip)`

Supported question types:
- `rating`
- `yes_no`
- `multiple_choice`
- `text`
- `emoji_scale`

UI behavior:
- Sort order forced to 999
- Dark panel color set to `#0f0f13` at ~90% opacity
- Required-question validation before submit
- Skip button visibility controlled by config `allowSurveyDismiss`

## 5.7 PlayProbeHttp

Async `UnityWebRequest` wrapper.

Methods:
- `Configure(PlayProbeConfig config)`
- `Task<string> PostAsync(string endpoint, string json, string authToken = null)`
- `Task<string> GetAsync(string endpoint, string authToken = null)`

Headers applied:
- `apikey`
- `Content-Type: application/json`
- Optional `Authorization: Bearer ...`

## 6. Recommended Workflows

### 6.1 Basic Integration

1. Place manager in startup scene
2. Configure `PlayProbeConfig`
3. Call `Initialize(shareToken)`
4. Let SDK auto-handle session start/analytics/event flushing

### 6.2 Custom Gameplay Events

Use numeric custom events:

```csharp
PlayProbeManager.Instance?.Events.LogEvent("level_complete", 3f);
```

Use structured payload events:

```csharp
PlayProbeManager.Instance?.Events.LogEvent(
    "boss_defeated",
    new System.Collections.Generic.Dictionary<string, object>
    {
        { "boss_id", "dragon_01" },
        { "time_seconds", 127.45f },
        { "damage_taken", 2300 }
    }
);
```

### 6.3 Analytics Tracking Setup

```csharp
PlayProbeManager manager = PlayProbeManager.Instance;
manager.Analytics.SetTrackedTransform(playerTransform);
manager.Analytics.RegisterTrackedObject("npc_vendor", vendorTransform);
manager.Analytics.RegisterTrackedObject("objective", objectiveTransform);
```

### 6.4 Survey Workflow

1. Call preload when you know `testId`
2. Trigger surveys by dashboard `trigger_key`
3. Handle completion callback

```csharp
await PlayProbeManager.Instance.Survey.PreloadSurveys(testId);
PlayProbeManager.Instance.Survey.ShowSurvey("after_level_1", OnSurveyFinished);
```

If a key is not found in cache, SDK logs a warning and safely returns.

## 7. Error Handling and Reliability

SDK behavior by design:
- Does not throw unhandled exceptions into gameplay flow
- Uses warnings for network and payload failures
- Keeps game running even when backend is unavailable

Reliability features:
- Event buffering + batched upload
- Timed auto-flush and threshold flush
- Retry attempts before dropping stuck buffers
- Survey cache preloading for mid-game resilience

## 8. Important Notes

- `PlayProbeConfig.asset` must be in `Assets/Resources`
- `Initialize` should be called once per app session
- Survey preloading currently requires explicit `testId`
  - `StartSession` resolves test id internally from share token, but this id is not exposed publicly
  - If needed, fetch/store `testId` from your backend or dashboard metadata for survey preload
- If you use custom overlay visuals, keep a `SurveyOverlay` component on the prefab

## 9. Troubleshooting

### "PlayProbeConfig not found"
- Ensure file exists at `Assets/Resources/PlayProbeConfig.asset`

### No events appear in backend
- Verify `supabaseUrl` and `supabaseAnonKey`
- Check network connectivity
- Confirm table policies allow insert/select from anon key
- Watch Unity console for `[PlayProbe]` warnings

### Session does not start
- Ensure valid `shareToken`
- Confirm `/rest/v1/tests?share_token=eq...` returns a test row

### Position heatmap is empty
- Set `enablePositionHeatmap = true`
- Call `SetTrackedTransform` or `RegisterTrackedObject`
- Ensure transforms are not null at runtime

### Survey does not show
- Ensure `PreloadSurveys(testId)` was called successfully
- Check `surveyKey` exactly matches `trigger_key`
- Verify `form_questions` rows use `phase = mid_game`

## 10. Minimal API Checklist

At startup:
- `PlayProbeManager.Instance.Initialize(shareToken)`

Optional analytics setup:
- `PlayProbeManager.Instance.Analytics.SetTrackedTransform(player)`
- `PlayProbeManager.Instance.Analytics.RegisterTrackedObject("tag", transform)`

Survey setup:
- `await PlayProbeManager.Instance.Survey.PreloadSurveys(testId)`
- `PlayProbeManager.Instance.Survey.ShowSurvey("trigger_key")`

Custom events:
- `PlayProbeManager.Instance.Events.LogEvent("event_name", value)`
- `PlayProbeManager.Instance.Events.LogEvent("event_name", dictionaryPayload)`
