using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FirebaseManager for WebGL builds.
/// Uses JavaScript bridge for all Firebase operations.
/// FIXED: Replaced async/await with coroutine-based callbacks for WebGL compatibility.
/// </summary>
public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }

    public bool IsReady { get; private set; } = false;
    public string CurrentParticipantCode { get; private set; }

    // Callback delegates for async operations
    private Action<string> _loginSuccessCallback;
    private Action<string> _loginErrorCallback;
    private Action<bool> _demographicsCallback;
    private Action<bool> _badgeCallback;
    private Action<bool> _cardCallback;
    private Action<List<string>> _loadBadgesCallback;
    private Action<int> _loadCardsCallback;
    private Action<bool> _roomStatsCallback;
    private Action<bool> _scoreCallback;
    private Action<bool> _interactionCallback;
    private Action<bool> _statsCallback;
    private Action<bool> _checkDemographicsCallback;
    private Action<List<BadgeDocumentData>> _loadBadgesWithDataCallback;
    private Action<List<CardDocumentData>> _loadCardsWithDataCallback;
    private Action<ProgressSummaryData> _loadProgressSummaryCallback;
    private Action<List<RoomStatsDocumentData>> _loadRoomStatsCallback;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[FirebaseManager] WebGL build - waiting for Firebase initialization from JavaScript");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Called from JavaScript when Firebase is initialized and ready
    /// </summary>
    public void OnFirebaseReady()
    {
        IsReady = true;
        Debug.Log("[FirebaseManager] Firebase ready (WebGL)");
    }

    // ------------------------------------------------------------------------
    // PARTICIPANT LOGIN - Coroutine-based approach
    // ------------------------------------------------------------------------

    /// <summary>
    /// Login with participant code using coroutine callback pattern
    /// </summary>
    public void LoginWithParticipantCode(string participantCode, Action<string> onSuccess, Action<string> onError)
    {
        if (!IsReady)
        {
            Debug.LogWarning("[FirebaseManager] Firebase not ready yet.");
            onError?.Invoke("Firebase not ready");
            return;
        }

        if (string.IsNullOrWhiteSpace(participantCode))
        {
            Debug.LogError("[FirebaseManager] Participant code cannot be empty.");
            onError?.Invoke("Participant code cannot be empty");
            return;
        }

        participantCode = participantCode.Trim().ToUpper();
        CurrentParticipantCode = participantCode;

        var data = JsonUtility.ToJson(new UserData
        {
            participantCode = participantCode,
            lastActive = "SERVER_TIMESTAMP"
        });

        _loginSuccessCallback = onSuccess;
        _loginErrorCallback = onError;

        FirebaseBridge.SetDocument("users", participantCode, data,
            gameObject.name, "OnLoginSuccess", "OnLoginError");
    }

    /// <summary>
    /// Async wrapper for backward compatibility - use sparingly in WebGL
    /// </summary>
    public IEnumerator LoginWithParticipantCodeCoroutine(string participantCode, Action<string> callback)
    {
        bool completed = false;
        string result = null;

        LoginWithParticipantCode(participantCode,
            (userId) => { result = userId; completed = true; },
            (error) => { result = null; completed = true; }
        );

        while (!completed)
        {
            yield return null;
        }

        callback?.Invoke(result);
    }

    public void OnLoginSuccess(string resultMsg)
    {
        Debug.Log($"[FirebaseManager] Participant {CurrentParticipantCode} logged in!");
        _loginSuccessCallback?.Invoke(CurrentParticipantCode);
        _loginSuccessCallback = null;
        _loginErrorCallback = null;
    }

    public void OnLoginError(string error)
    {
        Debug.LogError($"[FirebaseManager] Login failed: {error}");
        _loginErrorCallback?.Invoke(error);
        _loginSuccessCallback = null;
        _loginErrorCallback = null;
    }

    // ------------------------------------------------------------------------
    // SAVE DEMOGRAPHICS
    // ------------------------------------------------------------------------

    public void SaveDemographics(string odId, string age, string gender, string nationality,
        string computerSkills, string vrInterest, Action<bool> callback = null)
    {
        if (!IsReady)
        {
            Debug.LogError("[FirebaseManager] Firestore not ready.");
            callback?.Invoke(false);
            return;
        }

        Debug.Log($"[FirebaseManager] Saving demographics for {odId}...");

        var data = JsonUtility.ToJson(new DemographicsData
        {
            age = age,
            gender = gender,
            nationality = nationality,
            computerSkills = computerSkills,
            vrInterest = vrInterest
        });

        _demographicsCallback = callback;

        FirebaseBridge.SetDocumentInSubcollection(
            $"users/{odId}/demographics",
            "info",
            data,
            gameObject.name,
            "OnDemographicsSaved",
            "OnDemographicsError"
        );
    }

    public IEnumerator SaveDemographicsCoroutine(string odId, string age, string gender,
        string nationality, string computerSkills, string vrInterest, Action<bool> callback)
    {
        bool completed = false;
        bool success = false;

        SaveDemographics(odId, age, gender, nationality, computerSkills, vrInterest,
            (result) => { success = result; completed = true; });

        while (!completed)
        {
            yield return null;
        }

        callback?.Invoke(success);
    }

    public void OnDemographicsSaved(string result)
    {
        Debug.Log("[FirebaseManager] Demographics saved!");
        _demographicsCallback?.Invoke(true);
        _demographicsCallback = null;
    }

    public void OnDemographicsError(string error)
    {
        Debug.LogError($"[FirebaseManager] Failed to save demographics: {error}");
        _demographicsCallback?.Invoke(false);
        _demographicsCallback = null;
    }

    // ------------------------------------------------------------------------
    // SAVE BADGE
    // ------------------------------------------------------------------------

    public void SaveBadge(string odId, string badgeId, string badgeName, string description, Action<bool> callback = null)
    {
        if (!IsReady)
        {
            Debug.LogError("[FirebaseManager] Firestore not ready.");
            callback?.Invoke(false);
            return;
        }

        var data = JsonUtility.ToJson(new BadgeData
        {
            badgeId = badgeId,
            badgeName = badgeName,
            description = description
        });

        _badgeCallback = callback;

        FirebaseBridge.SetDocumentInSubcollection(
            $"users/{odId}/badges",
            badgeId,
            data,
            gameObject.name,
            "OnBadgeSaved",
            "OnBadgeError"
        );
    }

    public void OnBadgeSaved(string result)
    {
        Debug.Log("[FirebaseManager] Badge saved!");
        _badgeCallback?.Invoke(true);
        _badgeCallback = null;
    }

    public void OnBadgeError(string error)
    {
        Debug.LogError($"[FirebaseManager] Badge error: {error}");
        _badgeCallback?.Invoke(false);
        _badgeCallback = null;
    }

    // ------------------------------------------------------------------------
    // SAVE CARD
    // ------------------------------------------------------------------------

    public void SaveCardCollected(string odId, string cardId, int totalCardsCollected, Action<bool> callback = null)
    {
        if (!IsReady)
        {
            Debug.LogError("[FirebaseManager] Firestore not ready.");
            callback?.Invoke(false);
            return;
        }

        _cardCallback = callback;

        // Save the card document
        var cardData = JsonUtility.ToJson(new CardData
        {
            cardId = cardId,
            found = true
        });

        FirebaseBridge.SetDocumentInSubcollection(
            $"users/{odId}/cards",
            cardId,
            cardData,
            gameObject.name,
            "OnCardSavedStep1",
            "OnCardError"
        );

        // Store data for step 2
        _pendingCardOdId = odId;
        _pendingTotalCards = totalCardsCollected;
        _pendingCardId = cardId;
    }

    private string _pendingCardOdId;
    private int _pendingTotalCards;
    private string _pendingCardId;

    public void OnCardSavedStep1(string result)
    {
        // Now save progress summary
        var progressData = JsonUtility.ToJson(new ProgressData
        {
            totalCardsCollected = _pendingTotalCards,
            lastCardFound = _pendingCardId
        });

        FirebaseBridge.SetDocumentInSubcollection(
            $"users/{_pendingCardOdId}/progress",
            "summary",
            progressData,
            gameObject.name,
            "OnCardSaved",
            "OnCardError"
        );
    }

    public void OnCardSaved(string result)
    {
        Debug.Log($"[FirebaseManager] Card '{_pendingCardId}' saved for {_pendingCardOdId}");
        _cardCallback?.Invoke(true);
        _cardCallback = null;
    }

    public void OnCardError(string error)
    {
        Debug.LogError($"[FirebaseManager] Card error: {error}");
        _cardCallback?.Invoke(false);
        _cardCallback = null;
    }

    // ------------------------------------------------------------------------
    // LOAD USER BADGES
    // ------------------------------------------------------------------------

    public void LoadUserBadges(string odId, Action<List<string>> callback)
    {
        if (!IsReady)
        {
            Debug.LogError("[FirebaseManager] Firestore not ready.");
            callback?.Invoke(new List<string>());
            return;
        }

        _loadBadgesCallback = callback;

        FirebaseBridge.GetCollection(
            $"users/{odId}/badges",
            gameObject.name,
            "OnBadgesLoaded",
            "OnBadgesLoadError"
        );
    }

    public void OnBadgesLoaded(string jsonArray)
    {
        try
        {
            var wrapper = JsonUtility.FromJson<StringArrayWrapper>("{\"items\":" + jsonArray + "}");
            var badges = new List<string>(wrapper.items ?? new string[0]);
            Debug.Log($"[FirebaseManager] Loaded {badges.Count} badges");
            _loadBadgesCallback?.Invoke(badges);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Failed to parse badges: {e.Message}");
            _loadBadgesCallback?.Invoke(new List<string>());
        }
        _loadBadgesCallback = null;
    }

    public void OnBadgesLoadError(string error)
    {
        Debug.LogError($"[FirebaseManager] Failed to load badges: {error}");
        _loadBadgesCallback?.Invoke(new List<string>());
        _loadBadgesCallback = null;
    }

    // ------------------------------------------------------------------------
    // LOAD USER CARDS COUNT
    // ------------------------------------------------------------------------

    public void LoadUserCards(string odId, Action<int> callback)
    {
        if (!IsReady)
        {
            Debug.LogError("[FirebaseManager] Firestore not ready.");
            callback?.Invoke(0);
            return;
        }

        _loadCardsCallback = callback;

        FirebaseBridge.GetCollection(
            $"users/{odId}/cards",
            gameObject.name,
            "OnCardsLoaded",
            "OnCardsLoadError"
        );
    }

    public void OnCardsLoaded(string jsonArray)
    {
        try
        {
            var wrapper = JsonUtility.FromJson<StringArrayWrapper>("{\"items\":" + jsonArray + "}");
            int count = wrapper.items?.Length ?? 0;
            Debug.Log($"[FirebaseManager] Loaded {count} cards");
            _loadCardsCallback?.Invoke(count);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Failed to parse cards: {e.Message}");
            _loadCardsCallback?.Invoke(0);
        }
        _loadCardsCallback = null;
    }

    public void OnCardsLoadError(string error)
    {
        Debug.LogError($"[FirebaseManager] Failed to load cards: {error}");
        _loadCardsCallback?.Invoke(0);
        _loadCardsCallback = null;
    }

    // ------------------------------------------------------------------------
    // SAVE ROOM TIME
    // ------------------------------------------------------------------------

    public void SaveRoomTime(string odId, string roomId, float timeSpent, Action<bool> callback = null)
    {
        if (!IsReady)
        {
            callback?.Invoke(false);
            return;
        }

        var data = JsonUtility.ToJson(new RoomStatsData
        {
            timeSpent = timeSpent,
            visitCount = 1
        });

        _roomStatsCallback = callback;

        FirebaseBridge.SetDocumentInSubcollection(
            $"users/{odId}/roomStats",
            roomId,
            data,
            gameObject.name,
            "OnRoomStatsSaved",
            "OnRoomStatsError"
        );
    }

    public void OnRoomStatsSaved(string result)
    {
        Debug.Log("[FirebaseManager] Room stats saved!");
        _roomStatsCallback?.Invoke(true);
        _roomStatsCallback = null;
    }

    public void OnRoomStatsError(string error)
    {
        Debug.LogError($"[FirebaseManager] Room stats error: {error}");
        _roomStatsCallback?.Invoke(false);
        _roomStatsCallback = null;
    }

    // ------------------------------------------------------------------------
    // SAVE USER SCORE
    // ------------------------------------------------------------------------

    public void SaveUserScore(string odId, int totalScore, Action<bool> callback = null)
    {
        if (!IsReady)
        {
            Debug.LogError("[FirebaseManager] Firestore not ready.");
            callback?.Invoke(false);
            return;
        }

        var data = JsonUtility.ToJson(new ScoreData { totalScore = totalScore });

        _scoreCallback = callback;

        FirebaseBridge.SetDocumentInSubcollection(
            $"users/{odId}/progress",
            "summary",
            data,
            gameObject.name,
            "OnScoreSaved",
            "OnScoreError"
        );
    }

    public void OnScoreSaved(string result)
    {
        Debug.Log("[FirebaseManager] Score saved!");
        _scoreCallback?.Invoke(true);
        _scoreCallback = null;
    }

    public void OnScoreError(string error)
    {
        Debug.LogError($"[FirebaseManager] Score error: {error}");
        _scoreCallback?.Invoke(false);
        _scoreCallback = null;
    }

    // ------------------------------------------------------------------------
    // SAVE OBJECT INTERACTION
    // ------------------------------------------------------------------------

    private string _pendingInteractionOdId;
    private string _pendingObjectName;
    private int _pendingTotalInteractions;
    private float _pendingAverageTime;

    public void SaveObjectInteraction(string odId, string objectName, float duration,
        int totalInteractions, float averageTime, Action<bool> callback = null)
    {
        if (!IsReady)
        {
            Debug.LogError("[FirebaseManager] Firestore not ready.");
            callback?.Invoke(false);
            return;
        }

        string docId = $"{objectName}_{DateTime.UtcNow.Ticks}";

        // Store for step 2
        _pendingInteractionOdId = odId;
        _pendingObjectName = objectName;
        _pendingTotalInteractions = totalInteractions;
        _pendingAverageTime = averageTime;
        _interactionCallback = callback;

        // Save interaction record
        var interactionData = JsonUtility.ToJson(new ObjectInteractionData
        {
            objectName = objectName,
            duration = duration,
            totalInteractions = totalInteractions,
            averageTime = averageTime
        });

        FirebaseBridge.SetDocumentInSubcollection(
            $"users/{odId}/objectInteractions",
            docId,
            interactionData,
            gameObject.name,
            "OnInteractionSavedStep1",
            "OnInteractionError"
        );
    }

    public void OnInteractionSavedStep1(string result)
    {
        // Save stats summary
        var statsData = JsonUtility.ToJson(new ObjectStatsData
        {
            totalInteractions = _pendingTotalInteractions,
            averageTime = _pendingAverageTime
        });

        FirebaseBridge.SetDocumentInSubcollection(
            $"users/{_pendingInteractionOdId}/objectStats",
            _pendingObjectName,
            statsData,
            gameObject.name,
            "OnInteractionSaved",
            "OnStatsError"
        );
    }

    public void OnInteractionSaved(string result)
    {
        Debug.Log($"[FirebaseManager] Object '{_pendingObjectName}' interaction saved");
        _interactionCallback?.Invoke(true);
        _interactionCallback = null;
    }

    public void OnInteractionError(string error)
    {
        Debug.LogError($"[FirebaseManager] Interaction error: {error}");
        _interactionCallback?.Invoke(false);
        _interactionCallback = null;
    }

    public void OnStatsSaved(string result)
    {
        Debug.Log("[FirebaseManager] Stats saved!");
        _statsCallback?.Invoke(true);
        _statsCallback = null;
    }

    public void OnStatsError(string error)
    {
        Debug.LogError($"[FirebaseManager] Stats error: {error}");
        _interactionCallback?.Invoke(false);
        _interactionCallback = null;
    }

    // ------------------------------------------------------------------------
    // CHECK IF USER HAS DEMOGRAPHICS
    // ------------------------------------------------------------------------

    public void CheckUserHasDemographics(string odId, Action<bool> callback)
    {
        if (!IsReady)
        {
            Debug.LogError("[FirebaseManager] Firestore not ready.");
            callback?.Invoke(false);
            return;
        }

        _checkDemographicsCallback = callback;

        FirebaseBridge.DocumentExists(
            $"users/{odId}/demographics",
            "info",
            gameObject.name,
            "OnDemographicsCheckSuccess",
            "OnDemographicsCheckError"
        );
    }

    public void OnDemographicsCheckSuccess(string result)
    {
        bool exists = result.ToLower() == "true";
        Debug.Log($"[FirebaseManager] Demographics check: {exists}");
        _checkDemographicsCallback?.Invoke(exists);
        _checkDemographicsCallback = null;
    }

    public void OnDemographicsCheckError(string error)
    {
        Debug.LogError($"[FirebaseManager] Demographics check error: {error}");
        _checkDemographicsCallback?.Invoke(false);
        _checkDemographicsCallback = null;
    }

    // ------------------------------------------------------------------------
    // LOAD BADGES WITH DATA (for AchievementsManager)
    // ------------------------------------------------------------------------

    public void LoadBadgesWithData(string odId, Action<List<BadgeDocumentData>> callback)
    {
        if (!IsReady)
        {
            Debug.LogError("[FirebaseManager] Firestore not ready.");
            callback?.Invoke(new List<BadgeDocumentData>());
            return;
        }

        _loadBadgesWithDataCallback = callback;

        FirebaseBridge.GetCollectionWithData(
            $"users/{odId}/badges",
            gameObject.name,
            "OnBadgesWithDataLoaded",
            "OnBadgesWithDataError"
        );
    }

    public void OnBadgesWithDataLoaded(string json)
    {
        try
        {
            var wrapper = JsonUtility.FromJson<DocumentArrayWrapper>("{\"documents\":" + json + "}");
            var badges = new List<BadgeDocumentData>();

            if (wrapper.documents != null)
            {
                foreach (var doc in wrapper.documents)
                {
                    badges.Add(new BadgeDocumentData
                    {
                        badgeId = doc.id,
                        badgeName = doc.data?.badgeName ?? "",
                        description = doc.data?.description ?? ""
                    });
                }
            }

            Debug.Log($"[FirebaseManager] Loaded {badges.Count} badges with data");
            _loadBadgesWithDataCallback?.Invoke(badges);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Failed to parse badges: {e.Message}");
            _loadBadgesWithDataCallback?.Invoke(new List<BadgeDocumentData>());
        }
        _loadBadgesWithDataCallback = null;
    }

    public void OnBadgesWithDataError(string error)
    {
        Debug.LogError($"[FirebaseManager] Failed to load badges with data: {error}");
        _loadBadgesWithDataCallback?.Invoke(new List<BadgeDocumentData>());
        _loadBadgesWithDataCallback = null;
    }

    // ------------------------------------------------------------------------
    // LOAD CARDS WITH DATA (for AchievementsManager)
    // ------------------------------------------------------------------------

    public void LoadCardsWithData(string odId, Action<List<CardDocumentData>> callback)
    {
        if (!IsReady)
        {
            Debug.LogError("[FirebaseManager] Firestore not ready.");
            callback?.Invoke(new List<CardDocumentData>());
            return;
        }

        _loadCardsWithDataCallback = callback;

        FirebaseBridge.GetCollectionWithData(
            $"users/{odId}/cards",
            gameObject.name,
            "OnCardsWithDataLoaded",
            "OnCardsWithDataError"
        );
    }

    public void OnCardsWithDataLoaded(string json)
    {
        try
        {
            var wrapper = JsonUtility.FromJson<CardDocumentArrayWrapper>("{\"documents\":" + json + "}");
            var cards = new List<CardDocumentData>();

            if (wrapper.documents != null)
            {
                foreach (var doc in wrapper.documents)
                {
                    cards.Add(new CardDocumentData
                    {
                        cardId = !string.IsNullOrEmpty(doc.data?.cardId) ? doc.data.cardId : doc.id,
                        found = doc.data?.found ?? true
                    });
                }
            }

            Debug.Log($"[FirebaseManager] Loaded {cards.Count} cards with data");
            _loadCardsWithDataCallback?.Invoke(cards);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Failed to parse cards: {e.Message}");
            _loadCardsWithDataCallback?.Invoke(new List<CardDocumentData>());
        }
        _loadCardsWithDataCallback = null;
    }

    public void OnCardsWithDataError(string error)
    {
        Debug.LogError($"[FirebaseManager] Failed to load cards with data: {error}");
        _loadCardsWithDataCallback?.Invoke(new List<CardDocumentData>());
        _loadCardsWithDataCallback = null;
    }

    // ------------------------------------------------------------------------
    // LOAD PROGRESS SUMMARY (for AchievementsManager)
    // ------------------------------------------------------------------------

    public void LoadProgressSummary(string odId, Action<ProgressSummaryData> callback)
    {
        if (!IsReady)
        {
            Debug.LogError("[FirebaseManager] Firestore not ready.");
            callback?.Invoke(new ProgressSummaryData());
            return;
        }

        _loadProgressSummaryCallback = callback;

        FirebaseBridge.GetDocument(
            $"users/{odId}/progress",
            "summary",
            gameObject.name,
            "OnProgressSummaryLoaded",
            "OnProgressSummaryError"
        );
    }

    public void OnProgressSummaryLoaded(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<ProgressSummaryData>(json);
            Debug.Log($"[FirebaseManager] Loaded progress summary: {data.totalCardsCollected} cards, {data.totalScore} score");
            _loadProgressSummaryCallback?.Invoke(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Failed to parse progress summary: {e.Message}");
            _loadProgressSummaryCallback?.Invoke(new ProgressSummaryData());
        }
        _loadProgressSummaryCallback = null;
    }

    public void OnProgressSummaryError(string error)
    {
        Debug.LogWarning($"[FirebaseManager] Failed to load progress summary: {error}");
        _loadProgressSummaryCallback?.Invoke(new ProgressSummaryData());
        _loadProgressSummaryCallback = null;
    }

    // ------------------------------------------------------------------------
    // LOAD ROOM STATS (for AchievementsManager)
    // ------------------------------------------------------------------------

    public void LoadRoomStats(string odId, Action<List<RoomStatsDocumentData>> callback)
    {
        if (!IsReady)
        {
            Debug.LogError("[FirebaseManager] Firestore not ready.");
            callback?.Invoke(new List<RoomStatsDocumentData>());
            return;
        }

        _loadRoomStatsCallback = callback;

        FirebaseBridge.GetCollectionWithData(
            $"users/{odId}/roomStats",
            gameObject.name,
            "OnRoomStatsWithDataLoaded",
            "OnRoomStatsWithDataError"
        );
    }

    public void OnRoomStatsWithDataLoaded(string json)
    {
        try
        {
            var wrapper = JsonUtility.FromJson<RoomStatsDocumentArrayWrapper>("{\"documents\":" + json + "}");
            var roomStats = new List<RoomStatsDocumentData>();

            if (wrapper.documents != null)
            {
                foreach (var doc in wrapper.documents)
                {
                    roomStats.Add(new RoomStatsDocumentData
                    {
                        roomId = doc.id,
                        timeSpent = doc.data?.timeSpent ?? 0f,
                        visitCount = doc.data?.visitCount ?? 0
                    });
                }
            }

            Debug.Log($"[FirebaseManager] Loaded {roomStats.Count} room stats");
            _loadRoomStatsCallback?.Invoke(roomStats);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Failed to parse room stats: {e.Message}");
            _loadRoomStatsCallback?.Invoke(new List<RoomStatsDocumentData>());
        }
        _loadRoomStatsCallback = null;
    }

    public void OnRoomStatsWithDataError(string error)
    {
        Debug.LogError($"[FirebaseManager] Failed to load room stats: {error}");
        _loadRoomStatsCallback?.Invoke(new List<RoomStatsDocumentData>());
        _loadRoomStatsCallback = null;
    }
}

