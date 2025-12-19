using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Player Data")]
    public string userId;              
    public string age;
    public string nationality;
    public List<string> badges = new List<string>();
    public Dictionary<string, float> roomTimes = new Dictionary<string, float>();
    public int totalCardsCollected = 0;
    public List<string> cardsFound = new List<string>();

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
    /// </summary>
    public async Task<bool> LoginWithParticipantCode(string code)
    {
        if (FirebaseManager.Instance == null)
        {
            Debug.LogError("FirebaseManager not found in scene!");
            return false;
        }

        // The returned value IS the participant code
        userId = await FirebaseManager.Instance.LoginWithParticipantCodeAsync(code);
        
        if (!string.IsNullOrEmpty(userId))
        {
            Debug.Log($"✅ Logged in participant: {userId}");
            return true;
        }

        Debug.LogError("❌ Login failed.");
        return false;
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
        badges.Clear();
        roomTimes.Clear();
        cardsFound.Clear();
        totalCardsCollected = 0;
        Debug.Log(" Player data cleared.");
    }
}