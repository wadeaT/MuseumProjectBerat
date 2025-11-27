using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.InputSystem; // ✅ NEW INPUT SYSTEM

public class CardDiscoveryUI : MonoBehaviour
{
    public static CardDiscoveryUI Instance { get; private set; }

    [Header("UI References")]
    public GameObject discoveryPanel;
    public TextMeshProUGUI cardTitleText;
    public TextMeshProUGUI cardDescriptionText;
    public UnityEngine.UI.Button continueButton;

    [Header("Animation Settings")]
    public float fadeInDuration = 0.5f;
    public float fadeOutDuration = 0.3f;

    [Header("Background Dimming")]
    public UnityEngine.UI.Image backgroundDim;
    public Color dimColor = new Color(0, 0, 0, 0.7f);

    private CanvasGroup panelCanvasGroup;
    private bool isShowing = false;
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
        if (discoveryPanel != null)
        {
            panelCanvasGroup = discoveryPanel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = discoveryPanel.AddComponent<CanvasGroup>();
            }
        }

        // Setup continue button
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(CloseDiscovery);
        }
    }

    void Start()
    {
        // Hide panel initially
        if (discoveryPanel != null)
        {
            discoveryPanel.SetActive(false);
        }

        if (backgroundDim != null)
        {
            backgroundDim.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Show card discovery with title and description
    /// </summary>
    public void ShowCardDiscovery(string title, string description)
    {
        if (isShowing)
        {
            Debug.LogWarning("Card discovery already showing!");
            return;
        }

        StartCoroutine(ShowDiscoveryRoutine(title, description));
    }

    IEnumerator ShowDiscoveryRoutine(string title, string description)
    {
        isShowing = true;
        showStartTime = Time.unscaledTime;

        // Set text content
        if (cardTitleText != null)
            cardTitleText.text = title;

        if (cardDescriptionText != null)
            cardDescriptionText.text = description;

        // Show background dim
        if (backgroundDim != null)
        {
            backgroundDim.gameObject.SetActive(true);
            backgroundDim.color = new Color(dimColor.r, dimColor.g, dimColor.b, 0);
        }

        // Activate panel
        discoveryPanel.SetActive(true);

        // Start invisible
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
        }

        // Fade in
        yield return StartCoroutine(FadePanel(0f, 1f, fadeInDuration));

        // Also fade in background
        if (backgroundDim != null)
        {
            yield return StartCoroutine(FadeBackground(0f, dimColor.a, fadeInDuration * 0.5f));
        }
    }

    public void CloseDiscovery()
    {
        if (!isShowing) return;

        StartCoroutine(CloseDiscoveryRoutine());
    }

    IEnumerator CloseDiscoveryRoutine()
    {
        // Fade out panel
        yield return StartCoroutine(FadePanel(1f, 0f, fadeOutDuration));

        // Fade out background
        if (backgroundDim != null)
        {
            yield return StartCoroutine(FadeBackground(dimColor.a, 0f, fadeOutDuration * 0.5f));
            backgroundDim.gameObject.SetActive(false);
        }

        // Hide panel
        discoveryPanel.SetActive(false);

        isShowing = false;
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

    IEnumerator FadeBackground(float startAlpha, float endAlpha, float duration)
    {
        if (backgroundDim == null)
            yield break;

        float elapsed = 0f;
        Color startColor = new Color(dimColor.r, dimColor.g, dimColor.b, startAlpha);
        Color endColor = new Color(dimColor.r, dimColor.g, dimColor.b, endAlpha);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            backgroundDim.color = Color.Lerp(startColor, endColor, t);

            yield return null;
        }

        backgroundDim.color = endColor;
    }

    void Update()
    {
        // ✅ FIXED: New Input System - allow closing with keyboard after a short delay
        if (isShowing && Time.unscaledTime > showStartTime + 0.5f)
        {
            if (Keyboard.current != null &&
                (Keyboard.current[Key.E].wasPressedThisFrame || Keyboard.current[Key.Space].wasPressedThisFrame))
            {
                CloseDiscovery();
            }
        }
    }
}