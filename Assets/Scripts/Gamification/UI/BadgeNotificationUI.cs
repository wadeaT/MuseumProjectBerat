using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class BadgeNotificationUI : MonoBehaviour
{
    public static BadgeNotificationUI instance;

    [Header("UI References")]
    public GameObject notificationPanel;
    public UnityEngine.UI.Image badgeIcon;
    public TextMeshProUGUI badgeNameText;
    public TextMeshProUGUI badgeDescriptionText;

    [Header("Animation Settings")]
    public float slideInDuration = 0.5f;
    public float displayDuration = 3.5f;
    public float slideOutDuration = 0.5f;

    [Header("Positions")]
    public float hiddenYPosition = -100f;
    public float visibleYPosition = -100f;

    private RectTransform panelRect;
    private bool isShowing = false;

    void Awake()
    {
        // Singleton
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (notificationPanel != null)
        {
            panelRect = notificationPanel.GetComponent<RectTransform>();
        }
    }

    void Start()
    {
        // Make sure panel is hidden
        if (notificationPanel != null)
        {
            notificationPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Show a badge notification
    /// </summary>
    public void ShowBadge(string badgeName, string description, string icon = "🎖️")
    {
        if (isShowing)
        {
            // If already showing a badge, wait until this one finishes
            // In a more complex system, you'd queue badges
            Debug.Log("Badge notification already showing. Skipping...");
            return;
        }

        StartCoroutine(ShowBadgeRoutine(badgeName, description, icon));
    }

    IEnumerator ShowBadgeRoutine(string badgeName, string description, string icon)
    {
        isShowing = true;

        // Set text
        if (badgeIcon != null)
        {
            badgeIcon.enabled = true;
        }

        // Set header text (top small text)
        if (badgeNameText != null)
            badgeNameText.text = "ACHIEVEMENT UNLOCKED";

        // Set description
        if (badgeDescriptionText != null)
            badgeDescriptionText.text = $"{badgeName}\n\n{description}";

        // Activate panel
        notificationPanel.SetActive(true);

        // Start position (hidden above screen)
        if (panelRect != null)
        {
            panelRect.anchoredPosition = new Vector2(0, hiddenYPosition);
        }

        // Slide in
        yield return StartCoroutine(SlideToPosition(visibleYPosition, slideInDuration));

        // Wait (display time)
        yield return new WaitForSeconds(displayDuration);

        // Slide out
        yield return StartCoroutine(SlideToPosition(hiddenYPosition, slideOutDuration));

        // Hide panel
        notificationPanel.SetActive(false);

        isShowing = false;
    }

    IEnumerator SlideToPosition(float targetY, float duration)
    {
        if (panelRect == null)
            yield break;

        Vector2 startPos = panelRect.anchoredPosition;
        Vector2 targetPos = new Vector2(0, targetY);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Smooth easing
            t = Mathf.SmoothStep(0f, 1f, t);

            panelRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);

            yield return null;
        }

        panelRect.anchoredPosition = targetPos;
    }
}