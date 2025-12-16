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
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public Button loginButton;
    public Button registerButton;
    public TMP_Text messageText;

    [Header("Questions UI Elements")]
    public ToggleGroup ageGroup;          // ✅ Replaced TMP_InputField with ToggleGroup
    public ToggleGroup genderGroup;       // ✅ New question
    public TMP_InputField nationalityInput; // ✅ Keep this as text input
    public ToggleGroup skillsGroup;       // ✅ New question
    public ToggleGroup vrGroup;           // ✅ New question
    public Button startButton;

    [Header("Next Scene Settings")]
    public string firstMuseumScene = "Room1"; // Change to your actual first room scene name

    [Header("LoginDropdown")]
    public GameObject LanguageDropDownParent;

    private async void Start()
    {
        // Start with login panel visible
        loginPanel.SetActive(true);
        questionsPanel.SetActive(false);
        LanguageDropDownParent.SetActive(true);

        messageText.text = "Initializing...";

        // Add button listeners
        loginButton.onClick.AddListener(OnLoginClicked);
        registerButton.onClick.AddListener(OnRegisterClicked);
        startButton.onClick.AddListener(OnStartClicked);

        CheckToggleGroups();

        // ✅ NEW: Wait for Firebase to be ready
        await WaitForFirebase();

        messageText.text = ""; // Clear the initializing message
    }

    /// <summary>
    /// Wait until Firebase is fully initialized
    /// </summary>
    private async System.Threading.Tasks.Task WaitForFirebase()
    {
        int maxRetries = 50; // 5 seconds max
        int retries = 0;

        while (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
        {
            await System.Threading.Tasks.Task.Delay(100); // Wait 100ms
            retries++;

            if (retries >= maxRetries)
            {
                Debug.LogError(" Firebase failed to initialize after 5 seconds!");
                messageText.text = " Connection error. Please restart the app.";
                messageText.color = Color.red;

                // Disable buttons if Firebase never initializes
                loginButton.interactable = false;
                registerButton.interactable = false;
                return;
            }
        }

        Debug.Log(" Firebase ready! Login UI enabled.");
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
    // LOGIN & REGISTER BUTTONS
    // ------------------------------------------------------------------------

    private async void OnLoginClicked()
    {
        string email = emailInput.text.Trim();
        string password = passwordInput.text.Trim();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            await ShowLocalizedMessage("Please enter both email and password.", Color.yellow);
            return;
        }

        await ShowLocalizedMessage("Logging in...", Color.white);

        bool success = await PlayerManager.Instance.LoginUser(email, password);

        if (success)
        {
            await ShowLocalizedMessage(" Login successful!", Color.green);

            //NEW: Load user's badge and card progress
            if (BadgeManager.instance != null)
            {
                await BadgeManager.instance.LoadProgressFromFirebase();
            }

            await Task.Delay(400);
            string userId = PlayerManager.Instance.userId;
            bool hasDemographics = await FirebaseManager.Instance.UserHasDemographicsAsync(userId);
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
            await ShowLocalizedMessage(" Login failed. Check your email or password.", Color.red);
        }
    }

    private async void OnRegisterClicked()
    {
        string email = emailInput.text.Trim();
        string password = passwordInput.text.Trim();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            await ShowLocalizedMessage("Please fill in both fields.", Color.yellow);
            return;
        }

        await ShowLocalizedMessage("Creating account...", Color.white);

        bool success = await PlayerManager.Instance.RegisterUser(email, password);

        if (success)
        {
            await ShowLocalizedMessage(" Account created successfully!", Color.green);
            await Task.Delay(800);
            ShowQuestionsPanel();
        }
        else
        {
            await ShowLocalizedMessage(" Registration failed. Try again.", Color.red);
        }
    }

    // ------------------------------------------------------------------------
    // QUESTIONS PANEL
    // ------------------------------------------------------------------------

    private async void OnStartClicked()
    {
        Debug.Log("START TOUR CLICKED");
        Debug.Log("[LoginUI] Start button clicked!");
        // ✅ 1. Collect all answers
        string age = GetSelectedOption(ageGroup);
        string gender = GetSelectedOption(genderGroup);
        string nationality = nationalityInput.text.Trim();
        string skills = GetSelectedOption(skillsGroup);
        string vr = GetSelectedOption(vrGroup);

        Debug.Log($"[LoginUI] Collected answers -> Age: {age}, Gender: {gender}, Nationality: {nationality}, Skills: {skills}, VR: {vr}");

        // ✅ 2. Check for missing answers
        if (string.IsNullOrEmpty(age) || string.IsNullOrEmpty(gender) ||
            string.IsNullOrEmpty(nationality) || string.IsNullOrEmpty(skills) || string.IsNullOrEmpty(vr))
        {
            await ShowLocalizedMessage("Please answer all questions before continuing.", Color.yellow);
            Debug.Log("[LoginUI] Exit NullAnswers");
            return;
        }

        // ✅ 3. Show progress message
        await ShowLocalizedMessage("Saving your answers...", Color.white);
        Debug.Log("[LoginUI] Starting demographic save...");

        // ✅ 4. Validate Firebase and PlayerManager
        if (FirebaseManager.Instance == null)
        {
            Debug.LogError("[LoginUI] FirebaseManager.Instance is null!");
            await ShowLocalizedMessage("❌ Firebase not initialized.", Color.red);
            return;
        }

        if (PlayerManager.Instance == null || string.IsNullOrEmpty(PlayerManager.Instance.userId))
        {
            Debug.LogError("[LoginUI] PlayerManager or userId is null!");
            await ShowLocalizedMessage("❌ User not logged in.", Color.red);
            return;
        }

        string userId = PlayerManager.Instance.userId;

        try
        {
            // ✅ 5. Save data using FirebaseManager’s method
            Debug.Log("[LoginUI] Calling SaveDemographicsAsync...");
            await FirebaseManager.Instance.SaveDemographicsAsync(
                userId,
                age,
                gender,
                nationality,
                skills,
                vr
            );

            Debug.Log("[LoginUI] Firestore save completed successfully!");
            await ShowLocalizedMessage("✅ Data saved! Loading museum...", Color.green);

            // ✅ 6. Delay a bit then load next scene
            await Task.Delay(1200);
            SceneManager.LoadScene(firstMuseumScene);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[LoginUI] ❌ Error saving demographics: " + ex.Message);
            await ShowLocalizedMessage("❌ Failed to save data. Check your internet connection.", Color.red);
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

        // ✅ Search including inactive children
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
            messageText.text = "...";
            messageText.color = color;
        }
    }
}
