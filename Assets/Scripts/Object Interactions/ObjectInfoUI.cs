using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// MEASUREMENT STAGE (UI Component): Object Information Panel
/// 
/// Responsibilities:
/// 1. Display object information to user
/// 2. Measure viewing duration accurately (unscaled time)
/// 3. Report reading events to ReadingBehaviorTracker for closed-loop adaptation
/// 4. Fire OnPanelClosed event for other listeners
/// 
/// Flow:
/// Panel Opens → User views content → Panel Closes → Report to ReadingBehaviorTracker → 
/// EngagementClassifier updates → Next panel adapts content length
/// </summary>
public class ObjectInfoUI : MonoBehaviour
{
    public static ObjectInfoUI Instance { get; private set; }

    [Header("UI References")]
    public GameObject infoPanel;
    public TextMeshProUGUI objectTitleText;
    public TextMeshProUGUI objectDescriptionText;
    public UnityEngine.UI.Button closeButton;
    public UnityEngine.UI.Image objectImage;

    [Header("Animation Settings")]
    public float fadeInDuration = 0.3f;
    public float fadeOutDuration = 0.2f;
    public bool autoCloseAfterSeconds = false;
    public float autoCloseDelay = 5f;

    [Header("Reading Time Analysis")]
    [Tooltip("Average words per minute for reading speed estimation")]
    public float averageWordsPerMinute = 250f; // VR CALIBRATED: Increased from 200 for faster skimming

    [Tooltip("Minimum time (seconds) to consider content was actually read")]
    public float minimumReadTime = 1.5f; // VR CALIBRATED: Reduced from 2f

    [Header("Debug")]
    public bool showDebugLogs = true;

    // Events for tracking (used by InteractiveObject and other listeners)
    public event Action<string, string, float, bool> OnPanelClosed; // (objectId, objectName, duration, wasLikelyRead)

    private CanvasGroup panelCanvasGroup;
    private bool isShowing = false;
    private Coroutine autoCloseCoroutine;

