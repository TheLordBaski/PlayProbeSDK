// Copyright PlayProbe.io 2026. All rights reserved

using System;
using System.Collections.Generic;

namespace PlayProbe.Data
{
    [Serializable]
    internal class PlayProbeDictionaryPayload
    {
        public List<PlayProbeDictionaryEntry> entries = new List<PlayProbeDictionaryEntry>();
    }
}