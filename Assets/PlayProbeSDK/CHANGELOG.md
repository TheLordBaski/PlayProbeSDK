# Changelog

All notable changes to the PlayProbe Unity SDK. This project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

The first release-ready pass: a complete built-in UI layer, a tightened public API, and fixes for
several bugs that only showed up over a long session.

### Added

- **A generated UI layer.** Every screen the SDK needs now ships as a prefab: the Instant Feedback
  popup, the floating feedback button, the survey overlay, the handoff-code screen, an optional
  consent dialog, and a confirmation toast.
- **`PlayProbeUiTheme`** — one ScriptableObject holding every colour, size and string the SDK's
  screens use. Edit it and rebuild to restyle or translate the whole UI.
- **`Tools > PlayProbe > UI`** — menu items that generate the prefabs from the theme.
  `Create Missing Prefabs` never overwrites; `Rebuild All Prefabs` does, after confirming.
- **`PlayProbeTagSelector` / `PlayProbeTagChip`** — the answer-tag chooser, rendered from the
  vocabulary delivered at session start. Wired into the feedback popup and open-ended survey
  questions, so `tag_ids` now reaches the backend from the built-in UI.
- **`PlayProbeConsentDialog`** — an optional prebuilt consent prompt. The SDK never shows it by
  itself; you call `PlayProbeConsentDialog.Show()` when you decide it is the right moment.
- **`PlayProbeLinkButton`** — a privacy-policy / terms link that hides itself when no URL is set.
- **`PlayProbeToast`** — the confirmation shown after a survey or feedback submission.
- **`PlayProbeManager.PrivacyPolicyUrl`**, **`PlayProbeFeedback.IsOpen`**,
  **`PlayProbeFeedback.Categories`**, **`PlayProbeFeedback.MaxTitleLength`**,
  **`PlayProbeFeedback.MaxDescriptionLength`**.
- The setup window now covers Instant Feedback and the UI theme, warns when the share token is
  empty or the feedback prefab is missing, and assigns the config to the manager it creates.
