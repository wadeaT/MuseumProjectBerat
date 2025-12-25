using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// LoginUI - WebGL-safe version using coroutines instead of async/await
/// </summary>
public class LoginUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject loginPanel;
    public GameObject questionsPanel;

    [Header("Login UI Elements")]
    public TMP_InputField participantCodeInput;
    public Button loginButton;
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

    private void Start()
    {
        // Start with login panel visible
        if (loginPanel != null) loginPanel.SetActive(true);
        if (questionsPanel != null) questionsPanel.SetActive(false);
        if (LanguageDropDownParent != null) LanguageDropDownParent.SetActive(true);

        if (messageText != null) messageText.text = "Initializing...";

        // Add button listeners
        if (loginButton != null) loginButton.onClick.AddListener(OnLoginButtonClicked);
        if (startButton != null) startButton.onClick.AddListener(OnStartButtonClicked);

        // Wait for Firebase using coroutine (safer for WebGL)
        StartCoroutine(WaitForFirebaseCoroutine());
    }

    /// <summary>
    /// Wait until Firebase is fully initialized (using coroutine for WebGL safety)
    /// </summary>
    private IEnumerator WaitForFirebaseCoroutine()
    {
        float maxWaitTime = 10f;
        float elapsed = 0f;

        while (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
        {
            elapsed += Time.deltaTime;

            if (elapsed >= maxWaitTime)
            {
                Debug.LogError("[LoginUI] Firebase failed to initialize after 10 seconds!");
                ShowMessage("Connection error. Please refresh the page.", Color.red);
                if (loginButton != null) loginButton.interactable = false;
                yield break;
            }

            yield return null;
        }

        Debug.Log("[LoginUI] Firebase ready! Login UI enabled.");
        ShowMessage("", Color.white);
        if (loginButton != null) loginButton.interactable = true;
    }

    // ------------------------------------------------------------------------
    // LOGIN BUTTON
    // ------------------------------------------------------------------------

    private void OnLoginButtonClicked()
    {
        StartCoroutine(LoginCoroutine());
    }

    private IEnumerator LoginCoroutine()
    {
        Debug.Log("[LoginUI] === LOGIN CLICKED ===");

        // Get participant code
        string code = participantCodeInput != null ? participantCodeInput.text.Trim() : "";

        // Validate input
        if (string.IsNullOrEmpty(code))
        {
            ShowMessage("Please enter your participant code.", Color.yellow);
            yield break;
        }

        if (code.Length < minCodeLength)
        {
            ShowMessage($"Code must be at least {minCodeLength} characters.", Color.yellow);
            yield break;
        }

        if (code.Length > maxCodeLength)
        {
            ShowMessage($"Code must be {maxCodeLength} characters or less.", Color.yellow);
            yield break;
        }

        ShowMessage("Logging in...", Color.white);
        if (loginButton != null) loginButton.interactable = false;

        // Check PlayerManager exists
        if (PlayerManager.Instance == null)
        {
            Debug.LogError("[LoginUI] PlayerManager.Instance is NULL!");
            ShowMessage("Error: PlayerManager not found.", Color.red);
            if (loginButton != null) loginButton.interactable = true;
            yield break;
        }

        // Call PlayerManager with participant code
        Debug.Log("[LoginUI] Calling LoginWithParticipantCode...");

        bool loginCompleted = false;
        bool loginSuccess = false;

        PlayerManager.Instance.LoginWithParticipantCode(code, (success) =>
        {
            loginSuccess = success;
            loginCompleted = true;
        });

        // Wait for login to complete with timeout
        float timeout = 15f;
        float elapsed = 0f;
        while (!loginCompleted && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!loginCompleted)
        {
            Debug.LogError("[LoginUI] Login timed out!");
            ShowMessage("Login timed out. Please try again.", Color.red);
            if (loginButton != null) loginButton.interactable = true;
            yield break;
        }

        if (loginSuccess)
        {
            Debug.Log("[LoginUI] Login succeeded!");
            ShowMessage("Login successful!", Color.green);

            // Small delay
            yield return new WaitForSeconds(0.5f);

            // Load badge progress (optional, with null check)
            if (BadgeManager.instance != null)
            {
                Debug.Log("[LoginUI] Loading badge progress...");
                bool badgeLoadComplete = false;

                BadgeManager.instance.LoadProgressFromFirebaseCoroutine(() =>
                {
                    badgeLoadComplete = true;
                });

                // Wait with timeout
                float badgeTimeout = 0f;
                while (!badgeLoadComplete && badgeTimeout < 5f)
                {
                    badgeTimeout += Time.deltaTime;
                    yield return null;
                }

                if (!badgeLoadComplete)
                {
                    Debug.LogWarning("[LoginUI] Badge loading timed out, continuing...");
                }
            }

            // Check if user already has demographics
            string odId = PlayerManager.Instance.userId;
            Debug.Log($"[LoginUI] Checking demographics for user: {odId}");

            bool demographicsCheckComplete = false;
            bool hasDemographics = false;

            FirebaseManager.Instance.CheckUserHasDemographics(odId, (exists) =>
            {
                hasDemographics = exists;
                demographicsCheckComplete = true;
            });

            // Wait with timeout
            float demoTimeout = 0f;
            while (!demographicsCheckComplete && demoTimeout < 5f)
            {
                demoTimeout += Time.deltaTime;
                yield return null;
            }

            if (!demographicsCheckComplete)
            {
                Debug.LogWarning("[LoginUI] Demographics check timed out. Assuming no demographics.");
                hasDemographics = false;
            }

            Debug.Log($"[LoginUI] Demographics check result: {hasDemographics}");

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
            Debug.LogError("[LoginUI] Login failed!");
            ShowMessage("Login failed. Please try again.", Color.red);
            if (loginButton != null) loginButton.interactable = true;
        }
    }

    // ------------------------------------------------------------------------
    // START BUTTON
    // ------------------------------------------------------------------------

    private void OnStartButtonClicked()
    {
        StartCoroutine(StartTourCoroutine());
    }

    private IEnumerator StartTourCoroutine()
    {
        Debug.Log("[LoginUI] === START TOUR CLICKED ===");

        // Collect all answers
        string age = GetSelectedOption(ageGroup);
        string gender = GetSelectedOption(genderGroup);
        string nationality = nationalityInput != null ? nationalityInput.text.Trim() : "";
        string skills = GetSelectedOption(skillsGroup);
        string vr = GetSelectedOption(vrGroup);

        Debug.Log($"[LoginUI] Collected answers -> Age: {age}, Gender: {gender}, Nationality: {nationality}, Skills: {skills}, VR: {vr}");

        // Check for missing answers
        if (string.IsNullOrEmpty(age) || string.IsNullOrEmpty(gender) ||
            string.IsNullOrEmpty(nationality) || string.IsNullOrEmpty(skills) || string.IsNullOrEmpty(vr))
        {
            ShowMessage("Please answer all questions before continuing.", Color.yellow);
            yield break;
        }

        ShowMessage("Saving your answers...", Color.white);
        if (startButton != null) startButton.interactable = false;

        // Validate Firebase and PlayerManager
        if (FirebaseManager.Instance == null)
        {
            Debug.LogError("[LoginUI] FirebaseManager.Instance is null!");
            ShowMessage("Firebase not initialized.", Color.red);
            if (startButton != null) startButton.interactable = true;
            yield break;
        }

        if (PlayerManager.Instance == null || string.IsNullOrEmpty(PlayerManager.Instance.userId))
        {
            Debug.LogError("[LoginUI] PlayerManager or userId is null!");
            ShowMessage("User not logged in.", Color.red);
            if (startButton != null) startButton.interactable = true;
            yield break;
        }

        string odId = PlayerManager.Instance.userId;

        // Save demographics
        Debug.Log("[LoginUI] Calling SaveDemographics...");

        bool saveCompleted = false;
        bool saveSuccess = false;

        FirebaseManager.Instance.SaveDemographics(odId, age, gender, nationality, skills, vr, (success) =>
        {
            saveSuccess = success;
            saveCompleted = true;
        });

        // Wait with timeout
        float timeout = 0f;
        while (!saveCompleted && timeout < 10f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        if (saveCompleted && saveSuccess)
        {
            Debug.Log("[LoginUI] Demographics saved successfully!");
            ShowMessage("Data saved! Loading museum...", Color.green);

            yield return new WaitForSeconds(1f);

            SceneManager.LoadScene(firstMuseumScene);
        }
        else
        {
            Debug.LogError("[LoginUI] Failed to save demographics!");
            ShowMessage("Failed to save data. Please try again.", Color.red);
            if (startButton != null) startButton.interactable = true;
        }
    }

    // ------------------------------------------------------------------------
    // HELPERS
    // ------------------------------------------------------------------------

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

    private void ShowQuestionsPanel()
    {
        Debug.Log("[LoginUI] === SHOWING QUESTIONS PANEL ===");
        if (loginPanel != null) loginPanel.SetActive(false);
        if (LanguageDropDownParent != null) LanguageDropDownParent.SetActive(false);
        if (questionsPanel != null) questionsPanel.SetActive(true);
        ShowMessage("", Color.white);
        if (startButton != null) startButton.interactable = true;
    }

    /// <summary>
    /// Show message directly (no async localization to avoid WebGL issues)
    /// </summary>
    private void ShowMessage(string message, Color color)
    {
        if (messageText != null)
        {
            messageText.text = message;
            messageText.color = color;
        }
    }
}