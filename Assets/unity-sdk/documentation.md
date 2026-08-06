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

Privacy (see [section 10](#10-privacy-and-gdpr)):
- `requireConsent` — when `true`, nothing is collected or sent until you call `SetConsent(true)`. Default `false`.
- `privacyPolicyUrl` — **your** privacy policy URL, shown in the feedback popup and any consent UI you build.
- `feedbackPrivacyNotice` — override the built-in notice line in the feedback popup (for translation).

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
- `IReadOnlyList<AnswerTag> AnswerTags` — global tag vocabulary delivered at session start (see **Answer tags**).
- `void SetConsent(bool granted)` — record the player's privacy decision (see [section 10](#10-privacy-and-gdpr)).
- `void ResetConsent()` — forget the stored decision so the player is asked again.
- `PlayProbeConsent Consent` — current decision (`Status`, `HasAnswered`, `Changed` event).
- `bool IsCollectionAllowed` — whether the SDK may currently collect.

### PlayProbeSurvey
- `SurveyBuilder Register(string triggerKey)` — create/extend a survey. Chain question builders:
  - `AddRating(label, sdkQuestionId, required = true)` — 1–5.
  - `AddEmojiScale(label, sdkQuestionId, required = true)` — 1–5 emoji.
  - `AddYesNo(label, sdkQuestionId, required = true)`
  - `AddMultipleChoice(label, sdkQuestionId, string[] options, required = true)`
  - `AddText(label, sdkQuestionId, required = false)`
- `sdkQuestionId` must be unique per test and stable across builds (it maps to the backend question).

Register surveys **before** `StartSession()` — the schema is sent with the start request and the backend returns the concrete question IDs used at submit time.

### Answer tags
A global tag vocabulary (managed by PlayProbe) lets testers label **open-ended answers** and **Instant Feedback** so results can be grouped by theme (Combat, UI/UX, Performance, …). The active list arrives with the start-session response as `PlayProbeManager.Instance.AnswerTags` (`AnswerTag { id, slug, label, sort_order }`).
- **Surveys:** in your survey UI, show the tags on `text` questions and write the chosen ids into `SurveyResponse.tag_ids` before submit. Ignored server-side for non-text questions.
- **Instant Feedback:** pass the chosen ids to `PlayProbeManager.Instance.SubmitFeedback(title, description, category, attachScreenshot, tagIds)`.
Tags are always optional; unknown/inactive ids are dropped by the backend.

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
- **Nothing is sent at all** — if `requireConsent` is on, the SDK waits for `SetConsent(true)`. The console logs `Session start is waiting for consent`.

## 10. Privacy and GDPR

### Who is responsible for what

When you ship the SDK in your game, **you are the data controller and PlayProbe is your processor**. You decide what is collected and why; we process it on your instructions. In practice that means:

- **Telling your players** that you use PlayProbe is your job, not ours. We deliberately do not show a consent popup inside your game — the wording, timing and legal basis are yours to choose.
- **Obtaining consent**, where your jurisdiction requires it, is also yours. The SDK gives you the switch (`requireConsent` + `SetConsent`); you decide when to flip it.
- Our obligations to you are in the [Data Processing Agreement](https://playprobe.io/dpa), which applies automatically through the PlayProbe Terms — no signature needed.

### What the SDK collects

| Data | When | Notes |
| --- | --- | --- |
| Platform, Unity version, screen size, SDK version | Session start | Coarse (e.g. `Windows`, `Android`) |
| Session duration, average/min FPS | Session end | |
| Custom events you log | `LogEvent(...)` | You choose the names and values |
| Unity `Error`/`Exception` logs + stack traces | If `enableCrashReporting` | Avoid putting player data in exception messages |
| Tracked object positions | If `enablePositionHeatmap` | In-game coordinates, not real-world location |
| Survey answers | On submit | Free text — tell players not to type personal details |
| Feedback title, description, scene, world position, playtime, FPS, memory | Instant Feedback submit | |
| **Hardware profile**: OS + version, CPU model and core count, GPU model, RAM, video memory, device type, device model | Instant Feedback submit | Distinctive in combination — treat it as personal data |
| **Screenshot** of the current screen | Instant Feedback, if the player leaves the toggle on | Whatever is on screen is captured |

The SDK does **not** collect advertising IDs, contact details, precise location, or any persistent cross-app device identifier. Recordings and feedback screenshots are deleted automatically after 30 days.

The only thing the SDK writes to the device is the consent decision, in `PlayerPrefs` under `playprobe_consent`.

### Gating collection on consent

Turn it on in the config, then tell the SDK what the player chose:

```csharp
using PlayProbe;
using UnityEngine;

public class PrivacyGate : MonoBehaviour
{
    private void Start()
    {
        // Safe to call before consent: with requireConsent = true it waits, and starts
        // by itself once the player agrees. No network call happens in the meantime.
        PlayProbeManager.Instance.StartSession();

        if (!PlayProbeManager.Instance.Consent.HasAnswered)
        {
            ShowYourOwnConsentDialog();
        }
    }

    // Wire these to the buttons in YOUR dialog.
    public void OnPlayerAccepted() => PlayProbeManager.Instance.SetConsent(true);
    public void OnPlayerDeclined() => PlayProbeManager.Instance.SetConsent(false);
}
```

Behaviour with `requireConsent = true`:

- **Before consent** — `StartSession()` sends nothing and remembers it was asked. No session exists. Crash reporting hooks Unity's log callback at startup, but events raised before consent are dropped rather than buffered, so granting consent later does not upload anything from before the player agreed.
- **On `SetConsent(true)`** — the deferred session starts automatically.
- **On `SetConsent(false)`** — collection stops immediately, buffered events are **discarded** rather than sent, the feedback button is removed, and any open feedback popup is cancelled. Note that no session-end call is made either, so a withdrawn session stays open in the dashboard until it times out — that is deliberate, since sending the duration and FPS summary would be more processing after the player said stop.
- The decision persists between runs, so you only ask once. Give players a way to change their mind (an options-menu toggle calling `SetConsent`), since withdrawing consent must be as easy as giving it.

With `requireConsent = false` (the default) nothing is blocked — the decision is still recorded if you call `SetConsent`, so you can adopt this gradually.

### The feedback popup must show a notice

The Instant Feedback report sends a hardware profile and, by default, a screenshot. Players should know that *before* they submit. If you build your own feedback popup, display `PrivacyNotice` and link `PrivacyPolicyUrl`:

```csharp
PlayProbeFeedback feedback = PlayProbeManager.Instance.Feedback;
noticeLabel.text = feedback.PrivacyNotice;              // config override, or built-in default
if (feedback.PrivacyPolicyUrl != null)
{
    policyButton.onClick.AddListener(() => Application.OpenURL(feedback.PrivacyPolicyUrl));
}
```

Set `feedbackPrivacyNotice` in the config to translate it.

### Copy-paste text for your privacy policy

Adapt and paste this into your own policy. Replace the bracketed parts.

> **Playtesting and analytics**
>
> We use PlayProbe, a playtesting platform, to understand how people play [GAME NAME] and to collect
> your feedback. When you play, PlayProbe receives technical and gameplay information: the platform
> and Unity version, your screen resolution, how long you played, frame-rate measurements, crash
> reports, and in-game events such as your position in a level.
>
> If you send us feedback from inside the game, PlayProbe also receives what you wrote, technical
> details about your computer or device (operating system, processor, graphics card, and memory),
> and — if you leave the option ticked — a screenshot of your screen at that moment.
>
> PlayProbe processes this on our behalf as our data processor and does not use it for its own
> purposes. Feedback screenshots and any session recordings are deleted after 30 days. PlayProbe is
> operated by Jakub Gabčo (Drages Studio), Brno, Czechia; its privacy policy is at
> https://playprobe.io/privacy.
>
> Our legal basis is [YOUR LEGAL BASIS — e.g. your consent, which you can withdraw at any time in
> Settings → Privacy]. To ask for a copy of your data or its deletion, contact us at [YOUR EMAIL].

### Checklist before you ship

- [ ] Name PlayProbe in your own privacy policy (text above).
- [ ] Decide whether you need `requireConsent = true` — you probably do for EU/UK players.
- [ ] Give players a way to withdraw consent later, not just at first launch.
- [ ] Set `privacyPolicyUrl` in the config.
- [ ] Show `PrivacyNotice` in your feedback popup if you built your own.
- [ ] Do not log personal data through `LogEvent` values or exception messages.
- [ ] If your game is played by children, check the age of digital consent in your markets — it ranges from 13 to 16 across the EU.
