using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using Firebase.Firestore;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class LoginUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject loginPanel;
    public GameObject questionsPanel;

    [Header("Login UI Elements")]
    // CHANGED: Single input field for participant code
    public TMP_InputField participantCodeInput;  // ← Was: emailInput + passwordInput
    public Button loginButton;
    // REMOVED: registerButton - not needed for anonymous auth
    public TMP_Text messageText;

    [Header("Questions UI Elements")]
    public ToggleGroup ageGroup;
    public ToggleGroup genderGroup;
    public TMP_InputField nationalityInput;
    public ToggleGroup skillsGroup;
    public ToggleGroup vrGroup;
    public Button startButton;

    [Header("Next Scene Settings")]
    public string firstMuseumScene = "Room1";

    [Header("LoginDropdown")]
    public GameObject LanguageDropDownParent;

    [Header("Participant Code Settings")]
    public int minCodeLength = 2;
    public int maxCodeLength = 20;

    private async void Start()
    {
        // Start with login panel visible
        loginPanel.SetActive(true);
        questionsPanel.SetActive(false);
        LanguageDropDownParent.SetActive(true);

        messageText.text = "Initializing...";

        // Add button listeners
        loginButton.onClick.AddListener(OnLoginClicked);
        // REMOVED: registerButton listener
        startButton.onClick.AddListener(OnStartClicked);

        CheckToggleGroups();

        // Wait for Firebase to be ready
        await WaitForFirebase();

        messageText.text = "";
    }

    /// <summary>
    /// Wait until Firebase is fully initialized
    /// </summary>
    private async Task WaitForFirebase()
    {
        int maxRetries = 50; // 5 seconds max
        int retries = 0;

        while (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
        {
            await Task.Delay(100);
            retries++;

            if (retries >= maxRetries)
            {
                Debug.LogError("Firebase failed to initialize after 5 seconds!");
                messageText.text = "Connection error. Please restart the app.";
                messageText.color = Color.red;

                loginButton.interactable = false;
                return;
            }
        }

        Debug.Log("Firebase ready! Login UI enabled.");
    }

    private void CheckToggleGroups()
    {
        foreach (var group in new[] { ageGroup, genderGroup, skillsGroup, vrGroup })
        {
            if (group == null) continue;

            var toggles = group.GetComponentsInChildren<Toggle>(true);
            Debug.Log($"[CheckToggleGroups] Group '{group.name}' has {toggles.Length} toggles.");

            foreach (var toggle in toggles)
            {
                Debug.Log($"   → {toggle.name} | isOn = {toggle.isOn} | group = {(toggle.group == null ? "❌ none" : toggle.group.name)}");
            }
        }
    }

    // ------------------------------------------------------------------------
    // LOGIN BUTTON (CHANGED: Now uses participant code)
    // ------------------------------------------------------------------------

    private async void OnLoginClicked()
    {
        // Get participant code
        string code = participantCodeInput.text.Trim();

        // Validate input
        if (string.IsNullOrEmpty(code))
        {
            await ShowLocalizedMessage("Please enter your participant code.", Color.yellow);
            return;
        }

        if (code.Length < minCodeLength)
        {
            await ShowLocalizedMessage($"Code must be at least {minCodeLength} characters.", Color.yellow);
            return;
        }

        if (code.Length > maxCodeLength)
        {
            await ShowLocalizedMessage($"Code must be {maxCodeLength} characters or less.", Color.yellow);
            return;
        }

        await ShowLocalizedMessage("Logging in...", Color.white);

        // CHANGED: Call PlayerManager with participant code
        bool success = await PlayerManager.Instance.LoginWithParticipantCode(code);

        if (success)
        {
            await ShowLocalizedMessage("Login successful!", Color.green);

            // Load user's badge and card progress
            if (BadgeManager.instance != null)
            {
                await BadgeManager.instance.LoadProgressFromFirebase();
            }

            await Task.Delay(400);

            // Check if user already has demographics
            string odId = PlayerManager.Instance.userId;
            bool hasDemographics = await FirebaseManager.Instance.UserHasDemographicsAsync(odId);
            
            if (hasDemographics)
            {
                Debug.Log("[LoginUI] Existing user with demographics → go to museum.");
                SceneManager.LoadScene(firstMuseumScene);
            }
            else
            {
                Debug.Log("[LoginUI] No demographics yet → show questions panel.");
                ShowQuestionsPanel();
            }
        }
        else
        {
            await ShowLocalizedMessage("Login failed. Please try again.", Color.red);
        }
    }

    // REMOVED: OnRegisterClicked() - not needed for anonymous auth

    // ------------------------------------------------------------------------
    // QUESTIONS PANEL (unchanged)
    // ------------------------------------------------------------------------

    private async void OnStartClicked()
    {
        Debug.Log("START TOUR CLICKED");
        Debug.Log("[LoginUI] Start button clicked!");

        // Collect all answers
        string age = GetSelectedOption(ageGroup);
        string gender = GetSelectedOption(genderGroup);
        string nationality = nationalityInput.text.Trim();
        string skills = GetSelectedOption(skillsGroup);
        string vr = GetSelectedOption(vrGroup);

        Debug.Log($"[LoginUI] Collected answers -> Age: {age}, Gender: {gender}, Nationality: {nationality}, Skills: {skills}, VR: {vr}");

        // Check for missing answers
        if (string.IsNullOrEmpty(age) || string.IsNullOrEmpty(gender) ||
            string.IsNullOrEmpty(nationality) || string.IsNullOrEmpty(skills) || string.IsNullOrEmpty(vr))
        {
            await ShowLocalizedMessage("Please answer all questions before continuing.", Color.yellow);
            Debug.Log("[LoginUI] Exit NullAnswers");
            return;
        }

        // Show progress message
        await ShowLocalizedMessage("Saving your answers...", Color.white);
        Debug.Log("[LoginUI] Starting demographic save...");

        // Validate Firebase and PlayerManager
        if (FirebaseManager.Instance == null)
        {
            Debug.LogError("[LoginUI] FirebaseManager.Instance is null!");
            await ShowLocalizedMessage("Firebase not initialized.", Color.red);
            return;
        }

        if (PlayerManager.Instance == null || string.IsNullOrEmpty(PlayerManager.Instance.userId))
        {
            Debug.LogError("[LoginUI] PlayerManager or odId is null!");
            await ShowLocalizedMessage("User not logged in.", Color.red);
            return;
        }

        string odId = PlayerManager.Instance.userId;

        try
        {
            // Save data using FirebaseManager's method
            Debug.Log("[LoginUI] Calling SaveDemographicsAsync...");
            await FirebaseManager.Instance.SaveDemographicsAsync(
                odId,
                age,
                gender,
                nationality,
                skills,
                vr
            );

            Debug.Log("[LoginUI] Firestore save completed successfully!");
            await ShowLocalizedMessage("Data saved! Loading museum...", Color.green);

            // Delay a bit then load next scene
            await Task.Delay(1200);
            SceneManager.LoadScene(firstMuseumScene);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[LoginUI] Error saving demographics: " + ex.Message);
            await ShowLocalizedMessage("Failed to save data. Check your internet connection.", Color.red);
        }
    }

    private string GetSelectedOption(ToggleGroup group)
    {
        if (group == null)
        {
            Debug.LogWarning("[LoginUI] ToggleGroup is null!");
            return string.Empty;
        }

        Toggle activeToggle = group.ActiveToggles().FirstOrDefault();

        if (activeToggle == null)
        {
            Debug.LogWarning($"[LoginUI] No toggle selected in group: {group.name}");
            return string.Empty;
        }

        // Search including inactive children
        TMP_Text tmpText = activeToggle.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null)
            return tmpText.text;

        Text legacyText = activeToggle.GetComponentInChildren<Text>(true);
        if (legacyText != null)
            return legacyText.text;

        Debug.LogWarning($"[LoginUI] No text component found, using toggle name: {activeToggle.name}");
        return activeToggle.name;
    }

    // ------------------------------------------------------------------------
    // UI HELPERS
    // ------------------------------------------------------------------------

    private void ShowQuestionsPanel()
    {
        loginPanel.SetActive(false);
        LanguageDropDownParent.SetActive(false);
        questionsPanel.SetActive(true);
        messageText.text = "";
    }

    private async Task ShowLocalizedMessage(string tableKey, Color color)
    {
        if (messageText == null || string.IsNullOrEmpty(tableKey))
            return;

        try
        {
            var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("StatusMessages", tableKey);
            string localizedText = await operation.Task;

            messageText.text = localizedText;
            messageText.color = color;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to get localized string for key '{tableKey}': {ex.Message}");
            messageText.text = tableKey; // Fallback to the key itself
            messageText.color = color;
        }
    }
}