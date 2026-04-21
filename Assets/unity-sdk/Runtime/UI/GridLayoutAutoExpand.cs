// Copyright PlayProbe.io 2026. All rights reserved

using UnityEngine;
using UnityEngine.UI;

namespace PlayProbe
{
    public class GridLayoutAutoExpand : MonoBehaviour
    {
        [SerializeField] private GridLayoutGroup gridLayout;
        [SerializeField] private RectTransform rectTransform;

        private const int ColumnCount = 2;
        private bool _isRecalculateQueued;

        private void OnEnable()
        {
            QueueRecalculate();
        }

        private void Start()
        {
            QueueRecalculate();
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= HandleWillRenderCanvases;
            _isRecalculateQueued = false;
        }

        private void OnRectTransformDimensionsChange()
        {
            QueueRecalculate();
        }

        private void OnTransformParentChanged()
        {
            QueueRecalculate();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            QueueRecalculate();
        }
#endif

        private void QueueRecalculate()
        {
            return;
            if (!isActiveAndEnabled || _isRecalculateQueued)
            {
                return;
            }

            _isRecalculateQueued = true;
            // Recalculate after the layout system has applied parent-driven sizing.
            Canvas.willRenderCanvases += HandleWillRenderCanvases;
        }

        private void HandleWillRenderCanvases()
        {
            return;
            Canvas.willRenderCanvases -= HandleWillRenderCanvases;
            _isRecalculateQueued = false;
            RecalculateGridLayout();
        }

        public void RecalculateGridLayout()
        {
            return;
            if (gridLayout == null)
            {
                gridLayout = GetComponent<GridLayoutGroup>();
            }

            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }

            if (gridLayout == null || rectTransform == null)
            {
                return;
            }

            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = ColumnCount;

            float width = rectTransform.rect.width;
            float totalHorizontalPadding = gridLayout.padding.left + gridLayout.padding.right;
            float totalHorizontalSpacing = gridLayout.spacing.x * (ColumnCount - 1);
            float availableWidth = width - totalHorizontalPadding - totalHorizontalSpacing;
            float childWidth = Mathf.Max(0f, availableWidth / ColumnCount);

            Vector2 cellSize = gridLayout.cellSize;
            if (Mathf.Approximately(cellSize.x, childWidth))
            {
                return;
            }

            cellSize.x = childWidth; // Only expand in X; Y remains author-defined.
            gridLayout.cellSize = cellSize;
        }

#if UNITY_EDITOR

        [UnityEditor.CustomEditor(typeof(GridLayoutAutoExpand))]
        public class GridLayoutAutoExpandEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();
                if (GUILayout.Button("Recalculate GridLayout"))
                {
                    (target as GridLayoutAutoExpand)?.RecalculateGridLayout();
                }
            }
        }


#endif
    }
}