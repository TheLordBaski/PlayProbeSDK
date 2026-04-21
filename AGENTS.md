# AGENTS.md

## Project snapshot
- Unity SDK package lives under `Assets/unity-sdk` (`package.json` name: `io.playprobe.sdk`, version `0.1.0`).
- Runtime assembly: `Assets/unity-sdk/Runtime/PlayProbeSDK.asmdef`; data DTO assembly: `Assets/unity-sdk/Data/PlayProbeData.asmdef`; editor tooling: `Assets/unity-sdk/Editor/PlayProbeEditor.asmdef`.
- Most active path is the new manager (`PlayProbeManager`), but large legacy flow still exists (`PlayProbeManagerOld`, `PlayProbeSurveyOld`, `PlayProbeHttpOld`) and is still referenced by editor tooling.

## Architecture and boundaries
- `PlayProbeManager` (`Assets/unity-sdk/Runtime/PlayProbeManager.cs`) is the runtime entrypoint singleton (`DontDestroyOnLoad`).
- New flow: app registers surveys via `PlayProbeManager.Instance.Survey.Register(...).Add...()` then starts session via `StartSession()`.
- Old flow (`PlayProbeManagerOld`) still owns analytics/events/session orchestration and contains richer behavior (flush loop, crash hook, trigger mapping).
- `PlayProbeEvents`, `PlayProbeAnalytics`, `PlayProbeSession` are currently coupled to `PlayProbeManagerOld.Instance`; do not assume they are wired into `PlayProbeManager` yet.
- Data contracts are plain serializable classes in `Assets/unity-sdk/Data` (`PlayProbeSdkSessionStartRequest`, `SurveySchemaItem`, etc.).

## Runtime data flow (current new manager)
- Config is serialized on the manager (`PlayProbeConfig` field), then copied into `PlayProbeRuntimeConfig` in `BuildRuntimeConfig()`.
- Session start:
  - standalone: POST `https://api.playprobe.io/sdk-start-session`
  - handoff: load `Resources/PlayProbeStartSessionScreen` and validate token via `sdk-check-function`, then POST `sdk-start-session`.
- Session end: POST `https://api.playprobe.io/sdk-session-end` with `session_id`, placeholder duration/FPS, and `Survey.GetSurveyResponses()` (currently returns empty list).
- Survey registration is schema-only in new flow (`PlayProbeSurvey`), not full trigger preload/display/submit like old flow.

## Integration points
- External backend is `https://api.playprobe.io/` (see `PlayProbeRuntimeConfig.ApiEndpoint` and `PlayProbeConfig.ApiEndpoint`).
- New manager uses edge-function style routes (`sdk-check-function`, `sdk-start-session`, `sdk-session-end`).
- Old/session code uses Supabase REST-like routes (`/rest/v1/tests`, `/rest/v1/sessions`, `/rest/v1/sdk_sessions`, `/rest/v1/sdk_events`).
- UI resources expected by name: `PlayProbeStartSessionScreen`, `PlayProbeSurveyOverlay`/`SurveyOverlay` (see `Resources.Load(...)` calls).

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
- When changing survey/runtime behavior, check both new and old paths to avoid breaking editor buttons and legacy integrations.
- `documentation.md` describes a broader API than current new-manager implementation; verify behavior against runtime code before adding features.

