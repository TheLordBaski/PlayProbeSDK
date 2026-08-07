// Copyright PlayProbe.io 2026. All rights reserved

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PlayProbe
{
    /// <summary>
    /// Lays children out left to right, wrapping onto a new line when the next one will not fit —
    /// each keeping its own preferred width.
    ///
    /// uGUI ships nothing that does this. <c>HorizontalLayoutGroup</c> keeps everything on one line,
    /// and <c>GridLayoutGroup</c> wraps but forces every cell to an identical size — which is why a
    /// row of tag chips came out with "Tag" as wide as "Progression / Pacing". This is the flow
    /// layout in between: wrapping, with cells sized to their content.
    ///
    /// A child's width comes from its own layout, so a chip sizes to its label if it carries a
    /// <c>HorizontalLayoutGroup</c> with padding around the text. Set an explicit width with a
    /// <c>LayoutElement</c> when you want to override that.
    ///
    /// The group reports the height it needs, so a <c>ContentSizeFitter</c> or an enclosing vertical
    /// layout grows to fit however many lines the content ends up on.
    /// </summary>
    [AddComponentMenu("Layout/PlayProbe Flow Layout Group")]
    public class PlayProbeFlowLayoutGroup : LayoutGroup
    {
        [Tooltip("Gap between children on a line (x) and between lines (y).")]
        [SerializeField] private Vector2 spacing = new Vector2(8f, 8f);

        [Tooltip("Give every child on a line the height of the tallest one. Off, each keeps its own.")]
        [SerializeField] private bool uniformLineHeight = true;

        // One entry per wrapped line: how many children it holds, and its measured extents.
        private readonly List<int> _lineCounts = new();
        private readonly List<float> _lineWidths = new();
        private readonly List<float> _lineHeights = new();

        /// <summary>Gap between children on a line (x) and between lines (y).</summary>
        public Vector2 Spacing
        {
            get => spacing;
            set
            {
                spacing = value;
                SetDirty();
            }
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();

            float widest = 0f;
            float sum = 0f;

            for (int i = 0; i < rectChildren.Count; i++)
            {
                float width = LayoutUtility.GetPreferredWidth(rectChildren[i]);
                widest = Mathf.Max(widest, width);
                sum += width + (i > 0 ? spacing.x : 0f);
            }

            // Minimum is the widest single child — below that a child would have to be squashed.
            // Preferred is everything on one line, which is what the group takes if it is offered.
            SetLayoutInputForAxis(widest + padding.horizontal, sum + padding.horizontal, -1, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            // The width is final by now: the parent assigned it during SetLayoutHorizontal, which runs
            // before every CalculateLayoutInputVertical in the rebuild.
            float height = Measure();
            SetLayoutInputForAxis(height, height, -1, 1);
        }

        public override void SetLayoutHorizontal()
        {
            Place(applyHorizontal: true, applyVertical: false);
        }

        public override void SetLayoutVertical()
        {
            Place(applyHorizontal: false, applyVertical: true);
        }

        /// <summary>Splits the children into lines and returns the total height they need.</summary>
        private float Measure()
        {
            _lineCounts.Clear();
            _lineWidths.Clear();
            _lineHeights.Clear();

            float available = rectTransform.rect.width - padding.horizontal;
            float lineWidth = 0f;
            float lineHeight = 0f;
            int lineCount = 0;

            foreach (RectTransform child in rectChildren)
            {
                float width = LayoutUtility.GetPreferredWidth(child);
                float height = LayoutUtility.GetPreferredHeight(child);
                float advance = lineCount > 0 ? width + spacing.x : width;

                // Wrap — but never onto an empty line, or a child wider than the whole group would
                // loop forever putting itself on a line of its own that still does not fit.
                if (lineCount > 0 && lineWidth + advance > available)
                {
                    _lineCounts.Add(lineCount);
                    _lineWidths.Add(lineWidth);
                    _lineHeights.Add(lineHeight);

                    lineWidth = width;
                    lineHeight = height;
                    lineCount = 1;
                    continue;
                }

                lineWidth += advance;
                lineHeight = Mathf.Max(lineHeight, height);
                lineCount++;
            }

            if (lineCount > 0)
            {
                _lineCounts.Add(lineCount);
                _lineWidths.Add(lineWidth);
                _lineHeights.Add(lineHeight);
            }

            float total = padding.vertical;
            for (int i = 0; i < _lineHeights.Count; i++)
            {
                total += _lineHeights[i] + (i > 0 ? spacing.y : 0f);
            }

            return total;
        }

        private void Place(bool applyHorizontal, bool applyVertical)
        {
            Measure();

            float available = rectTransform.rect.width - padding.horizontal;
            float y = padding.top;
            int index = 0;

            for (int line = 0; line < _lineCounts.Count; line++)
            {
                if (line > 0)
                {
                    y += spacing.y;
                }

                // childAlignment decides what happens to the slack at the end of a line.
                float slack = Mathf.Max(0f, available - _lineWidths[line]);
                float x = padding.left + slack * GetAlignmentOnAxis(0);
                float height = _lineHeights[line];

                for (int i = 0; i < _lineCounts[line]; i++, index++)
                {
                    RectTransform child = rectChildren[index];
                    float width = LayoutUtility.GetPreferredWidth(child);
                    float childHeight = uniformLineHeight ? height : LayoutUtility.GetPreferredHeight(child);
                    float offset = uniformLineHeight ? 0f : (height - childHeight) * GetAlignmentOnAxis(1);

                    if (applyHorizontal)
                    {
                        SetChildAlongAxis(child, 0, x, width);
                    }

                    if (applyVertical)
                    {
                        SetChildAlongAxis(child, 1, y + offset, childHeight);
                    }

                    x += width + spacing.x;
                }

                y += height;
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            spacing.x = Mathf.Max(0f, spacing.x);
            spacing.y = Mathf.Max(0f, spacing.y);
        }
#endif
    }
}
