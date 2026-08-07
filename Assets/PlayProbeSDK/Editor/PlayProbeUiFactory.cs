// Copyright PlayProbe.io 2026. All rights reserved

using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PlayProbe.Editor
{
    /// <summary>
    /// The primitives <see cref="PlayProbeUiBuilder"/> composes its prefabs from: panels, labels,
    /// buttons, inputs, toggles.
    ///
    /// Everything reads its colours and sizes from a <see cref="PlayProbeUiTheme"/>, so restyling the
    /// whole SDK is a matter of editing the theme asset and rebuilding — no prefab surgery.
    /// </summary>
    internal static class PlayProbeUiFactory
    {
        /// <summary>How a button is filled.</summary>
        internal enum ButtonStyle
        {
            /// <summary>Solid brand fill. One per screen — the action you want taken.</summary>
            Primary,

            /// <summary>Outlined, transparent fill. Secondary actions like Cancel or Skip.</summary>
            Secondary,

            /// <summary>No fill or outline, accent-coloured text. Links.</summary>
            Ghost,
        }

        /// <summary>The silhouettes the SDK's UI is built from.</summary>
        internal enum Shape
        {
            /// <summary>Rounded rectangle. Buttons, inputs, option tiles, question cards.</summary>
            Button,

            /// <summary>Rounded rectangle with a softer corner. Modal panels.</summary>
            Panel,

            /// <summary>Capsule. Tag chips and the floating feedback button.</summary>
            Pill,

            /// <summary>Solid circle.</summary>
            Circle,
        }

        // Unity's built-ins, used only when the theme has no sprite for a shape. They keep a project
        // that never imported the PlayProbe sprite set rendering something reasonable — but UISprite's
        // corner is small and fixed, so the real set is worth importing.
        private const string RoundedSpritePath = "UI/Skin/UISprite.psd";
        private const string KnobSpritePath = "UI/Skin/Knob.psd";
        private const string CheckmarkSpritePath = "UI/Skin/Checkmark.psd";

        internal static Sprite RoundedSprite =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>(RoundedSpritePath);

        internal static Sprite CheckmarkSprite(PlayProbeUiTheme theme) =>
            theme.checkmark != null
                ? theme.checkmark
                : AssetDatabase.GetBuiltinExtraResource<Sprite>(CheckmarkSpritePath);

        internal static Sprite ChatBubbleSprite(PlayProbeUiTheme theme) => theme.chatBubble;

        private static Sprite FillSpriteFor(Shape shape, PlayProbeUiTheme theme)
        {
            switch (shape)
            {
                case Shape.Panel:
                    return theme.panelFill != null ? theme.panelFill : RoundedSprite;
                case Shape.Pill:
                    return theme.pillFill != null ? theme.pillFill : RoundedSprite;
                case Shape.Circle:
                    return theme.circleFill != null
                        ? theme.circleFill
                        : AssetDatabase.GetBuiltinExtraResource<Sprite>(KnobSpritePath);
                default:
                    return theme.buttonFill != null ? theme.buttonFill : RoundedSprite;
            }
        }

        private static Sprite OutlineSpriteFor(Shape shape, PlayProbeUiTheme theme)
        {
            switch (shape)
            {
                case Shape.Panel: return theme.panelOutline;
                case Shape.Pill: return theme.pillOutline;
                case Shape.Circle: return null;
                default: return theme.buttonOutline;
            }
        }

        #region Roots

        /// <summary>
        /// A screen-space overlay canvas sized to the theme's reference resolution, with a raycaster
        /// so its children are clickable.
        /// </summary>
        internal static GameObject CreateOverlayCanvas(string name, int sortOrder, PlayProbeUiTheme theme)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = LayerMask.NameToLayer("UI");

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = theme.referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return root;
        }

        /// <summary>
        /// A full-screen dimmed backdrop. Also swallows clicks, so the player cannot interact with the
        /// game through an open modal.
        /// </summary>
        internal static Image CreateScrim(Transform parent, PlayProbeUiTheme theme)
        {
            GameObject scrim = CreateUiObject("Scrim", parent, typeof(Image));
            Stretch((RectTransform)scrim.transform);

            Image image = scrim.GetComponent<Image>();
            image.color = theme.scrim;
            image.raycastTarget = true;
            return image;
        }

        /// <summary>
        /// A centred card that grows to fit its children. Children are stacked vertically with the
        /// theme's spacing and padding.
        /// </summary>
        internal static RectTransform CreatePanel(Transform parent, string name, float width,
            PlayProbeUiTheme theme)
        {
            GameObject panel = CreateUiObject(name, parent, typeof(Image), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));

            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, 0f);

            ApplyStaticFill(panel.GetComponent<Image>(), Shape.Panel, theme.surface, theme);

            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(theme.panelPadding);
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.spacing = theme.spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddBorder(panel, Shape.Panel, theme.border, theme);

            return rect;
        }

        /// <summary>A transparent row that lays its children out left to right.</summary>
        internal static RectTransform CreateRow(Transform parent, string name, float spacing,
            TextAnchor alignment = TextAnchor.MiddleLeft, bool expandChildWidth = false)
        {
            GameObject row = CreateUiObject(name, parent, typeof(HorizontalLayoutGroup));

            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = expandChildWidth;
            layout.childForceExpandHeight = false;

            return (RectTransform)row.transform;
        }

        /// <summary>
        /// A container whose children wrap onto new lines when they run out of width — used for the tag
        /// pills, where the number of tags is decided by the dashboard, not the layout.
        /// </summary>
        internal static RectTransform CreateWrapRow(Transform parent, string name, float spacing)
        {
            GameObject row = CreateUiObject(name, parent, typeof(PlayProbeFlowLayoutGroup));

            PlayProbeFlowLayoutGroup layout = row.GetComponent<PlayProbeFlowLayoutGroup>();
            layout.Spacing = new Vector2(spacing, spacing);
            layout.childAlignment = TextAnchor.UpperLeft;

            // No ContentSizeFitter: the flow group reports its own height through
            // CalculateLayoutInputVertical, so the enclosing vertical layout already sizes it. Adding a
            // fitter on top would have the two fight over the same axis.
            return (RectTransform)row.transform;
        }

        #endregion

        #region Text

        /// <summary>A text label. Height follows the text, so labels never clip in a vertical layout.</summary>
        internal static TextMeshProUGUI CreateLabel(Transform parent, string name, string text,
            float fontSize, Color color, PlayProbeUiTheme theme,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, FontStyles style = FontStyles.Normal)
        {
            GameObject labelObject = CreateUiObject(name, parent, typeof(TextMeshProUGUI));

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = text ?? string.Empty;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            // Body copy at 1.0 line spacing is cramped at UI sizes; a touch of air makes multi-line
            // privacy notices readable.
            label.lineSpacing = 6f;

            if (theme.font != null)
            {
                label.font = theme.font;
            }

            ContentSizeFitter fitter = labelObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return label;
        }

        #endregion

        #region Controls

        /// <summary>A button with a centred label. The label is returned so callers can retheme it.</summary>
        internal static Button CreateButton(Transform parent, string name, string text, ButtonStyle style,
            PlayProbeUiTheme theme, out TextMeshProUGUI label, float? height = null)
        {
            GameObject buttonObject = CreateUiObject(name, parent, typeof(Image), typeof(Button),
                typeof(LayoutElement));

            Image background = buttonObject.GetComponent<Image>();
            Button button = buttonObject.GetComponent<Button>();

            Color fill;
            Color textColor;

            switch (style)
            {
                case ButtonStyle.Primary:
                    fill = theme.primary;
                    textColor = theme.textOnPrimary;
                    break;
                case ButtonStyle.Secondary:
                    fill = theme.surfaceRaised;
                    textColor = theme.textPrimary;
                    break;
                default:
                    fill = new Color(0f, 0f, 0f, 0f);
                    textColor = theme.accent;
                    break;
            }

            // White image; the colour block below supplies the fill and every interaction state.
            ApplyTintableFill(background, Shape.Button, theme);
            // A Ghost button still needs a raycast target or its own click never lands.
            background.raycastTarget = true;

            if (style == ButtonStyle.Secondary)
            {
                AddBorder(buttonObject, Shape.Button, theme.border, theme);
            }

            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = BuildColorBlock(fill, style);

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = height ?? theme.controlHeight;
            layoutElement.preferredHeight = height ?? theme.controlHeight;

            label = CreateLabel(buttonObject.transform, "Label", text, theme.fontSizeBody, textColor, theme,
                TextAlignmentOptions.Center, style == ButtonStyle.Primary ? FontStyles.Bold : FontStyles.Normal);

            // The label fills the button rather than sizing itself, so text stays centred.
            Object.DestroyImmediate(label.GetComponent<ContentSizeFitter>());
            Stretch((RectTransform)label.transform, 12f, 4f);

            return button;
        }

        /// <summary>
        /// A text field. Multi-line fields get a fixed height and top-aligned text; single-line ones
        /// use the theme's control height.
        /// </summary>
        internal static TMP_InputField CreateInputField(Transform parent, string name, string placeholder,
            PlayProbeUiTheme theme, bool multiline = false, float? height = null)
        {
            GameObject fieldObject = CreateUiObject(name, parent, typeof(Image), typeof(TMP_InputField),
                typeof(LayoutElement));

            Image background = fieldObject.GetComponent<Image>();
            ApplyTintableFill(background, Shape.Button, theme);
            AddBorder(fieldObject, Shape.Button, theme.border, theme);

            float resolvedHeight = height ?? (multiline ? 150f : theme.controlHeight);
            LayoutElement layoutElement = fieldObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = resolvedHeight;
            layoutElement.preferredHeight = resolvedHeight;

            // The viewport masks the text so a long entry scrolls instead of spilling out of the box.
            GameObject viewport = CreateUiObject("Text Area", fieldObject.transform, typeof(RectMask2D));
            Stretch((RectTransform)viewport.transform, 16f, 10f);

            TextMeshProUGUI placeholderLabel = CreateRawText(viewport.transform, "Placeholder", placeholder,
                theme.fontSizeBody, theme.textMuted, theme, multiline);
            placeholderLabel.fontStyle = FontStyles.Italic;

            TextMeshProUGUI textLabel = CreateRawText(viewport.transform, "Text", string.Empty,
                theme.fontSizeBody, theme.textPrimary, theme, multiline);

            TMP_InputField input = fieldObject.GetComponent<TMP_InputField>();
            input.textViewport = (RectTransform)viewport.transform;
            input.textComponent = textLabel;
            input.placeholder = placeholderLabel;
            input.targetGraphic = background;
            input.selectionColor = new Color(theme.primary.r, theme.primary.g, theme.primary.b, 0.4f);
            input.caretColor = theme.primary;
            input.customCaretColor = true;
            input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            input.colors = BuildColorBlock(theme.surfaceRaised, ButtonStyle.Secondary);

            return input;
        }

        /// <summary>A checkbox with a label to its right.</summary>
        internal static Toggle CreateToggle(Transform parent, string name, string text,
            PlayProbeUiTheme theme, out TextMeshProUGUI label)
        {
            GameObject toggleObject = CreateUiObject(name, parent, typeof(Toggle), typeof(LayoutElement),
                typeof(HorizontalLayoutGroup));

            HorizontalLayoutGroup layout = toggleObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            LayoutElement layoutElement = toggleObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = 36f;
            layoutElement.preferredHeight = 36f;

            GameObject box = CreateUiObject("Box", toggleObject.transform, typeof(Image), typeof(LayoutElement));
            LayoutElement boxLayout = box.GetComponent<LayoutElement>();
            boxLayout.minWidth = 28f;
            boxLayout.preferredWidth = 28f;
            boxLayout.minHeight = 28f;
            boxLayout.preferredHeight = 28f;

            Image boxImage = box.GetComponent<Image>();
            ApplyTintableFill(boxImage, Shape.Button, theme);
            AddBorder(box, Shape.Button, theme.border, theme);

            GameObject check = CreateUiObject("Checkmark", box.transform, typeof(Image));
            Stretch((RectTransform)check.transform, 5f, 5f);
            Image checkImage = check.GetComponent<Image>();
            checkImage.sprite = CheckmarkSprite(theme);
            checkImage.color = theme.primary;
            checkImage.raycastTarget = false;

            label = CreateLabel(toggleObject.transform, "Label", text, theme.fontSizeCaption,
                theme.textMuted, theme, TextAlignmentOptions.MidlineLeft);

            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;

            Toggle toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.isOn = true;
            toggle.colors = BuildColorBlock(theme.surfaceRaised, ButtonStyle.Secondary);

            return toggle;
        }

        /// <summary>A thin horizontal rule.</summary>
        internal static void CreateDivider(Transform parent, PlayProbeUiTheme theme)
        {
            GameObject divider = CreateUiObject("Divider", parent, typeof(Image), typeof(LayoutElement));

            Image image = divider.GetComponent<Image>();
            image.color = theme.border;
            image.raycastTarget = false;

            LayoutElement layoutElement = divider.GetComponent<LayoutElement>();
            layoutElement.minHeight = 1f;
            layoutElement.preferredHeight = 1f;
        }

        /// <summary>Empty vertical space, for when layout spacing alone is not enough.</summary>
        internal static void CreateSpacer(Transform parent, float height)
        {
            GameObject spacer = CreateUiObject("Spacer", parent, typeof(LayoutElement));
            LayoutElement layoutElement = spacer.GetComponent<LayoutElement>();
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
        }

        #endregion

        #region Primitives

        /// <summary>Creates a GameObject with a RectTransform, parented without keeping world position.</summary>
        internal static GameObject CreateUiObject(string name, Transform parent, params System.Type[] components)
        {
            GameObject created = new GameObject(name, components);
            created.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = created.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = created.AddComponent<RectTransform>();
            }

            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.anchoredPosition3D = Vector3.zero;

            return created;
        }

        /// <summary>Anchors a rect to fill its parent, optionally inset.</summary>
        internal static void Stretch(RectTransform rect, float horizontalInset = 0f, float verticalInset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(horizontalInset, verticalInset);
            rect.offsetMax = new Vector2(-horizontalInset, -verticalInset);
        }

        /// <summary>
        /// Fills a <b>non-interactive</b> graphic — a panel, the scrim, a divider — with the shape's
        /// sprite in the given colour. Nothing tints these at runtime, so the colour goes on the image.
        /// </summary>
        internal static void ApplyStaticFill(Image image, Shape shape, Color color, PlayProbeUiTheme theme)
        {
            ApplySprite(image, FillSpriteFor(shape, theme), RadiusFor(shape, theme));
            image.color = color;
            AttachCapsuleDriver(image, shape);
        }

        /// <summary>
        /// Fills an <b>interactive</b> graphic — anything with a Button, Toggle or InputField driving
        /// it — and leaves the image white.
        ///
        /// Unity multiplies a Selectable's colour block by its target graphic's colour, so tinting both
        /// multiplies them together: the brand purple comes out muddy and the disabled state loses its
        /// alpha. White image plus a coloured block gives one sprite correct normal, hover, pressed and
        /// disabled states for free.
        /// </summary>
        internal static void ApplyTintableFill(Image image, Shape shape, PlayProbeUiTheme theme)
        {
            ApplySprite(image, FillSpriteFor(shape, theme), RadiusFor(shape, theme));
            image.color = Color.white;
            AttachCapsuleDriver(image, shape);
        }

        /// <summary>
        /// Adds a border as a child Image using the shape's ring sprite, which strokes cleanly around a
        /// rounded corner. Returns the ring so callers can recolour it — chips and selectable buttons
        /// brighten their border on selection.
        ///
        /// Falls back to uGUI's <c>Outline</c> effect and returns null when the theme has no ring sprite
        /// for the shape. That effect just draws the graphic four times at an offset, which smears
        /// rather than strokes on a rounded shape — it is a stand-in until the sprites are imported.
        /// </summary>
        internal static Image AddBorder(GameObject target, Shape shape, Color color, PlayProbeUiTheme theme)
        {
            Sprite ring = OutlineSpriteFor(shape, theme);

            if (ring == null)
            {
                Outline outline = target.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = target.AddComponent<Outline>();
                }

                outline.effectColor = color;
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                outline.useGraphicAlpha = false;
                return null;
            }

            GameObject borderObject = CreateUiObject("Border", target.transform,
                typeof(Image), typeof(LayoutElement));
            Stretch((RectTransform)borderObject.transform);

            // Panels and question cards carry a layout group, which would otherwise treat the border as
            // another stacked child and push the real content down by its height.
            borderObject.GetComponent<LayoutElement>().ignoreLayout = true;

            Image image = borderObject.GetComponent<Image>();
            ApplySprite(image, ring, RadiusFor(shape, theme));
            image.color = color;
            // The fill underneath is the raycast target; a border that also caught clicks would sit on
            // top of the label and swallow them.
            image.raycastTarget = false;
            AttachCapsuleDriver(image, shape);

            // Drawn over the fill but under the label, which is added after this call.
            borderObject.transform.SetAsFirstSibling();

            return image;
        }

        // Assigns a sprite and works out the pixels-per-unit multiplier that makes its 9-slice border
        // render at the requested radius. Unity draws the border at `borderPixels / multiplier`
        // reference pixels, so the multiplier is what decouples the sprite's export resolution from the
        // design's corner radius — re-export the PNGs at any size and this still lands correctly.
        private static void ApplySprite(Image image, Sprite sprite, float radius)
        {
            image.sprite = sprite;

            float border = sprite != null ? sprite.border.x : 0f;

            if (border <= 0f)
            {
                // No slice data: either a glyph, or a sliced sprite whose border was never set in the
                // importer. Stretching it would distort the corners, so draw it whole.
                image.type = Image.Type.Simple;
                image.pixelsPerUnitMultiplier = 1f;
                return;
            }

            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = Mathf.Max(0.05f, border / Mathf.Max(1f, radius));
        }

        // A capsule's corner has to be half the element's height, which nothing at build time knows —
        // a tag chip and the feedback button are different heights and would need different values.
        // PlayProbeCapsuleImage recomputes it at runtime from the height the element actually gets.
        private static void AttachCapsuleDriver(Image image, Shape shape)
        {
            if (shape != Shape.Pill)
            {
                return;
            }

            if (image.GetComponent<PlayProbeCapsuleImage>() == null)
            {
                image.gameObject.AddComponent<PlayProbeCapsuleImage>();
            }
        }

        private static float RadiusFor(Shape shape, PlayProbeUiTheme theme)
        {
            switch (shape)
            {
                case Shape.Panel:
                    // Panels get a softer corner than the controls inside them.
                    return theme.cornerRadius * 1.6f;
                case Shape.Pill:
                    // Only the starting value, so the prefab looks right in the editor preview.
                    // PlayProbeCapsuleImage replaces it with half the real height at runtime.
                    return theme.controlHeight * 0.5f;
                default:
                    return theme.cornerRadius;
            }
        }

        /// <summary>Sets a fixed preferred size on an element inside a layout group.</summary>
        internal static LayoutElement SetSize(GameObject target, float? width, float? height)
        {
            LayoutElement layoutElement = target.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = target.AddComponent<LayoutElement>();
            }

            if (width.HasValue)
            {
                layoutElement.minWidth = width.Value;
                layoutElement.preferredWidth = width.Value;
            }

            if (height.HasValue)
            {
                layoutElement.minHeight = height.Value;
                layoutElement.preferredHeight = height.Value;
            }

            return layoutElement;
        }

        // A label with no ContentSizeFitter, stretched to its parent. Input fields need this: their
        // text components are positioned by TMP_InputField itself, and a fitter fights it.
        private static TextMeshProUGUI CreateRawText(Transform parent, string name, string text,
            float fontSize, Color color, PlayProbeUiTheme theme, bool multiline)
        {
            GameObject textObject = CreateUiObject(name, parent, typeof(TextMeshProUGUI));
            Stretch((RectTransform)textObject.transform);

            TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
            label.text = text ?? string.Empty;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = multiline ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = multiline ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            label.richText = false;

            if (theme.font != null)
            {
                label.font = theme.font;
            }

            return label;
        }

        private static ColorBlock BuildColorBlock(Color baseColor, ButtonStyle style)
        {
            ColorBlock block = ColorBlock.defaultColorBlock;
            block.normalColor = baseColor;
            // Tint multipliers rather than fixed colours, so a retheme keeps the same hover feel.
            block.highlightedColor = Brighten(baseColor, style == ButtonStyle.Primary ? 0.12f : 0.08f);
            block.pressedColor = Brighten(baseColor, -0.08f);
            block.selectedColor = Brighten(baseColor, 0.06f);
            block.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * 0.4f);
            block.fadeDuration = 0.1f;
            return block;
        }

        private static Color Brighten(Color color, float amount)
        {
            return new Color(
                Mathf.Clamp01(color.r + amount),
                Mathf.Clamp01(color.g + amount),
                Mathf.Clamp01(color.b + amount),
                color.a);
        }

        #endregion
    }
}
