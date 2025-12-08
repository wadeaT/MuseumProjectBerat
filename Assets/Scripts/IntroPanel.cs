using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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

    [Header("Content")]
    [TextArea(3, 6)]
    public string welcomeTitle = "Welcome to the Berat Museum!";

    [TextArea(5, 10)]
    public string welcomeInstructions =
        "<b>Your Mission:</b>\n" +
        "Explore the museum and discover hidden cards that tell the story of Ottoman-era Albanian life.\n\n" +
        "<b>Controls:</b>\n" +
        "• Left side of screen - Move\n" +
        "• Right side of screen - Look around\n" +
        "• TAP button - Interact with objects\n\n" +
        "<b>Collect all 18 cards to unlock all badges!</b>";

    private CanvasGroup canvasGroup;
    private bool isShowing = false;

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
    }

    void Start()
    {
        // Set content
        if (titleText != null)
            titleText.text = welcomeTitle;

        if (instructionsText != null)
            instructionsText.text = welcomeInstructions;

        // Show intro on start
        ShowIntro();
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

    // Optional: Close with any key/tap
    void Update()
    {
        if (isShowing)
        {
            // Close on Escape key
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                CloseIntro();
            }
        }
    }
}