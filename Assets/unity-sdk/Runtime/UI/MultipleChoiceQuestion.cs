// Copyright PlayProbe.io 2026. All rights reserved

using System;
using PlayProbe.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayProbe
{
    public class MultipleChoiceQuestion : MonoBehaviour, IPlayProbeQuestion
    {
        [SerializeField] private TextMeshProUGUI questionText;

        [SerializeField] private PlayProbeSelectableButton selectionButton;

        [SerializeField] private RectTransform answersContainer;

        private PlayProbeSelectableButton _selectableButton;
        
        private void Start()
        {
            InitQuestion(new SurveyQuestionSchema()
            {
                label = "What is your favorite color?",
                options = new string[] { "Red", "Green", "Blue", "Yellow", "Purple" },
            });
        }


        public void InitQuestion(SurveyQuestionSchema questionSchema)
        {
            if (questionSchema == null)
            {
                Debug.LogWarning("[PlayProbe] MultipleChoiceQuestion.InitQuestion got null schema.");
                return;
            }

            if (questionText != null)
            {
                questionText.SetText(questionSchema.label ?? string.Empty);
            }

            if (answersContainer == null)
            {
                Debug.LogWarning("[PlayProbe] MultipleChoiceQuestion is missing answers container.");
                return;
            }

            if (selectionButton == null)
            {
                Debug.LogWarning("[PlayProbe] MultipleChoiceQuestion is missing selection button prefab.");
                return;
            }

            ClearAnswers();

            string[] options = questionSchema.options;
            if (options == null || options.Length == 0)
            {
                return;
            }

            for (int i = 0; i < options.Length; i += 2)
            {
                RectTransform row = CreateRow(i / 2);

                SpawnOptionButton(row, options[i], true);

                bool hasSecondOption = i + 1 < options.Length;
                SpawnOptionButton(row, hasSecondOption ? options[i + 1] : string.Empty, hasSecondOption);
            }
        }

        private void ClearAnswers()
        {
            for (int i = answersContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(answersContainer.GetChild(i).gameObject);
            }
        }

        private RectTransform CreateRow(int rowIndex)
        {
            GameObject rowObject = new GameObject($"AnswerRow_{rowIndex}", typeof(RectTransform),
                typeof(HorizontalLayoutGroup));
            RectTransform rowTransform = rowObject.GetComponent<RectTransform>();
            rowTransform.SetParent(answersContainer, false);

            HorizontalLayoutGroup horizontalGroup = rowObject.GetComponent<HorizontalLayoutGroup>();
            horizontalGroup.childAlignment = TextAnchor.MiddleCenter;
            horizontalGroup.childControlWidth = true;
            horizontalGroup.childControlHeight = false;
            horizontalGroup.childForceExpandWidth = true;
            horizontalGroup.childForceExpandHeight = false;
            horizontalGroup.spacing = 50f;
            rowTransform.sizeDelta = new Vector2(0, 75); // Set a fixed height for the row

            return rowTransform;
        }

        private void SpawnOptionButton(RectTransform parent, string label, bool isActive)
        {
            PlayProbeSelectableButton optionButton = Instantiate(selectionButton, parent);
            optionButton.SetLabel(label);
            if (!isActive)
            {
                optionButton.Hide();
            }
            optionButton.button.onClick.AddListener(() => OnOptionSelected(optionButton));
        }

        private void OnOptionSelected(PlayProbeSelectableButton optionButton)
        {
            if (_selectableButton)
            {
                _selectableButton.DeselectButton();
            }
            _selectableButton = optionButton;
            _selectableButton.SelectButton();
        }
    }
}