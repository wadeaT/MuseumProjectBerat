using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// BadgeManager - WebGL-safe version using coroutines instead of async/await
/// FIXED: Properly handles multi-user scenarios by clearing/restoring state on login
/// </summary>
public class BadgeManager : MonoBehaviour
{
    // Singleton pattern - only one BadgeManager exists
    public static BadgeManager instance;

    [Header("Badge Tracking")]
    private int totalCardsCollected = 0;
    private HashSet<string> unlockedBadges = new HashSet<string>();

    // Track which cards have been found in each room
    private Dictionary<string, HashSet<string>> cardsByRoom = new Dictionary<string, HashSet<string>>();

    // Define how many cards each room should have
    private Dictionary<string, int> expectedCardsPerRoom = new Dictionary<string, int>()
    {
        { "balcony", 3 },
        { "bedroom", 3 },
        { "guest_room", 3 },
        { "kitchen", 3 },
        { "workshop", 3 },
        { "archive", 3 }
    };

    [Header("Localization")]
    public string badgeTableName = "FullMuseum";

    [Header("Debug")]
    public bool showDebugMessages = true;

    [Header("Firebase Integration")]
    public bool useFirebase = true;

    private bool _isInitialized = false;

    void Awake()
    {
        // Singleton setup
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // Use coroutine-based initialization instead of async
        StartCoroutine(InitializeCoroutine());
    }

    private IEnumerator InitializeCoroutine()
    {
        // Wait a frame to ensure other managers are initialized
        yield return null;

        // Load progress from Firebase when game starts
        if (useFirebase && PlayerManager.Instance != null && !string.IsNullOrEmpty(PlayerManager.Instance.userId))
        {
            yield return StartCoroutine(LoadProgressFromFirebaseCoroutineInternal());
        }
        else
        {
            // Fallback to local PlayerPrefs
            LoadBadgeProgress();
        }

        _isInitialized = true;

        if (showDebugMessages)
        {
            Debug.Log($"[BadgeManager] Initialized. Total cards collected: {totalCardsCollected}");
            Debug.Log($"[BadgeManager] Unlocked badges: {unlockedBadges.Count}");
        }
    }

    // ============================================================================
    // USER SESSION MANAGEMENT - Critical for multi-user support
    // ============================================================================

    /// <summary>
    /// Clears all in-memory state for new user session.
    /// Call this BEFORE loading a new user's data from Firebase.
    /// Does NOT clear PlayerPrefs (LoginUI handles that separately).
    /// </summary>
    public void ClearForNewUser()
    {
        totalCardsCollected = 0;
        unlockedBadges.Clear();
        cardsByRoom.Clear();
        _isInitialized = false;

        Debug.Log("[BadgeManager] In-memory state cleared for new user session");
    }

    /// <summary>
    /// Extracts the room ID from a card ID.
    /// Assumes format like "balcony_card_01" where room is everything before "_card_"
    /// Also handles simpler formats like "balcony_01" where room is the first part
    /// </summary>
    private string ExtractRoomIdFromCardId(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
            return "unknown";

        // Try to find "_card_" pattern first (e.g., "balcony_card_01")
        int cardIndex = cardId.IndexOf("_card_");
        if (cardIndex > 0)
        {
            return cardId.Substring(0, cardIndex);
        }

        // Fallback: take everything before the last underscore (e.g., "balcony_01" → "balcony")
        int lastUnderscore = cardId.LastIndexOf('_');
        if (lastUnderscore > 0)
        {
            return cardId.Substring(0, lastUnderscore);
        }

        // Can't determine room, return unknown
        return "unknown";
    }

    /// <summary>
    /// Restores cardsByRoom dictionary from a list of collected card IDs.
    /// This is needed when loading a returning user's progress.
    /// </summary>
    private void RestoreCardsByRoomFromCardList(List<string> cardIds)
    {
        cardsByRoom.Clear();

        foreach (string cardId in cardIds)
        {
            string roomId = ExtractRoomIdFromCardId(cardId);

            if (!cardsByRoom.ContainsKey(roomId))
            {
                cardsByRoom[roomId] = new HashSet<string>();
            }

            cardsByRoom[roomId].Add(cardId);
        }

        if (showDebugMessages)
        {
            Debug.Log($"[BadgeManager] Restored cardsByRoom from {cardIds.Count} cards:");
            foreach (var kvp in cardsByRoom)
            {
                Debug.Log($"  Room '{kvp.Key}': {kvp.Value.Count} cards");
            }
        }
    }

