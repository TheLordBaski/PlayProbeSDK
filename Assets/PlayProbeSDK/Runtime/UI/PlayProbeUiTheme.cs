// Copyright PlayProbe.io 2026. All rights reserved

using UnityEngine;

namespace PlayProbe
{
    /// <summary>
    /// Colours, sizes and text used by every built-in PlayProbe screen (the feedback popup, the
    /// survey, the handoff-token screen, the consent dialog and the toast).
    ///
    /// This is the one place to restyle the SDK's UI. Edit the asset at
    /// <c>Assets/Resources/PlayProbeUiTheme.asset</c>, then run
    /// <c>Tools &gt; PlayProbe &gt; UI &gt; Rebuild Prefabs</c> to regenerate the prefabs with your
    /// values. Nothing else in the SDK needs to change.
    ///
    /// If no asset exists, the SDK falls back to <see cref="Default"/> (the PlayProbe dark theme), so
    /// the UI always renders even in a project that never created one.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayProbeUiTheme", menuName = "PlayProbe/UI Theme")]
    public class PlayProbeUiTheme : ScriptableObject
    {
        /// <summary>Resources path the SDK loads the theme from. Case-sensitive on some platforms.</summary>
        public const string ResourcePath = "PlayProbeUiTheme";

        private static PlayProbeUiTheme _cached;
        private static bool _lookupDone;

        #region Palette

        [Header("Palette")]
        [Tooltip("Dimmed backdrop behind a modal screen. The alpha is what makes the game visible through it.")]
        public Color scrim = new Color32(0x05, 0x05, 0x09, 0xD9);

        [Tooltip("Panel / card fill.")]
        public Color surface = new Color32(0x12, 0x12, 0x17, 0xFF);

        [Tooltip("Fill for controls sitting on top of a panel (inputs, unselected chips).")]
        public Color surfaceRaised = new Color32(0x20, 0x20, 0x27, 0xFF);

        [Tooltip("Hairline borders and dividers.")]
        public Color border = new Color32(0x2A, 0x2A, 0x32, 0xFF);

        [Tooltip("Brand colour. Primary buttons, selected states, focus rings.")]
        public Color primary = new Color32(0x79, 0x3C, 0xDD, 0xFF);

        [Tooltip("Secondary brand colour. Links and accents.")]
        public Color accent = new Color32(0x21, 0xD5, 0xED, 0xFF);

        [Tooltip("Errors and destructive actions.")]
        public Color danger = new Color32(0xDC, 0x28, 0x28, 0xFF);

        [Tooltip("Confirmation states.")]
        public Color success = new Color32(0x22, 0xC5, 0x5E, 0xFF);

        #endregion

        #region Text

        [Header("Text")]
        [Tooltip("Headings and body copy.")]
        public Color textPrimary = new Color32(0xF2, 0xF2, 0xF2, 0xFF);

        [Tooltip("Helper text, placeholders, captions.")]
        public Color textMuted = new Color32(0x87, 0x87, 0x92, 0xFF);

        [Tooltip("Text drawn on top of the primary colour.")]
        public Color textOnPrimary = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

        [Tooltip("Leave empty to use the project's TextMeshPro default font.")]
        public TMPro.TMP_FontAsset font;

        public float fontSizeTitle = 30f;
        public float fontSizeHeading = 22f;
        public float fontSizeBody = 18f;
        public float fontSizeCaption = 14f;

        #endregion

        #region Metrics

        [Header("Metrics")]
        [Tooltip("Reference resolution the canvas scaler matches. UI scales around this.")]
        public Vector2 referenceResolution = new Vector2(1920f, 1080f);

        [Tooltip("Width of a modal panel, in reference-resolution pixels.")]
        public float panelWidth = 720f;

        [Tooltip("Padding inside a modal panel.")]
        public float panelPadding = 32f;

        [Tooltip("Vertical gap between stacked elements.")]
        public float spacing = 16f;

        [Tooltip("Height of buttons and single-line inputs.")]
        public float controlHeight = 52f;

        [Tooltip("Corner rounding, in reference-resolution pixels. Applied to the sliced sprite.")]
        public float cornerRadius = 10f;

        #endregion

        #region Sprites

        [Header("Sprites")]
        [Tooltip("Leave any slot empty and that shape falls back to Unity's built-in UISprite.\n\n" +
                 "These must be WHITE with an alpha shape: interactive elements tint them through the " +
                 "Button's colour block, so a pre-coloured sprite comes out doubled. Generate a matching " +
                 "set with the SDK's sprite tool, drop them in Assets/unity-sdk/Textures/UI, and the " +
                 "prefab builder fills these in by filename.")]
        public Sprite buttonFill;

