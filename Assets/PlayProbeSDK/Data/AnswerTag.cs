// Copyright PlayProbe.io 2026. All rights reserved

using System;

namespace PlayProbe.Data
{
    // One entry of the global answer-tag vocabulary, delivered by sdk-start-session. Testers pick
    // from these on open-ended survey answers and Instant Feedback; the ids travel back as tag_ids.
    [Serializable]
    public class AnswerTag
    {
        public string id;
        public string slug;
        public string label;
        public int sort_order;
    }
}
