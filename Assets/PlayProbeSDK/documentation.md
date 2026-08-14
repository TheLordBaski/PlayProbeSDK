# PlayProbe Unity SDK

Everything the SDK does, and how to use it. Package `io.playprobe.sdk`, Unity 6000.0 or newer.

1. [Overview](#1-overview)
2. [Install and set up](#2-install-and-set-up)
3. [Configuration reference](#3-configuration-reference)
4. [Sessions](#4-sessions)
5. [Events and analytics](#5-events-and-analytics)
6. [Surveys](#6-surveys)
7. [Instant Feedback](#7-instant-feedback)
8. [Answer tags](#8-answer-tags)
9. [The UI layer](#9-the-ui-layer)
10. [Building your own UI](#10-building-your-own-ui)
11. [Privacy and GDPR](#11-privacy-and-gdpr)
12. [Reliability and troubleshooting](#12-reliability-and-troubleshooting)
13. [API reference](#13-api-reference)

---

## 1. Overview

PlayProbe records what happens in a playtest and sends it to your dashboard:

- **Session lifecycle** — standalone, or handed off from a specific dashboard session.
- **Passive analytics** — an FPS summary, and optionally a position heatmap.
- **Events** — your own gameplay events plus captured Unity errors, buffered and batched.
- **Mid-game surveys** — registered in code, rendered by the SDK, submitted on answer.
- **Instant Feedback** — a report form with a screenshot and a hardware profile attached.

Everything goes through one entry point: `PlayProbeManager`, a `DontDestroyOnLoad` singleton.
The backend is a set of edge functions under `https://api.playprobe.io/`.

**The SDK never throws into your game.** Every failure path logs a `[PlayProbe]` warning and
degrades: a missing config means no session, a missing prefab means no popup, a dead network means
dropped events. Nothing bubbles an exception into your `Update`.

## 2. Install and set up

**Requirements:** Unity 6000.0+, `com.unity.ugui` (already in every project), a PlayProbe **Pro**
plan, and a test with a **share token** and SDK mode enabled.

> The SDK awaits `AsyncOperation` directly, which Unity only supports from 2023.1 onward. 6000.0 is
> the version it is built and tested against.

Then, from `Tools > PlayProbe > Setup`:

1. **Create PlayProbeConfig Asset** — written to `Assets/Resources/PlayProbeConfig.asset`.
2. Paste your **share token**. It is checked automatically the moment it is complete.
3. **Create Missing UI Prefabs** — writes the screens into the package's `Resources` folder. Without
   this the survey and feedback popups have nothing to spawn.
4. **Create PlayProbeManager In Active Scene** — adds the manager and assigns your config to it.

### The SDK is a Pro feature

`sdk-start-session` refuses sessions for tests owned by a Free account — it answers
`plan_required` and the game logs a warning instead of collecting anything. Nothing crashes, but
nothing is recorded either.

The setup window asks the backend the same question at edit time, so you find out in the editor
rather than from a build you already shipped. It reports which of the three gates is unmet — plan,
SDK mode, test open — and offers a button straight to the upgrade page when it is the plan.

The check runs **by itself** as soon as the token field holds a complete token (36 characters), a
fraction of a second after you paste. Before that the window tells you what is wrong with the value
without touching the network — too short, too long, whitespace around it, or the right length but
not the usual `8-4-4-4-12` shape. **Check Again** re-runs it on demand, which is what you want after
switching the test to SDK mode or upgrading the account: the token has not changed, so nothing would
re-trigger on its own.

Put the manager in your first scene. It survives scene loads.

## 3. Configuration reference

`PlayProbeConfig` is a ScriptableObject. Edit it through the setup window or directly in the
inspector.

### Connection

| Field | Meaning |
| --- | --- |
| `shareToken` | The test's share token, from the dashboard. Without it the SDK stays inactive. |
| `isStandaloneTest` | `true`: start immediately with the share token. `false`: show the handoff-code screen first. |

### Session

| Field | Meaning |
| --- | --- |
| `enableFpsTracking` | Sample FPS every second. The average and minimum are sent at session end. |
| `enablePositionHeatmap` | Periodically log the positions of transforms you registered. |
| `positionLogInterval` | Seconds between position samples. |
| `enableCrashReporting` | Capture Unity `Error` and `Exception` logs as events. **See the warning in [section 11](#11-privacy-and-gdpr).** |

### Survey

| Field | Meaning |
| --- | --- |
| `allowSurveyDismiss` | Show the skip button, and let Escape close the survey. |
| `pauseTimeDuringSurvey` | Set `Time.timeScale` to 0 while a survey is on screen. |

### Instant Feedback

| Field | Meaning |
| --- | --- |
| `enableInstantFeedback` | Master switch. When off, `Feedback` is null and `OpenFeedback()` warns. |
| `feedbackButtonCorner` | Which corner the floating button parks in. |
| `pauseGameDuringFeedback` | Freeze the game while the popup is open. |
| `feedbackAllowScreenshot` | Whether screenshots are possible at all. Off hides the whole block. |
| `feedbackScreenshotDefaultOn` | Whether the attach-screenshot toggle starts ticked. |
| `feedbackScreenshotMaxWidth` | Screenshots wider than this are downscaled before upload. |

### Privacy

| Field | Meaning |
| --- | --- |
| `requireConsent` | When on, nothing is collected or sent until `SetConsent(true)`. Default off. |
| `useBuiltInConsentDialog` | With the above on, `StartSession()` shows PlayProbe's consent prompt when the player has not answered. Default on. Turn it off if you show your own. |
| `privacyPolicyUrl` | **Your** policy URL. Shown in the feedback popup and consent dialog; the link hides itself when blank. |
| `feedbackPrivacyNotice` | Overrides the built-in notice line — use it to translate. |

## 4. Sessions

```csharp
PlayProbeManager.Instance.StartSession();
// ...
PlayProbeManager.Instance.EndSession();   // also happens automatically on quit
```

**Standalone** (`isStandaloneTest = true`) posts to `sdk-start-session` with the share token and
starts straight away. Use it for editor testing and public builds.

**Handoff** (`isStandaloneTest = false`) shows the code-entry screen first. The tester types the
eight-character code from their dashboard session page; it is validated against
`sdk-check-function`, and only then does the session start — tied to that tester's session. The
screen closes itself once the session is live, and shows an inline error if the code is refused.

`EndSession()` stops tracking, flushes what is buffered, removes the feedback button, and posts the
duration and the FPS summary.

Register your surveys **before** `StartSession()`: the schema travels with the start request, and the
backend returns the question ids used at submit time.

## 5. Events and analytics

### Custom events

```csharp
PlayProbeEvents events = PlayProbeManager.Instance.Events;

events.LogEvent("checkpoint_reached");                  // no value
events.LogEvent("score_gained", 250f);                  // numeric
events.LogEvent("difficulty_selected", "hard");         // text
events.LogPosition(player.position, "player", "death"); // a tagged point
```

Events are buffered and uploaded in batches — whenever 20 pile up, every 30 seconds, and on session
end. A failed upload is retried three times before the batch is dropped, and the buffer is capped at
500 events so an offline player never grows the SDK's memory use without limit.

`LogEvent` is a no-op with a warning when no session is active.

### Analytics

```csharp
PlayProbeAnalytics analytics = PlayProbeManager.Instance.Analytics;

analytics.SetTrackedTransform(player.transform);      // the primary subject
analytics.RegisterTrackedObject("enemy", boss);        // additional tagged objects

float average = analytics.AverageFps;
float worst = analytics.MinFps;
```

FPS is sampled every second while `enableFpsTracking` is on. Positions are logged every
`positionLogInterval` seconds while `enablePositionHeatmap` is on — the primary transform plus every
tagged object you registered.

### Crash capture

With `enableCrashReporting`, the SDK hooks Unity's log callback and turns every `Error` and
`Exception` into an event carrying the message and the stack trace. Note this includes your own
`Debug.LogError` calls — see the privacy warning in [section 11](#11-privacy-and-gdpr).

## 6. Surveys

Register a survey against a **trigger key**, then show it when that moment arrives.

```csharp
PlayProbeManager.Instance.Survey.Register("after_level_1")
    .AddRating("How would you rate this level?", "lvl1_rating")
    .AddEmojiScale("How did the boss feel?", "lvl1_boss_feel")
    .AddYesNo("Hit any bugs?", "lvl1_bugs")
    .AddMultipleChoice("Favourite part?", "lvl1_fav",
        new[] { "Enemies", "Graphics", "Sound", "Gameplay" })
    .AddText("Anything else?", "lvl1_notes", required: false);

// Later:
PlayProbeManager.Instance.ShowSurvey("after_level_1");
```

The second argument to every `Add…` is an **`sdkQuestionId`**: your own stable identifier for that
question. It must be unique within the test and must not change between builds — it is what the
backend maps answers onto. The label can change freely; the id cannot.

| Method | Renders as | Submitted as |
| --- | --- | --- |
| `AddRating` | A 1–5 bar that fills up to your pick | `value_number` |
| `AddEmojiScale` | A row of five faces, one chosen | `value_number` |
| `AddYesNo` | Two buttons | `value_choice` — `"Yes"` or `"No"` |
| `AddMultipleChoice` | Options, two per row | `value_choice` — the option text |
| `AddText` | A multi-line box, with the tag chooser | `value_text` + `tag_ids` |

`required` defaults to `true` for everything except `AddText`. Required questions block submission
until answered; optional ones the player skipped are simply left out of the submission.

`ShowSurvey` warns and does nothing when there is no active session, or when the trigger key was
never registered — which almost always means a typo, or a `Register` call that happened after
`StartSession`.

## 7. Instant Feedback

Turn on `enableInstantFeedback` and a floating button appears when the session starts. Clicking it
captures the current frame, pauses the game, and opens the report form: a title, a description, a
category, the screenshot with a preview and a toggle, the tag chooser, and the privacy notice.

Open it from your own UI instead — a pause-menu entry, a keyboard shortcut:

```csharp
PlayProbeManager.Instance.OpenFeedback();
```

Don't want the floating button at all? Delete `PlayProbeFeedbackButton.prefab` from the package's
`Resources` folder and use `OpenFeedback()` from wherever suits your game.

Or skip the popup entirely and submit from code:

```csharp
PlayProbeManager.Instance.SubmitFeedback(
    title: "Fell through the floor",
    description: "Standing on the bridge in level 2, near the second torch.",
    category: "bug",              // bug | suggestion | praise | other
    attachScreenshot: true,
    tagIds: null);
```

Each report carries: what the player typed, the scene name and build index, their world position,
playtime, instantaneous and average FPS, memory, quality settings, screen size, a **hardware
profile** (OS, CPU, GPU, RAM), and — if the toggle is left on — a **screenshot**. The last two are
why the popup shows a notice.

Categories are fixed server-side (`bug`, `suggestion`, `praise`, `other`); anything else is stored as
no category. Translate the *labels* in the UI theme, not the ids. Titles are capped at 200
characters and descriptions at 4000.

## 8. Answer tags

A tag vocabulary managed in PlayProbe (Combat, UI/UX, Performance, …) lets testers label what an
open-ended answer is *about*, so results can be grouped by theme. The active list arrives with the
start-session response:

```csharp
IReadOnlyList<AnswerTag> tags = PlayProbeManager.Instance.AnswerTags;
// AnswerTag { id, slug, label, sort_order }
```

The built-in UI already handles this: `PlayProbeTagSelector` renders the pills on open-ended survey
questions and in the feedback popup, and writes the chosen ids into the submission. When the test has
no tags configured, the whole chooser hides itself.

Tags are always optional, at most three per answer by default, and the backend drops ids that are not
in the active vocabulary. They are ignored on anything other than free-text answers and feedback.

## 9. The UI layer

Every PlayProbe screen is **generated from one asset** rather than hand-built:

| Prefab | What it is |
| --- | --- |
| `PlayProbeSurveyCanvas` | The survey overlay, with a scroll area for long surveys |
| `PlayProbeFeedbackCanvas` | The Instant Feedback popup |
| `PlayProbeFeedbackButton` | The floating corner button |
| `PlayProbeStartSessionScreen` | The handoff code entry |
| `PlayProbeConsentDialog` | The optional consent prompt |
| `PlayProbeToast` | The "thanks, sent" confirmation |
| `PlayProbeTagChip` / `PlayProbeSelectableButton` | Shared pieces the others spawn |
| `PlayProbe*Question` | One per question type |

### The sprite set

The shapes — rounded rectangles, capsules, the checkmark and the speech bubble — come from nine PNGs
in `Textures/UI`. They are **white with an alpha shape**, because Unity multiplies a Button's colour
block by its target graphic's colour: tint both and the two multiply together, so the brand purple
comes out muddy and the disabled state loses its alpha. One white sprite gives correct normal, hover,
pressed and disabled states for every colour.

Interactive things (buttons, inputs, toggles) keep a white image and take their colour from the
colour block. Non-interactive things (panels, the scrim, question cards) colour their image directly,
because there is no colour block to do it for them.

Borders are a separate ring Image rather than uGUI's `Outline` effect — that effect draws the graphic
four times at an offset, which smears rather than strokes on a rounded corner.

Drop the PNGs into the package's `Textures/UI/` folder under their exported filenames and the prefab
builder assigns them to the theme's empty sprite slots for you. A slot you have already filled is
left alone, so pointing one at your own artwork survives a rebuild. Leave a slot empty and that shape
falls back to Unity's built-in `UISprite`.

The corner radius does not depend on the PNG's resolution: the builder derives
`pixelsPerUnitMultiplier` from the sprite's own 9-slice border so the rendered radius matches
`cornerRadius` in the theme. Re-export at any size and it still lands correctly.

**Capsules are a special case.** A 9-sliced sprite draws its corners at a fixed size, so a pill can
only be truly round at one height — and a tag chip and the feedback button are different heights.
When the top and bottom borders together exceed the element, Unity scales them down to fit, so the
corner keeps its width but loses height and the round end flattens into an ellipse.
`PlayProbeCapsuleImage` sits on the pill graphics and recomputes the multiplier from the height the
element actually gets, keeping the corner at exactly half the height. It is added automatically and
needs no configuration; put it on your own Image if you build a pill-shaped control by hand.

### Wrapping rows

Tag chips sit in a `PlayProbeFlowLayoutGroup` — a layout uGUI does not ship. `HorizontalLayoutGroup`
keeps everything on one line, and `GridLayoutGroup` wraps but forces every cell to the same size, so
"Tag" came out as wide as "Progression / Pacing". The flow group is the one in between: it wraps, and
each child keeps its own preferred width.

A chip gets that width from its own `HorizontalLayoutGroup` — padding plus the label's text width —
so it fits its label. Give a child a `LayoutElement` to override that. The group reports the height it
needs, so an enclosing vertical layout or `ContentSizeFitter` grows to however many lines the content
lands on.

Reuse it anywhere you want wrapping rows of variable-width things:

```csharp
PlayProbeFlowLayoutGroup flow = container.gameObject.AddComponent<PlayProbeFlowLayoutGroup>();
flow.Spacing = new Vector2(8f, 8f);
```

### Restyling and translating

```
Tools > PlayProbe > UI > Create Theme Asset      → Assets/Resources/PlayProbeUiTheme.asset
   ... edit colours, sizes, and every string ...
Tools > PlayProbe > UI > Rebuild All Prefabs
```

`PlayProbeUiTheme` holds the palette, the type scale, the metrics, and **every user-facing string**
the SDK shows. Translating the SDK means editing one asset — there is no second place to look.

Colours are read at runtime as well as at build time, so changing `primary` restyles selection
states immediately without a rebuild; changing sizes or copy needs the rebuild.

If no theme asset exists, the SDK falls back to the built-in PlayProbe dark theme. You never *have*
to create one.

### The two menu items

- **Create Missing Prefabs** — writes only prefabs that do not exist. Safe: it never touches a prefab
  you customised.
- **Rebuild All Prefabs** — overwrites all of them, after asking. This is what you run after editing
  the theme.

### Keeping your own version of a screen

Replace the prefab in the package's `Resources` folder with your own. The only requirement is that it
carries the matching controller component (`PlayProbeFeedbackCanvas`, `PlayProbeSurveyCanvas`, …)
with its serialized fields wired. Every one of those fields is optional — a popup with no title
input, or no tag chooser, works fine.

## 10. Building your own UI

Nothing about the SDK requires its screens. Every subsystem is callable directly.

**A feedback form of your own** — gather a title and description, then:

```csharp
PlayProbeFeedback feedback = PlayProbeManager.Instance.Feedback;

noticeLabel.text = feedback.PrivacyNotice;                    // your override, or the default
if (feedback.PrivacyPolicyUrl != null)
{
    policyButton.onClick.AddListener(
        () => Application.OpenURL(feedback.PrivacyPolicyUrl));
}

sendButton.onClick.AddListener(() =>
    PlayProbeManager.Instance.SubmitFeedback(
        titleField.text, descriptionField.text, chosenCategory, attachToggle.isOn, chosenTagIds));
```

**Display the notice.** A report sends a hardware profile and possibly a screenshot of whatever is on
screen. The player should know before they submit, not after.

**A survey of your own** — read `PlayProbeManager.Instance.AnswerTags` for the tag vocabulary and
build `SurveyResponse` objects yourself. Or add a new *question type* by implementing
`IPlayProbeQuestionElement` on a prefab in a `Resources` folder; the survey canvas will drive it
exactly like the built-in ones.

**A consent prompt of your own** — see the next section. That one you were always going to build.

## 11. Privacy and GDPR

### Who is responsible for what

When you ship the SDK in your game, **you are the data controller and PlayProbe is your processor**.
You decide what is collected and why; we process it on your instructions.

- **Telling your players** you use PlayProbe is your job. We deliberately do not show a consent popup
  by ourselves — the wording, timing and legal basis are yours to choose.
- **Obtaining consent** where your jurisdiction requires it is also yours. The SDK gives you the
  switch; you decide when to flip it.
- Our obligations to you are in the [Data Processing Agreement](https://playprobe.io/dpa), which
  applies automatically through the PlayProbe Terms — no signature needed.

### What the SDK collects

| Data | When | Notes |
| --- | --- | --- |
| Platform, Unity version, screen size, SDK version | Session start | Coarse — `Windows`, `Android`, … |
| Session duration, average and minimum FPS | Session end | |
| Custom events you log | `LogEvent(...)` | You choose the names and values |
| Unity `Error` / `Exception` logs and stack traces | If `enableCrashReporting` | **Includes your own `Debug.LogError` messages** |
| Tracked object positions | If `enablePositionHeatmap` | In-game coordinates, not real-world location |
| Survey answers | On submit | Free text — tell players not to type personal details |
| Feedback text, scene, world position, playtime, FPS, memory | Feedback submit | |
| **Hardware profile**: OS and version, CPU model and core count, GPU model, RAM, video memory, device type and model | Feedback submit | Distinctive in combination — treat it as personal data |
| **Screenshot** of the current screen | Feedback submit, if the toggle is left on | Whatever is on screen is captured |

The SDK does **not** collect advertising ids, contact details, precise location, or any persistent
cross-app device identifier. Feedback screenshots and session recordings are deleted after 30 days.

The only thing written to the device is the consent decision, in `PlayerPrefs` under
`playprobe_consent`.

> **Crash reporting is broader than it sounds.** It uploads *every* `Debug.LogError`, message and
> stack trace included — not just crashes. If your error messages contain player names, account
> emails, save paths with a username in them, or anything else personal, that goes too. Audit your
> error logging before enabling it, or leave it off.

### Gating collection on consent

Turn on `requireConsent`, then tell the SDK what the player chose:

```csharp
private void Start()
{
    // Safe to call before consent: it waits, and starts by itself once the player agrees.
    // No network call happens in the meantime.
    PlayProbeManager.Instance.StartSession();

    if (!PlayProbeManager.Instance.Consent.HasAnswered)
    {
        ShowYourOwnConsentDialog();
    }
}

public void OnPlayerAccepted() => PlayProbeManager.Instance.SetConsent(true);
public void OnPlayerDeclined() => PlayProbeManager.Instance.SetConsent(false);
```

Behaviour with `requireConsent = true`:

- **Before consent** — `StartSession()` sends nothing and remembers it was asked. Crash reporting
  hooks Unity's log callback at startup, but events raised before consent are *dropped* rather than
  buffered, so agreeing later never uploads anything from before.
- **On `SetConsent(true)`** — the deferred session starts automatically.
- **On `SetConsent(false)`** — collection stops immediately, buffered events are **discarded** rather
  than sent, the feedback button is removed, and an open feedback popup is cancelled. No session-end
  call is made either, so a withdrawn session stays open in the dashboard until it times out. That is
  deliberate: sending the duration and FPS summary would be more processing after the player said
  stop.
- The decision persists between runs. Give players a way to change their mind — an options-menu
  toggle calling `SetConsent` — because withdrawing has to be as easy as agreeing.

With `requireConsent = false` (the default) nothing is blocked, but the decision is still recorded if
you call `SetConsent`, so you can adopt this gradually.

### The built-in consent dialog

With `requireConsent` and `useBuiltInConsentDialog` both on — the default once you require consent —
`StartSession()` shows PlayProbe's prompt itself when the player has not answered, and starts the
session as soon as they agree. There is nothing else to write.

If the player has already declined it is not shown again: re-prompting after a refusal is nagging,
and in several jurisdictions a problem in itself. Call `ResetConsent()` from an options menu to give
them a way back.

Showing your own prompt instead? Turn `useBuiltInConsentDialog` off, or the player sees two:

```csharp
if (!PlayProbeManager.Instance.Consent.HasAnswered)
{
    ShowYourOwnDialog();   // ... which calls SetConsent(true) or SetConsent(false)
}
```

You can also spawn PlayProbe's dialog yourself, at a moment of your choosing:

```csharp
PlayProbeConsentDialog.Show(granted => ResumeWhateverYouPaused());
```

**Read its copy before you ship it.** The default text in the UI theme describes what the SDK
collects in plain language, but it cannot know what *else* your game collects, what your legal basis
is, or which market you are in. Edit it to match your policy. It is a starting point, not legal
advice, and shipping it unchanged does not make you compliant on its own.

### The share token is in your build

`shareToken` lives in a ScriptableObject inside your game, so anyone who unpacks the build can read
it. That is inherent — the SDK has to authenticate somehow, and there is no secret a client can keep.
The token only grants what a legitimate player has: starting sessions and submitting data to your
test. It cannot read results, reach other tests, or touch your account. Close tests when a playtest
is over, and rotate the token if you publish a build widely.

### Copy-paste text for your privacy policy

Adapt this and paste it into your own policy. Replace the bracketed parts.

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
- [ ] Audit your `Debug.LogError` messages if crash reporting is on.
- [ ] Do not log personal data through `LogEvent` values.
- [ ] If children play your game, check the age of digital consent in your markets — it ranges from
      13 to 16 across the EU.

## 12. Reliability and troubleshooting

The SDK never throws into gameplay. Failures log a `[PlayProbe]` warning — check the console first.

| Symptom | Cause |
| --- | --- |
| **Nothing happens at all** | No share token, or `requireConsent` is on and waiting. The console says which. |
| **"requires a Pro plan"** | The account owning the test is on Free. Upgrade, then press **Check Again** in the setup window. |
| **`requireConsent` is on but no prompt appears** | `useBuiltInConsentDialog` is off (so you are expected to show your own), the player already declined, or the prefab was never generated. |
| **Session does not start** | Open the setup window and read the token banner — it separates a bad token from SDK mode being off, a closed test, and a Free account. |
| **Survey does not show** | The trigger key must match a `Register(...)` made *before* `StartSession()`. |
| **Survey or feedback popup does nothing** | The prefabs were never generated. Run `Tools > PlayProbe > UI > Create Missing Prefabs`. |
| **UI appears but clicks do nothing** | Something else in the scene is covering it, or an input module is missing. The SDK creates an EventSystem when there is none. |
| **Position heatmap is empty** | Set `enablePositionHeatmap` and call `SetTrackedTransform` / `RegisterTrackedObject` with non-null transforms. |
| **Custom events missing** | `LogEvent` only records while a session is active. |
| **Events stop arriving mid-session** | Uploads are failing. Earlier console warnings carry the status code; after three failures a batch is dropped. |
| **"Privacy policy" link is missing** | `privacyPolicyUrl` is blank. The link hides rather than dead-ending. |

## 13. API reference

### `PlayProbeManager`

| Member | |
| --- | --- |
| `static PlayProbeManager Instance` | The singleton. Null before its `Awake`. |
| `bool IsSessionActive` | |
| `void StartSession()` | Standalone or handoff, per `isStandaloneTest`. |
| `void EndSession()` | Also happens automatically on quit. |
| `void ShowSurvey(string triggerKey)` | |
| `void OpenFeedback()` | |
| `void SubmitFeedback(title, description, category, attachScreenshot, tagIds)` | Bypasses the popup. |
| `void SetConsent(bool granted)` | |
| `void ResetConsent()` | Forgets the decision so the player is asked again. |
| `bool IsCollectionAllowed` | |
| `string PrivacyPolicyUrl` | Your policy URL from the config, or null. |
| `IReadOnlyList<AnswerTag> AnswerTags` | Delivered at session start. |
| `PlayProbeSurvey Survey` · `PlayProbeAnalytics Analytics` · `PlayProbeEvents Events` · `PlayProbeFeedback Feedback` · `PlayProbeConsent Consent` | Subsystems. `Feedback` is null when Instant Feedback is off. |

### `PlayProbeSurvey` and `SurveyBuilder`

`Register(triggerKey)` returns a builder: `AddRating`, `AddEmojiScale`, `AddYesNo`,
`AddMultipleChoice`, `AddText`. All take `(label, sdkQuestionId, ... , required)`.

### `PlayProbeEvents`

`LogEvent(name)`, `LogEvent(name, float)`, `LogEvent(name, string)`,
`LogPosition(Vector3, name, tag = null)`.

### `PlayProbeAnalytics`

`SetTrackedTransform(Transform)`, `RegisterTrackedObject(tag, Transform)`, `AverageFps`, `MinFps`,
`HasFpsSamples`.

### `PlayProbeFeedback`

`Open()`, `Submit(title, description, category, attachScreenshot, tagIds)`, `Cancel()`, `IsOpen`,
`PendingScreenshot`, `AllowScreenshot`, `ScreenshotDefaultOn`, `PrivacyNotice`, `PrivacyPolicyUrl`,
`static Categories`, `MaxTitleLength`, `MaxDescriptionLength`.

### `PlayProbeConsent`

`Status` (`Unknown` / `Granted` / `Denied`), `HasAnswered`, `Set(bool)`, `Clear()`,
`event Action<PlayProbeConsentStatus> Changed`.

### UI

`PlayProbeUiTheme` (`Default`, `InvalidateCache()`), `PlayProbeFlowLayoutGroup` (`Spacing`),
`PlayProbeCapsuleImage` (`Apply()`), `PlayProbeConsentDialog.Show(callback)`,
`PlayProbeToast.Show(message, isError)`, `PlayProbeTagSelector` (`Build()`, `SelectedTagIds`,
`ClearSelection()`), `PlayProbeLinkButton` (`Target`, `ResolvedUrl`, `Open()`),
`IPlayProbeQuestionElement` for custom question types.

### Backend endpoints

`sdk-verify-token` (editor only), `sdk-check-function`, `sdk-start-session`, `sdk-session-end`,
`sdk-events`, `sdk-mid-survey`, `sdk-feedback` — all under `https://api.playprobe.io/`, all HTTPS,
all snake_case payloads.
