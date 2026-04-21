// Copyright PlayProbe.io 2026. All rights reserved

using System;
using System.Collections.Generic;
using PlayProbe.Data;
using UnityEngine;
using UnityEngine.UI;

namespace PlayProbe
{
    // EDITOR: Attach to a Canvas prefab with DarkPanel > Title > QuestionContainer > ButtonRow
    public class SurveyOverlay : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Image _darkPanel;
        [SerializeField] private Text _titleText;
        [SerializeField] private RectTransform _questionContainer;
        [SerializeField] private Button _submitButton;
        [SerializeField] private Button _skipButton;
        [SerializeField] private Font _font;

        private readonly Dictionary<string, object> _answers = new Dictionary<string, object>();
        private readonly List<SurveyQuestion> _questions = new List<SurveyQuestion>();

        private Action<Dictionary<string, object>> _onSubmit;
        private Action _onSkip;

        private void Awake()
        {
            EnsureUi();
        }

        public void Initialize(List<SurveyQuestion> questions, Action<Dictionary<string, object>> onSubmit, Action onSkip)
        {
            EnsureUi();

            _onSubmit = onSubmit;
            _onSkip = onSkip;

            _questions.Clear();

            if (questions != null)
            {
                _questions.AddRange(questions);
            }

            _questions.Sort((left, right) => left.order_index.CompareTo(right.order_index));
            _answers.Clear();

            _canvas.sortingOrder = 999;
            _darkPanel.color = new Color32(0x0F, 0x0F, 0x13, 230);
            _titleText.text = "Quick Question from the Developer";

            RebuildQuestionUi();
            ConfigureButtons();
        }

        private void ConfigureButtons()
        {
            _submitButton.onClick.RemoveAllListeners();
            _submitButton.onClick.AddListener(HandleSubmitClicked);
            SetButtonLabel(_submitButton, "Submit");

            _skipButton.onClick.RemoveAllListeners();
            _skipButton.onClick.AddListener(HandleSkipClicked);
            SetButtonLabel(_skipButton, "Skip");

            PlayProbeConfig config = Resources.Load<PlayProbeConfig>("PlayProbeConfig");
            bool allowDismiss = config == null || config.allowSurveyDismiss;
            _skipButton.gameObject.SetActive(allowDismiss);
        }

        private void HandleSubmitClicked()
        {
            if (!ValidateRequiredAnswers())
            {
                return;
            }

            _onSubmit?.Invoke(new Dictionary<string, object>(_answers));
            Destroy(gameObject);
        }

        private void HandleSkipClicked()
        {
            _onSkip?.Invoke();
            Destroy(gameObject);
        }

        private bool ValidateRequiredAnswers()
        {
            for (int index = 0; index < _questions.Count; index++)
            {
                SurveyQuestion question = _questions[index];

                if (!question.required)
                {
                    continue;
                }

                if (!_answers.TryGetValue(question.id, out object value) || value == null)
                {
                    Debug.LogWarning("[PlayProbe] Required question is unanswered: " + question.id);
                    return false;
                }

                if (value is string textValue && string.IsNullOrWhiteSpace(textValue))
                {
                    Debug.LogWarning("[PlayProbe] Required question is unanswered: " + question.id);
                    return false;
                }
            }

            return true;
        }

        private void RebuildQuestionUi()
        {
            for (int childIndex = _questionContainer.childCount - 1; childIndex >= 0; childIndex--)
            {
                Destroy(_questionContainer.GetChild(childIndex).gameObject);
            }

            for (int index = 0; index < _questions.Count; index++)
            {
                CreateQuestionWidget(_questions[index]);
            }
        }

        private void CreateQuestionWidget(SurveyQuestion question)
        {
            GameObject rootObject = CreateUiObject("Question_" + question.id, _questionContainer);
            VerticalLayoutGroup rootLayout = rootObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.spacing = 8f;
            rootLayout.childControlHeight = false;
            rootLayout.childControlWidth = true;
            rootLayout.childForceExpandHeight = false;
            rootLayout.childForceExpandWidth = true;

            LayoutElement rootLayoutElement = rootObject.AddComponent<LayoutElement>();
            rootLayoutElement.minHeight = 80f;

            CreateText(
                "Label",
                rootObject.transform,
                string.IsNullOrWhiteSpace(question.label) ? "Question" : question.label,
                22,
                FontStyle.Bold,
                TextAnchor.MiddleLeft
            );

            string questionType = string.IsNullOrWhiteSpace(question.question_type)
                ? "text"
                : question.question_type.ToLowerInvariant();

            switch (questionType)
            {
                case "rating":
                    BuildRatingQuestion(rootObject.transform, question);
                    break;

                case "yes_no":
                    BuildSingleChoiceQuestion(rootObject.transform, question, new[] { "Yes", "No" }, true);
                    break;

                case "multiple_choice":
                    BuildSingleChoiceQuestion(rootObject.transform, question, question.options ?? Array.Empty<string>(), false);
                    break;

                case "emoji_scale":
                    BuildEmojiScaleQuestion(rootObject.transform, question);
                    break;

                case "text":
                default:
                    BuildTextQuestion(rootObject.transform, question);
                    break;
            }
        }