    // ============================================================================
    // LOCALIZATION HELPERS
    // ============================================================================

    /// <summary>
    /// Get localized string from the Badges table - synchronous version with fallback
    /// </summary>
    private string GetLocalizedString(string key)
    {
        try
        {
            var stringTable = LocalizationSettings.StringDatabase.GetTable(badgeTableName);
            if (stringTable != null)
            {
                var entry = stringTable.GetEntry(key);
                if (entry != null)
                {
                    return entry.GetLocalizedString();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BadgeManager] Localization error for '{key}': {e.Message}");
        }

        // Fallback: return the key itself if not found
        return key;
    }

    /// <summary>
    /// Get localized badge name
    /// </summary>
    private string GetBadgeName(string badgeID)
    {
        return GetLocalizedString($"{badgeID}_name");
    }

    /// <summary>
    /// Get localized badge description
    /// </summary>
    private string GetBadgeDescription(string badgeID)
    {
        return GetLocalizedString($"{badgeID}_desc");
    }

    // ============================================================================
    // FIREBASE INTEGRATION - Coroutine-based
    // ============================================================================

    /// <summary>
    /// Public method to load progress from Firebase with callback
    /// </summary>
    public void LoadProgressFromFirebaseCoroutine(System.Action callback = null)
    {
        StartCoroutine(LoadProgressFromFirebaseCoroutineWithCallback(callback));
    }

    private IEnumerator LoadProgressFromFirebaseCoroutineWithCallback(System.Action callback)
    {
        yield return StartCoroutine(LoadProgressFromFirebaseCoroutineInternal());
        callback?.Invoke();
    }

    /// <summary>
    /// Load user's progress from Firebase using coroutine
    /// </summary>
    private IEnumerator LoadProgressFromFirebaseCoroutineInternal()
    {
        if (PlayerManager.Instance == null || string.IsNullOrEmpty(PlayerManager.Instance.userId))
        {
            Debug.LogWarning("[BadgeManager] Cannot load from Firebase: User not logged in");
            yield break;
        }

        // CRITICAL: Clear existing state before loading new user's data
        ClearForNewUser();

        bool completed = false;

        // Load from PlayerManager (which loads from Firebase)
        PlayerManager.Instance.LoadUserProgress(() =>
        {
            completed = true;
        });

        // Wait for completion with timeout
        float timeout = 10f;
        float elapsed = 0f;
        while (!completed && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!completed)
        {
            Debug.LogWarning("[BadgeManager] Loading from Firebase timed out");
            yield break;
        }

        // Sync local data from PlayerManager
        totalCardsCollected = PlayerManager.Instance.totalCardsCollected;

        unlockedBadges.Clear();
        foreach (string badgeId in PlayerManager.Instance.badges)
        {
            unlockedBadges.Add(badgeId);
        }

        // CRITICAL FIX: Restore cardsByRoom from the list of collected cards
        RestoreCardsByRoomFromCardList(PlayerManager.Instance.cardsFound);

        _isInitialized = true;

        Debug.Log($"[BadgeManager] Loaded from Firebase: {totalCardsCollected} cards, {unlockedBadges.Count} badges");
    }

    // ============================================================================
    // CARD COLLECTION
    // ============================================================================

    /// <summary>
    /// Call this when ANY card is collected
    /// </summary>
    public void OnCardCollected(string cardID, string roomID)
    {
        totalCardsCollected++;

        // Track room-specific progress
        if (!cardsByRoom.ContainsKey(roomID))
        {
            cardsByRoom[roomID] = new HashSet<string>();
        }

        // Check if this is the first card in this room
        bool isFirstInRoom = cardsByRoom[roomID].Count == 0;

        cardsByRoom[roomID].Add(cardID);

        // Award points for card collection
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnCardCollected(cardID, roomID, isFirstInRoom);
        }

        // Log room progress for debugging
        int cardsInRoom = cardsByRoom[roomID].Count;
        int expectedCards = expectedCardsPerRoom.ContainsKey(roomID) ? expectedCardsPerRoom[roomID] : 0;
        Debug.Log($"[BadgeManager] Room '{roomID}' progress: {cardsInRoom}/{expectedCards} cards found");

        // Save to Firebase
        if (useFirebase && PlayerManager.Instance != null && !string.IsNullOrEmpty(PlayerManager.Instance.userId))
        {
            PlayerManager.Instance.OnCardCollected(cardID);
        }
        else
        {
            // Fallback to local storage
            SaveProgress();
        }

        if (showDebugMessages)
        {
            Debug.Log($"[BadgeManager] Card collected! Total: {totalCardsCollected}");
        }

        // Check if any badges should be unlocked
        CheckBadgeUnlocks();
    }