    // Reading time tracking
    private float showStartTime = 0f;
    private string currentObjectId = "";
    private string currentObjectName = "";
    private string currentContent = "";
    private int currentWordCount = 0;
    private float estimatedReadTime = 0f;
    private bool wasAutoClose = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (infoPanel != null)
        {
            panelCanvasGroup = infoPanel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = infoPanel.AddComponent<CanvasGroup>();
            }
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseInfo);
        }
    }

    void Start()
    {
        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Show object information panel with tracking
    /// </summary>
    public void ShowObjectInfo(string title, string description, Sprite image = null)
    {
        ShowObjectInfo("", title, description, image);
    }

    /// <summary>
    /// Show object information panel with explicit object ID for tracking
    /// </summary>
    public void ShowObjectInfo(string objectId, string title, string description, Sprite image = null)
    {
        if (isShowing)
        {
            UpdateContent(objectId, title, description, image);
            return;
        }

        // Store for tracking
        currentObjectId = objectId;
        currentObjectName = title;
        currentContent = description;
        currentWordCount = CountWords(description);
        estimatedReadTime = CalculateEstimatedReadTime(currentWordCount);
        wasAutoClose = false;

        StartCoroutine(ShowInfoRoutine(title, description, image));
    }

    IEnumerator ShowInfoRoutine(string title, string description, Sprite image)
    {
        isShowing = true;
        showStartTime = Time.unscaledTime; // Use unscaled time for accurate tracking

        if (objectTitleText != null)
            objectTitleText.text = title;

        if (objectDescriptionText != null)
            objectDescriptionText.text = description;

        if (objectImage != null && image != null)
        {
            objectImage.sprite = image;
            objectImage.gameObject.SetActive(true);
        }
        else if (objectImage != null)
        {
            objectImage.gameObject.SetActive(false);
        }

        infoPanel.SetActive(true);

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
        }

        yield return StartCoroutine(FadePanel(0f, 1f, fadeInDuration));

        if (showDebugLogs)
        {
            Debug.Log($"📖 [ObjectInfoUI] Panel opened: '{title}' ({currentWordCount} words, est. {estimatedReadTime:F1}s read time)");
        }

        if (autoCloseAfterSeconds)
        {
            autoCloseCoroutine = StartCoroutine(AutoCloseRoutine());
        }
    }

    void UpdateContent(string objectId, string title, string description, Sprite image)
    {
        // Report previous content viewing before updating
        if (!string.IsNullOrEmpty(currentObjectName) && currentWordCount > 1)
        {
            float previousDuration = Time.unscaledTime - showStartTime;
            bool wasLikelyRead = DetermineIfLikelyRead(previousDuration);

            // Report to ReadingBehaviorTracker
            ReportReadingEvent(previousDuration, wasLikelyRead);

            if (showDebugLogs)
            {
                Debug.Log($"📖 [ObjectInfoUI] Content updated (previous: {previousDuration:F1}s, likely read: {wasLikelyRead})");
            }
        }

        // Update tracking for new content
        currentObjectId = objectId;
        currentObjectName = title;
        currentContent = description;
        currentWordCount = CountWords(description);
        estimatedReadTime = CalculateEstimatedReadTime(currentWordCount);
        showStartTime = Time.unscaledTime; // Reset timer for new content

        if (objectTitleText != null)
            objectTitleText.text = title;

        if (objectDescriptionText != null)
            objectDescriptionText.text = description;

        if (objectImage != null && image != null)
        {
            objectImage.sprite = image;
            objectImage.gameObject.SetActive(true);
        }

        if (autoCloseAfterSeconds && autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = StartCoroutine(AutoCloseRoutine());
        }
    }

    public void CloseInfo()
    {
        if (!isShowing) return;

        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = null;
        }

        StartCoroutine(CloseInfoRoutine());
    }

    IEnumerator CloseInfoRoutine()
    {
        // Calculate reading metrics before closing
        float viewDuration = Time.unscaledTime - showStartTime;
        bool wasLikelyRead = DetermineIfLikelyRead(viewDuration);

        if (showDebugLogs)
        {
            string readStatus = wasLikelyRead ? "✅ LIKELY READ" : "❌ SKIPPED/SKIMMED";
            Debug.Log($"📖 [ObjectInfoUI] Panel closing: '{currentObjectName}'");
            Debug.Log($"   Duration: {viewDuration:F1}s (estimated: {estimatedReadTime:F1}s)");
            Debug.Log($"   Word count: {currentWordCount}");
            Debug.Log($"   Status: {readStatus}");
            Debug.Log($"   Close type: {(wasAutoClose ? "Auto-close" : "Manual")}");
        }

        // ✅ CRITICAL: Report to ReadingBehaviorTracker for closed-loop adaptation
        ReportReadingEvent(viewDuration, wasLikelyRead);

        // Fire event for external tracking (InteractiveObject, etc.)
        OnPanelClosed?.Invoke(currentObjectId, currentObjectName, viewDuration, wasLikelyRead);

        // Fade out panel
        yield return StartCoroutine(FadePanel(1f, 0f, fadeOutDuration));

        infoPanel.SetActive(false);
        isShowing = false;

        // Reset tracking variables
        currentObjectId = "";
        currentObjectName = "";
        currentContent = "";
        currentWordCount = 0;
        estimatedReadTime = 0f;
    }

    IEnumerator AutoCloseRoutine()
    {
        yield return new WaitForSecondsRealtime(autoCloseDelay);
        wasAutoClose = true;
        CloseInfo();
    }

    IEnumerator FadePanel(float startAlpha, float endAlpha, float duration)
    {
        if (panelCanvasGroup == null)
            yield break;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);
            panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        panelCanvasGroup.alpha = endAlpha;
    }

    void Update()
    {
        // Allow closing after short delay to prevent immediate closure
        if (isShowing && Time.unscaledTime > showStartTime + 0.5f)
        {
            if (Keyboard.current != null &&
                (Keyboard.current[Key.Escape].wasPressedThisFrame ||
                 Keyboard.current[Key.E].wasPressedThisFrame))
            {
                CloseInfo();
            }
        }
    }

    // ============================================================================
    // READING TIME ANALYSIS
    // ============================================================================

    int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return text.Split(new char[] { ' ', '\n', '\r', '\t' },
            StringSplitOptions.RemoveEmptyEntries).Length;
    }

    float CalculateEstimatedReadTime(int wordCount)
    {
        if (wordCount <= 0)
            return minimumReadTime;

        float estimatedSeconds = (wordCount / averageWordsPerMinute) * 60f;
        estimatedSeconds *= 1.2f; // Add buffer for processing/comprehension

        return Mathf.Max(estimatedSeconds, minimumReadTime);
    }

    bool DetermineIfLikelyRead(float viewDuration)
    {
        if (viewDuration < minimumReadTime)
            return false;

        if (viewDuration >= estimatedReadTime * 0.4f) // VR CALIBRATED: Reduced from 0.5f
            return true;

        // For short content, be more lenient
        if (currentWordCount <= 30 && viewDuration >= 2f) // VR CALIBRATED: Reduced from 3f
            return true;

        if (currentWordCount <= 75 && viewDuration >= 3f) // VR CALIBRATED: Reduced from 5f
            return true;

        return false;
    }

    // ============================================================================
    // CLOSED-LOOP INTEGRATION
    // ============================================================================

    /// <summary>
    /// Report reading event to ReadingBehaviorTracker for engagement adaptation
    /// This is the key integration point for the closed-loop system
    /// </summary>
    void ReportReadingEvent(float viewDuration, bool wasLikelyRead)
    {
        if (currentWordCount <= 1 || string.IsNullOrEmpty(currentContent) || currentContent == "Loading...")
        {
            if (showDebugLogs)
                Debug.Log($"[ObjectInfoUI] Skipping reading report for placeholder content (wordCount={currentWordCount})");
            return;
        }

        if (ReadingBehaviorTracker.Instance == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("[ObjectInfoUI] ReadingBehaviorTracker not found - reading event not reported for adaptation");
            }
            return;
        }

        // Create reading event data
        ReadingEventData eventData = new ReadingEventData
        {
            contentName = currentObjectName,
            contentType = "Object",
            viewDuration = viewDuration,
            estimatedReadTime = estimatedReadTime,
            wordCount = currentWordCount,
            wasLikelyRead = wasLikelyRead
        };

        // Report to tracker (this feeds into EngagementClassifier)
        ReadingBehaviorTracker.Instance.OnContentViewed(eventData);

        if (showDebugLogs)
        {
            Debug.Log($"🔄 [ObjectInfoUI] Reading event reported to tracker → affects next content adaptation");
        }
    }

    // ============================================================================
    // PUBLIC API
    // ============================================================================

    public bool IsShowing => isShowing;

    public float GetCurrentViewDuration()
    {
        if (!isShowing)
            return 0f;
        return Time.unscaledTime - showStartTime;
    }

    public string GetCurrentObjectName()
    {
        return currentObjectName;
    }
}