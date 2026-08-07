// Copyright PlayProbe.io 2026. All rights reserved

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayProbe
{
    /// <summary>
    /// The Instant Feedback popup: a title, a description, a category, an optional screenshot with a
    /// preview, optional tags, and the privacy notice the player must see before they send.
    ///
    /// The SDK spawns this from <c>Resources/PlayProbeFeedbackCanvas</c> when
    /// <see cref="PlayProbeManager.OpenFeedback"/> is called — you do not instantiate it yourself.
    /// Regenerate the prefab with <c>Tools &gt; PlayProbe &gt; UI &gt; Rebuild Prefabs</c> after
    /// editing <see cref="PlayProbeUiTheme"/>, or replace the prefab entirely with your own as long as
    /// it carries this component with its fields wired.
    ///
    /// Building a feedback form from scratch instead? You do not need this class — gather your own
    /// title and description and call
    /// <see cref="PlayProbeManager.SubmitFeedback(string, string, string, bool, string[])"/>. Just
    /// display <see cref="PlayProbeFeedback.PrivacyNotice"/> somewhere, because a report sends a
    /// hardware profile and possibly a screenshot.
    /// </summary>
    public class PlayProbeFeedbackCanvas : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI subtitleLabel;

        [Header("Form")]
        [SerializeField] private TMP_InputField titleInput;
        [SerializeField] private TMP_InputField descriptionInput;
        [SerializeField] private TextMeshProUGUI descriptionCounter;

        [Header("Category")]
        [Tooltip("Parent for the category buttons. One is spawned per PlayProbeFeedback.Categories entry.")]
        [SerializeField] private RectTransform categoryContainer;
        [SerializeField] private PlayProbeSelectableButton categoryButtonPrefab;

        [Header("Screenshot")]
        [Tooltip("The whole screenshot block. Hidden when feedbackAllowScreenshot is off in the config.")]
        [SerializeField] private GameObject screenshotSection;
        [SerializeField] private Toggle screenshotToggle;
        [SerializeField] private TextMeshProUGUI screenshotLabel;
        [SerializeField] private RawImage screenshotPreview;
        [SerializeField] private AspectRatioFitter screenshotAspect;

        [Header("Tags")]
        [SerializeField] private PlayProbeTagSelector tagSelector;

        [Header("Privacy")]
        [SerializeField] private TextMeshProUGUI privacyNoticeLabel;
        [SerializeField] private PlayProbeLinkButton privacyPolicyButton;
        [SerializeField] private TextMeshProUGUI privacyPolicyButtonLabel;

        [Header("Actions")]
        [SerializeField] private Button submitButton;
        [SerializeField] private TextMeshProUGUI submitButtonLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TextMeshProUGUI cancelButtonLabel;
        [SerializeField] private TextMeshProUGUI errorLabel;

        [Header("Behaviour")]
        [Tooltip("Close the popup when the player presses Escape.")]
        [SerializeField] private bool closeOnEscape = true;

        private readonly List<PlayProbeSelectableButton> _categoryButtons = new();
        private string _selectedCategory;

        private void Start()
        {
            PlayProbeUi.EnsureEventSystem();
            PlayProbeUi.ConfigureOverlayCanvas(GetComponent<Canvas>(), PlayProbeUi.SortOrderFeedback,
                PlayProbeUiTheme.Default);

            ApplyTheme();
            BuildCategories();
            SetUpScreenshot();
            SetUpPrivacy();

            if (submitButton != null)
            {
                submitButton.onClick.AddListener(OnSubmit);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancel);
            }

            if (descriptionInput != null)
            {
                descriptionInput.characterLimit = PlayProbeFeedback.MaxDescriptionLength;
                descriptionInput.onValueChanged.AddListener(OnDescriptionChanged);
                descriptionInput.Select();
                descriptionInput.ActivateInputField();
            }

            if (titleInput != null)
            {
                titleInput.characterLimit = PlayProbeFeedback.MaxTitleLength;
            }

            ClearError();
            UpdateCounter();
        }

        private void Update()
        {
            if (closeOnEscape && PlayProbeInput.WasCancelPressedThisFrame())
            {
                OnCancel();
            }
        }

        #region Setup

        private void ApplyTheme()
        {
            PlayProbeUiTheme theme = PlayProbeUiTheme.Default;

            SetText(titleLabel, theme.feedbackTitle);
            SetText(subtitleLabel, theme.feedbackSubtitle);
            SetText(screenshotLabel, theme.feedbackScreenshotLabel);
            SetText(submitButtonLabel, theme.feedbackSubmitLabel);
            SetText(cancelButtonLabel, theme.feedbackCancelLabel);
            SetText(privacyPolicyButtonLabel, theme.privacyPolicyLinkLabel);

            SetPlaceholder(titleInput, theme.feedbackTitlePlaceholder);
            SetPlaceholder(descriptionInput, theme.feedbackDescriptionPlaceholder);

            if (tagSelector != null)
            {
                tagSelector.SetHeading(theme.feedbackTagsLabel);
            }
        }

        private void BuildCategories()
        {
            if (categoryContainer == null || categoryButtonPrefab == null)
            {
                return;
            }

            PlayProbeUiTheme theme = PlayProbeUiTheme.Default;
            string[] labels = theme.feedbackCategoryLabels;

            for (int i = 0; i < PlayProbeFeedback.Categories.Length; i++)
            {
                string categoryId = PlayProbeFeedback.Categories[i];
                // Fall back to the id when the theme's label array is short or was cleared, so a
                // half-translated theme still renders usable buttons.
                string label = labels != null && i < labels.Length && !string.IsNullOrWhiteSpace(labels[i])
                    ? labels[i]
                    : categoryId;

                PlayProbeSelectableButton button = Instantiate(categoryButtonPrefab, categoryContainer);
                button.SetLabel(label);
                button.button.onClick.AddListener(() => SelectCategory(categoryId, button));
                _categoryButtons.Add(button);
            }
        }

        private void SetUpScreenshot()
        {
            PlayProbeFeedback feedback = PlayProbeManager.Instance != null
                ? PlayProbeManager.Instance.Feedback
                : null;

            bool allowed = feedback != null && feedback.AllowScreenshot;

            if (screenshotSection != null)
            {
                screenshotSection.SetActive(allowed);
            }

            if (!allowed)
            {
                return;
            }

            if (screenshotToggle != null)
            {
                screenshotToggle.isOn = feedback.ScreenshotDefaultOn;
                screenshotToggle.onValueChanged.AddListener(OnScreenshotToggled);
            }

            Texture2D shot = feedback.PendingScreenshot;
            if (screenshotPreview != null && shot != null)
            {
                screenshotPreview.texture = shot;

                if (screenshotAspect != null && shot.height > 0)
                {
                    screenshotAspect.aspectRatio = (float)shot.width / shot.height;
                }
            }

            OnScreenshotToggled(screenshotToggle == null || screenshotToggle.isOn);
        }

        private void SetUpPrivacy()
        {
            PlayProbeFeedback feedback = PlayProbeManager.Instance != null
                ? PlayProbeManager.Instance.Feedback
                : null;

            SetText(privacyNoticeLabel,
                feedback != null ? feedback.PrivacyNotice : PlayProbeFeedback.DefaultPrivacyNotice);

            // The link button hides itself when the developer never set privacyPolicyUrl.
            if (privacyPolicyButton != null)
            {
                privacyPolicyButton.Refresh();
            }
        }

        #endregion

        #region Interaction

        private void SelectCategory(string categoryId, PlayProbeSelectableButton button)
        {
            _selectedCategory = categoryId;

            foreach (PlayProbeSelectableButton candidate in _categoryButtons)
            {
                if (candidate == button)
                {
                    candidate.SelectButton();
                }
                else
                {
                    candidate.DeselectButton();
                }
            }
        }

        private void OnScreenshotToggled(bool isOn)
        {
            if (screenshotPreview != null)
            {
                // Dim rather than hide, so the layout does not jump when the player changes their mind.
                Color tint = screenshotPreview.color;
                tint.a = isOn ? 1f : 0.25f;
                screenshotPreview.color = tint;
            }
        }

        private void OnDescriptionChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                ClearError();
            }

            UpdateCounter();
        }

        private void UpdateCounter()
        {
            if (descriptionCounter == null)
            {
                return;
            }

            int length = descriptionInput != null && descriptionInput.text != null
                ? descriptionInput.text.Length
                : 0;

            descriptionCounter.SetText($"{length} / {PlayProbeFeedback.MaxDescriptionLength}");
        }

        private void OnSubmit()
        {
            string description = descriptionInput != null ? descriptionInput.text : null;

            if (string.IsNullOrWhiteSpace(description))
            {
                ShowError(PlayProbeUiTheme.Default.feedbackEmptyError);

                if (descriptionInput != null)
                {
                    descriptionInput.Select();
                    descriptionInput.ActivateInputField();
                }

                return;
            }

            PlayProbeManager manager = PlayProbeManager.Instance;
            if (manager == null)
            {
                return;
            }

            // Stop double-sends while the request is in flight. The popup destroys itself immediately
            // after, but a fast double-click can land in the same frame.
            if (submitButton != null)
            {
                submitButton.interactable = false;
            }

            bool attachScreenshot = screenshotToggle != null && screenshotToggle.isOn;
            string[] tagIds = tagSelector != null ? tagSelector.SelectedTagIds : null;
            string title = titleInput != null ? titleInput.text : null;

            PlayProbeToast.Show(PlayProbeUiTheme.Default.feedbackSentMessage);

            // Submit closes the popup and unpauses via PlayProbeFeedback.Cleanup, so this component may
            // be destroyed the moment the call returns — do not touch anything on `this` afterwards.
            manager.SubmitFeedback(title, description, _selectedCategory, attachScreenshot, tagIds);
        }

        private void OnCancel()
        {
            PlayProbeManager manager = PlayProbeManager.Instance;

            if (manager != null && manager.Feedback != null)
            {
                // Goes through the subsystem so the game is unpaused and the screenshot is released.
                manager.Feedback.Cancel();
                return;
            }

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
            if (errorLabel != null)
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

        private static void SetPlaceholder(TMP_InputField input, string value)
        {
            if (input != null && input.placeholder is TextMeshProUGUI placeholder)
            {
                placeholder.SetText(value ?? string.Empty);
            }
        }

        #endregion
    }
}