        private void BuildRatingQuestion(Transform parent, SurveyQuestion question)
        {
            string[] labels = { "★", "★★", "★★★", "★★★★", "★★★★★" };
            BuildSingleChoiceButtons(parent, labels, true, selectedIndex =>
            {
                _answers[question.id] = selectedIndex + 1;
            });
        }

        private void BuildEmojiScaleQuestion(Transform parent, SurveyQuestion question)
        {
            string[] labels = { "😫", "😐", "😊", "😍", "🤩" };
            BuildSingleChoiceButtons(parent, labels, true, selectedIndex =>
            {
                _answers[question.id] = labels[selectedIndex];
            });
        }

        private void BuildSingleChoiceQuestion(Transform parent, SurveyQuestion question, string[] options, bool horizontal)
        {
            string[] resolvedOptions = options != null && options.Length > 0 ? options : new[] { "Option" };

            BuildSingleChoiceButtons(parent, resolvedOptions, horizontal, selectedIndex =>
            {
                _answers[question.id] = resolvedOptions[selectedIndex];
            });
        }

        private void BuildTextQuestion(Transform parent, SurveyQuestion question)
        {
            InputField inputField = CreateInputField(parent, true);
            inputField.onValueChanged.AddListener(value =>
            {
                _answers[question.id] = value;
            });
        }

        private void BuildSingleChoiceButtons(Transform parent, string[] labels, bool horizontal, Action<int> onSelected)
        {
            GameObject groupObject = CreateUiObject("ChoiceGroup", parent);

            if (horizontal)
            {
                HorizontalLayoutGroup horizontalLayout = groupObject.AddComponent<HorizontalLayoutGroup>();
                horizontalLayout.spacing = 8f;
                horizontalLayout.childControlHeight = true;
                horizontalLayout.childControlWidth = true;
                horizontalLayout.childForceExpandHeight = false;
                horizontalLayout.childForceExpandWidth = true;
            }
            else
            {
                VerticalLayoutGroup verticalLayout = groupObject.AddComponent<VerticalLayoutGroup>();
                verticalLayout.spacing = 8f;
                verticalLayout.childControlHeight = true;
                verticalLayout.childControlWidth = true;
                verticalLayout.childForceExpandHeight = false;
                verticalLayout.childForceExpandWidth = true;
            }

            List<Button> buttons = new List<Button>();

            for (int index = 0; index < labels.Length; index++)
            {
                int capturedIndex = index;
                Button optionButton = CreateButton(
                    "Option_" + index,
                    groupObject.transform,
                    labels[index],
                    new Color(0.21f, 0.25f, 0.33f, 1f)
                );

                optionButton.onClick.AddListener(() =>
                {
                    for (int buttonIndex = 0; buttonIndex < buttons.Count; buttonIndex++)
                    {
                        Image image = buttons[buttonIndex].GetComponent<Image>();

                        if (image != null)
                        {
                            image.color = buttonIndex == capturedIndex
                                ? new Color(0.31f, 0.55f, 0.85f, 1f)
                                : new Color(0.21f, 0.25f, 0.33f, 1f);
                        }
                    }

                    onSelected?.Invoke(capturedIndex);
                });

                buttons.Add(optionButton);
            }
        }

        private void EnsureUi()
        {
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            _canvas = _canvas != null ? _canvas : GetComponent<Canvas>();

            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
            }

            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999;

            CanvasScaler scaler = GetComponent<CanvasScaler>();

            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            if (_darkPanel == null)
            {
                Transform existingPanel = transform.Find("DarkPanel");

                if (existingPanel != null)
                {
                    _darkPanel = existingPanel.GetComponent<Image>();
                }
            }

            if (_darkPanel == null)
            {
                GameObject panelObject = CreateUiObject("DarkPanel", transform);
                _darkPanel = panelObject.AddComponent<Image>();
            }

            RectTransform panelRect = _darkPanel.rectTransform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup panelLayout = _darkPanel.GetComponent<VerticalLayoutGroup>();