- **Automatic share token verification in the setup window.** The SDK is a Pro feature and
  `sdk-start-session` refuses sessions for Free accounts, which used to surface only as a console
  warning in a shipped build. Paste a token and the window checks it against the backend by itself,
  reporting which gate is unmet — plan, SDK mode, or the test being open — with a button to the
  upgrade page when it is the plan. Incomplete or malformed values are answered locally ("too
  short — 24 of 36 characters") rather than by a pointless round trip, and **Check Again** re-runs
  it after a dashboard change that the token itself would not reflect.
- **HTML documentation** in `Documentation~/playprobe-unity-sdk.html`: self-contained, dark on
  screen and light when printed, so one file serves as the online manual and the PDF in the package.
- **`PlayProbeFlowLayoutGroup`** — a wrapping layout that lets each child keep its own width, which
  uGUI has no equivalent of. Tag chips now fit their labels instead of every one being stretched to a
  uniform `GridLayoutGroup` cell, which had "Tag" as wide as "Progression / Pacing".
- **`PlayProbeCapsuleImage`** — keeps a 9-sliced pill genuinely round at whatever height its layout
  gives it. A sliced sprite draws its corners at a fixed size, so a capsule can only be correct at
  one height; and when the top and bottom borders together exceed the element, Unity scales them
  down to fit, flattening the round end into an ellipse (a 38px tag chip built for a 52px corner came
  out at 2.7:1). The component derives the multiplier from the actual height instead, so the corner
  is always exactly half of it.
- **A custom sprite set.** Nine white PNGs in `Textures/UI` replace Unity's built-in `UISprite`:
  three shapes (rounded rectangle, panel, capsule) filled and outlined, plus a circle, a checkmark
  and a speech bubble. The prefab builder assigns them to the theme's empty sprite slots by filename,
  and derives `pixelsPerUnitMultiplier` from each sprite's own 9-slice border, so the rendered corner
  radius tracks the theme regardless of what resolution the PNGs were exported at. Empty slots still
  fall back to `UISprite`.

### Fixed

- **The editor tools wrote to a folder nothing loaded from.** The prefab output path, the sprite
  folder and the emoji atlas were hardcoded to `Assets/unity-sdk`, so renaming the package folder
  left the builder recreating the old path — two `Resources` folders holding prefabs of the same
  name, which makes `Resources.Load` ambiguous. All three now derive from where the package
  actually sits, so a rename or a move just works.
- **A failed start-session reported the wrong thing.** An HTTP 4xx arrives as a `ProtocolError`,
  which was handled as a transport failure, throwing away the response body — the only thing that
  distinguishes a bad token from a closed test or a Free account. The body is now read and reported,
  with a dedicated message for the Pro-plan case.
- **`requireConsent` collected nothing and showed nothing.** The built-in consent dialog existed but
  nothing ever spawned it, so turning consent on left the SDK silently inert unless the developer had
  already written their own prompt. `StartSession()` now shows it, gated on the new
  `useBuiltInConsentDialog` config switch (default on) — enabling that is the developer choosing to
  show the prompt, which keeps them the data controller. A player who already declined is not asked
  again.
- **Interactive graphics were tinted twice.** The image carried a colour *and* the Selectable's
  colour block multiplied another over it, so brand purple came out muddy and disabled states lost
  their alpha. Interactive graphics are now white and take their colour entirely from the colour
  block; only non-interactive graphics (panels, the scrim, question cards) colour their image.
- **Borders smeared on rounded corners.** They used uGUI's `Outline` effect, which redraws the
  graphic four times at an offset rather than stroking. They are now a ring Image using the outline
  sprite, which also means chips and selectable buttons can recolour their border on selection.
- **Events stopped uploading for the rest of the session** after a single payload that failed to
  serialize: the in-flight flag was set before an early return that skipped the block resetting it.
- **A failed upload never counted as a retry.** `UnityWebRequest` reports transport and HTTP errors
  through its result rather than by throwing, so the retry counter — which only advanced in the
  exception handler — never moved. An unreachable backend buffered events indefinitely. Failures now
  count, and the buffer has a hard 500-event ceiling.
- **The handoff-code screen never closed.** After a successful validation the session started but the
  screen stayed on top of the game for the rest of the playtest. Session start now reports success
  back, and the screen closes on it.
- **`pauseTimeDuringSurvey` and `allowSurveyDismiss` did nothing.** Both were read into the runtime
  config and never consulted. The survey overlay now pauses the game and shows or hides its skip
  button accordingly.
- **`enableFpsTracking` did nothing.** FPS sampling ran whether or not it was switched on.
- **Every survey question was effectively required.** Submission was blocked until all questions were
  answered, regardless of the `required` flag, and unanswered questions were submitted as blank
  responses. Only required questions block now, and skipped optional questions are left out.
- **Survey questions could appear in the wrong order.** Each question prefab was loaded in its own
  un-awaited async call, so display order followed whichever `Resources` request finished first.
- **A manager with no config asset threw a `NullReferenceException`** out of `StartSession` and into
  the game's own `Start`. It now warns and stays inactive.
- **`PlayProbeSelectableButton.Hide()` threw** on any prefab without an `Outline` or an `Image`.
- **A selectable button selected in its first frame got stuck highlighted**, because its resting
  colour was captured in `Start` — after the selection had already changed it.
- **`ShowSurvey` threw** when called before a session existed, and silently did nothing when the
  trigger key was unknown. Both now warn and explain.
- Screenshot capture failing no longer leaves the game paused with no popup to unpause it.

### Changed

- **The package no longer depends on `com.unity.inputsystem`.** The assembly definition referenced it
  outright, which stopped the SDK compiling in any project without that package installed. Keyboard
  handling reaches the new input backend by reflection instead, resolved once and cached.
- **`PlayProbeData` is auto-referenced.** `AnswerTag` lives in that assembly and appears in the
  public API, so game code using `PlayProbeManager.Instance.AnswerTags` could not compile without
  adding the reference by hand.
- **`com.unity.ugui` is now declared as a dependency** and the minimum Unity version is stated
  honestly as 6000.0 — the SDK awaits `AsyncOperation` directly, which older versions cannot do.
- **The public API surface is much smaller.** `PlayProbeHttp` and the request/response DTOs are now
  internal: they are the wire format of the edge functions, and pinning a game to them would break it
  on any backend change. `PlayProbeManager.surveySchemaItems` was a public field that let a game
  rewrite the schema after the backend had assigned question ids; it is now private.
- **Policy links are restricted to `http` and `https`.** `Application.OpenURL` will launch local
  files and custom schemes on desktop, so a mistyped or hostile config value is no longer passed
  through unchecked.
- The play-mode test harness moved out of the package to `Assets/PlayProbeSampleGame/`. It was
  shipping to every customer, in the global namespace.
- The SDK creates an `EventSystem` when the scene has none, so its UI is clickable in games that
  ship no uGUI of their own.

## [0.1.0]

- Initial internal release: sessions, events, FPS and position analytics, mid-game surveys,
  Instant Feedback, consent gating, answer tags.
