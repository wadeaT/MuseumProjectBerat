using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.Localization; // ✅ ADD THIS
using UnityEngine.Localization.Settings; // ✅ ADD THIS

public class IntroPanel : MonoBehaviour
{
    [Header("UI References")]
    public GameObject introPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI instructionsText;
    public Button closeButton;

    [Header("Settings")]
    [Tooltip("Auto-close after this many seconds (0 = never auto-close)")]
    public float autoCloseDelay = 0f;

    [Tooltip("Pause the game while intro is showing")]
    public bool pauseGameWhileShowing = true;

    [Header("Animation")]
    public float fadeInDuration = 0.5f;
    public float fadeOutDuration = 0.3f;

    [Header("Localized Content")]
    [Tooltip("Localized welcome title")]
    public LocalizedString welcomeTitle; // ✅ CHANGED from string to LocalizedString

    [Tooltip("Localized welcome instructions")]
    public LocalizedString welcomeInstructions; // ✅ CHANGED from string to LocalizedString

    private CanvasGroup canvasGroup;
    private bool isShowing = false;
    private InputAction closeAction;

    void Awake()
    {
        // Get or add CanvasGroup for fading
        if (introPanel != null)
        {
            canvasGroup = introPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = introPanel.AddComponent<CanvasGroup>();
            }
        }

        // Setup close button
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseIntro);
        }

        // Setup new input system actions
        SetupInputActions();

        // ✅ Subscribe to locale change events
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    void SetupInputActions()
    {
        // Create composite action for Escape, Space, and Enter keys
        closeAction = new InputAction(
            name: "CloseIntro",
            binding: "<Keyboard>/escape"
        );

        // Add additional bindings for Space and Enter
        closeAction.AddBinding("<Keyboard>/space");
        closeAction.AddBinding("<Keyboard>/enter");

        // Subscribe to the action
        closeAction.performed += OnCloseInput;
    }

    void OnEnable()
    {
        closeAction?.Enable();
    }

    void OnDisable()
    {
        closeAction?.Disable();
    }

    void OnDestroy()
    {
        // Clean up
        if (closeAction != null)
        {
            closeAction.performed -= OnCloseInput;
            closeAction.Dispose();
        }

        // ✅ Unsubscribe from locale change events
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    void Start()
    {
        // ✅ Load localized content
        UpdateLocalizedContent();

        // Show intro on start
        ShowIntro();
    }

    // ✅ NEW METHOD: Update all localized text
    void UpdateLocalizedContent()
    {
        // Get localized title
        if (welcomeTitle != null && !welcomeTitle.IsEmpty)
        {
            var titleOp = welcomeTitle.GetLocalizedStringAsync();
            titleOp.Completed += (op) =>
            {
                if (titleText != null)
                {
                    titleText.text = op.Result;
                }
            };
        }

        // Get localized instructions
        if (welcomeInstructions != null && !welcomeInstructions.IsEmpty)
        {
            var instructionsOp = welcomeInstructions.GetLocalizedStringAsync();
            instructionsOp.Completed += (op) =>
            {
                if (instructionsText != null)
                {
                    instructionsText.text = op.Result;
                }
            };
        }
    }

    // ✅ NEW METHOD: Handle language changes
    private void OnLocaleChanged(UnityEngine.Localization.Locale locale)
    {
        // Update text when language changes
        UpdateLocalizedContent();
    }

    private void OnCloseInput(InputAction.CallbackContext context)
    {
        if (isShowing)
        {
            CloseIntro();
        }
    }

    public void ShowIntro()
    {
        if (isShowing) return;

        StartCoroutine(ShowIntroRoutine());
    }

    IEnumerator ShowIntroRoutine()
    {
        isShowing = true;

        // Pause game if enabled
        if (pauseGameWhileShowing)
        {
            Time.timeScale = 0f;
        }

        // Activate panel
        introPanel.SetActive(true);

        // Start invisible
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        // Fade in (using unscaled time since game might be paused)
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            }
            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        // Auto-close timer
        if (autoCloseDelay > 0)
        {
            yield return new WaitForSecondsRealtime(autoCloseDelay);
            CloseIntro();
        }
    }

    public void CloseIntro()
    {
        if (!isShowing) return;

        StartCoroutine(CloseIntroRoutine());
    }

    IEnumerator CloseIntroRoutine()
    {
        // Fade out
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            }
            yield return null;
        }

        // Hide panel
        introPanel.SetActive(false);

        // Resume game
        if (pauseGameWhileShowing)
        {
            Time.timeScale = 1f;
        }

        isShowing = false;
    }
}