# AGENTS.md

## Project snapshot
- Unity SDK package lives under `Assets/unity-sdk` (`package.json` name: `io.playprobe.sdk`, version `0.1.0`).
- Runtime assembly: `Assets/unity-sdk/Runtime/PlayProbeSDK.asmdef`; data DTO assembly: `Assets/unity-sdk/Data/PlayProbeData.asmdef`; editor tooling: `Assets/unity-sdk/Editor/PlayProbeEditor.asmdef`.
- `PlayProbeManager` is the single runtime path. The old `PlayProbeManagerOld`/`PlayProbeSurveyOld`/`PlayProbeHttpOld` classes have been removed; there is no legacy flow left in `Assets`.

## Architecture and boundaries
- `PlayProbeManager` (`Assets/unity-sdk/Runtime/PlayProbeManager.cs`) is the runtime entrypoint singleton (`DontDestroyOnLoad`).
- App registers surveys via `PlayProbeManager.Instance.Survey.Register(...).Add...()` then starts a session via `StartSession()`.
- `PlayProbeManager` owns the subsystems it creates in `Awake`: `Survey` (`PlayProbeSurvey`), `Analytics` (`PlayProbeAnalytics`), and `Events` (`PlayProbeEvents`). All are wired to `PlayProbeManager.Instance` — there is no separate session orchestrator class.
- Custom gameplay events: `PlayProbeManager.Instance.Events.LogEvent(name)` / `LogEvent(name, float)` / `LogEvent(name, string)` (server `event_type` "custom"). `Events` is public; calls are no-ops with a warning when no session is active.
- Consent: `PlayProbeConsent` (`Runtime/PlayProbeConsent.cs`) holds the player's decision in `PlayerPrefs` (`playprobe_consent`). Enforced only when `PlayProbeConfig.requireConsent` is on, via `PlayProbeManager.IsCollectionAllowed`. `StartSession()` defers instead of calling the network when consent is missing, and re-runs itself once `SetConsent(true)` fires; `SetConsent(false)` stops tracking and calls `Events.DiscardBufferedEvents()` without a session-end request. **Anything new that sends data must check `IsCollectionAllowed`.**
- Data contracts are plain serializable classes in `Assets/unity-sdk/Data` (`PlayProbeSdkSessionStartRequest`, `SurveySchemaItem`, etc.).

## Runtime data flow (current manager)
- Config is serialized on the manager (`PlayProbeConfig` field), then copied into `PlayProbeRuntimeConfig` in `BuildRuntimeConfig()`.
- Session start:
  - standalone: POST `https://api.playprobe.io/sdk-start-session`
  - handoff: load `Resources/PlayProbeStartSessionScreen` and validate token via `sdk-check-function`, then POST `sdk-start-session`.
- Session end: POST `https://api.playprobe.io/sdk-session-end` with `session_id`, `session_token`, real `duration_seconds`, and `avg_fps`/`min_fps` from `PlayProbeAnalytics`.
- Mid-game surveys are fully wired: `ShowSurvey(triggerKey)` spawns `PlayProbeSurveyCanvas`, which collects answers and submits to `sdk-mid-survey` via `SubmitSurveyResponses`. Question elements emit the backend question UUID (`SurveyQuestionSchema.id` from the start-session `survey_triggers` response).

## Integration points
- External backend is `https://api.playprobe.io/` (see `PlayProbeRuntimeConfig.ApiEndpoint`).
- All backend calls use edge-function routes: `sdk-check-function`, `sdk-start-session`, `sdk-session-end`, `sdk-events`, `sdk-mid-survey`.
- UI resources expected by name (via `Resources.Load(...)`): `PlayProbeStartSessionScreen`, `PlayProbeSurveyCanvas`, and the per-question prefabs (`PlayProbeRatingQuestion`, `PlayProbeYesNoQuestion`, `PlayProbeMultipleOptions`, `PlayProbeTextQuestion`, `PlayProbeEmojiQuestion`).

## Developer workflow (repo-specific)
- Create/edit SDK config via Unity menu `Tools > PlayProbe > Setup` (`PlayProbeSetupWindow`).
- Config asset path expectation is `Assets/Resources/PlayProbeConfig.asset`.
- Sample integration entrypoint is `Assets/TutorialInfo/SdkTestController.cs`.
- No dedicated SDK test suite is present in `Assets`; validation is currently scene/play-mode driven.
- Treat `*.csproj` and `*.sln` as Unity-generated artifacts; prefer editing source + asmdefs, then let Unity regenerate project files.

## Conventions and gotchas for edits
- Keep payload field names snake_case to match backend contracts (`share_token`, `session_id`, `trigger_key`, etc.).
- Preserve `[PlayProbe]` log prefix; existing diagnostics depend on it.
- Null/empty checks are defensive and often non-throwing; follow warning-based failure style for runtime safety.
- When changing survey/runtime behavior, keep the editor buttons in `SdkTestController` working (they call `ShowSurvey`).
- `documentation.md` has been rewritten to match the current runtime; still verify behavior against source before adding features.

