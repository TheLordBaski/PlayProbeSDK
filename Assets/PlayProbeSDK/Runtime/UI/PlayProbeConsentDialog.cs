// Copyright PlayProbe.io 2026. All rights reserved

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayProbe
{
    /// <summary>
    /// An optional, ready-made consent prompt for games that want one but do not want to build it.
    ///
    /// <b>The SDK never shows this by itself.</b> Inside your game you are the data controller and
    /// PlayProbe is your processor, so the wording, the timing and the legal basis are yours to decide.
    /// You call it, when you decide it is the right moment:
    ///
    /// <code>
    /// if (!PlayProbeManager.Instance.Consent.HasAnswered)
    /// {
    ///     PlayProbeConsentDialog.Show();
    /// }
    /// </code>
    ///
    /// The buttons call <see cref="PlayProbeManager.SetConsent"/> for you, and the decision persists
    /// between runs. Also give players a way back — an options-menu toggle calling <c>SetConsent</c> —
    /// because withdrawing has to be as easy as agreeing.
    ///
    /// <b>Read the default copy before you ship it.</b> The text in <see cref="PlayProbeUiTheme"/>
    /// describes what the SDK collects in plain language, but it cannot know what else your game
    /// collects, what your legal basis is, or which market you are in. Edit it to match your privacy
    /// policy. This dialog is a starting point, not legal advice, and shipping it unchanged does not
    /// make you compliant on its own.
    /// </summary>
    public class PlayProbeConsentDialog : MonoBehaviour
    {
        private const string ResourcePath = "PlayProbeConsentDialog";

        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI bodyLabel;
        [SerializeField] private Button acceptButton;
        [SerializeField] private TextMeshProUGUI acceptButtonLabel;
        [SerializeField] private Button declineButton;
        [SerializeField] private TextMeshProUGUI declineButtonLabel;
        [SerializeField] private PlayProbeLinkButton privacyPolicyButton;
        [SerializeField] private TextMeshProUGUI privacyPolicyButtonLabel;

        private Action<bool> _onDecision;

        /// <summary>Whether a consent dialog is on screen right now.</summary>
        public static bool IsOpen => _current != null;

        private static PlayProbeConsentDialog _current;

        /// <summary>
        /// Spawns the dialog. Does nothing if one is already open.
        /// </summary>
        /// <param name="onDecision">
        /// Optional callback with the player's answer, raised after the SDK has recorded it. Use it to
        /// resume whatever you paused to ask.
        /// </param>
        /// <returns>The spawned dialog, or null when the prefab is missing.</returns>
        public static PlayProbeConsentDialog Show(Action<bool> onDecision = null)
        {
            if (_current != null)
            {
                return _current;
            }

            GameObject prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    "[PlayProbe] No PlayProbeConsentDialog prefab found in Resources. Run " +
                    "Tools > PlayProbe > UI > Rebuild Prefabs, or show your own dialog and call " +
                    "PlayProbeManager.Instance.SetConsent(...).");
                return null;
            }

            PlayProbeUi.EnsureEventSystem();

            GameObject instance = Instantiate(prefab);
            DontDestroyOnLoad(instance);

            PlayProbeConsentDialog dialog = instance.GetComponent<PlayProbeConsentDialog>();
            if (dialog == null)
            {
                Debug.LogWarning("[PlayProbe] The PlayProbeConsentDialog prefab has no dialog component.");
                Destroy(instance);
                return null;
            }

            dialog._onDecision = onDecision;
            _current = dialog;
            return dialog;
        }

        private void Start()
        {
            PlayProbeUi.EnsureEventSystem();
            PlayProbeUi.ConfigureOverlayCanvas(GetComponent<Canvas>(), PlayProbeUi.SortOrderConsent,
                PlayProbeUiTheme.Default);

            PlayProbeUiTheme theme = PlayProbeUiTheme.Default;
            SetText(titleLabel, theme.consentTitle);
            SetText(bodyLabel, theme.consentBody);
            SetText(acceptButtonLabel, theme.consentAcceptLabel);
            SetText(declineButtonLabel, theme.consentDeclineLabel);
            SetText(privacyPolicyButtonLabel, theme.privacyPolicyLinkLabel);

            if (acceptButton != null)
            {
                acceptButton.onClick.AddListener(() => Decide(true));
            }

            if (declineButton != null)
            {
                declineButton.onClick.AddListener(() => Decide(false));
            }

            if (privacyPolicyButton != null)
            {
                privacyPolicyButton.Refresh();
            }
        }

        private void OnDestroy()
        {
            if (_current == this)
            {
                _current = null;
            }
        }

        /// <summary>
        /// Records the decision and closes. Wired to the two buttons; call it directly if you drive the
        /// dialog from elsewhere.
        /// </summary>
        /// <param name="granted">True if the player agreed.</param>
        public void Decide(bool granted)
        {
            if (PlayProbeManager.Instance != null)
            {
                PlayProbeManager.Instance.SetConsent(granted);
            }
            else
            {
                Debug.LogWarning("[PlayProbe] Consent decision could not be recorded: no PlayProbeManager in the scene.");
            }

            Action<bool> callback = _onDecision;
            _onDecision = null;

            Destroy(gameObject);

            try
            {
                callback?.Invoke(granted);
            }
            catch (Exception exception)
            {
                // A throwing game callback must not leave the dialog half-torn-down.
                Debug.LogWarning($"[PlayProbe] Consent callback threw: {exception.Message}");
            }
        }

        private static void SetText(TextMeshProUGUI label, string value)
        {
            if (label != null)
            {
                label.SetText(value ?? string.Empty);
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] 
        private static void ResetStatic(){
            _current = null;
        }
    }
}
