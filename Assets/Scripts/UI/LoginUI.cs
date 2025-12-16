using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Collections;

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

    private async void Start()
    {
        Debug.Log("[LoginUI] ========== START ==========");

        if (loginPanel != null) loginPanel.SetActive(true);
        if (questionsPanel != null) questionsPanel.SetActive(false);
        if (LanguageDropDownParent != null) LanguageDropDownParent.SetActive(true);

        ShowMessage("Initializing...", Color.white);

        if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
        if (registerButton != null) registerButton.onClick.AddListener(OnRegisterClicked);
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);

        await WaitForFirebase();

        ShowMessage("", Color.white);
        Debug.Log("[LoginUI] ========== READY ==========");
    }

    private async Task WaitForFirebase()
    {
        Debug.Log("[LoginUI] Waiting for Firebase...");
        int maxRetries = 50;
        int retries = 0;

        while (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
        {
            await Task.Delay(100);
            retries++;

            if (retries >= maxRetries)
            {
                Debug.LogError("❌ [LoginUI] Firebase failed to initialize!");
                ShowMessage("Connection error. Please restart.", Color.red);

                if (loginButton != null) loginButton.interactable = false;
                if (registerButton != null) registerButton.interactable = false;
                return;
            }
        }

        Debug.Log("✅ [LoginUI] Firebase ready!");
    }

    // ------------------------------------------------------------------------
    // LOGIN & REGISTER BUTTONS
    // ------------------------------------------------------------------------

    private async void OnLoginClicked()
    {
        Debug.Log("[LoginUI] ========== LOGIN CLICKED ==========");
        string email = emailInput.text.Trim();
        string password = passwordInput.text.Trim();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowMessage("Please enter both email and password.", Color.yellow);
            return;
        }

        ShowMessage("Logging in...", Color.white);

        bool success = await PlayerManager.Instance.LoginUser(email, password);
        Debug.Log($"[LoginUI] Login result: {success}");

        if (success)
        {
            ShowMessage("Login successful!", Color.green);

            if (BadgeManager.instance != null)
            {
                await BadgeManager.instance.LoadProgressFromFirebase();
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: Use Coroutine for delay, then show questions panel
            Debug.Log("[LoginUI] WebGL build - using Coroutine for delay");
            StartCoroutine(DelayThenShowQuestions(0.4f));
#else
            await Task.Delay(400);

            string userId = PlayerManager.Instance.userId;
            bool hasDemographics = await FirebaseManager.Instance.UserHasDemographicsAsync(userId);

            if (hasDemographics)
            {
                Debug.Log("[LoginUI] Has demographics → loading museum");
                SceneManager.LoadScene(firstMuseumScene);
            }
            else
            {
                Debug.Log("[LoginUI] No demographics → showing questions");
                ShowQuestionsPanel();
            }
#endif
        }
        else
        {
            ShowMessage("Login failed. Check your email or password.", Color.red);
        }
    }

    private async void OnRegisterClicked()
    {
        Debug.Log("[LoginUI] ========== REGISTER CLICKED ==========");
        string email = emailInput.text.Trim();
        string password = passwordInput.text.Trim();

        Debug.Log($"[LoginUI] Email: {email}, Password length: {password.Length}");

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            Debug.LogWarning("[LoginUI] Empty email or password");
            ShowMessage("Please fill in both fields.", Color.yellow);
            return;
        }

        ShowMessage("Creating account...", Color.white);
        Debug.Log($"[LoginUI] Calling RegisterUser for: {email}");

        bool success = await PlayerManager.Instance.RegisterUser(email, password);

        Debug.Log($"[LoginUI] Registration returned: {success}");

        if (success)
        {
            Debug.Log("[LoginUI] SUCCESS! Showing success message...");
            ShowMessage("Account created successfully!", Color.green);

            Debug.Log("[LoginUI] Starting delay coroutine...");
            // ✅ FIX: Use Coroutine instead of Task.Delay for WebGL
            StartCoroutine(DelayThenShowQuestions(0.8f));
        }
        else
        {
            Debug.LogError("[LoginUI] Registration FAILED");
            ShowMessage("Registration failed. Try again.", Color.red);
        }

        Debug.Log("[LoginUI] ========== REGISTER COMPLETE ==========");
    }

    /// <summary>
    /// ✅ NEW: Coroutine to delay then show questions panel
    /// This works reliably in WebGL unlike Task.Delay
    /// </summary>
    private IEnumerator DelayThenShowQuestions(float delaySeconds)
    {
        Debug.Log($"[LoginUI] Coroutine started - waiting {delaySeconds} seconds...");
        yield return new WaitForSeconds(delaySeconds);

        Debug.Log("[LoginUI] Delay finished, calling ShowQuestionsPanel()...");
        ShowQuestionsPanel();
        Debug.Log("[LoginUI] ShowQuestionsPanel() completed");
    }

    // ------------------------------------------------------------------------
    // QUESTIONS PANEL
    // ------------------------------------------------------------------------

    private async void OnStartClicked()
    {
        Debug.Log("[LoginUI] ========== START CLICKED ==========");

        string age = GetSelectedOption(ageGroup);
        string gender = GetSelectedOption(genderGroup);
        string nationality = nationalityInput != null ? nationalityInput.text.Trim() : "";
        string skills = GetSelectedOption(skillsGroup);
        string vr = GetSelectedOption(vrGroup);

        Debug.Log($"[LoginUI] Age: '{age}', Gender: '{gender}', Nationality: '{nationality}', Skills: '{skills}', VR: '{vr}'");

        if (string.IsNullOrEmpty(age) || string.IsNullOrEmpty(gender) ||
            string.IsNullOrEmpty(nationality) || string.IsNullOrEmpty(skills) || string.IsNullOrEmpty(vr))
        {
            ShowMessage("Please answer all questions.", Color.yellow);
            Debug.LogWarning("[LoginUI] Some answers are missing");
            return;
        }

        ShowMessage("Saving your answers...", Color.white);

        if (FirebaseManager.Instance == null)
        {
            Debug.LogError("[LoginUI] FirebaseManager.Instance is null!");
            ShowMessage("Firebase not initialized.", Color.red);
            return;
        }

        if (PlayerManager.Instance == null || string.IsNullOrEmpty(PlayerManager.Instance.userId))
        {
            Debug.LogError("[LoginUI] PlayerManager or userId is null!");
            ShowMessage("User not logged in.", Color.red);
            return;
        }

        string userId = PlayerManager.Instance.userId;
        Debug.Log($"[LoginUI] Saving demographics for user: {userId}");

        try
        {
            await FirebaseManager.Instance.SaveDemographicsAsync(
                userId, age, gender, nationality, skills, vr
            );

            Debug.Log("[LoginUI] Demographics saved!");
            ShowMessage("Data saved! Loading museum...", Color.green);

#if UNITY_WEBGL && !UNITY_EDITOR
            // Use Coroutine for delay in WebGL
            StartCoroutine(DelayThenLoadScene(1.2f));
#else
            await Task.Delay(1200);
            Debug.Log($"[LoginUI] Loading scene: {firstMuseumScene}");
            SceneManager.LoadScene(firstMuseumScene);
#endif
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LoginUI] Error: {ex.Message}\n{ex.StackTrace}");
            ShowMessage("Failed to save data.", Color.red);
        }
    }

    /// <summary>
    /// Coroutine to delay then load scene
    /// </summary>
    private IEnumerator DelayThenLoadScene(float delaySeconds)
    {
        Debug.Log($"[LoginUI] Waiting {delaySeconds}s before loading scene...");
        yield return new WaitForSeconds(delaySeconds);

        Debug.Log($"[LoginUI] Loading scene: {firstMuseumScene}");
        SceneManager.LoadScene(firstMuseumScene);
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
            Debug.LogWarning($"[LoginUI] No toggle selected in: {group.name}");
            return "";
        }

        TMP_Text label = selected.transform.Find("Label")?.GetComponent<TMP_Text>();
        if (label != null)
        {
            return label.text;
        }

        label = selected.GetComponentsInChildren<TMP_Text>(true)
                        .FirstOrDefault(t => t.name.ToLower().Contains("label"));
        if (label != null)
        {
            return label.text;
        }

        Debug.LogWarning($"[LoginUI] No label found in: {group.name}");
        return "";
    }

    // ------------------------------------------------------------------------
    // UI HELPERS
    // ------------------------------------------------------------------------

    private void ShowQuestionsPanel()
    {
        Debug.Log("[LoginUI] ========== ShowQuestionsPanel START ==========");
        Debug.Log($"[LoginUI] BEFORE - loginPanel: {(loginPanel != null ? loginPanel.activeSelf.ToString() : "null")}, questionsPanel: {(questionsPanel != null ? questionsPanel.activeSelf.ToString() : "null")}");

        if (loginPanel == null)
        {
            Debug.LogError("[LoginUI] loginPanel is NULL!");
        }
        else
        {
            loginPanel.SetActive(false);
            Debug.Log("[LoginUI] loginPanel.SetActive(false) called");
        }

        if (LanguageDropDownParent != null)
        {
            LanguageDropDownParent.SetActive(false);
        }

        if (questionsPanel == null)
        {
            Debug.LogError("[LoginUI] questionsPanel is NULL!");
        }
        else
        {
            questionsPanel.SetActive(true);
            Debug.Log("[LoginUI] questionsPanel.SetActive(true) called");
        }

        if (messageText != null)
        {
            messageText.text = "";
        }

        Debug.Log($"[LoginUI] AFTER - loginPanel: {(loginPanel != null ? loginPanel.activeSelf.ToString() : "null")}, questionsPanel: {(questionsPanel != null ? questionsPanel.activeSelf.ToString() : "null")}");
        Debug.Log("[LoginUI] ========== ShowQuestionsPanel END ==========");
    }

    private void ShowMessage(string message, Color color)
    {
        if (messageText != null)
        {
            messageText.text = message;
            messageText.color = color;
        }
    }
}