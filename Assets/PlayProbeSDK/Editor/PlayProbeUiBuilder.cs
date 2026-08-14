// Copyright PlayProbe.io 2026. All rights reserved

using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static PlayProbe.Editor.PlayProbeUiFactory;

namespace PlayProbe.Editor
{
    /// <summary>
    /// Generates every PlayProbe UI prefab from code, styled by <see cref="PlayProbeUiTheme"/>.
    ///
    /// The point is that nobody has to hand-build these in the scene view. Change a colour or a string
    /// in the theme asset, run <c>Tools &gt; PlayProbe &gt; UI &gt; Rebuild All Prefabs</c>, and every
    /// screen picks it up — consistently, and without a diff full of prefab YAML.
    ///
    /// <c>Create Missing Prefabs</c> is the safe entry point: it only writes prefabs that do not exist
    /// yet, so your own customised versions are never clobbered. <c>Rebuild All Prefabs</c> overwrites,
    /// and asks first.
    /// </summary>
    internal static class PlayProbeUiBuilder
    {
        // Resolved from where the package actually sits, so moving or renaming the folder does not
        // send generated prefabs to a path nothing loads from. The theme is the exception: it
        // belongs to the project, not the package, so it keeps a fixed home the developer owns.
        private static string ResourcesDirectory => PlayProbePackagePaths.ResourcesFolder;
        private static string EmojiTexturePath => $"{PlayProbePackagePaths.TexturesFolder}/Emoji.png";
        private static string SpriteDirectory => PlayProbePackagePaths.UiSpritesFolder;

        private const string ThemeAssetPath = "Assets/Resources/PlayProbeUiTheme.asset";

        // Every prefab this builder owns, in the order it reports them.
        private static readonly string[] PrefabNames =
        {
            "PlayProbeToast",
            "PlayProbeTagChip",
            "PlayProbeSelectableButton",
            "PlayProbeFeedbackButton",
            "PlayProbeFeedbackCanvas",
            "PlayProbeConsentDialog",
            "PlayProbeStartSessionScreen",
            "PlayProbeSurveyCanvas",
            "PlayProbeRatingQuestion",
            "PlayProbeEmojiQuestion",
            "PlayProbeYesNoQuestion",
            "PlayProbeMultipleOptions",
            "PlayProbeTextQuestion",
        };

        #region Menu

        [MenuItem("Tools/PlayProbe/UI/Create Missing Prefabs", priority = 20)]
        private static void CreateMissing()
        {
            Build(overwrite: false);
        }

        [MenuItem("Tools/PlayProbe/UI/Rebuild All Prefabs (overwrite)", priority = 21)]
        private static void RebuildAll()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Rebuild PlayProbe UI prefabs",
                $"This overwrites all {PrefabNames.Length} PlayProbe UI prefabs in {ResourcesDirectory} " +
                "with freshly generated ones.\n\nAny changes you made to them by hand will be lost. " +
                "Prefabs you renamed or moved elsewhere are not touched.",
                "Rebuild", "Cancel");

