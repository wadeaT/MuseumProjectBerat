using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using Firebase.Firestore;

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

    private async void Start()
    {
        // Start with login panel visible
        loginPanel.SetActive(true);
        questionsPanel.SetActive(false);

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
                Debug.LogError("❌ Firebase failed to initialize after 5 seconds!");
                messageText.text = "❌ Connection error. Please restart the app.";
                messageText.color = Color.red;

                // Disable buttons if Firebase never initializes
                loginButton.interactable = false;
                registerButton.interactable = false;
                return;
            }
        }

        Debug.Log("✅ Firebase ready! Login UI enabled.");
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
            ShowMessage("Please enter both email and password.", Color.yellow);
            return;
        }

        ShowMessage("Logging in...", Color.white);

        bool success = await PlayerManager.Instance.LoginUser(email, password);

        if (success)
        {
            ShowMessage("✅ Login successful!", Color.green);

            // ✅ NEW: Load user's badge and card progress
            if (BadgeManager.instance != null)
            {
                await BadgeManager.instance.LoadProgressFromFirebase();
            }

            await Task.Delay(800);
            ShowQuestionsPanel();
        }
        else
        {
            ShowMessage("❌ Login failed. Check your email or password.", Color.red);
        }
    }

    private async void OnRegisterClicked()
    {
        string email = emailInput.text.Trim();
        string password = passwordInput.text.Trim();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowMessage("Please fill in both fields.", Color.yellow);
            return;
        }

        ShowMessage("Creating account...", Color.white);

        bool success = await PlayerManager.Instance.RegisterUser(email, password);

        if (success)
        {
            ShowMessage("✅ Account created successfully!", Color.green);
            await Task.Delay(800);
            ShowQuestionsPanel();
        }
        else
        {
            ShowMessage("❌ Registration failed. Try again.", Color.red);
        }
    }

    // ------------------------------------------------------------------------
    // QUESTIONS PANEL
    // ------------------------------------------------------------------------

    private async void OnStartClicked()
    {
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
            ShowMessage("Please answer all questions before continuing.", Color.yellow);
            Debug.Log("[LoginUI] Exit NullAnswers");
            return;
        }

        // ✅ 3. Show progress message
        ShowMessage("Saving your answers...", Color.white);
        Debug.Log("[LoginUI] Starting demographic save...");

        // ✅ 4. Validate Firebase and PlayerManager
        if (FirebaseManager.Instance == null)
        {
            Debug.LogError("[LoginUI] FirebaseManager.Instance is null!");
            ShowMessage("❌ Firebase not initialized.", Color.red);
            return;
        }

        if (PlayerManager.Instance == null || string.IsNullOrEmpty(PlayerManager.Instance.userId))
        {
            Debug.LogError("[LoginUI] PlayerManager or userId is null!");
            ShowMessage("❌ User not logged in.", Color.red);
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
            ShowMessage("✅ Data saved! Loading museum...", Color.green);

            // ✅ 6. Delay a bit then load next scene
            await Task.Delay(1200);
            SceneManager.LoadScene(firstMuseumScene);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[LoginUI] ❌ Error saving demographics: " + ex.Message);
            ShowMessage("❌ Failed to save data. Check your internet connection.", Color.red);
        }
    }


    private string GetSelectedOption(ToggleGroup group)
    {
        if (group == null)
        {
            Debug.LogWarning("[LoginUI] ToggleGroup is null!");
            return "";
        }

        var selected = group.ActiveToggles().FirstOrDefault();
        if (selected == null)
        {
            Debug.LogWarning($"[LoginUI] No selected toggle found in group: {group.name}");
            return "";
        }

        // Look specifically for a TextMeshPro label named "Label" under the toggle
        TMP_Text label = selected.transform.Find("Label")?.GetComponent<TMP_Text>();
        if (label != null)
        {
            Debug.Log($"[LoginUI] Selected: {label.text}");
            return label.text;
        }

        // Fallback if structure is different
        label = selected.GetComponentsInChildren<TMP_Text>(true)
                        .FirstOrDefault(t => t.name.ToLower().Contains("label"));
        if (label != null)
        {
            Debug.Log($"[LoginUI] Selected (fallback): {label.text}");
            return label.text;
        }

        Debug.LogWarning($"[LoginUI] Could not find label text for toggle in group {group.name}");
        return "";
    }

    // ------------------------------------------------------------------------
    // UI HELPERS
    // ------------------------------------------------------------------------

    private void ShowQuestionsPanel()
    {
        loginPanel.SetActive(false);
        questionsPanel.SetActive(true);
        messageText.text = "";
    }

    private void ShowMessage(string text, Color color)
    {
        if (messageText != null)
        {
            messageText.text = text;
            messageText.color = color;
        }
    }
}