    // ============================================================================
    // BADGE UNLOCK CHECKS
    // ============================================================================

    /// <summary>
    /// Check if player earned any badges
    /// </summary>
    void CheckBadgeUnlocks()
    {
        // ============================================================
        // MILESTONE BADGES (based on total card count)
        // ============================================================

        // Badge 1: First Discovery - collect any card
        if (totalCardsCollected >= 1 && !HasBadge("first_discovery"))
        {
            UnlockBadge("first_discovery");
        }

        // Badge 2: Curious Explorer - collect 3 cards
        if (totalCardsCollected >= 3 && !HasBadge("curious_explorer"))
        {
            UnlockBadge("curious_explorer");
        }

        // Badge 3: Dedicated Seeker - collect 6 cards
        if (totalCardsCollected >= 6 && !HasBadge("dedicated_seeker"))
        {
            UnlockBadge("dedicated_seeker");
        }

        // Badge 4: Persistent Scholar - collect 9 cards
        if (totalCardsCollected >= 9 && !HasBadge("persistent_scholar"))
        {
            UnlockBadge("persistent_scholar");
        }

        // Badge 5: Heritage Guardian - collect 15 cards
        if (totalCardsCollected >= 15 && !HasBadge("heritage_guardian"))
        {
            UnlockBadge("heritage_guardian");
        }

        // Badge 6: Complete Collection - collect all 18 cards
        if (totalCardsCollected >= 18 && !HasBadge("complete_collection"))
        {
            UnlockBadge("complete_collection");
        }

        // ============================================================
        // ROOM COMPLETION BADGES (based on completing specific rooms)
        // ============================================================

        // Balcony Complete (Çardak)
        if (HasAllCardsInRoom("balcony") && !HasBadge("balcony_master"))
        {
            UnlockBadge("balcony_master");
        }

        // Bedroom Complete
        if (HasAllCardsInRoom("bedroom") && !HasBadge("bedroom_master"))
        {
            UnlockBadge("bedroom_master");
        }

        // Guest Room Complete
        if (HasAllCardsInRoom("guest_room") && !HasBadge("guest_room_master"))
        {
            UnlockBadge("guest_room_master");
        }

        // Kitchen Complete
        if (HasAllCardsInRoom("kitchen") && !HasBadge("kitchen_master"))
        {
            UnlockBadge("kitchen_master");
        }

        // Workshop Complete
        if (HasAllCardsInRoom("workshop") && !HasBadge("workshop_master"))
        {
            UnlockBadge("workshop_master");
        }

        // Archive Complete
        if (HasAllCardsInRoom("archive") && !HasBadge("archive_master"))
        {
            UnlockBadge("archive_master");
        }

        // ============================================================
        // MASTER COMPLETION BADGE (all rooms finished)
        // ============================================================

        if (HasCompletedAllRooms() && !HasBadge("museum_master"))
        {
            UnlockBadge("museum_master");
        }
    }

    // ============================================================================
    // BADGE UNLOCKING
    // ============================================================================

    /// <summary>
    /// Unlock a specific badge (localized version)
    /// </summary>
    void UnlockBadge(string badgeID)
    {
        if (unlockedBadges.Contains(badgeID))
        {
            return; // Already unlocked
        }

        unlockedBadges.Add(badgeID);

        // Get localized name and description
        string badgeName = GetBadgeName(badgeID);
        string description = GetBadgeDescription(badgeID);

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnBadgeUnlocked(totalCardsCollected);
        }

        // Save to Firebase
        if (useFirebase && PlayerManager.Instance != null && !string.IsNullOrEmpty(PlayerManager.Instance.userId))
        {
            PlayerManager.Instance.AddBadge(badgeID, badgeName, description);
        }
        else
        {
            // Fallback to local storage
            PlayerPrefs.SetInt($"Badge_{badgeID}_Unlocked", 1);
            PlayerPrefs.Save();
        }