            if (confirmed)
            {
                Build(overwrite: true);
            }
        }

        [MenuItem("Tools/PlayProbe/UI/Create Theme Asset", priority = 22)]
        private static void CreateThemeAsset()
        {
            PlayProbeUiTheme existing = AssetDatabase.LoadAssetAtPath<PlayProbeUiTheme>(ThemeAssetPath);

            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ThemeAssetPath) ?? "Assets/Resources");

            PlayProbeUiTheme theme = ScriptableObject.CreateInstance<PlayProbeUiTheme>();
            AssetDatabase.CreateAsset(theme, ThemeAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            PlayProbeUiTheme.InvalidateCache();

            Selection.activeObject = theme;
            EditorGUIUtility.PingObject(theme);
            Debug.Log($"[PlayProbe] Created the UI theme at {ThemeAssetPath}. Edit it, then rebuild the prefabs.");
        }

        #endregion

        private static void Build(bool overwrite)
        {
            Directory.CreateDirectory(ResourcesDirectory);
            PlayProbeUiTheme.InvalidateCache();
            PlayProbeUiTheme theme = PlayProbeUiTheme.Default;

            ResolveThemeSprites(theme);

            List<string> written = new();
            List<string> skipped = new();

            // Deliberately not batched with StartAssetEditing: each prefab has to be importable straight
            // away, because the cross-reference pass below loads them back as assets.
            foreach (string name in PrefabNames)
            {
                string path = $"{ResourcesDirectory}/{name}.prefab";

                if (!overwrite && File.Exists(path))
                {
                    skipped.Add(name);
                    continue;
                }

                GameObject root = Construct(name, theme);

                if (root == null)
                {
                    continue;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                written.Add(name);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // The tag chip and the option button are referenced by other prefabs, so they have to exist
            // as assets before the prefabs that point at them are saved. The first pass creates them;
            // this pass re-saves the dependents now that the references can resolve.
            LinkCrossPrefabReferences(theme);

            Debug.Log(
                $"[PlayProbe] UI prefabs written: {(written.Count > 0 ? string.Join(", ", written) : "none")}." +
                (skipped.Count > 0 ? $" Left alone (already present): {string.Join(", ", skipped)}." : string.Empty));
        }

        // Theme slot -> the filename the sprite tool exports. Saves dragging nine references in by hand.
        private static readonly (string Field, string File)[] SpriteSlots =
        {
            ("buttonFill", "PlayProbeButton"),
            ("buttonOutline", "PlayProbeButtonOutline"),
            ("panelFill", "PlayProbePanel"),
            ("panelOutline", "PlayProbePanelOutline"),
            ("pillFill", "PlayProbePill"),
            ("pillOutline", "PlayProbePillOutline"),
            ("circleFill", "PlayProbeCircle"),
            ("checkmark", "PlayProbeCheck"),
            ("chatBubble", "PlayProbeChatBubble"),
        };

        /// <summary>
        /// Fills any empty sprite slot on the theme from <c>Textures/UI</c>, matching by filename.
        /// Slots that are already assigned are left alone, so pointing one at your own artwork survives
        /// a rebuild. Slots with no matching file stay empty and fall back to Unity's built-ins.
        /// </summary>
        private static void ResolveThemeSprites(PlayProbeUiTheme theme)
        {
            SerializedObject serialized = new SerializedObject(theme);
            List<string> resolved = new();

            foreach ((string field, string file) in SpriteSlots)
            {
                SerializedProperty property = serialized.FindProperty(field);

                if (property == null || property.objectReferenceValue != null)
                {
                    continue;
                }

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDirectory}/{file}.png");

                if (sprite == null)
                {
                    continue;
                }

                property.objectReferenceValue = sprite;
                resolved.Add(file);
            }

            if (resolved.Count == 0)
            {
                return;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Only worth persisting when the theme is a real asset; the built-in fallback instance is
            // rebuilt from defaults on every domain reload anyway.
            if (AssetDatabase.Contains(theme))
            {
                EditorUtility.SetDirty(theme);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[PlayProbe] Filled empty theme sprite slots from {SpriteDirectory}: {string.Join(", ", resolved)}.");
        }

        private static GameObject Construct(string name, PlayProbeUiTheme theme)
        {
            switch (name)
            {
                case "PlayProbeToast": return BuildToast(theme);
                case "PlayProbeTagChip": return BuildTagChip(theme);
                case "PlayProbeSelectableButton": return BuildSelectableButton(theme);
                case "PlayProbeFeedbackButton": return BuildFeedbackButton(theme);
                case "PlayProbeFeedbackCanvas": return BuildFeedbackCanvas(theme);
                case "PlayProbeConsentDialog": return BuildConsentDialog(theme);
                case "PlayProbeStartSessionScreen": return BuildTokenScreen(theme);
                case "PlayProbeSurveyCanvas": return BuildSurveyCanvas(theme);
                case "PlayProbeRatingQuestion": return BuildRatingQuestion(theme, emoji: false);
                case "PlayProbeEmojiQuestion": return BuildRatingQuestion(theme, emoji: true);
                case "PlayProbeYesNoQuestion": return BuildYesNoQuestion(theme);
                case "PlayProbeMultipleOptions": return BuildMultipleChoiceQuestion(theme);
                case "PlayProbeTextQuestion": return BuildTextQuestion(theme);
                default:
                    Debug.LogWarning($"[PlayProbe] No builder for prefab '{name}'.");
                    return null;
            }
        }

        #region Small prefabs

        private static GameObject BuildToast(PlayProbeUiTheme theme)
        {
            GameObject root = CreateOverlayCanvas("PlayProbeToast", PlayProbeUi.SortOrderToast, theme);
            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            GameObject panel = CreateUiObject("Panel", root.transform, typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));

            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 80f);
            panelRect.sizeDelta = new Vector2(0f, 64f);

            ApplyStaticFill(panel.GetComponent<Image>(), Shape.Panel, theme.surface, theme);
            AddBorder(panel, Shape.Panel, theme.border, theme);

            HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 24, 0, 0);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;

            ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // A coloured spine on the left edge is the whole visual difference between a confirmation
            // and an error, so it is a real element rather than a tint on the panel.
            GameObject accent = CreateUiObject("AccentBar", panel.transform, typeof(Image));
            SetSize(accent, 5f, null);
            Image accentImage = accent.GetComponent<Image>();
            accentImage.color = theme.success;
            accentImage.raycastTarget = false;

            TextMeshProUGUI label = CreateLabel(panel.transform, "Message", "Message", theme.fontSizeBody,
                theme.textPrimary, theme, TextAlignmentOptions.MidlineLeft);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Object.DestroyImmediate(label.GetComponent<ContentSizeFitter>());

            PlayProbeToast toast = root.AddComponent<PlayProbeToast>();
            Wire(toast, "canvasGroup", group);
            Wire(toast, "label", label);
            Wire(toast, "accentBar", accentImage);

            return root;
        }

        private static GameObject BuildTagChip(PlayProbeUiTheme theme)
        {
            // A horizontal layout with padding is what gives the chip a preferred width: the group
            // reports padding + the label's own text width, and PlayProbeFlowLayoutGroup lays each chip
            // out at exactly that. No two chips end up the same width unless their labels are.
            GameObject root = CreateUiObject("PlayProbeTagChip", null, typeof(Image), typeof(Button),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement));

            HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 6, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Floor on the height so short labels still read as a pill rather than a sliver.
            LayoutElement chipSize = root.GetComponent<LayoutElement>();
            chipSize.minHeight = 36f;

            Image background = root.GetComponent<Image>();
            // The chip component drives this colour itself — a selected chip gets a tinted wash rather
            // than a solid fill — so it is a static fill even though the chip is clickable.
            ApplyStaticFill(background, Shape.Pill, theme.surfaceRaised, theme);

            Image border = AddBorder(root, Shape.Pill, theme.border, theme);

            Button button = root.GetComponent<Button>();
            button.targetGraphic = background;
            // A neutral colour block: PlayProbeTagChip sets the fill, border and label colours itself,
            // and Unity would otherwise multiply its own tint over them — doubling the dim on a chip
            // that is both deselected and past the selection limit.
            button.colors = NeutralColorBlock();

            TextMeshProUGUI label = CreateLabel(root.transform, "Label", "Tag", theme.fontSizeCaption,
                theme.textMuted, theme, TextAlignmentOptions.Center);
            // The layout group sizes the label from its own ILayoutElement, so it must not also size
            // itself — and it must not wrap, or its preferred width collapses and every chip shrinks
            // to one word per line.
            Object.DestroyImmediate(label.GetComponent<ContentSizeFitter>());
            label.textWrappingMode = TextWrappingModes.NoWrap;

            PlayProbeTagChip chip = root.AddComponent<PlayProbeTagChip>();
            Wire(chip, "button", button);
            Wire(chip, "label", label);
            Wire(chip, "background", background);
            Wire(chip, "border", border);

            return root;
        }

        private static GameObject BuildSelectableButton(PlayProbeUiTheme theme)
        {
            GameObject root = CreateUiObject("PlayProbeSelectableButton", null, typeof(Image), typeof(Button),
                typeof(LayoutElement));

            Image background = root.GetComponent<Image>();
            ApplyTintableFill(background, Shape.Button, theme);

            Image border = AddBorder(root, Shape.Button, theme.border, theme);

            Button button = root.GetComponent<Button>();
            button.targetGraphic = background;
            button.colors = MakeSelectableColors(theme);

            SetSize(root, null, theme.controlHeight);

            TextMeshProUGUI label = CreateLabel(root.transform, "Label", "Option", theme.fontSizeBody,
                theme.textPrimary, theme, TextAlignmentOptions.Center);
            Object.DestroyImmediate(label.GetComponent<ContentSizeFitter>());
            Stretch((RectTransform)label.transform, 12f, 4f);

            PlayProbeSelectableButton selectable = root.AddComponent<PlayProbeSelectableButton>();
            Wire(selectable, "button", button);
            Wire(selectable, "buttonText", label);
            Wire(selectable, "border", border);
            Wire(selectable, "background", background);
            WireColor(selectable, "selectedColor", theme.primary);

            return root;
        }

        private static GameObject BuildFeedbackButton(PlayProbeUiTheme theme)
        {
            GameObject root = CreateOverlayCanvas("PlayProbeFeedbackButton", PlayProbeUi.SortOrderFeedbackButton, theme);
            CanvasGroup group = root.AddComponent<CanvasGroup>();

            GameObject panel = CreateUiObject("Panel", root.transform, typeof(Image), typeof(Button));
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.anchoredPosition = new Vector2(-28f, 28f);
            // Height is the collapsed square; width is the expanded state the controller lerps to.
            panelRect.sizeDelta = new Vector2(190f, 56f);

            Image background = panel.GetComponent<Image>();
            ApplyTintableFill(background, Shape.Pill, theme);

            Button button = panel.GetComponent<Button>();
            button.targetGraphic = background;
            button.colors = MakeSelectableColors(theme, theme.primary);

            // The speech-bubble glyph. With the sprite set it is one Image; without it, the shape is
            // approximated with a rounded body plus a small square tail, so the button still reads as
            // "say something" in a project that has not imported the sprite set.
            Sprite bubble = ChatBubbleSprite(theme);

            GameObject icon = CreateUiObject("Icon", panel.transform, typeof(Image));
            RectTransform iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(28f, bubble != null ? 0f : 2f);
            iconRect.sizeDelta = bubble != null ? new Vector2(26f, 26f) : new Vector2(24f, 20f);

            Image iconImage = icon.GetComponent<Image>();
            iconImage.color = theme.textOnPrimary;
            iconImage.raycastTarget = false;

            if (bubble != null)
            {
                iconImage.sprite = bubble;
                iconImage.type = Image.Type.Simple;
                iconImage.preserveAspect = true;
            }
            else
            {
                ApplyStaticFill(iconImage, Shape.Button, theme.textOnPrimary, theme);

                GameObject tail = CreateUiObject("IconTail", panel.transform, typeof(Image));
                RectTransform tailRect = (RectTransform)tail.transform;
                tailRect.anchorMin = new Vector2(0f, 0.5f);
                tailRect.anchorMax = new Vector2(0f, 0.5f);
                tailRect.pivot = new Vector2(0.5f, 0.5f);
                tailRect.anchoredPosition = new Vector2(22f, -10f);
                tailRect.sizeDelta = new Vector2(8f, 8f);
                Image tailImage = tail.GetComponent<Image>();
                tailImage.color = theme.textOnPrimary;
                tailImage.raycastTarget = false;
            }

            TextMeshProUGUI label = CreateLabel(panel.transform, "Label", "Feedback", theme.fontSizeBody,
                theme.textOnPrimary, theme, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            Object.DestroyImmediate(label.GetComponent<ContentSizeFitter>());
            RectTransform labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(52f, 0f);
            labelRect.offsetMax = new Vector2(-18f, 0f);
            label.textWrappingMode = TextWrappingModes.NoWrap;

            PlayProbeFeedbackButton feedbackButton = root.AddComponent<PlayProbeFeedbackButton>();
            Wire(feedbackButton, "button", button);
            Wire(feedbackButton, "panel", panelRect);
            Wire(feedbackButton, "canvasGroup", group);
            Wire(feedbackButton, "label", label);

            return root;
        }

        #endregion

        #region Modal screens

        private static GameObject BuildFeedbackCanvas(PlayProbeUiTheme theme)
        {
            GameObject root = CreateOverlayCanvas("PlayProbeFeedbackCanvas", PlayProbeUi.SortOrderFeedback, theme);
            CreateScrim(root.transform, theme);

            RectTransform panel = CreateFixedPanel(root.transform, "Panel", theme.panelWidth, 940f, theme);

            TextMeshProUGUI title = CreateLabel(panel, "Title", theme.feedbackTitle, theme.fontSizeTitle,
                theme.textPrimary, theme, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            TextMeshProUGUI subtitle = CreateLabel(panel, "Subtitle", theme.feedbackSubtitle,
                theme.fontSizeCaption, theme.textMuted, theme);

            RectTransform content = CreateScrollArea(panel, "Body", theme);

            // Category
            CreateLabel(content, "CategoryHeading", "Type", theme.fontSizeCaption, theme.textMuted, theme);
            RectTransform categoryRow = CreateRow(content, "Categories", 10f, TextAnchor.MiddleLeft,
                expandChildWidth: true);
            SetSize(categoryRow.gameObject, null, theme.controlHeight);

            // Inputs
            TMP_InputField titleInput = CreateInputField(content, "TitleInput",
                theme.feedbackTitlePlaceholder, theme);
            TMP_InputField descriptionInput = CreateInputField(content, "DescriptionInput",
                theme.feedbackDescriptionPlaceholder, theme, multiline: true, height: 190f);

            TextMeshProUGUI counter = CreateLabel(content, "Counter", "0 / 4000", theme.fontSizeCaption,
                theme.textMuted, theme, TextAlignmentOptions.TopRight);

            // Screenshot
            GameObject screenshotSection = CreateSection(content, "Screenshot", theme);
            Toggle screenshotToggle = CreateToggle(screenshotSection.transform, "ScreenshotToggle",
                theme.feedbackScreenshotLabel, theme, out TextMeshProUGUI screenshotLabel);

            GameObject previewFrame = CreateUiObject("PreviewFrame", screenshotSection.transform,
                typeof(Image), typeof(LayoutElement));
            SetSize(previewFrame, null, 240f);
            ApplyStaticFill(previewFrame.GetComponent<Image>(), Shape.Panel, theme.surfaceRaised, theme);

            GameObject preview = CreateUiObject("Preview", previewFrame.transform,
                typeof(RawImage), typeof(AspectRatioFitter));
            Stretch((RectTransform)preview.transform, 6f, 6f);
            RawImage previewImage = preview.GetComponent<RawImage>();
            previewImage.raycastTarget = false;
            AspectRatioFitter previewAspect = preview.GetComponent<AspectRatioFitter>();
            previewAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            previewAspect.aspectRatio = 16f / 9f;

            // Tags
            PlayProbeTagSelector tagSelector = CreateTagSelector(content, theme.feedbackTagsLabel, theme);

            // Footer
            CreateDivider(panel, theme);
            TextMeshProUGUI notice = CreateLabel(panel, "PrivacyNotice",
                PlayProbeFeedback.DefaultPrivacyNotice, theme.fontSizeCaption, theme.textMuted, theme);

            Button policyButton = CreateButton(panel, "PrivacyPolicyLink", theme.privacyPolicyLinkLabel,
                ButtonStyle.Ghost, theme, out TextMeshProUGUI policyLabel, height: 34f);
            PlayProbeLinkButton linkButton = policyButton.gameObject.AddComponent<PlayProbeLinkButton>();
            policyLabel.alignment = TextAlignmentOptions.MidlineLeft;
            policyLabel.fontStyle = FontStyles.Underline;

            TextMeshProUGUI error = CreateLabel(panel, "Error", theme.feedbackEmptyError,
                theme.fontSizeCaption, theme.danger, theme);
            error.gameObject.SetActive(false);

            RectTransform actions = CreateRow(panel, "Actions", 12f, TextAnchor.MiddleRight,
                expandChildWidth: true);
            SetSize(actions.gameObject, null, theme.controlHeight);
            Button cancel = CreateButton(actions, "Cancel", theme.feedbackCancelLabel, ButtonStyle.Secondary,
                theme, out TextMeshProUGUI cancelLabel);
            Button submit = CreateButton(actions, "Submit", theme.feedbackSubmitLabel, ButtonStyle.Primary,
                theme, out TextMeshProUGUI submitLabel);

            PlayProbeFeedbackCanvas canvas = root.AddComponent<PlayProbeFeedbackCanvas>();
            Wire(canvas, "titleLabel", title);
            Wire(canvas, "subtitleLabel", subtitle);
            Wire(canvas, "titleInput", titleInput);
            Wire(canvas, "descriptionInput", descriptionInput);
            Wire(canvas, "descriptionCounter", counter);
            Wire(canvas, "categoryContainer", categoryRow);
            Wire(canvas, "screenshotSection", screenshotSection);
            Wire(canvas, "screenshotToggle", screenshotToggle);
            Wire(canvas, "screenshotLabel", screenshotLabel);
            Wire(canvas, "screenshotPreview", previewImage);
            Wire(canvas, "screenshotAspect", previewAspect);
            Wire(canvas, "tagSelector", tagSelector);
            Wire(canvas, "privacyNoticeLabel", notice);
            Wire(canvas, "privacyPolicyButton", linkButton);
            Wire(canvas, "privacyPolicyButtonLabel", policyLabel);
            Wire(canvas, "submitButton", submit);
            Wire(canvas, "submitButtonLabel", submitLabel);
            Wire(canvas, "cancelButton", cancel);
            Wire(canvas, "cancelButtonLabel", cancelLabel);
            Wire(canvas, "errorLabel", error);

            return root;
        }

        private static GameObject BuildConsentDialog(PlayProbeUiTheme theme)
        {
            GameObject root = CreateOverlayCanvas("PlayProbeConsentDialog", PlayProbeUi.SortOrderConsent, theme);
            CreateScrim(root.transform, theme);

            RectTransform panel = CreatePanel(root.transform, "Panel", 640f, theme);

            TextMeshProUGUI title = CreateLabel(panel, "Title", theme.consentTitle, theme.fontSizeHeading,
                theme.textPrimary, theme, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            TextMeshProUGUI body = CreateLabel(panel, "Body", theme.consentBody, theme.fontSizeBody,
                theme.textMuted, theme);

            Button policyButton = CreateButton(panel, "PrivacyPolicyLink", theme.privacyPolicyLinkLabel,
                ButtonStyle.Ghost, theme, out TextMeshProUGUI policyLabel, height: 34f);
            PlayProbeLinkButton linkButton = policyButton.gameObject.AddComponent<PlayProbeLinkButton>();
            policyLabel.alignment = TextAlignmentOptions.MidlineLeft;
            policyLabel.fontStyle = FontStyles.Underline;

            CreateSpacer(panel, 8f);

            RectTransform actions = CreateRow(panel, "Actions", 12f, TextAnchor.MiddleRight,
                expandChildWidth: true);
            SetSize(actions.gameObject, null, theme.controlHeight);
            Button decline = CreateButton(actions, "Decline", theme.consentDeclineLabel, ButtonStyle.Secondary,
                theme, out TextMeshProUGUI declineLabel);
            Button accept = CreateButton(actions, "Accept", theme.consentAcceptLabel, ButtonStyle.Primary,
                theme, out TextMeshProUGUI acceptLabel);

            PlayProbeConsentDialog dialog = root.AddComponent<PlayProbeConsentDialog>();
            Wire(dialog, "titleLabel", title);
            Wire(dialog, "bodyLabel", body);
            Wire(dialog, "acceptButton", accept);
            Wire(dialog, "acceptButtonLabel", acceptLabel);
            Wire(dialog, "declineButton", decline);
            Wire(dialog, "declineButtonLabel", declineLabel);
            Wire(dialog, "privacyPolicyButton", linkButton);
            Wire(dialog, "privacyPolicyButtonLabel", policyLabel);

            return root;
        }

        private static GameObject BuildTokenScreen(PlayProbeUiTheme theme)
        {
            GameObject root = CreateOverlayCanvas("PlayProbeStartSessionScreen", PlayProbeUi.SortOrderTokenScreen, theme);
            CreateScrim(root.transform, theme);

            RectTransform panel = CreatePanel(root.transform, "Panel", 720f, theme);

            TextMeshProUGUI title = CreateLabel(panel, "Title", theme.tokenTitle, theme.fontSizeTitle,
                theme.textPrimary, theme, TextAlignmentOptions.Center, FontStyles.Bold);
            TextMeshProUGUI subtitle = CreateLabel(panel, "Subtitle", theme.tokenSubtitle,
                theme.fontSizeCaption, theme.textMuted, theme, TextAlignmentOptions.Center);

            CreateSpacer(panel, 8f);

            RectTransform codeRow = CreateRow(panel, "CodeRow", 10f, TextAnchor.MiddleCenter);
            SetSize(codeRow.gameObject, null, 80f);

            const int tokenLength = 8;
            TMP_InputField[] fields = new TMP_InputField[tokenLength];

            for (int i = 0; i < tokenLength; i++)
            {
                TMP_InputField field = CreateInputField(codeRow, $"Char{i}", string.Empty, theme, height: 80f);
                SetSize(field.gameObject, 68f, 80f);
                field.characterLimit = 1;
                field.textComponent.alignment = TextAlignmentOptions.Center;
                field.textComponent.fontSize = 34f;
                field.textComponent.fontStyle = FontStyles.Bold;
                fields[i] = field;
            }

            TextMeshProUGUI error = CreateLabel(panel, "Error", theme.tokenInvalidError, theme.fontSizeCaption,
                theme.danger, theme, TextAlignmentOptions.Center);
            error.gameObject.SetActive(false);

            Button start = CreateButton(panel, "StartButton", theme.tokenStartLabel, ButtonStyle.Primary,
                theme, out TextMeshProUGUI startLabel, height: 60f);
            Button paste = CreateButton(panel, "PasteButton", theme.tokenPasteLabel, ButtonStyle.Ghost,
                theme, out TextMeshProUGUI pasteLabel, height: 36f);
            Button cancel = CreateButton(panel, "CancelButton", "Not now", ButtonStyle.Ghost, theme,
                out TextMeshProUGUI _, height: 32f);
            cancel.gameObject.SetActive(false);

            PlayProbeTokenInputController controller = root.AddComponent<PlayProbeTokenInputController>();
            Wire(controller, "titleLabel", title);
            Wire(controller, "subtitleLabel", subtitle);
            Wire(controller, "errorLabel", error);
            WireArray(controller, "inputFields", fields);
            Wire(controller, "startSessionButton", start);
            Wire(controller, "startSessionButtonLabel", startLabel);
            Wire(controller, "pasteButton", paste);
            Wire(controller, "pasteButtonLabel", pasteLabel);
            Wire(controller, "cancelButton", cancel);

            return root;
        }

        private static GameObject BuildSurveyCanvas(PlayProbeUiTheme theme)
        {
            GameObject root = CreateOverlayCanvas("PlayProbeSurveyCanvas", PlayProbeUi.SortOrderSurvey, theme);
            CreateScrim(root.transform, theme);

            RectTransform panel = CreateFixedPanel(root.transform, "Panel", theme.panelWidth, 880f, theme);

            CreateLabel(panel, "Title", "A few quick questions", theme.fontSizeHeading, theme.textPrimary,
                theme, TextAlignmentOptions.TopLeft, FontStyles.Bold);

            RectTransform container = CreateScrollArea(panel, "Questions", theme);

            TextMeshProUGUI error = CreateLabel(panel, "Error", theme.surveyIncompleteError,
                theme.fontSizeCaption, theme.danger, theme);
            error.gameObject.SetActive(false);

            RectTransform actions = CreateRow(panel, "Actions", 12f, TextAnchor.MiddleRight,
                expandChildWidth: true);
            SetSize(actions.gameObject, null, theme.controlHeight);
            Button skip = CreateButton(actions, "Skip", theme.surveySkipLabel, ButtonStyle.Secondary, theme,
                out TextMeshProUGUI skipLabel);
            Button submit = CreateButton(actions, "Submit", theme.surveySubmitLabel, ButtonStyle.Primary,
                theme, out TextMeshProUGUI submitLabel);

            PlayProbeSurveyCanvas canvas = root.AddComponent<PlayProbeSurveyCanvas>();
            Wire(canvas, "container", container);
            Wire(canvas, "submitButton", submit);
            Wire(canvas, "submitButtonLabel", submitLabel);
            Wire(canvas, "skipButton", skip);
            Wire(canvas, "skipButtonLabel", skipLabel);
            Wire(canvas, "errorLabel", error);

            return root;
        }

        #endregion

        #region Question prefabs

        private static GameObject BuildRatingQuestion(PlayProbeUiTheme theme, bool emoji)
        {
            string name = emoji ? "PlayProbeEmojiQuestion" : "PlayProbeRatingQuestion";
            GameObject root = CreateQuestionShell(name, theme, out TextMeshProUGUI question);

            RectTransform row = CreateRow(root.transform, "Scale", 4f, TextAnchor.MiddleCenter,
                expandChildWidth: true);
            SetSize(row.gameObject, null, emoji ? 80f : 64f);

            Sprite[] emojiSprites = emoji ? LoadEmojiSprites(5) : null;
            List<PlayProbeSelectableButton> buttons = new();

            for (int i = 0; i < 5; i++)
            {
                GameObject buttonObject = CreateUiObject($"Step{i + 1}", row, typeof(Image), typeof(Button));
                Image background = buttonObject.GetComponent<Image>();
                ApplyTintableFill(background, Shape.Button, theme);

                Image border = AddBorder(buttonObject, Shape.Button, theme.border, theme);

                Button button = buttonObject.GetComponent<Button>();
                button.targetGraphic = background;
                button.colors = MakeSelectableColors(theme);

                TextMeshProUGUI label = CreateLabel(buttonObject.transform, "Label",
                    emoji ? string.Empty : (i + 1).ToString(), emoji ? theme.fontSizeCaption : theme.fontSizeHeading,
                    theme.textPrimary, theme, TextAlignmentOptions.Center, FontStyles.Bold);
                Object.DestroyImmediate(label.GetComponent<ContentSizeFitter>());
                Stretch((RectTransform)label.transform);

                if (emoji && emojiSprites != null && i < emojiSprites.Length && emojiSprites[i] != null)
                {
                    GameObject face = CreateUiObject("Face", buttonObject.transform, typeof(Image));
                    Stretch((RectTransform)face.transform, 3f, 3f);
                    Image faceImage = face.GetComponent<Image>();
                    faceImage.sprite = emojiSprites[i];
                    faceImage.preserveAspect = true;
                    faceImage.raycastTarget = false;
                }

                PlayProbeSelectableButton selectable = buttonObject.AddComponent<PlayProbeSelectableButton>();
                Wire(selectable, "button", button);
                Wire(selectable, "buttonText", label);
                Wire(selectable, "border", border);
                Wire(selectable, "background", background);
                WireColor(selectable, "selectedColor", theme.primary);

                buttons.Add(selectable);
            }

            RatingPlayProbeQuestion component = root.AddComponent<RatingPlayProbeQuestion>();
            Wire(component, "question", question);
            WireArray(component, "ratingButtons", buttons.ToArray());
            WireBool(component, "isEmojiRating", emoji);

            return root;
        }

        private static GameObject BuildYesNoQuestion(PlayProbeUiTheme theme)
        {
            GameObject root = CreateQuestionShell("PlayProbeYesNoQuestion", theme, out TextMeshProUGUI question);

            RectTransform row = CreateRow(root.transform, "Answers", 12f, TextAnchor.MiddleLeft,
                expandChildWidth: true);
            SetSize(row.gameObject, null, theme.controlHeight);

            PlayProbeSelectableButton yes = CreateOptionButton(row, "Yes", "Yes", theme);
            PlayProbeSelectableButton no = CreateOptionButton(row, "No", "No", theme);

            YesNoQuestion component = root.AddComponent<YesNoQuestion>();
            Wire(component, "title", question);
            Wire(component, "yesButton", yes);
            Wire(component, "noButton", no);

            return root;
        }

        private static GameObject BuildMultipleChoiceQuestion(PlayProbeUiTheme theme)
        {
            GameObject root = CreateQuestionShell("PlayProbeMultipleOptions", theme, out TextMeshProUGUI question);

            GameObject answers = CreateUiObject("Answers", root.transform, typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            VerticalLayoutGroup layout = answers.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            answers.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            MultipleChoiceQuestion component = root.AddComponent<MultipleChoiceQuestion>();
            Wire(component, "questionText", question);
            Wire(component, "answersContainer", (RectTransform)answers.transform);
            // selectionButton points at the shared PlayProbeSelectableButton asset; resolved in the
            // cross-reference pass once that prefab exists on disk.

            return root;
        }

        private static GameObject BuildTextQuestion(PlayProbeUiTheme theme)
        {
            GameObject root = CreateQuestionShell("PlayProbeTextQuestion", theme, out TextMeshProUGUI question);

            TMP_InputField input = CreateInputField(root.transform, "Input",
                "Type your answer…", theme, multiline: true, height: 150f);

            PlayProbeTagSelector tagSelector = CreateTagSelector(root.transform, theme.surveyTagsLabel, theme);

            TextQuestion component = root.AddComponent<TextQuestion>();
            Wire(component, "questionText", question);
            Wire(component, "inputField", input);
            Wire(component, "tagSelector", tagSelector);

            return root;
        }

        #endregion

        #region Composition helpers

        // A question is a card in the survey's scroll list: heading on top, answer control underneath.
        private static GameObject CreateQuestionShell(string name, PlayProbeUiTheme theme,
            out TextMeshProUGUI question)
        {
            GameObject root = CreateUiObject(name, null, typeof(Image), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));

            ApplyStaticFill(root.GetComponent<Image>(), Shape.Panel, theme.surfaceRaised, theme);

            VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            root.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            question = CreateLabel(root.transform, "Question", "Question", theme.fontSizeBody,
                theme.textPrimary, theme, TextAlignmentOptions.TopLeft, FontStyles.Bold);

            return root;
        }

        private static PlayProbeSelectableButton CreateOptionButton(Transform parent, string name, string label,
            PlayProbeUiTheme theme)
        {
            GameObject buttonObject = CreateUiObject(name, parent, typeof(Image), typeof(Button));

            Image background = buttonObject.GetComponent<Image>();
            ApplyTintableFill(background, Shape.Button, theme);
            Image border = AddBorder(buttonObject, Shape.Button, theme.border, theme);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.colors = MakeSelectableColors(theme);

            TextMeshProUGUI text = CreateLabel(buttonObject.transform, "Label", label, theme.fontSizeBody,
                theme.textPrimary, theme, TextAlignmentOptions.Center);
            Object.DestroyImmediate(text.GetComponent<ContentSizeFitter>());
            Stretch((RectTransform)text.transform, 10f, 4f);

            PlayProbeSelectableButton selectable = buttonObject.AddComponent<PlayProbeSelectableButton>();
            Wire(selectable, "button", button);
            Wire(selectable, "buttonText", text);
            Wire(selectable, "border", border);
            Wire(selectable, "background", background);
            WireColor(selectable, "selectedColor", theme.primary);

            return selectable;
        }

        // A panel with a fixed height: needed wherever the content scrolls, because a
        // ContentSizeFitter and a ScrollRect cannot both decide the height.
        private static RectTransform CreateFixedPanel(Transform parent, string name, float width, float height,
            PlayProbeUiTheme theme)
        {
            GameObject panel = CreateUiObject(name, parent, typeof(Image), typeof(VerticalLayoutGroup));

            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);

            ApplyStaticFill(panel.GetComponent<Image>(), Shape.Panel, theme.surface, theme);
            AddBorder(panel, Shape.Panel, theme.border, theme);

            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(theme.panelPadding);
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.spacing = theme.spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            return rect;
        }

        // A scrolling region that takes whatever vertical space is left in its parent panel. Returns the
        // content transform, which is what callers add children to.
        private static RectTransform CreateScrollArea(Transform parent, string name, PlayProbeUiTheme theme)
        {
            GameObject scrollObject = CreateUiObject(name, parent, typeof(ScrollRect), typeof(LayoutElement));
            LayoutElement layoutElement = scrollObject.GetComponent<LayoutElement>();
            layoutElement.flexibleHeight = 1f;
            layoutElement.minHeight = 120f;

            GameObject viewport = CreateUiObject("Viewport", scrollObject.transform, typeof(RectMask2D));
            Stretch((RectTransform)viewport.transform);

            GameObject content = CreateUiObject("Content", viewport.transform, typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));

            RectTransform contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(0f, 0f);
            contentRect.offsetMax = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = theme.spacing;
            // Right padding leaves room for the scrollbar gutter without a visible scrollbar.
            layout.padding = new RectOffset(0, 6, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            return contentRect;
        }

        // A transparent vertical group used to keep a related cluster together, so the whole cluster can
        // be shown or hidden as one (the screenshot block, for instance).
        private static GameObject CreateSection(Transform parent, string name, PlayProbeUiTheme theme)
        {
            GameObject section = CreateUiObject(name, parent, typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));

            VerticalLayoutGroup layout = section.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            section.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return section;
        }

        private static PlayProbeTagSelector CreateTagSelector(Transform parent, string heading,
            PlayProbeUiTheme theme)
        {
            GameObject section = CreateSection(parent, "Tags", theme);

            TextMeshProUGUI headingLabel = CreateLabel(section.transform, "Heading", heading,
                theme.fontSizeCaption, theme.textMuted, theme);

            RectTransform chips = CreateWrapRow(section.transform, "Chips", 8f);

            PlayProbeTagSelector selector = section.AddComponent<PlayProbeTagSelector>();
            Wire(selector, "heading", headingLabel);
            Wire(selector, "chipContainer", chips);
            // chipPrefab points at the shared PlayProbeTagChip asset; resolved in the cross-reference pass.

            return selector;
        }

        // For graphics whose colour is driven entirely from script. Every state is plain white so the
        // Selectable's multiply is a no-op.
        private static ColorBlock NeutralColorBlock()
        {
            ColorBlock block = ColorBlock.defaultColorBlock;
            block.normalColor = Color.white;
            block.highlightedColor = Color.white;
            block.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            block.selectedColor = Color.white;
            block.disabledColor = Color.white;
            block.fadeDuration = 0.08f;
            return block;
        }

        private static ColorBlock MakeSelectableColors(PlayProbeUiTheme theme, Color? baseColor = null)
        {
            Color resting = baseColor ?? theme.surfaceRaised;
            ColorBlock block = ColorBlock.defaultColorBlock;
            block.normalColor = resting;
            block.highlightedColor = new Color(
                Mathf.Clamp01(resting.r + 0.08f), Mathf.Clamp01(resting.g + 0.08f),
                Mathf.Clamp01(resting.b + 0.08f), resting.a);
            block.pressedColor = new Color(
                Mathf.Clamp01(resting.r - 0.05f), Mathf.Clamp01(resting.g - 0.05f),
                Mathf.Clamp01(resting.b - 0.05f), resting.a);
            block.selectedColor = resting;
            block.disabledColor = new Color(resting.r, resting.g, resting.b, resting.a * 0.4f);
            block.fadeDuration = 0.1f;
            return block;
        }

        private static Sprite[] LoadEmojiSprites(int count)
        {
            Object[] all = AssetDatabase.LoadAllAssetRepresentationsAtPath(EmojiTexturePath);

            if (all == null || all.Length == 0)
            {
                return null;
            }

            List<Sprite> sprites = new();
            foreach (Object candidate in all)
            {
                if (candidate is Sprite sprite)
                {
                    sprites.Add(sprite);
                }
            }

            if (sprites.Count < count)
            {
                return null;
            }

            // Spread the picks across the sheet so the five faces are visibly different expressions
            // rather than five near-identical neighbours.
            Sprite[] picked = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                picked[i] = sprites[Mathf.Clamp(i * sprites.Count / count, 0, sprites.Count - 1)];
            }

            return picked;
        }

        #endregion

        #region Cross-prefab references

        // Two prefabs point at other prefabs as assets: the tag selector needs the chip, and multiple
        // choice needs the option button. Those targets must exist on disk first, so the references are
        // patched in afterwards rather than during construction.
        private static void LinkCrossPrefabReferences(PlayProbeUiTheme theme)
        {
            PlayProbeTagChip chipPrefab = LoadPrefabComponent<PlayProbeTagChip>("PlayProbeTagChip");
            PlayProbeSelectableButton optionPrefab =
                LoadPrefabComponent<PlayProbeSelectableButton>("PlayProbeSelectableButton");

            LinkInPrefab<PlayProbeTagSelector>("PlayProbeFeedbackCanvas", "chipPrefab", chipPrefab);
            LinkInPrefab<PlayProbeTagSelector>("PlayProbeTextQuestion", "chipPrefab", chipPrefab);
            LinkInPrefab<MultipleChoiceQuestion>("PlayProbeMultipleOptions", "selectionButton", optionPrefab);
        }

        private static T LoadPrefabComponent<T>(string prefabName) where T : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ResourcesDirectory}/{prefabName}.prefab");
            return prefab != null ? prefab.GetComponent<T>() : null;
        }

        private static void LinkInPrefab<T>(string prefabName, string fieldName, Object value) where T : Component
        {
            if (value == null)
            {
                return;
            }

            string path = $"{ResourcesDirectory}/{prefabName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                return;
            }

            T[] targets = prefab.GetComponentsInChildren<T>(true);
            if (targets.Length == 0)
            {
                return;
            }

            foreach (T target in targets)
            {
                SerializedObject serialized = new SerializedObject(target);
                SerializedProperty property = serialized.FindProperty(fieldName);

                if (property == null)
                {
                    continue;
                }

                property.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SavePrefabAsset(prefab);
        }

        #endregion

        #region Serialized field wiring

        // The controllers keep their fields private, which is right for a public API but means the
        // builder cannot assign them directly. SerializedObject is the supported way in.
        private static void Wire(Component target, string fieldName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogWarning($"[PlayProbe] {target.GetType().Name} has no serialized field '{fieldName}'.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireArray(Component target, string fieldName, Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null || !property.isArray)
            {
                Debug.LogWarning($"[PlayProbe] {target.GetType().Name} has no serialized array '{fieldName}'.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireBool(Component target, string fieldName, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogWarning($"[PlayProbe] {target.GetType().Name} has no serialized field '{fieldName}'.");
                return;
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireColor(Component target, string fieldName, Color value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogWarning($"[PlayProbe] {target.GetType().Name} has no serialized field '{fieldName}'.");
                return;
            }

            property.colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion
    }
}
