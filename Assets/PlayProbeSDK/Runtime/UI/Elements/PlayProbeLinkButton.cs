// Copyright PlayProbe.io 2026. All rights reserved

using UnityEngine;
using UnityEngine.UI;

namespace PlayProbe
{
    /// <summary>
    /// A button that opens a policy page in the player's browser. Drop it on any uGUI
    /// <see cref="Button"/> — it wires its own click handler, so you do not add one in the inspector.
    ///
    /// Used by the built-in feedback popup and consent dialog for the "Privacy policy" link, but it is
    /// public so you can reuse it anywhere in your own menus:
    ///
    /// <code>
    /// // In an options screen, link the policy the player agreed to:
    /// linkButton.target = PlayProbeLinkButton.LinkTarget.DeveloperPrivacyPolicy;
    /// </code>
    ///
    /// When the target resolves to no URL — typically because <c>privacyPolicyUrl</c> is blank in
    /// <see cref="PlayProbeConfig"/> — the button hides itself instead of showing a dead control.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class PlayProbeLinkButton : MonoBehaviour
    {
        /// <summary>Which page the button opens.</summary>
        public enum LinkTarget
        {
            /// <summary>Your game's privacy policy, from <c>privacyPolicyUrl</c> in the config.</summary>
            DeveloperPrivacyPolicy = 0,

            /// <summary>PlayProbe's own privacy policy (we are your processor, not the controller).</summary>
            PlayProbePrivacyPolicy = 1,

            /// <summary>PlayProbe's terms of service.</summary>
            PlayProbeTerms = 2,

            /// <summary>The literal URL in <see cref="customUrl"/>.</summary>
            CustomUrl = 3,
        }

        /// <summary>PlayProbe's public privacy policy.</summary>
        public const string PlayProbePrivacyUrl = "https://playprobe.io/privacy";

        /// <summary>PlayProbe's public terms of service.</summary>
        public const string PlayProbeTermsUrl = "https://playprobe.io/terms";

        [Tooltip("Which page this button opens.")]
        [SerializeField] private LinkTarget target = LinkTarget.DeveloperPrivacyPolicy;

        [Tooltip("Used only when Target is Custom Url. Must be http or https.")]
        [SerializeField] private string customUrl = "";

        [Tooltip("Hide the button when its target resolves to no URL, rather than showing a dead link.")]
        [SerializeField] private bool hideWhenUnavailable = true;

        private Button _button;

        /// <summary>Which page this button opens. Changing it re-evaluates whether the button is shown.</summary>
        public LinkTarget Target
        {
            get => target;
            set
            {
                target = value;
                Refresh();
            }
        }

        /// <summary>
        /// The URL this button will open, or <c>null</c> when the target is not configured. Useful if
        /// you want to show the link as text somewhere else.
        /// </summary>
        public string ResolvedUrl
        {
            get
            {
                switch (target)
                {
                    case LinkTarget.PlayProbePrivacyPolicy:
                        return PlayProbePrivacyUrl;
                    case LinkTarget.PlayProbeTerms:
                        return PlayProbeTermsUrl;
                    case LinkTarget.CustomUrl:
                        return string.IsNullOrWhiteSpace(customUrl) ? null : customUrl.Trim();
                    default:
                        return PlayProbeManager.Instance != null
                            ? PlayProbeManager.Instance.PrivacyPolicyUrl
                            : null;
                }
            }
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(Open);
        }

        private void OnEnable()
        {
            Refresh();
        }

        /// <summary>Opens the resolved URL. Wired to the button's click; safe to call directly too.</summary>
        public void Open()
        {
            PlayProbeUi.OpenExternalUrl(ResolvedUrl);
        }

        /// <summary>
        /// Re-checks whether a URL is available and shows or hides the button accordingly. Call this if
        /// you change the config at runtime.
        /// </summary>
        public void Refresh()
        {
            if (!hideWhenUnavailable)
            {
                return;
            }

            bool available = !string.IsNullOrWhiteSpace(ResolvedUrl);
            if (gameObject.activeSelf != available)
            {
                gameObject.SetActive(available);
            }
        }
    }
}
