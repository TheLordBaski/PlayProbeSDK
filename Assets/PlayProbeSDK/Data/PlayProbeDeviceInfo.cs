// Copyright PlayProbe.io 2026. All rights reserved

using System;

namespace PlayProbe.Data
{
    // Device profile from UnityEngine.SystemInfo. Captured once per session.
    [Serializable]
    internal class PlayProbeDeviceInfo
    {
        public string cpu;
        public int cpu_cores;
        public int cpu_mhz;
        public string gpu;
        public int gpu_mem_mb;
        public int ram_mb;
        public string os;
        public string device_model;
        public string device_type;
    }
}
