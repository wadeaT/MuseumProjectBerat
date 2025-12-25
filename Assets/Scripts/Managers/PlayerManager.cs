using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerManager - WebGL-safe version using coroutines instead of async/await
/// </summary>
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
            Debug.Log("[PlayerManager] Initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ------------------------------------------------------------------------
    //  AUTHENTICATION (Participant Code) - Coroutine-based
    // ------------------------------------------------------------------------

    /// <summary>
    /// Login with a participant code using coroutine callback pattern
    /// Returns success via callback
    /// </summary>
    public void LoginWithParticipantCode(string code, System.Action<bool> callback)
    {
        StartCoroutine(LoginCoroutine(code, callback));
    }

    private IEnumerator LoginCoroutine(string code, System.Action<bool> callback)
    {
        if (FirebaseManager.Instance == null)
        {
            Debug.LogError("[PlayerManager] FirebaseManager not found in scene!");
            callback?.Invoke(false);
            yield break;
        }

        bool completed = false;
        string result = null;

        FirebaseManager.Instance.LoginWithParticipantCode(code,
            (uid) => { result = uid; completed = true; },
            (error) => { result = null; completed = true; }
        );

        // Wait for completion with timeout
        float timeout = 10f;
        float elapsed = 0f;
        while (!completed && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!string.IsNullOrEmpty(result))
        {
            userId = result;
            Debug.Log($"[PlayerManager] Logged in participant: {userId}");
            callback?.Invoke(true);
        }
        else
        {
            Debug.LogError("[PlayerManager] Login failed.");
            callback?.Invoke(false);
        }
    }

    // ------------------------------------------------------------------------
    //  DEMOGRAPHICS - Coroutine-based
    // ------------------------------------------------------------------------

    public void SaveDemographics(string age, string gender, string nationality, string skills, string vr, System.Action<bool> callback = null)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("[PlayerManager] User not logged in; demographics not saved.");
            callback?.Invoke(false);
            return;
        }

        this.age = age;
        this.nationality = nationality;

        FirebaseManager.Instance.SaveDemographics(userId, age, gender, nationality, skills, vr, callback);
    }

    // ------------------------------------------------------------------------
    //  BADGES - Coroutine-based
    // ------------------------------------------------------------------------

    public void AddBadge(string badgeId, string badgeName, string description, System.Action<bool> callback = null)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("[PlayerManager] User not logged in; badge not saved.");
            callback?.Invoke(false);
            return;
        }

        if (!badges.Contains(badgeId))
        {
            badges.Add(badgeId);
            FirebaseManager.Instance.SaveBadge(userId, badgeId, badgeName, description, (success) =>
            {
                if (success)
                {
                    Debug.Log($"[PlayerManager] Badge added: {badgeName}");
                }
                callback?.Invoke(success);
            });
        }
        else
        {
            Debug.Log($"[PlayerManager] Badge '{badgeId}' already unlocked.");
            callback?.Invoke(true);
        }
    }

    /// <summary>
    /// Load user's badges from Firebase when they log in
    /// </summary>
    public void LoadUserProgress(System.Action callback = null)
    {
        StartCoroutine(LoadUserProgressCoroutine(callback));
    }

    private IEnumerator LoadUserProgressCoroutine(System.Action callback)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("[PlayerManager] User not logged in; cannot load progress.");
            callback?.Invoke();
            yield break;
        }

        bool badgesLoaded = false;
        bool cardsLoaded = false;

        // Load badges
        FirebaseManager.Instance.LoadUserBadges(userId, (loadedBadges) =>
        {
            badges = loadedBadges;
            badgesLoaded = true;
        });

        // Load cards count
        FirebaseManager.Instance.LoadUserCards(userId, (count) =>
        {
            totalCardsCollected = count;
            cardsLoaded = true;
        });

        // Wait for both to complete with timeout
        float timeout = 10f;
        float elapsed = 0f;
        while ((!badgesLoaded || !cardsLoaded) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log($"[PlayerManager] Loaded user progress: {badges.Count} badges, {totalCardsCollected} cards");
        callback?.Invoke();
    }

    // ------------------------------------------------------------------------
    //  CARDS - Coroutine-based
    // ------------------------------------------------------------------------

    public void OnCardCollected(string cardId, System.Action<bool> callback = null)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("[PlayerManager] User not logged in; card not saved.");
            callback?.Invoke(false);
            return;
        }

        if (!cardsFound.Contains(cardId))
        {
            cardsFound.Add(cardId);
            totalCardsCollected++;

            FirebaseManager.Instance.SaveCardCollected(userId, cardId, totalCardsCollected, (success) =>
            {
                if (success)
                {
                    Debug.Log($"[PlayerManager] Card collected: {cardId} (Total: {totalCardsCollected})");
                }
                callback?.Invoke(success);
            });
        }
        else
        {
            callback?.Invoke(true);
        }
    }

    // ------------------------------------------------------------------------
    //  ROOM TIMES - Coroutine-based
    // ------------------------------------------------------------------------

    public void SaveRoomTime(string roomId, float time, System.Action<bool> callback = null)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("[PlayerManager] User not logged in; room time not saved.");
            callback?.Invoke(false);
            return;
        }

        roomTimes[roomId] = time;
        FirebaseManager.Instance.SaveRoomTime(userId, roomId, time, callback);
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
        Debug.Log("[PlayerManager] Player data cleared.");
    }
}