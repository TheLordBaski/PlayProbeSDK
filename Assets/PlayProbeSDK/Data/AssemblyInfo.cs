// Copyright PlayProbe.io 2026. All rights reserved

using System.Runtime.CompilerServices;

// Most types here are the wire format of the sdk-* edge functions, not API. They are internal so a
// game cannot build against a payload shape that changes whenever the backend does; the runtime
// assembly is the only thing that needs to see them.
[assembly: InternalsVisibleTo("PlayProbeSDK")]
