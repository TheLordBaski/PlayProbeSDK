// Copyright PlayProbe.io 2026. All rights reserved

using System;

namespace PlayProbe.Data
{
    [Serializable]
    internal class PlayProbeVec3
    {
        public float x;
        public float y;
        public float z;
        public float ry; // optional facing (yaw), degrees
    }
}