        Debug.Log("╔════════════════════════════════════════╗");
        Debug.Log($"║   BADGE UNLOCKED: {badgeName}");
        Debug.Log($"║  {description}");
        Debug.Log("╚════════════════════════════════════════╝");

        // Show badge popup UI with badgeID for icon lookup
        ShowBadgeNotification(badgeID, badgeName, description);
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

    // ============================================================================
    // LOCAL STORAGE (FALLBACK)
    // ============================================================================

    /// <summary>
    /// Save progress to PlayerPrefs (fallback when offline)
    /// </summary>
    void SaveProgress()
    {
        PlayerPrefs.SetInt("TotalCardsCollected", totalCardsCollected);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Load progress from PlayerPrefs (fallback when offline)
    /// </summary>
    void LoadBadgeProgress()
    {
        // Load total cards collected
        totalCardsCollected = PlayerPrefs.GetInt("TotalCardsCollected", 0);

        // Load unlocked badges
        unlockedBadges.Clear();

        // Check each possible badge
        string[] allBadgeIDs = {
            "first_discovery",
            "curious_explorer",
            "dedicated_seeker",
            "persistent_scholar",
            "heritage_guardian",
            "complete_collection",
            "balcony_master",
            "bedroom_master",
            "guest_room_master",
            "kitchen_master",
            "workshop_master",
            "archive_master",
            "museum_master"
        };

        foreach (string badgeID in allBadgeIDs)
        {
            if (PlayerPrefs.GetInt($"Badge_{badgeID}_Unlocked", 0) == 1)
            {
                unlockedBadges.Add(badgeID);
            }
        }
    }

    /// <summary>
    /// Show badge notification UI
    /// </summary>
    void ShowBadgeNotification(string badgeID, string badgeName, string description)
    {
        // Show UI notification with badgeID for icon lookup
        if (BadgeNotificationUI.instance != null)
        {
            BadgeNotificationUI.instance.ShowBadge(badgeID, badgeName, description);
        }
        else
        {
            Debug.LogWarning("[BadgeManager] BadgeNotificationUI not found in scene!");
        }
    }

    /// <summary>
    /// Reset all badges (for testing)
    /// </summary>
    public void ResetAllBadges()
    {
        unlockedBadges.Clear();
        totalCardsCollected = 0;
        cardsByRoom.Clear();

        // Clear Firebase data
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.badges.Clear();
            PlayerManager.Instance.totalCardsCollected = 0;
            PlayerManager.Instance.cardsFound.Clear();
        }

        // Clear ALL PlayerPrefs
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Debug.Log("[BadgeManager] All badges, cards, and progress reset!");
    }

    // ============================================================================
    // ROOM PROGRESS HELPERS
    // ============================================================================

    /// <summary>
    /// Check if player has found all cards in a specific room
    /// </summary>
    public bool HasAllCardsInRoom(string roomID)
    {
        if (!expectedCardsPerRoom.ContainsKey(roomID))
        {
            Debug.LogWarning($"[BadgeManager] Room '{roomID}' not defined in expectedCardsPerRoom!");
            return false;
        }

        int expectedCards = expectedCardsPerRoom[roomID];
        int foundCards = cardsByRoom.ContainsKey(roomID) ? cardsByRoom[roomID].Count : 0;

        return foundCards >= expectedCards;
    }

    /// <summary>
    /// Get progress for a specific room (e.g., "2/4")
    /// </summary>
    public string GetRoomProgress(string roomID)
    {
        int expectedCards = expectedCardsPerRoom.ContainsKey(roomID) ? expectedCardsPerRoom[roomID] : 0;
        int foundCards = cardsByRoom.ContainsKey(roomID) ? cardsByRoom[roomID].Count : 0;

        return $"{foundCards}/{expectedCards}";
    }

    /// <summary>
    /// Get how many cards have been found in a room
    /// </summary>
    public int GetCardsFoundInRoom(string roomID)
    {
        return cardsByRoom.ContainsKey(roomID) ? cardsByRoom[roomID].Count : 0;
    }

    /// <summary>
    /// Check if player has completed ALL rooms
    /// </summary>
    public bool HasCompletedAllRooms()
    {
        foreach (var room in expectedCardsPerRoom.Keys)
        {
            if (!HasAllCardsInRoom(room))
            {
                return false;
            }
        }
        return true;
    }
}