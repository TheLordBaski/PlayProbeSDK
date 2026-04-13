using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System.Text.RegularExpressions;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayProbeTokenInputController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Drag the 6 SingleInput fields here IN ORDER (left to right).")]
    [SerializeField] private TMP_InputField[] inputFields = new TMP_InputField[6];
    [SerializeField] private UnityEngine.UI.Button startButton;

    [Header("Token Settings")]
    [Tooltip("Expected token length without the dash")]
    private const int TokenLength = 6;

    private void Start()
    {
        InitializeInputs();
        TryAutoFillFromClipboard();
    }

    private void InitializeInputs()
    {
        for (int i = 0; i < inputFields.Length; i++)
        {
            int index = i; // Local copy for the closure

            // Force character limit to 1 per field
            inputFields[i].characterLimit = 1;
            
            // Add listener for when the user types a character
            inputFields[i].onValueChanged.AddListener((value) => OnInputValueChanged(value, index));
        }
    }

    private void TryAutoFillFromClipboard()
    {
        string clipboardText = GUIUtility.systemCopyBuffer;

        if (string.IsNullOrEmpty(clipboardText))
        {
            FocusInput(0);
            return;
        }

        // Clean the string: Remove dashes, spaces, and make it uppercase
        string cleanedToken = clipboardText.Replace("-", "").Replace(" ", "").ToUpper();

        // Validate pattern: Exactly 6 Alphanumeric characters
        if (cleanedToken.Length == TokenLength && Regex.IsMatch(cleanedToken, @"^[A-Z0-9]+$"))
        {
            FillAllFields(cleanedToken);
            
            if (startButton != null) startButton.Select();
        }
        else
        {
            // Clipboard data isn't a valid token, fallback to manual typing
            FocusInput(0);
        }
    }

    private void FillAllFields(string validToken)
    {
        for (int i = 0; i < inputFields.Length; i++)
        {
            inputFields[i].text = validToken[i].ToString();
        }
    }

    private void OnInputValueChanged(string value, int index)
    {
        // If the field was just cleared (e.g., via Backspace), do nothing here
        if (string.IsNullOrEmpty(value)) return;

        // Force uppercase for consistency
        inputFields[index].text = value.ToUpper();

        // Move to the next input field if not the last one
        if (index < inputFields.Length - 1)
        {
            FocusInput(index + 1);
        }
        else
        {
            // If it's the last field, focus the Start button
            if (startButton != null) startButton.Select();
        }
    }

    private void FocusInput(int index)
    {
        if (index >= 0 && index < inputFields.Length)
        {
            inputFields[index].Select();
            inputFields[index].ActivateInputField();
        }
    }

    private void Update()
    {
        // UX Polish: Handle Backspace navigation across BOTH input systems
        if (WasBackspacePressedThisFrame())
        {
            GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
            
            if (currentSelected != null)
            {
                // Find which input field currently has focus
                for (int i = 1; i < inputFields.Length; i++)
                {
                    if (currentSelected == inputFields[i].gameObject)
                    {
                        // If the current field is empty, jump back to the previous one
                        if (string.IsNullOrEmpty(inputFields[i].text))
                        {
                            FocusInput(i - 1);
                            // Clear the previous field so it's ready to be re-typed
                            inputFields[i - 1].text = ""; 
                        }
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Safely checks for Backspace input using whichever Input Systems are currently enabled in Player Settings.
    /// </summary>
    private bool WasBackspacePressedThisFrame()
    {
        // 1. Check Legacy Input Manager
        #if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            return true;
        }
        #endif

        // 2. Check New Input System
        #if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame)
        {
            return true;
        }
        #endif

        return false;
    }
}