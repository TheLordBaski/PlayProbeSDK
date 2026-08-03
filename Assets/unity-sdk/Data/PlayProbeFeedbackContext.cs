// Copyright PlayProbe.io 2026. All rights reserved

using System;

namespace PlayProbe.Data
{
    // Runtime performance/quality snapshot captured at feedback time.
    [Serializable]
    public class PlayProbeFeedbackContext
    {
        public float avg_fps;
        public float memory_mb;        // managed heap (GC), MB
        public int quality_level;
        public string quality_name;
        public int target_frame_rate;
        public int vsync;
    }
}
