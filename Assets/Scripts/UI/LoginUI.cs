using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// LoginUI - WebGL-safe version using coroutines instead of async/await
/// Now with localized status messages!
/// FIXED: Clears PlayerPrefs on new login to support multiple users on same device/browser
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

    // ========================================================================
    // LOCALIZED MESSAGES
    // ========================================================================
    [Header("Localized Messages")]
    [Tooltip("Message shown while initializing")]
    public LocalizedString msgInitializing;

    [Tooltip("Connection error message")]
    public LocalizedString msgConnectionError;

    [Tooltip("Please enter participant code")]
    public LocalizedString msgEnterCode;

    [Tooltip("Code too short message (use {0} for min length)")]
    public LocalizedString msgCodeTooShort;

    [Tooltip("Code too long message (use {0} for max length)")]
    public LocalizedString msgCodeTooLong;

    [Tooltip("Logging in message")]
    public LocalizedString msgLoggingIn;

    [Tooltip("Login successful message")]
    public LocalizedString msgLoginSuccess;

    [Tooltip("Login failed message")]
    public LocalizedString msgLoginFailed;

    [Tooltip("Login timeout message")]
    public LocalizedString msgLoginTimeout;

    [Tooltip("PlayerManager not found error")]
    public LocalizedString msgPlayerManagerError;

    [Tooltip("Please answer all questions")]
    public LocalizedString msgAnswerAllQuestions;

    [Tooltip("Saving answers message")]
    public LocalizedString msgSavingAnswers;

    [Tooltip("Firebase not initialized error")]
    public LocalizedString msgFirebaseError;

    [Tooltip("User not logged in error")]
    public LocalizedString msgUserNotLoggedIn;

    [Tooltip("Save failed message")]
    public LocalizedString msgSaveFailed;

    [Tooltip("Data saved, loading museum message")]
    public LocalizedString msgDataSaved;

    [Tooltip("Loading progress message")]
    public LocalizedString msgLoadingProgress;

    // Cache for localized strings (loaded at start)
    private Dictionary<string, string> localizedCache = new Dictionary<string, string>();
    private bool localizationReady = false;

    private void Start()
    {
        // Start with login panel visible
        if (loginPanel != null) loginPanel.SetActive(true);
        if (questionsPanel != null) questionsPanel.SetActive(false);
        if (LanguageDropDownParent != null) LanguageDropDownParent.SetActive(true);

        // Show initializing message (fallback to English until localization loads)
        if (messageText != null) messageText.text = "Initializing...";

        // Add button listeners
        if (loginButton != null) loginButton.onClick.AddListener(OnLoginButtonClicked);
        if (startButton != null) startButton.onClick.AddListener(OnStartButtonClicked);

        // Initialize localization first, then wait for Firebase
        StartCoroutine(InitializeCoroutine());

        // Listen for language changes
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    /// <summary>
    /// Called when user changes language - reload all cached strings
    /// </summary>
    private void OnLocaleChanged(Locale newLocale)
    {
        Debug.Log($"[LoginUI] Language changed to: {newLocale.LocaleName}");
        StartCoroutine(LoadLocalizedStringsCoroutine());
    }

    /// <summary>
    /// Initialize localization and Firebase
    /// </summary>
    private IEnumerator InitializeCoroutine()
    {
        // Wait for localization system to initialize
        yield return LocalizationSettings.InitializationOperation;

        // Load all localized strings
        yield return StartCoroutine(LoadLocalizedStringsCoroutine());

        // Now wait for Firebase
        yield return StartCoroutine(WaitForFirebaseCoroutine());
    }

    /// <summary>
    /// Load all localized strings into cache
    /// </summary>
    private IEnumerator LoadLocalizedStringsCoroutine()
    {
        localizationReady = false;
        localizedCache.Clear();

        // List of all messages to load
        var messagesToLoad = new List<(string key, LocalizedString localizedString)>
        {
            ("initializing", msgInitializing),
            ("connectionError", msgConnectionError),
            ("enterCode", msgEnterCode),
            ("codeTooShort", msgCodeTooShort),
            ("codeTooLong", msgCodeTooLong),
            ("loggingIn", msgLoggingIn),
            ("loginSuccess", msgLoginSuccess),
            ("loginFailed", msgLoginFailed),
            ("loginTimeout", msgLoginTimeout),
            ("playerManagerError", msgPlayerManagerError),
            ("answerAllQuestions", msgAnswerAllQuestions),
            ("savingAnswers", msgSavingAnswers),
            ("firebaseError", msgFirebaseError),
            ("userNotLoggedIn", msgUserNotLoggedIn),
            ("saveFailed", msgSaveFailed),
            ("dataSaved", msgDataSaved),
            ("loadingProgress", msgLoadingProgress)
        };

        int loadedCount = 0;
        int totalCount = messagesToLoad.Count;

        foreach (var (key, localizedString) in messagesToLoad)
        {
            if (localizedString != null && !localizedString.IsEmpty)
            {
                var op = localizedString.GetLocalizedStringAsync();
                op.Completed += (handle) =>
                {
                    localizedCache[key] = handle.Result;
                    loadedCount++;
                };
            }
            else
            {
                // Use fallback English text
                localizedCache[key] = GetFallbackText(key);
                loadedCount++;
            }
        }

        // Wait for all to load with timeout
        float timeout = 0f;
        while (loadedCount < totalCount && timeout < 5f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        localizationReady = true;
        Debug.Log($"[LoginUI] Loaded {loadedCount}/{totalCount} localized strings");

        // Update the initializing message if still showing
        if (messageText != null && messageText.text == "Initializing...")
        {
            ShowLocalizedMessage("initializing", Color.white);
        }
    }

    /// <summary>
    /// Fallback English text for messages
    /// </summary>
    private string GetFallbackText(string key)
    {
        return key switch
        {
            "initializing" => "Initializing...",
            "connectionError" => "Connection error. Please refresh the page.",
            "enterCode" => "Please enter your participant code.",
            "codeTooShort" => "Code must be at least {0} characters.",
            "codeTooLong" => "Code must be {0} characters or less.",
            "loggingIn" => "Logging in...",
            "loginSuccess" => "Login successful!",
            "loginFailed" => "Login failed. Please try again.",
            "loginTimeout" => "Login timed out. Please try again.",
            "playerManagerError" => "Error: PlayerManager not found.",
            "answerAllQuestions" => "Please answer all questions before continuing.",
            "savingAnswers" => "Saving your answers...",
            "firebaseError" => "Firebase not initialized.",
            "userNotLoggedIn" => "User not logged in.",
            "saveFailed" => "Failed to save data. Please try again.",
            "dataSaved" => "Data saved! Loading museum...",
            "loadingProgress" => "Loading your progress...",
            _ => key
        };
    }

    /// <summary>
    /// Get cached localized string, with optional format arguments
    /// </summary>
    private string GetLocalizedString(string key, params object[] args)
    {
        string text = localizedCache.ContainsKey(key) ? localizedCache[key] : GetFallbackText(key);

        if (args != null && args.Length > 0)
        {
            try
            {
                return string.Format(text, args);
            }
            catch
            {
                return text;
            }
        }

        return text;
    }

    /// <summary>
    /// Show a localized message
    /// </summary>
    private void ShowLocalizedMessage(string key, Color color, params object[] args)
    {
        if (messageText != null)
        {
            messageText.text = GetLocalizedString(key, args);
            messageText.color = color;
        }
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
                ShowLocalizedMessage("connectionError", Color.red);
                if (loginButton != null) loginButton.interactable = false;
                yield break;
            }

            yield return null;
        }

        Debug.Log("[LoginUI] Firebase ready! Login UI enabled.");
        ShowLocalizedMessage("", Color.white); // Clear message
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
            ShowLocalizedMessage("enterCode", Color.yellow);
            yield break;
        }

        if (code.Length < minCodeLength)
        {
            ShowLocalizedMessage("codeTooShort", Color.yellow, minCodeLength);
            yield break;
        }

        if (code.Length > maxCodeLength)
        {
            ShowLocalizedMessage("codeTooLong", Color.yellow, maxCodeLength);
            yield break;
        }

        ShowLocalizedMessage("loggingIn", Color.white);
        if (loginButton != null) loginButton.interactable = false;

        // =====================================================================
        // CRITICAL FIX: Clear ALL local data before new login
        // This ensures User 2 doesn't inherit User 1's card progress
        // =====================================================================
        Debug.Log("[LoginUI] Clearing previous user's local data...");
        ClearLocalUserData();

        // Also clear BadgeManager's in-memory state
        if (BadgeManager.instance != null)
        {
            BadgeManager.instance.ClearForNewUser();
        }

        // Check PlayerManager exists
        if (PlayerManager.Instance == null)
        {
            Debug.LogError("[LoginUI] PlayerManager.Instance is NULL!");
            ShowLocalizedMessage("playerManagerError", Color.red);
            if (loginButton != null) loginButton.interactable = true;
            yield break;
        }

        // Clear PlayerManager's in-memory data as well
        PlayerManager.Instance.ClearPlayerData();

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
            ShowLocalizedMessage("loginTimeout", Color.red);
            if (loginButton != null) loginButton.interactable = true;
            yield break;
        }

        if (loginSuccess)
        {
            Debug.Log("[LoginUI] Login succeeded!");
            ShowLocalizedMessage("loadingProgress", Color.green);

            // Small delay
            yield return new WaitForSeconds(0.3f);

            // =====================================================================
            // CRITICAL FIX: Load THIS user's card progress from Firebase
            // and restore to PlayerPrefs
            // =====================================================================
            string odId = PlayerManager.Instance.userId;
            Debug.Log($"[LoginUI] Loading card progress for user: {odId}");

            yield return StartCoroutine(LoadAndRestoreUserCardProgress(odId));

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
            ShowLocalizedMessage("loginFailed", Color.red);
            if (loginButton != null) loginButton.interactable = true;
        }
    }

    // ------------------------------------------------------------------------
    // CLEAR LOCAL USER DATA - Critical for multi-user support
    // ------------------------------------------------------------------------

    /// <summary>
    /// Clears all locally stored user data (PlayerPrefs) to support multiple users
    /// on the same device/browser
    /// </summary>
    private void ClearLocalUserData()
    {
        // Clear ALL PlayerPrefs - this ensures clean slate for new user
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Debug.Log("[LoginUI] All local PlayerPrefs cleared for new user session");
    }

    // ------------------------------------------------------------------------
    // LOAD AND RESTORE USER CARD PROGRESS FROM FIREBASE
    // ------------------------------------------------------------------------

    /// <summary>
    /// Loads the user's collected cards from Firebase and restores them to PlayerPrefs
    /// so HiddenCard.CheckIfAlreadyDiscovered() works correctly
    /// </summary>
    private IEnumerator LoadAndRestoreUserCardProgress(string odId)
    {
        if (FirebaseManager.Instance == null)
        {
            Debug.LogWarning("[LoginUI] FirebaseManager not available, skipping card restore");
            yield break;
        }

        bool loadComplete = false;
        List<CardDocumentData> userCards = null;

        FirebaseManager.Instance.LoadCardsWithData(odId, (cards) =>
        {
            userCards = cards;
            loadComplete = true;
        });

        // Wait with timeout
        float timeout = 0f;
        while (!loadComplete && timeout < 5f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        if (!loadComplete)
        {
            Debug.LogWarning("[LoginUI] Card loading timed out");
            yield break;
        }

        if (userCards == null || userCards.Count == 0)
        {
            Debug.Log("[LoginUI] No previously collected cards for this user");
            yield break;
        }

        // Restore each collected card to PlayerPrefs
        int restoredCount = 0;
        foreach (var card in userCards)
        {
            if (card.found)
            {
                string prefKey = $"Card_{card.cardId}_Found";
                PlayerPrefs.SetInt(prefKey, 1);
                restoredCount++;
                Debug.Log($"[LoginUI] Restored card to PlayerPrefs: {card.cardId}");
            }
        }

        PlayerPrefs.Save();
        Debug.Log($"[LoginUI] Restored {restoredCount} collected cards from Firebase to PlayerPrefs");

        // Also update PlayerManager's in-memory data
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.totalCardsCollected = restoredCount;
            foreach (var card in userCards)
            {
                if (card.found && !PlayerManager.Instance.cardsFound.Contains(card.cardId))
                {
                    PlayerManager.Instance.cardsFound.Add(card.cardId);
                }
            }
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
            ShowLocalizedMessage("answerAllQuestions", Color.yellow);
            yield break;
        }

        ShowLocalizedMessage("savingAnswers", Color.white);
        if (startButton != null) startButton.interactable = false;

        // Validate Firebase and PlayerManager
        if (FirebaseManager.Instance == null)
        {
            Debug.LogError("[LoginUI] FirebaseManager.Instance is null!");
            ShowLocalizedMessage("firebaseError", Color.red);
            if (startButton != null) startButton.interactable = true;
            yield break;
        }

        if (PlayerManager.Instance == null || string.IsNullOrEmpty(PlayerManager.Instance.userId))
        {
            Debug.LogError("[LoginUI] PlayerManager or userId is null!");
            ShowLocalizedMessage("userNotLoggedIn", Color.red);
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
            ShowLocalizedMessage("dataSaved", Color.green);

            yield return new WaitForSeconds(1f);

            SceneManager.LoadScene(firstMuseumScene);
        }
        else
        {
            Debug.LogError("[LoginUI] Failed to save demographics!");
            ShowLocalizedMessage("saveFailed", Color.red);
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

        // Clear message
        if (messageText != null)
        {
            messageText.text = "";
            messageText.color = Color.white;
        }

        if (startButton != null) startButton.interactable = true;
    }
}