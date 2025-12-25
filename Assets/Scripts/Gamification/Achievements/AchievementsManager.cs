using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// AchievementsManager - WebGL-safe version using coroutines instead of async/await
/// </summary>
public class AchievementsManager : MonoBehaviour
{
    [Header("Score UI")]
    public TMP_Text totalScoreText;
    public TMP_Text scoreBreakdownText;

    [Header("TEST MODE")]
    public bool useTestUserId = false;
    public string testUserId;

    [Header("Prefabs")]
    public GameObject badgeUIPrefab;
    public GameObject cardUIPrefab;

    [Header("ScrollView Content Parents")]
    public Transform badgesContent;
    public Transform cardsContent;

    [Header("Summary UI")]
    public TMP_Text totalCardsText;
    public TMP_Text totalBadgesText;
    public TMP_Text lastCardFoundText;
    public TMP_Text lastBadgeUnlockedText;
    public TMP_Text roomStatsSummaryText;

    // -------------------------------------------------------
    // BADGE ICON SYSTEM
    // -------------------------------------------------------
    [System.Serializable]
    public class BadgeIconEntry
    {
        public string badgeId;
        public Sprite icon;
    }

    [Header("Badge Icons (assign in Inspector)")]
    public List<BadgeIconEntry> badgeIcons;

    [Header("Localization")]
    public string localizationTableName = "FullMuseum";

    private string odId;
    private StringTable localizedTable;

    // -------------------------------------------------------
    // UNITY FLOW
    // -------------------------------------------------------

    void Start()
    {
        StartCoroutine(LoadAchievementsFlow());
    }