// ------------------------------------------------------------------------
// DATA CLASSES FOR JSON SERIALIZATION
// ------------------------------------------------------------------------

[Serializable]
public class UserData
{
    public string participantCode;
    public string lastActive;
}

[Serializable]
public class DemographicsData
{
    public string age;
    public string gender;
    public string nationality;
    public string computerSkills;
    public string vrInterest;
}

[Serializable]
public class BadgeData
{
    public string badgeId;
    public string badgeName;
    public string description;
}

[Serializable]
public class CardData
{
    public string cardId;
    public bool found;
}

[Serializable]
public class ProgressData
{
    public int totalCardsCollected;
    public string lastCardFound;
}

[Serializable]
public class RoomStatsData
{
    public float timeSpent;
    public int visitCount;
}

[Serializable]
public class ScoreData
{
    public int totalScore;
}

[Serializable]
public class ObjectInteractionData
{
    public string objectName;
    public float duration;
    public int totalInteractions;
    public float averageTime;
}

[Serializable]
public class ObjectStatsData
{
    public int totalInteractions;
    public float averageTime;
}

[Serializable]
public class StringArrayWrapper
{
    public string[] items;
}

// ------------------------------------------------------------------------
// DATA CLASSES FOR ACHIEVEMENTS MANAGER
// ------------------------------------------------------------------------

