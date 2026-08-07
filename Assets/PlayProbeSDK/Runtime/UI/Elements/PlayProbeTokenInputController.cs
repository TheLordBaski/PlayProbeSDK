// Copyright PlayProbe.io 2026. All rights reserved

using System;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlayProbe
{
    /// <summary>
    /// The handoff screen: the tester types the short code shown on the PlayProbe session page, and
    /// the SDK validates it and starts the session against that specific dashboard session.
    ///
    /// Shown automatically by <see cref="PlayProbeManager.StartSession"/> when
    /// <c>isStandaloneTest</c> is off. The prefab lives at
    /// <c>Resources/PlayProbeStartSessionScreen</c>; regenerate it with
    /// <c>Tools &gt; PlayProbe &gt; UI &gt; Rebuild Prefabs</c>.
    ///
    /// The code is one character per box, jumps forward as the tester types, jumps back on backspace,
    /// and can be pasted whole.
    /// </summary>
    public class PlayProbeTokenInputController : MonoBehaviour
    {
        [Header("Copy")]
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI subtitleLabel;
        [SerializeField] private TextMeshProUGUI errorLabel;

        [Header("Code entry")]
        [Tooltip("The single-character fields, in order, left to right. The code length is however many you wire up.")]
        [SerializeField] private TMP_InputField[] inputFields = new TMP_InputField[8];

        [Header("Actions")]
        [SerializeField] private Button startSessionButton;
        [SerializeField] private TextMeshProUGUI startSessionButtonLabel;
        [SerializeField] private Button pasteButton;
        [SerializeField] private TextMeshProUGUI pasteButtonLabel;

        [Tooltip("Optional. Shown only when Allow Cancel is on.")]
        [SerializeField] private Button cancelButton;

        [Header("Behaviour")]
        [Tooltip("Read the clipboard on open and fill the boxes when it holds a valid-looking code.")]
        [SerializeField] private bool autoFillFromClipboard = true;

        [Tooltip("Let the tester close this screen without starting a session. Off by default — " +
                 "without a code there is no session to record against.")]
        [SerializeField] private bool allowCancel = false;

        private bool _isChecking;
        private string _startButtonRestingLabel;

        /// <summary>How many characters the code has — one per wired-up input field.</summary>
        public int TokenLength => inputFields != null ? inputFields.Length : 0;

        private void Start()
        {
            PlayProbeUi.EnsureEventSystem();
            PlayProbeUi.ConfigureOverlayCanvas(GetComponent<Canvas>(), PlayProbeUi.SortOrderTokenScreen,
                PlayProbeUiTheme.Default);

            ApplyTheme();
            InitializeInputs();
            ClearError();

            if (startSessionButton != null)
            {
                startSessionButton.onClick.AddListener(OnStartSessionClicked);
            }

            if (pasteButton != null)
            {
                pasteButton.onClick.AddListener(PasteFromClipboard);
            }

            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(allowCancel);
                cancelButton.onClick.AddListener(Close);
            }

            if (autoFillFromClipboard && TryFillFromClipboard())
            {
                FocusStartButton();
            }
            else
            {
                FocusInput(0);
            }
        }

        private void Update()
        {
            HandleBackspaceNavigation();

            if (allowCancel && PlayProbeInput.WasCancelPressedThisFrame())
            {
                Close();
            }
        }

        #region Setup

        private void ApplyTheme()
        {
            PlayProbeUiTheme theme = PlayProbeUiTheme.Default;
            SetText(titleLabel, theme.tokenTitle);
            SetText(subtitleLabel, theme.tokenSubtitle);
            SetText(startSessionButtonLabel, theme.tokenStartLabel);
            SetText(pasteButtonLabel, theme.tokenPasteLabel);
            _startButtonRestingLabel = theme.tokenStartLabel;
        }

        private void InitializeInputs()
        {
            if (inputFields == null)
            {
                return;
            }

            for (int i = 0; i < inputFields.Length; i++)
            {
                TMP_InputField field = inputFields[i];
                if (field == null)
                {
                    continue;
                }

                int index = i;

                field.characterLimit = 1;
                field.onValidateInput = ValidateCharacter;
                field.onValueChanged.AddListener(value => OnInputValueChanged(value, index));
            }
        }

        // Codes are uppercase alphanumerics. Rejecting anything else at the keystroke means the tester
        // cannot end up staring at a box that silently refused their input.
        private static char ValidateCharacter(string text, int charIndex, char addedChar)
        {
            char upper = char.ToUpperInvariant(addedChar);
            bool allowed = (upper >= 'A' && upper <= 'Z') || (upper >= '0' && upper <= '9');
            return allowed ? upper : '\0';
        }

        #endregion

        #region Entry behaviour

        private void OnInputValueChanged(string value, int index)
        {
            ClearError();

            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            if (index < inputFields.Length - 1)
            {
                FocusInput(index + 1);
            }
            else
            {
                FocusStartButton();
            }
        }

        private void HandleBackspaceNavigation()
        {
            if (!PlayProbeInput.WasBackspacePressedThisFrame() || EventSystem.current == null)
            {
                return;
            }

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null)
            {
                return;
            }

            for (int i = 1; i < inputFields.Length; i++)
            {
                if (inputFields[i] == null || selected != inputFields[i].gameObject)
                {
                    continue;
                }

                // Backspacing out of an already-empty box steps back and clears the previous one, which
                // is what every other one-char-per-box code entry does.
                if (string.IsNullOrEmpty(inputFields[i].text) && inputFields[i - 1] != null)
                {
                    inputFields[i - 1].text = string.Empty;
                    FocusInput(i - 1);
                }

                return;
            }
        }

        /// <summary>Fills the boxes from the system clipboard. Wired to the "Paste" button.</summary>
        public void PasteFromClipboard()
        {
            if (!TryFillFromClipboard())
            {
                ShowError(PlayProbeUiTheme.Default.tokenIncompleteError);
                FocusInput(0);
                return;
            }

            ClearError();
            FocusStartButton();
        }

        private bool TryFillFromClipboard()
        {
            string clipboard = PlayProbeInput.ReadClipboard();
            if (string.IsNullOrEmpty(clipboard))
            {
                return false;
            }

            StringBuilder cleaned = new StringBuilder(clipboard.Length);
            foreach (char character in clipboard)
            {
                char upper = char.ToUpperInvariant(character);
                if ((upper >= 'A' && upper <= 'Z') || (upper >= '0' && upper <= '9'))
                {
                    cleaned.Append(upper);
                }
            }

            if (cleaned.Length != TokenLength)
            {
                return false;
            }

            for (int i = 0; i < inputFields.Length; i++)
            {
                if (inputFields[i] != null)
                {
                    inputFields[i].text = cleaned[i].ToString();
                }
            }

            return true;
        }

        private void FocusInput(int index)
        {
            if (inputFields == null || index < 0 || index >= inputFields.Length || inputFields[index] == null)
            {
                return;
            }

            inputFields[index].Select();
            inputFields[index].ActivateInputField();
        }

        private void FocusStartButton()
        {
            if (startSessionButton != null)
            {
                startSessionButton.Select();
            }
        }

        private string GetHandOffToken()
        {
            StringBuilder token = new StringBuilder(TokenLength);

            foreach (TMP_InputField field in inputFields)
            {
                if (field != null)
                {
                    token.Append(field.text);
                }
            }

            return token.ToString();
        }

        #endregion

        #region Submission

        private void OnStartSessionClicked()
        {
            if (_isChecking)
            {
                return;
            }

            _ = TryStartSessionAsync();
        }

        private async Task TryStartSessionAsync()
        {
            string token = GetHandOffToken();

            if (token.Length < TokenLength)
            {
                ShowError(PlayProbeUiTheme.Default.tokenIncompleteError);
                FocusInput(token.Length);
                return;
            }

            SetChecking(true);

            try
            {
                bool isValid = await PlayProbeManager.Instance.CheckHandOffStatus(token);

                if (!isValid)
                {
                    ShowError(PlayProbeUiTheme.Default.tokenInvalidError);
                    SetChecking(false);
                    FocusInput(0);
                    return;
                }

                bool started = await PlayProbeManager.Instance.StartHandOffSession(token);

                if (!started)
                {
                    ShowError(PlayProbeUiTheme.Default.tokenInvalidError);
                    SetChecking(false);
                    return;
                }

                // The session is live — this screen has done its job. Without this the handoff screen
                // stayed on top of the game for the rest of the playtest.
                Close();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayProbe] Failed to start session with token: {exception.Message}");
                ShowError(PlayProbeUiTheme.Default.tokenInvalidError);
                SetChecking(false);
            }
        }

        private void SetChecking(bool checking)
        {
            _isChecking = checking;

            if (startSessionButton != null)
            {
                // interactable, not enabled: disabling the component leaves the button looking live.
                startSessionButton.interactable = !checking;
            }

            if (pasteButton != null)
            {
                pasteButton.interactable = !checking;
            }

            SetText(startSessionButtonLabel,
                checking ? PlayProbeUiTheme.Default.tokenCheckingLabel : _startButtonRestingLabel);
        }

        /// <summary>Closes the screen. The session is unaffected — it either started or it did not.</summary>
        public void Close()
        {
            Destroy(gameObject);
        }

        #endregion

        #region Helpers

        private void ShowError(string message)
        {
            if (errorLabel == null)
            {
                Debug.LogWarning($"[PlayProbe] {message}");
                return;
            }

            errorLabel.SetText(message);
            errorLabel.gameObject.SetActive(true);
        }

        private void ClearError()
        {
            if (errorLabel != null && errorLabel.gameObject.activeSelf)
            {
                errorLabel.gameObject.SetActive(false);
            }
        }

        private static void SetText(TextMeshProUGUI label, string value)
        {
            if (label != null)
            {
                label.SetText(value ?? string.Empty);
            }
        }

        #endregion
    }
}