    private IEnumerator LoadAchievementsFlow()
    {
        // Wait for localization to initialize
        yield return LocalizationSettings.InitializationOperation;

        // Load the string table
        var tableOperation = LocalizationSettings.StringDatabase.GetTableAsync(localizationTableName);
        yield return tableOperation;
        localizedTable = tableOperation.Result;

        if (localizedTable == null)
        {
            Debug.LogError($"[AchievementsManager] Failed to load localization table '{localizationTableName}'!");
        }
        else
        {
            Debug.Log($"[AchievementsManager] Loaded localization table '{localizationTableName}' with {localizedTable.Count} entries");
        }

        // Wait for Firebase to be ready
        float firebaseWaitTime = 0f;
        while ((FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady) && firebaseWaitTime < 10f)
        {
            firebaseWaitTime += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
        {
            Debug.LogError("[AchievementsManager] Firebase not ready after timeout!");
            yield break;
        }

        if (useTestUserId)
        {
            if (string.IsNullOrEmpty(testUserId))
            {
                Debug.LogError("[AchievementsManager] useTestUserId is ON but testUserId is empty!");
                yield break;
            }
            odId = testUserId;
        }
        else
        {
            if (PlayerManager.Instance == null || string.IsNullOrEmpty(PlayerManager.Instance.userId))
            {
                Debug.LogError("[AchievementsManager] PlayerManager not initialized or user not logged in!");
                yield break;
            }
            odId = PlayerManager.Instance.userId;
        }

        Debug.Log($"[AchievementsManager] Loading achievements for user: {odId}");

        // Load all data using coroutines
        StartCoroutine(LoadBadgesCoroutine());
        StartCoroutine(LoadCardsCoroutine());
        StartCoroutine(LoadSummaryAndScoreCoroutine());
        StartCoroutine(LoadRoomStatsCoroutine());
    }

    // -------------------------------------------------------
    // LOCALIZATION HELPERS
    // -------------------------------------------------------

    /// <summary>
    /// Get localized string from the table
    /// </summary>
    private string GetLocalizedString(string key)
    {
        if (localizedTable != null)
        {
            var entry = localizedTable.GetEntry(key);
            if (entry != null)
            {
                return entry.GetLocalizedString();
            }
        }
        Debug.LogWarning($"[AchievementsManager] Localization key '{key}' not found!");
        return key;
    }

    /// <summary>
    /// Get localized string with formatting 
    /// </summary>
    private string GetLocalizedFormat(string key, params object[] args)
    {
        string format = GetLocalizedString(key);
        try
        {
            return string.Format(format, args);
        }
        catch
        {
            return format; // Return unformatted if format fails
        }
    }

    // -------------------------------------------------------
    // BADGE ICON HELPER
    // -------------------------------------------------------
    private Sprite GetBadgeIcon(string badgeId)
    {
        foreach (var entry in badgeIcons)
        {
            if (entry.badgeId == badgeId)
                return entry.icon;
        }
        return null; // fallback if missing
    }

    // -------------------------------------------------------
    // LOAD BADGES
    // -------------------------------------------------------
    private IEnumerator LoadBadgesCoroutine()
    {
        bool completed = false;
        List<BadgeDocumentData> badges = null;

        FirebaseManager.Instance.LoadBadgesWithData(odId, (result) =>
        {
            badges = result;
            completed = true;
        });

        // Wait with timeout
        float timeout = 0f;
        while (!completed && timeout < 10f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        if (!completed || badges == null)
        {
            Debug.LogError("[AchievementsManager] Failed to load badges (timeout)");
            yield break;
        }

        Debug.Log($"[AchievementsManager] Found {badges.Count} badges for user {odId}");

        foreach (var badge in badges)
        {
            string badgeId = badge.badgeId;

            // Get LOCALIZED name and description instead of Firebase values
            string badgeName = GetLocalizedString($"{badgeId}_name");
            string description = GetLocalizedString($"{badgeId}_desc");

            Debug.Log($"[AchievementsManager] Loading badge: {badgeId} → {badgeName}");

            // Spawn UI item
            if (badgeUIPrefab != null && badgesContent != null)
            {
                GameObject item = Instantiate(badgeUIPrefab, badgesContent);

                var titleTransform = item.transform.Find("Title");
                if (titleTransform != null)
                {
                    var titleText = titleTransform.GetComponent<TMP_Text>();
                    if (titleText != null) titleText.text = badgeName;
                }

                var descTransform = item.transform.Find("Desc");
                if (descTransform != null)
                {
                    var descText = descTransform.GetComponent<TMP_Text>();
                    if (descText != null) descText.text = description;
                }

                // Assign Icon
                var iconTransform = item.transform.Find("Icon");
                if (iconTransform != null)
                {
                    Image iconImage = iconTransform.GetComponent<Image>();
                    if (iconImage != null)
                    {
                        Sprite iconSprite = GetBadgeIcon(badgeId);
                        if (iconSprite != null)
                        {
                            iconImage.sprite = iconSprite;
                        }
                        else
                        {
                            iconImage.enabled = false;
                        }
                    }
                }
            }
        }
    }

    // -------------------------------------------------------
    // LOAD CARDS
    // -------------------------------------------------------
    private IEnumerator LoadCardsCoroutine()
    {
        bool completed = false;
        List<CardDocumentData> cards = null;

        FirebaseManager.Instance.LoadCardsWithData(odId, (result) =>
        {
            cards = result;
            completed = true;
        });

        // Wait with timeout
        float timeout = 0f;
        while (!completed && timeout < 10f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        if (!completed || cards == null)
        {
            Debug.LogError("[AchievementsManager] Failed to load cards (timeout)");
            yield break;
        }

        Debug.Log($"[AchievementsManager] Found {cards.Count} cards for user {odId}");

        foreach (var card in cards)
        {
            string cardId = card.cardId;
            bool found = card.found;

            if (cardUIPrefab != null && cardsContent != null)
            {
                GameObject item = Instantiate(cardUIPrefab, cardsContent);

                // Try to get localized card name, fallback to cardId
                string cardName = GetLocalizedString($"{cardId}_name");
                if (cardName == $"{cardId}_name") cardName = cardId; // Use ID if no translation

                var titleTransform = item.transform.Find("Title");
                if (titleTransform != null)
                {
                    var titleText = titleTransform.GetComponent<TMP_Text>();
                    if (titleText != null) titleText.text = cardName;
                }

                // Localized status text
                string status = found
                    ? GetLocalizedString("card_status_collected")
                    : GetLocalizedString("card_status_not_found");

                var statusTransform = item.transform.Find("Status");
                if (statusTransform != null)
                {
                    var statusText = statusTransform.GetComponent<TMP_Text>();
                    if (statusText != null) statusText.text = status;
                }
            }
        }
    }

    // -------------------------------------------------------
    // LOAD SUMMARY AND SCORE
    // -------------------------------------------------------
    private IEnumerator LoadSummaryAndScoreCoroutine()
    {
        bool completed = false;
        ProgressSummaryData summary = null;

        FirebaseManager.Instance.LoadProgressSummary(odId, (result) =>
        {
            summary = result;
            completed = true;
        });

        // Wait with timeout
        float timeout = 0f;
        while (!completed && timeout < 10f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        if (!completed || summary == null)
        {
            Debug.LogError("[AchievementsManager] Failed to load summary (timeout)");
            yield break;
        }

        // Update summary UI
        if (totalCardsText != null)
            totalCardsText.text = GetLocalizedFormat("ui_total_cards", summary.totalCardsCollected);

        if (totalBadgesText != null)
            totalBadgesText.text = GetLocalizedFormat("ui_total_badges", summary.totalBadges);

        // Try to get localized names for last card/badge
        if (lastCardFoundText != null && !string.IsNullOrEmpty(summary.lastCardFound))
        {
            string localizedLastCard = GetLocalizedString($"{summary.lastCardFound}_name");
            if (localizedLastCard == $"{summary.lastCardFound}_name")
                localizedLastCard = summary.lastCardFound;
            lastCardFoundText.text = GetLocalizedFormat("ui_last_card_found", localizedLastCard);
        }

        if (lastBadgeUnlockedText != null && !string.IsNullOrEmpty(summary.lastBadgeUnlocked))
        {
            string localizedLastBadge = GetLocalizedString($"{summary.lastBadgeUnlocked}_name");
            if (localizedLastBadge == $"{summary.lastBadgeUnlocked}_name")
                localizedLastBadge = summary.lastBadgeUnlocked;
            lastBadgeUnlockedText.text = GetLocalizedFormat("ui_last_badge_unlocked", localizedLastBadge);
        }

        // Update score UI
        if (totalScoreText != null)
        {
            totalScoreText.text = summary.totalScore.ToString("N0");
        }

        if (scoreBreakdownText != null)
        {
            int cardPoints = summary.totalCardsCollected * 100;
            int badgePoints = summary.totalScore - cardPoints;

            scoreBreakdownText.text = GetLocalizedFormat("ui_score_breakdown",
                cardPoints.ToString("N0"),
                badgePoints.ToString("N0"));
        }
    }

    // -------------------------------------------------------
    // LOAD ROOM STATS
    // -------------------------------------------------------
    private IEnumerator LoadRoomStatsCoroutine()
    {
        bool completed = false;
        List<RoomStatsDocumentData> roomStats = null;

        FirebaseManager.Instance.LoadRoomStats(odId, (result) =>
        {
            roomStats = result;
            completed = true;
        });

        // Wait with timeout
        float timeout = 0f;
        while (!completed && timeout < 10f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        if (!completed || roomStats == null)
        {
            Debug.LogError("[AchievementsManager] Failed to load room stats (timeout)");
            yield break;
        }

        string summary = "";

        foreach (var room in roomStats)
        {
            string roomId = room.roomId;
            float timeSpent = room.timeSpent;
            int visitCount = room.visitCount;

            // Try to get localized room name
            string roomName = GetLocalizedString($"room_{roomId}_name");
            if (roomName == $"room_{roomId}_name") roomName = roomId;

            summary += GetLocalizedFormat("ui_room_stats_line",
                roomName,
                timeSpent.ToString("F1"),
                visitCount) + "\n";
        }

        if (roomStatsSummaryText != null)
            roomStatsSummaryText.text = summary;
    }
}