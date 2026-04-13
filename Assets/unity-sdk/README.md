# PlayProbe SDK (UPM)

PlayProbe SDK provides session lifecycle, event logging, analytics sampling, and in-game surveys for Unity games.

## Installation

Add the package to your project through UPM:

- Local path: point to the `unity-sdk` folder
- Git URL: `https://<your-repo-url>.git?path=/unity-sdk`

## Quick Start

1. Create a config asset from `Assets > Create > PlayProbe > Configuration`.
2. Place the asset at `Assets/Resources/PlayProbeConfig.asset`.
3. Add `PlayProbeManager` to a bootstrap GameObject in your first scene.
4. After your backend issues a session id, call:

```csharp
PlayProbeManager.Instance.Initialize(sessionId);
```

## Runtime Components

- `PlayProbeManager`: singleton coordinator
- `PlayProbeSession`: start/end session lifecycle
- `PlayProbeEvents`: buffered event queue + flush
- `PlayProbeAnalytics`: FPS and position sampling
- `PlayProbeSurvey`: survey fetch + submit flow
- `PlayProbeHttp`: Supabase REST wrapper using UnityWebRequest + TaskCompletionSource

## Editor

Use `Tools > PlayProbe > Setup` to create or edit `PlayProbeConfig` quickly.
