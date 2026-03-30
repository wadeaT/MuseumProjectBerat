using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

/// <summary>
/// PlayerManager - FIXED VERSION v2
/// 
/// FIXES:
/// 1. Now properly passes group assignment to Firebase on login
/// 2. CLEARS PlayerPrefs for cards/score when new user logs in (prevents cross-user contamination)
/// 3. Uses user-specific PlayerPrefs keys for persistence
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Player Data")]
    public string userId;
    public string age;
    public string nationality;
    public string groupAssignment;
    public List<string> badges = new List<string>();
    public Dictionary<string, float> roomTimes = new Dictionary<string, float>();
    public int totalCardsCollected = 0;
    public List<string> cardsFound = new List<string>();

    // Track last logged-in user to detect user switches
    private const string LAST_USER_KEY = "LastLoggedInUser";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ------------------------------------------------------------------------
    //  AUTHENTICATION (Participant Code)
    // ------------------------------------------------------------------------

    /// <summary>
    /// Login with a participant code (e.g., "P001").
    /// The code becomes the document ID in Firestore.
    /// 
    /// ✅ FIX: Now clears previous user's PlayerPrefs data when a new user logs in
    /// </summary>
    public async Task<bool> LoginWithParticipantCode(string code)
    {
        if (FirebaseManager.Instance == null)
        {
            Debug.LogError("FirebaseManager not found in scene!");
            return false;
        }

        code = code.Trim().ToUpper();

        // ✅ CHECK IF THIS IS A DIFFERENT USER - Clear PlayerPrefs if so
        string lastUser = PlayerPrefs.GetString(LAST_USER_KEY, "");
        if (!string.IsNullOrEmpty(lastUser) && lastUser != code)
        {
            Debug.Log($"╔════════════════════════════════════════════════════════════════╗");
            Debug.Log($"║  NEW USER DETECTED: {lastUser} → {code}");
            Debug.Log($"║  Clearing previous user's local data...");
            Debug.Log($"╚════════════════════════════════════════════════════════════════╝");

            ClearAllGamePlayerPrefs();
        }

        // Save current user as last user
        PlayerPrefs.SetString(LAST_USER_KEY, code);
        PlayerPrefs.Save();

        // Determine group assignment from participant number
        groupAssignment = DetermineGroupAssignment(code);

        Debug.Log($"[PlayerManager] Participant {code} → Group: {groupAssignment}");

        // Login to Firebase
        userId = await FirebaseManager.Instance.LoginWithParticipantCodeAsync(code, groupAssignment);

        if (!string.IsNullOrEmpty(userId))
        {
            Debug.Log($"✅ Logged in participant: {userId} (Group: {groupAssignment})");

            // Clear in-memory data for fresh start
            ClearPlayerData();
            userId = code; // Re-set after clear
            groupAssignment = DetermineGroupAssignment(code);

            return true;
        }

        Debug.LogError("❌ Login failed.");
        return false;
    }

    /// <summary>
    /// ✅ NEW: Clear ALL game-related PlayerPrefs (cards, score, badges)
    /// Called when a different user logs in on the same device
    /// </summary>
    public void ClearAllGamePlayerPrefs()
    {
        Debug.Log("[PlayerManager] Clearing all game PlayerPrefs for new user...");

        // Clear score
        PlayerPrefs.DeleteKey("TotalScore");

        // Clear all card discovery states
        // Since we don't know all card IDs, we need to iterate through possible keys
        // The cards use format: Card_{cardID}_Found
        string[] knownCardPrefixes = new string[]
        {
            // Balcony cards
            "Card_balcony_card_01_Found",
            "Card_balcony_card_02_Found",
            "Card_balcony_card_03_Found",
            // Bedroom cards
            "Card_bedroom_card_01_Found",
            "Card_bedroom_card_02_Found",
            "Card_bedroom_card_03_Found",
            // Guest room cards
            "Card_guest_room_card_01_Found",
            "Card_guest_room_card_02_Found",
            "Card_guest_room_card_03_Found",
            // Kitchen cards
            "Card_kitchen_card_01_Found",
            "Card_kitchen_card_02_Found",
            "Card_kitchen_card_03_Found",
            // Workshop cards
            "Card_workshop_card_01_Found",
            "Card_workshop_card_02_Found",
            "Card_workshop_card_03_Found",
            // Archive cards
            "Card_archive_card_01_Found",
            "Card_archive_card_02_Found",
            "Card_archive_card_03_Found",
        };

        foreach (string key in knownCardPrefixes)
        {
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                Debug.Log($"   Cleared: {key}");
            }
        }

        // Also clear any dynamically named cards (brute force for safety)
        // This catches any cards with non-standard naming
        for (int i = 1; i <= 20; i++)
        {
            string[] rooms = { "balcony", "bedroom", "guest_room", "kitchen", "workshop", "archive", "living_room", "hallway" };
            foreach (string room in rooms)
            {
                string key = $"Card_{room}_card_{i:D2}_Found";
                PlayerPrefs.DeleteKey(key);
            }
        }

        PlayerPrefs.Save();
        Debug.Log("[PlayerManager] ✅ All game PlayerPrefs cleared!");
    }

    /// <summary>
    /// Determine group assignment based on participant number
    /// Odd = Control (Static), Even = Adaptive
    /// </summary>
    private string DetermineGroupAssignment(string code)
    {
        string digitsOnly = Regex.Replace(code, @"\D", "");

        if (int.TryParse(digitsOnly, out int number))
        {
            return (number % 2 == 1) ? "Control" : "Adaptive";
        }

        Debug.LogWarning($"[PlayerManager] Could not parse number from '{code}', defaulting to Control");
        return "Control";
    }

    /// <summary>
    /// Check if this participant is in the Adaptive group
    /// </summary>
    public bool IsAdaptiveGroup()
    {
        return groupAssignment == "Adaptive";
    }

    /// <summary>
    /// Check if this participant is in the Control group
    /// </summary>
    public bool IsControlGroup()
    {
        return groupAssignment == "Control";
    }

    // ------------------------------------------------------------------------
    //  DEMOGRAPHICS
    // ------------------------------------------------------------------------

    public async void SaveDemographics(string age, string gender, string nationality, string skills, string vr)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("User not logged in; demographics not saved.");
            return;
        }

        this.age = age;
        this.nationality = nationality;

        await FirebaseManager.Instance.SaveDemographicsAsync(userId, age, gender, nationality, skills, vr);
    }

    // ------------------------------------------------------------------------
    //  BADGES
    // ------------------------------------------------------------------------

    public async void AddBadge(string badgeId, string badgeName, string description)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("User not logged in; badge not saved.");
            return;
        }

        if (!badges.Contains(badgeId))
        {
            badges.Add(badgeId);
            await FirebaseManager.Instance.SaveBadgeAsync(userId, badgeId, badgeName, description);
            Debug.Log($"🏅 Badge added: {badgeName}");
        }
        else
        {
            Debug.Log($"Badge '{badgeId}' already unlocked.");
        }
    }

    /// <summary>
    /// Load user's badges from Firebase when they log in
    /// </summary>
    public async Task LoadUserProgressAsync()
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("User not logged in; cannot load progress.");
            return;
        }

        badges = await FirebaseManager.Instance.LoadUserBadgesAsync(userId);
        totalCardsCollected = await FirebaseManager.Instance.LoadUserCardsAsync(userId);

        Debug.Log($"Loaded user progress: {badges.Count} badges, {totalCardsCollected} cards");
    }

    // ------------------------------------------------------------------------
    //  CARDS
    // ------------------------------------------------------------------------

    public async void OnCardCollected(string cardId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("User not logged in; card not saved.");
            return;
        }

        if (!cardsFound.Contains(cardId))
        {
            cardsFound.Add(cardId);
            totalCardsCollected++;

            await FirebaseManager.Instance.SaveCardCollectedAsync(userId, cardId, totalCardsCollected);
            Debug.Log($"📜 Card collected: {cardId} (Total: {totalCardsCollected})");
        }
    }

    // ------------------------------------------------------------------------
    //  ROOM TIMES
    // ------------------------------------------------------------------------

    public async void SaveRoomTime(string roomId, float time)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("User not logged in; room time not saved.");
            return;
        }

        roomTimes[roomId] = time;
        await FirebaseManager.Instance.SaveRoomTimeAsync(userId, roomId, time);
    }

    // ------------------------------------------------------------------------
    //  UTILITY
    // ------------------------------------------------------------------------

    public void ClearPlayerData()
    {
        userId = null;
        age = null;
        nationality = null;
        groupAssignment = null;
        badges.Clear();
        roomTimes.Clear();
        cardsFound.Clear();
        totalCardsCollected = 0;
        Debug.Log("Player data cleared.");
    }
}