using UnityEngine;
using System.Collections.Generic;

public class BadgeManager : MonoBehaviour
{
    // Singleton pattern - only one BadgeManager exists
    public static BadgeManager instance;

    [Header("Badge Tracking")]
    private int totalCardsCollected = 0;
    private HashSet<string> unlockedBadges = new HashSet<string>();

    [Header("Debug")]
    public bool showDebugMessages = true;

    void Awake()
    {
        // Singleton setup
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // Persists between scenes
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        LoadBadgeProgress();
    }

    void Start()
    {
        if (showDebugMessages)
        {
            Debug.Log($"BadgeManager initialized. Total cards collected: {totalCardsCollected}");
            Debug.Log($"Unlocked badges: {unlockedBadges.Count}");
        }
    }

    /// <summary>
    /// Call this when ANY card is collected
    /// </summary>
    public void OnCardCollected(string cardID)
    {
        totalCardsCollected++;
        SaveProgress();

        if (showDebugMessages)
        {
            Debug.Log($"📊 Card collected! Total: {totalCardsCollected}");
        }

        // Check if any badges should be unlocked
        CheckBadgeUnlocks();
    }

    /// <summary>
    /// Check if player earned any badges
    /// </summary>
    void CheckBadgeUnlocks()
    {
        // Badge 1: First Discovery - collect any card
        if (totalCardsCollected >= 1 && !HasBadge("first_discovery"))
        {
            UnlockBadge("first_discovery", "First Discovery",
                "You found your first hidden card! The museum's secrets await.");
        }

        // Badge 2: Curious Explorer - collect 3 cards
        if (totalCardsCollected >= 3 && !HasBadge("curious_explorer"))
        {
            UnlockBadge("curious_explorer", "Curious Explorer",
                "You've discovered 3 cards. Your curiosity is rewarded!");
        }

        // Badge 3: Dedicated Seeker - collect 5 cards
        if (totalCardsCollected >= 5 && !HasBadge("dedicated_seeker"))
        {
            UnlockBadge("dedicated_seeker", "Dedicated Seeker",
                "Five cards found! You're truly exploring every corner.");
        }

        // Badge 4: Master Collector - collect 10 cards
        if (totalCardsCollected >= 10 && !HasBadge("master_collector"))
        {
            UnlockBadge("master_collector", "Master Collector",
                "Ten cards! You're a true museum detective.");
        }

        // Add more badges as you create more rooms and cards!
    }

    /// <summary>
    /// Unlock a specific badge
    /// </summary>
    void UnlockBadge(string badgeID, string badgeName, string description)
    {
        if (unlockedBadges.Contains(badgeID))
        {
            return; // Already unlocked
        }

        unlockedBadges.Add(badgeID);

        // Save immediately
        PlayerPrefs.SetInt($"Badge_{badgeID}_Unlocked", 1);
        PlayerPrefs.Save();

        Debug.Log($"🎉 BADGE UNLOCKED: {badgeName}");
        Debug.Log($"   {description}");

        // TODO: Show badge popup UI (we'll create this next)
        ShowBadgeNotification(badgeName, description);
    }

    /// <summary>
    /// Check if player has a specific badge
    /// </summary>
    public bool HasBadge(string badgeID)
    {
        return unlockedBadges.Contains(badgeID);
    }

    /// <summary>
    /// Get total number of cards collected
    /// </summary>
    public int GetTotalCardsCollected()
    {
        return totalCardsCollected;
    }

    /// <summary>
    /// Get list of all unlocked badges
    /// </summary>
    public List<string> GetUnlockedBadges()
    {
        return new List<string>(unlockedBadges);
    }

    /// <summary>
    /// Save progress to PlayerPrefs (later: save to Firebase)
    /// </summary>
    void SaveProgress()
    {
        PlayerPrefs.SetInt("TotalCardsCollected", totalCardsCollected);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Load progress from PlayerPrefs (later: load from Firebase)
    /// </summary>
    void LoadBadgeProgress()
    {
        // Load total cards collected
        totalCardsCollected = PlayerPrefs.GetInt("TotalCardsCollected", 0);

        // Load unlocked badges
        unlockedBadges.Clear();

        // Check each possible badge
        string[] allBadgeIDs = { "first_discovery", "curious_explorer", "dedicated_seeker", "master_collector" };

        foreach (string badgeID in allBadgeIDs)
        {
            if (PlayerPrefs.GetInt($"Badge_{badgeID}_Unlocked", 0) == 1)
            {
                unlockedBadges.Add(badgeID);
            }
        }
    }

    /// <summary>
    /// Show badge notification (placeholder for now)
    /// </summary>
    void ShowBadgeNotification(string badgeName, string description)
    {
        // Show UI notification
        if (BadgeNotificationUI.instance != null)
        {
            // Extract emoji from badge name if it has one
            string icon = "🎖️"; // Default

            if (badgeName.Contains("🎖️")) icon = "🎖️";
            else if (badgeName.Contains("🔍")) icon = "🔍";
            else if (badgeName.Contains("⭐")) icon = "⭐";
            else if (badgeName.Contains("👑")) icon = "👑";
            else if (badgeName.Contains("🏛️")) icon = "🏛️";

            BadgeNotificationUI.instance.ShowBadge(badgeName, description, icon);
        }
        else
        {
            Debug.LogWarning("BadgeNotificationUI not found in scene!");
        }
    }

    /// <summary>
    /// Reset all badges (for testing)
    /// </summary>
    public void ResetAllBadges()
    {
        unlockedBadges.Clear();
        totalCardsCollected = 0;

        PlayerPrefs.DeleteKey("TotalCardsCollected");
        PlayerPrefs.DeleteKey("Badge_first_discovery_Unlocked");
        PlayerPrefs.DeleteKey("Badge_curious_explorer_Unlocked");
        PlayerPrefs.DeleteKey("Badge_dedicated_seeker_Unlocked");
        PlayerPrefs.DeleteKey("Badge_master_collector_Unlocked");
        PlayerPrefs.Save();

        Debug.Log("🔄 All badges reset!");
    }
}