[Serializable]
public class BadgeDocumentData
{
    public string badgeId;
    public string badgeName;
    public string description;
}

[Serializable]
public class CardDocumentData
{
    public string cardId;
    public bool found;
}

[Serializable]
public class ProgressSummaryData
{
    public int totalCardsCollected;
    public int totalBadges;
    public string lastCardFound;
    public string lastBadgeUnlocked;
    public int totalScore;
}

[Serializable]
public class RoomStatsDocumentData
{
    public string roomId;
    public float timeSpent;
    public int visitCount;
}

// Wrapper classes for JSON parsing
[Serializable]
public class DocumentWrapper
{
    public string id;
    public DocumentDataWrapper data;
}

[Serializable]
public class DocumentDataWrapper
{
    public string badgeName;
    public string description;
    public string cardId;
    public bool found;
    public float timeSpent;
    public int visitCount;
}

[Serializable]
public class DocumentArrayWrapper
{
    public DocumentWrapper[] documents;
}

[Serializable]
public class CardDocumentWrapper
{
    public string id;
    public CardDataWrapper data;
}

[Serializable]
public class CardDataWrapper
{
    public string cardId;
    public bool found;
}

[Serializable]
public class CardDocumentArrayWrapper
{
    public CardDocumentWrapper[] documents;
}

[Serializable]
public class RoomStatsDocumentWrapper
{
    public string id;
    public RoomStatsDataWrapper data;
}

[Serializable]
public class RoomStatsDataWrapper
{
    public float timeSpent;
    public int visitCount;
}

[Serializable]
public class RoomStatsDocumentArrayWrapper
{
    public RoomStatsDocumentWrapper[] documents;
}