            if (panelLayout == null)
            {
                panelLayout = _darkPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            panelLayout.padding = new RectOffset(48, 48, 42, 42);
            panelLayout.spacing = 16f;
            panelLayout.childControlHeight = false;
            panelLayout.childControlWidth = true;
            panelLayout.childForceExpandHeight = false;
            panelLayout.childForceExpandWidth = true;

            if (_titleText == null)
            {
                Transform titleTransform = _darkPanel.transform.Find("Title");

                if (titleTransform != null)
                {
                    _titleText = titleTransform.GetComponent<Text>();
                }
            }

            if (_titleText == null)
            {
                _titleText = CreateText("Title", _darkPanel.transform, "Quick Question from the Developer", 30, FontStyle.Bold, TextAnchor.MiddleLeft);
            }

            if (_questionContainer == null)
            {
                Transform containerTransform = _darkPanel.transform.Find("QuestionContainer");

                if (containerTransform != null)
                {
                    _questionContainer = containerTransform as RectTransform;
                }
            }

            if (_questionContainer == null)
            {
                GameObject containerObject = CreateUiObject("QuestionContainer", _darkPanel.transform);
                _questionContainer = containerObject.GetComponent<RectTransform>();
            }

            VerticalLayoutGroup questionLayout = _questionContainer.GetComponent<VerticalLayoutGroup>();

            if (questionLayout == null)
            {
                questionLayout = _questionContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            questionLayout.spacing = 14f;
            questionLayout.childControlHeight = true;
            questionLayout.childControlWidth = true;
            questionLayout.childForceExpandHeight = false;
            questionLayout.childForceExpandWidth = true;

            ContentSizeFitter questionFitter = _questionContainer.GetComponent<ContentSizeFitter>();

            if (questionFitter == null)
            {
                questionFitter = _questionContainer.gameObject.AddComponent<ContentSizeFitter>();
            }

            questionFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject buttonRow = _submitButton != null ? _submitButton.transform.parent.gameObject : null;

            if (buttonRow == null)
            {
                Transform existingButtonRow = _darkPanel.transform.Find("ButtonRow");

                if (existingButtonRow != null)
                {
                    buttonRow = existingButtonRow.gameObject;
                }
            }

            if (buttonRow == null)
            {
                buttonRow = CreateUiObject("ButtonRow", _darkPanel.transform);
            }

            HorizontalLayoutGroup buttonLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();

            if (buttonLayout == null)
            {
                buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            }

            buttonLayout.spacing = 12f;
            buttonLayout.childControlHeight = true;
            buttonLayout.childControlWidth = true;
            buttonLayout.childForceExpandHeight = false;
            buttonLayout.childForceExpandWidth = true;

            if (_submitButton == null)
            {
                Transform submitTransform = buttonRow.transform.Find("Submit");

                if (submitTransform != null)
                {
                    _submitButton = submitTransform.GetComponent<Button>();
                }
            }

            if (_submitButton == null)
            {
                _submitButton = CreateButton("Submit", buttonRow.transform, "Submit", new Color(0.25f, 0.51f, 0.84f, 1f));
            }

            if (_skipButton == null)
            {
                Transform skipTransform = buttonRow.transform.Find("Skip");

                if (skipTransform != null)
                {
                    _skipButton = skipTransform.GetComponent<Button>();
                }
            }

            if (_skipButton == null)
            {
                _skipButton = CreateButton("Skip", buttonRow.transform, "Skip", new Color(0.24f, 0.26f, 0.30f, 1f));
            }
        }

        private GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject gameObjectInstance = new GameObject(name, typeof(RectTransform));
            gameObjectInstance.transform.SetParent(parent, false);
            return gameObjectInstance;
        }

        private Text CreateText(string name, Transform parent, string value, int size, FontStyle style, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = new Color(0.95f, 0.96f, 0.98f, 1f);
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = value;

            return text;
        }

        private Button CreateButton(string name, Transform parent, string label, Color color)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 56f;

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = color;

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.20f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.5f);
            button.colors = colors;

            Text buttonLabel = CreateText("Label", buttonObject.transform, label, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            RectTransform labelRect = buttonLabel.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 8f);
            labelRect.offsetMax = new Vector2(-8f, -8f);

            return button;
        }

        private InputField CreateInputField(Transform parent, bool multiline)
        {
            GameObject inputObject = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputObject.transform.SetParent(parent, false);

            LayoutElement layoutElement = inputObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = multiline ? 120f : 56f;

            Image image = inputObject.GetComponent<Image>();
            image.color = new Color(0.15f, 0.17f, 0.20f, 1f);

            InputField inputField = inputObject.GetComponent<InputField>();
            inputField.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;

            Text text = CreateText("Text", inputObject.transform, string.Empty, 18, FontStyle.Normal, TextAnchor.UpperLeft);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 10f);
            textRect.offsetMax = new Vector2(-12f, -12f);

            Text placeholder = CreateText("Placeholder", inputObject.transform, "Type your answer...", 18, FontStyle.Italic, TextAnchor.UpperLeft);
            placeholder.color = new Color(0.75f, 0.77f, 0.79f, 0.65f);
            RectTransform placeholderRect = placeholder.rectTransform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(12f, 10f);
            placeholderRect.offsetMax = new Vector2(-12f, -12f);

            inputField.textComponent = text;
            inputField.placeholder = placeholder;

            return inputField;
        }

        private static void SetButtonLabel(Button button, string text)
        {
            if (button == null)
            {
                return;
            }

            Text label = button.GetComponentInChildren<Text>();

            if (label != null)
            {
                label.text = text;
            }
        }
    }
}
