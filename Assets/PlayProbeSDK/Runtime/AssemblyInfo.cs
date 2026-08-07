// Copyright PlayProbe.io 2026. All rights reserved

using System.Runtime.CompilerServices;

// The editor tooling builds the UI prefabs and therefore needs the internal canvas sort orders and
// helpers in PlayProbeUi. Those stay internal deliberately: they are implementation detail of the
// SDK's own screens, not something a game should be wiring against.
[assembly: InternalsVisibleTo("PlayProbeEditor")]
