// Copyright PlayProbe.io 2026. All rights reserved

using System.Collections.Generic;
using PlayProbe.Data;
using TMPro;
using UnityEngine;

namespace PlayProbe
{
    /// <summary>
    /// Renders the global answer-tag vocabulary as a row of toggleable pills and reports back which
    /// ones the player picked. Used by the Instant Feedback popup and by open-ended survey questions
    /// so results can be grouped by theme.
    ///
    /// The vocabulary is delivered with the start-session response and read from
    /// <see cref="PlayProbeManager.AnswerTags"/>. When the test has no tags configured, the whole
    /// component hides itself — nothing to choose from.
    ///
    /// <code>
    /// // In your own survey UI:
    /// tagSelector.Build();
    /// // ... player answers ...
    /// response.tag_ids = tagSelector.SelectedTagIds;
    /// </code>
    /// </summary>
    public class PlayProbeTagSelector : MonoBehaviour
    {
        [Tooltip("Optional heading shown above the pills. Hidden along with the rest when there are no tags.")]
        [SerializeField] private TextMeshProUGUI heading;

        [Tooltip("The pills are spawned as children of this transform. Give it a flexible layout group.")]
        [SerializeField] private RectTransform chipContainer;

        [Tooltip("Prefab spawned once per tag.")]
        [SerializeField] private PlayProbeTagChip chipPrefab;

        [Tooltip("Most tags the player may pick at once. 0 means no limit.")]
        [SerializeField] private int maxSelection = 3;

        [Tooltip("Build the pill list automatically on Start. Turn off to call Build() yourself.")]
        [SerializeField] private bool buildOnStart = true;

        private readonly List<PlayProbeTagChip> _chips = new();
        private readonly List<PlayProbeTagChip> _selected = new();

        /// <summary>
        /// The ids the player picked, in the order they picked them. Empty (never null) when nothing is
        /// selected — pass it straight to <c>SurveyResponse.tag_ids</c> or
        /// <see cref="PlayProbeManager.SubmitFeedback"/>.
        /// </summary>
        public string[] SelectedTagIds
        {
            get
            {
                if (_selected.Count == 0)
                {
                    return System.Array.Empty<string>();
                }

                string[] ids = new string[_selected.Count];
                for (int i = 0; i < _selected.Count; i++)
                {
                    ids[i] = _selected[i].Tag != null ? _selected[i].Tag.id : null;
                }

                return ids;
            }
        }

        /// <summary>How many tags are currently selected.</summary>
        public int SelectedCount => _selected.Count;

        /// <summary>Sets the heading shown above the pills.</summary>
        public void SetHeading(string text)
        {
            if (heading != null)
            {
                heading.SetText(text ?? string.Empty);
            }
        }

        private void Start()
        {
            if (buildOnStart)
            {
                Build();
            }
        }

        /// <summary>
        /// Clears and rebuilds the pill list from <see cref="PlayProbeManager.AnswerTags"/>. Safe to
        /// call more than once. Hides the component when the vocabulary is empty.
        /// </summary>
        public void Build()
        {
            Clear();

            IReadOnlyList<AnswerTag> tags = PlayProbeManager.Instance != null
                ? PlayProbeManager.Instance.AnswerTags
                : null;

            if (tags == null || tags.Count == 0 || chipContainer == null || chipPrefab == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            // The backend orders by sort_order already, but a defensive sort keeps the UI stable if a
            // future response ever arrives unordered.
            List<AnswerTag> ordered = new List<AnswerTag>(tags);
            ordered.Sort((a, b) => a.sort_order.CompareTo(b.sort_order));

            foreach (AnswerTag tag in ordered)
            {
                if (tag == null || string.IsNullOrWhiteSpace(tag.id))
                {
                    continue;
                }

                PlayProbeTagChip chip = Instantiate(chipPrefab, chipContainer);
                chip.Bind(tag, this);
                _chips.Add(chip);
            }

            if (_chips.Count == 0)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>Removes every pill and forgets the selection.</summary>
        public void Clear()
        {
            foreach (PlayProbeTagChip chip in _chips)
            {
                if (chip != null)
                {
                    Destroy(chip.gameObject);
                }
            }

            _chips.Clear();
            _selected.Clear();
        }

        /// <summary>
        /// Flips a chip on or off, respecting the selection limit. Called by the chips themselves; you
        /// only need it if you are driving the selector from code.
        /// </summary>
        public void Toggle(PlayProbeTagChip chip)
        {
            if (chip == null)
            {
                return;
            }

            SetSelected(chip, !chip.IsSelected);
        }

        /// <summary>
        /// Selects or deselects a chip. Selecting past <c>maxSelection</c> is ignored rather than
        /// silently dropping an earlier pick.
        /// </summary>
        public void SetSelected(PlayProbeTagChip chip, bool selected)
        {
            if (chip == null)
            {
                return;
            }

            if (selected)
            {
                if (_selected.Contains(chip))
                {
                    return;
                }

                if (maxSelection > 0 && _selected.Count >= maxSelection)
                {
                    return;
                }

                _selected.Add(chip);
            }
            else
            {
                _selected.Remove(chip);
            }

            chip.SetSelected(selected);
            ApplyLimitState();
        }

        /// <summary>Deselects everything without rebuilding the pills.</summary>
        public void ClearSelection()
        {
            foreach (PlayProbeTagChip chip in _selected)
            {
                if (chip != null)
                {
                    chip.SetSelected(false);
                }
            }

            _selected.Clear();
            ApplyLimitState();
        }

        // Once the player is at the limit, the remaining chips are dimmed so it is obvious why they
        // stopped responding. Already-selected chips stay live so the choice can be swapped.
        private void ApplyLimitState()
        {
            bool atLimit = maxSelection > 0 && _selected.Count >= maxSelection;

            foreach (PlayProbeTagChip chip in _chips)
            {
                if (chip == null)
                {
                    continue;
                }

                chip.SetInteractable(!atLimit || chip.IsSelected);
            }
        }
    }
}