        [Tooltip("Ring version of buttonFill — draws the border.")]
        public Sprite buttonOutline;

        [Tooltip("Rounded rectangle with a softer corner. Modal panels.")]
        public Sprite panelFill;

        [Tooltip("Ring version of panelFill.")]
        public Sprite panelOutline;

        [Tooltip("Capsule. Tag chips and the floating feedback button.")]
        public Sprite pillFill;

        [Tooltip("Ring version of pillFill. This is what turns brand-coloured on chip selection.")]
        public Sprite pillOutline;

        [Tooltip("Solid circle.")]
        public Sprite circleFill;

        [Tooltip("Checkmark glyph for the screenshot toggle.")]
        public Sprite checkmark;

        [Tooltip("Speech-bubble glyph for the floating feedback button.")]
        public Sprite chatBubble;

        #endregion

        #region Copy

        [Header("Copy — feedback popup")]
        public string feedbackTitle = "Send feedback";
        public string feedbackSubtitle = "Tell the developer what you ran into. It goes straight to them.";
        public string feedbackTitlePlaceholder = "Short summary (optional)";
        public string feedbackDescriptionPlaceholder = "What happened? What did you expect instead?";
        public string feedbackScreenshotLabel = "Attach a screenshot of my screen";
        public string feedbackTagsLabel = "What is this about? (optional)";
        public string feedbackSubmitLabel = "Send";
        public string feedbackCancelLabel = "Cancel";
        public string feedbackSentMessage = "Thanks — your feedback was sent.";
        public string feedbackEmptyError = "Please describe what happened before sending.";
        public string privacyPolicyLinkLabel = "Privacy policy";

        [Tooltip("Display names for the category buttons, in the same order as " +
                 "PlayProbeFeedback.Categories (bug, suggestion, praise, other). Translate these, " +
                 "not the ids — the backend only accepts the fixed ids.")]
        public string[] feedbackCategoryLabels = { "Bug", "Suggestion", "Praise", "Other" };

        [Header("Copy — survey")]
        public string surveySubmitLabel = "Submit";
        public string surveySkipLabel = "Skip";
        public string surveyIncompleteError = "Please answer the required questions.";
        public string surveySentMessage = "Thanks for the feedback!";
        public string surveyTagsLabel = "Add tags (optional)";

        [Header("Copy — handoff token screen")]
        public string tokenTitle = "Enter your session code";
        public string tokenSubtitle = "Find the 8-character code on the PlayProbe session page and type it in.";
        public string tokenStartLabel = "Start playtest";
        public string tokenPasteLabel = "Paste from clipboard";
        public string tokenInvalidError = "That code was not accepted. Check it and try again.";
        public string tokenIncompleteError = "Enter all 8 characters.";
        public string tokenCheckingLabel = "Checking…";

        [Header("Copy — consent dialog")]
        public string consentTitle = "Help improve this game?";

        [TextArea(3, 8)]
        public string consentBody =
            "We'd like to collect anonymous playtesting data while you play — how long you play, " +
            "frame rate, crashes, and anything you choose to send us as feedback. It is never used " +
            "for advertising and you can change your mind at any time in Settings.";

        public string consentAcceptLabel = "Sure, count me in";
        public string consentDeclineLabel = "No thanks";

        #endregion

        /// <summary>
        /// The theme the SDK should use: the asset at <c>Resources/PlayProbeUiTheme</c> when the
        /// project has one, otherwise a built-in instance with the default PlayProbe styling.
        /// </summary>
        public static PlayProbeUiTheme Default
        {
            get
            {
                if (_cached != null)
                {
                    return _cached;
                }

                if (!_lookupDone)
                {
                    _lookupDone = true;
                    _cached = Resources.Load<PlayProbeUiTheme>(ResourcePath);
                }

                if (_cached == null)
                {
                    // CreateInstance gives every field its declared default, which is the PlayProbe theme.
                    _cached = CreateInstance<PlayProbeUiTheme>();
                    _cached.name = "PlayProbeUiTheme (built-in)";
                }

                return _cached;
            }
        }

        /// <summary>
        /// Drops the cached lookup so the next <see cref="Default"/> call re-reads Resources. Called by
        /// the editor tooling after the asset is created or edited.
        /// </summary>
        public static void InvalidateCache()
        {
            _cached = null;
            _lookupDone = false;
        }
    }
}
