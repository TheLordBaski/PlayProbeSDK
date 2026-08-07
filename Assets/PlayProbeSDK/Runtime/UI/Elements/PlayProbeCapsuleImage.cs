// Copyright PlayProbe.io 2026. All rights reserved

using UnityEngine;
using UnityEngine.UI;

namespace PlayProbe
{
    /// <summary>
    /// Keeps a 9-sliced Image reading as a true capsule — fully round ends — at whatever height its
    /// layout gives it.
    ///
    /// A sliced sprite draws its corner regions at a fixed size derived from
    /// <c>pixelsPerUnitMultiplier</c>. For a capsule the corner has to be exactly half the element's
    /// height, or it is not a semicircle. Pick one multiplier at build time and it can only ever be
    /// correct at one height: a tag chip and the feedback button are different heights, so one of them
    /// is always wrong.
    ///
    /// Worse, it does not fail gracefully. When the top and bottom borders together exceed the rect,
    /// Unity scales them down proportionally to fit — so the corner quad keeps its full width but
    /// loses height, and the round end flattens into an ellipse. A 38px chip built for a 52px corner
    /// comes out at roughly 2.7:1.
    ///
    /// So the multiplier is computed here instead, from the height the element actually has, and
    /// recomputed whenever that changes.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("PlayProbe/Capsule Image")]
    public class PlayProbeCapsuleImage : MonoBehaviour
    {
        private Image _image;
        private RectTransform _rect;
        private float _appliedForHeight = -1f;

        private void OnEnable()
        {
            Cache();
            Apply();
        }

        // Fires whenever a layout group, anchor change or resolution change resizes this element.
        private void OnRectTransformDimensionsChange()
        {
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Cache();
            _appliedForHeight = -1f;
            Apply();
        }
#endif

        private void Cache()
        {
            if (_image == null)
            {
                _image = GetComponent<Image>();
            }

            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }
        }

        /// <summary>
        /// Recomputes the multiplier from the current height. Called automatically; call it yourself
        /// only if you resize the element without going through the layout system.
        /// </summary>
        public void Apply()
        {
            Cache();

            if (_image == null || _rect == null || _image.sprite == null)
            {
                return;
            }

            // Uniform borders, so one axis is enough. No border means the sprite is not sliced and
            // there is nothing to correct.
            float spriteBorder = _image.sprite.border.x;
            if (spriteBorder <= 0f)
            {
                return;
            }

            float height = _rect.rect.height;
            if (height <= 1f || Mathf.Approximately(height, _appliedForHeight))
            {
                return;
            }

            _appliedForHeight = height;

            // Image.pixelsPerUnit is the sprite-to-canvas ratio *without* the multiplier, which is
            // exactly the conversion needed here. Reading it rather than assuming 100/100 keeps this
            // correct for a sprite imported at a different Pixels Per Unit, or a canvas with a
            // non-default reference.
            float ratio = _image.pixelsPerUnit;
            if (ratio <= 0f)
            {
                return;
            }

            // Corner radius == half the height is what makes the ends semicircles.
            float targetRadius = height * 0.5f;
            _image.pixelsPerUnitMultiplier = Mathf.Max(0.01f, spriteBorder / (ratio * targetRadius));
        }
    }
}
