using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class ObjectInfoUI : MonoBehaviour
{
    public static ObjectInfoUI Instance { get; private set; }

    [Header("UI References")]
    public GameObject infoPanel;
    public TextMeshProUGUI objectTitleText;
    public TextMeshProUGUI objectDescriptionText;
    public UnityEngine.UI.Button closeButton;
    public UnityEngine.UI.Image objectImage; // Optional - for showing artifact image

    [Header("Animation Settings")]
    public float fadeInDuration = 0.3f;
    public float fadeOutDuration = 0.2f;
    public bool autoCloseAfterSeconds = false;
    public float autoCloseDelay = 5f;

    private CanvasGroup panelCanvasGroup;
    private bool isShowing = false;
    private Coroutine autoCloseCoroutine;
    private float showStartTime = 0f;
    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Get or add CanvasGroup for fading
        if (infoPanel != null)
        {
            panelCanvasGroup = infoPanel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = infoPanel.AddComponent<CanvasGroup>();
            }
        }

        // Setup close button
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseInfo);
        }
    }

    void Start()
    {
        // Hide panel initially
        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Show object information panel
    /// </summary>
    public void ShowObjectInfo(string title, string description, Sprite image = null)
    {
        if (isShowing)
        {
            // If already showing, update content instead of creating new
            UpdateContent(title, description, image);
            return;
        }

        StartCoroutine(ShowInfoRoutine(title, description, image));
    }

    IEnumerator ShowInfoRoutine(string title, string description, Sprite image)
    {
        isShowing = true;
        showStartTime = Time.time;

        // Set text content
        if (objectTitleText != null)
            objectTitleText.text = title;

        if (objectDescriptionText != null)
            objectDescriptionText.text = description;

        // Set image (optional)
        if (objectImage != null && image != null)
        {
            objectImage.sprite = image;
            objectImage.gameObject.SetActive(true);
        }
        else if (objectImage != null)
        {
            objectImage.gameObject.SetActive(false);
        }

        // Activate panel
        infoPanel.SetActive(true);

        // Start invisible
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
        }

        // Fade in
        yield return StartCoroutine(FadePanel(0f, 1f, fadeInDuration));

        // Auto-close timer (optional)
        if (autoCloseAfterSeconds)
        {
            autoCloseCoroutine = StartCoroutine(AutoCloseRoutine());
        }
    }

    /// <summary>
    /// Update content if panel is already showing
    /// </summary>
    void UpdateContent(string title, string description, Sprite image)
    {
        if (objectTitleText != null)
            objectTitleText.text = title;

        if (objectDescriptionText != null)
            objectDescriptionText.text = description;

        if (objectImage != null && image != null)
        {
            objectImage.sprite = image;
            objectImage.gameObject.SetActive(true);
        }

        // Reset auto-close timer if enabled
        if (autoCloseAfterSeconds && autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = StartCoroutine(AutoCloseRoutine());
        }
    }

    public void CloseInfo()
    {
        if (!isShowing) return;

        // Stop auto-close if running
        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = null;
        }

        StartCoroutine(CloseInfoRoutine());
    }

    IEnumerator CloseInfoRoutine()
    {
        // Fade out panel
        yield return StartCoroutine(FadePanel(1f, 0f, fadeOutDuration));

        // Hide panel
        infoPanel.SetActive(false);

        isShowing = false;
    }

    IEnumerator AutoCloseRoutine()
    {
        yield return new WaitForSeconds(autoCloseDelay);
        CloseInfo();
    }

    IEnumerator FadePanel(float startAlpha, float endAlpha, float duration)
    {
        if (panelCanvasGroup == null)
            yield break;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Smooth easing
            t = Mathf.SmoothStep(0f, 1f, t);

            panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);

            yield return null;
        }

        panelCanvasGroup.alpha = endAlpha;
    }

    void Update()
    {
        // ✅ FIXED: Allow closing after short delay to prevent immediate closure
        if (isShowing && Time.time > showStartTime + 0.5f)
        {
            // Press ESC or E to close
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
            {
                CloseInfo();
            }
        }
    }
}