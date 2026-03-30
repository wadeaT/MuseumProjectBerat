using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;

/// <summary>
/// FirebaseManager — Single source of truth for Firebase lifecycle.
///
/// Responsibilities (and ONLY these):
/// 1. Initialize Firebase (merged from FirebaseInitializer)
/// 2. Expose Firestore/Auth references
/// 3. Manage participant login and session lifecycle
/// 4. Provide WaitUntilReady() for dependent systems
///
/// ALL data logging is handled by FirebaseLogger (static helper).
/// This class no longer contains individual LogXxxAsync/SaveXxxAsync methods.
///
/// Migration note:
///   Old: FirebaseManager.Instance.LogHandProximityAsync(...)
///   New: FirebaseLogger.LogSessionData("handTracking", data);
///
///   Old: FirebaseManager.Instance.SaveBadgeAsync(userId, badgeId, ...)
///   New: FirebaseLogger.MergeUserData("badges", data, badgeId);
/// </summary>
public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }

    public FirebaseApp App { get; private set; }
    public FirebaseFirestore Firestore { get; private set; }
    public FirebaseAuth Auth { get; private set; }

    public bool IsReady { get; private set; } = false;
    public string CurrentParticipantCode { get; private set; }

    // Session tracking
    private string sessionId;
    private float sessionStartTime;

    /// <summary>Expose sessionId for FirebaseLogger path construction.</summary>
    public string CurrentSessionId => sessionId;

    // ========================================================================
    // LIFECYCLE
    // ========================================================================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeFirebase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async void InitializeFirebase()
    {
        Debug.Log("[FirebaseManager] Checking dependencies...");

        var status = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (status != DependencyStatus.Available)
        {
            Debug.LogError($"[FirebaseManager] Firebase dependencies missing: {status}");
            return;
        }

        App = FirebaseApp.DefaultInstance;
        Auth = FirebaseAuth.DefaultInstance;

        try
        {
            Firestore = FirebaseFirestore.DefaultInstance;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FirebaseManager] Failed to initialize Firestore: {e.Message}");
            return;
        }

        IsReady = true;
        Debug.Log("[FirebaseManager] Firebase initialized successfully!");
    }

    /// <summary>
    /// Await this to block until Firebase is ready.
    /// Merged from the former FirebaseInitializer.WaitUntilReady().
    /// Timeout: 10 seconds.
    /// </summary>
    public static async Task WaitUntilReady()
    {
        int retries = 0;
        while ((Instance == null || !Instance.IsReady) && retries < 100)
        {
            await Task.Delay(100);
            retries++;
        }

        if (Instance == null || !Instance.IsReady)
            Debug.LogWarning("[FirebaseManager] WaitUntilReady() timed out after 10 seconds.");
    }

    // ========================================================================
    // PARTICIPANT LOGIN & SESSION MANAGEMENT
    // ========================================================================

    public async Task<string> LoginWithParticipantCodeAsync(string participantCode, string groupAssignment = "")
    {
        if (!IsReady)
        {
            Debug.LogWarning("[FirebaseManager] Firebase not ready yet.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(participantCode))
        {
            Debug.LogError("[FirebaseManager] Participant code cannot be empty.");
            return null;
        }

        participantCode = participantCode.Trim().ToUpper();
        CurrentParticipantCode = participantCode;

        // Generate unique session ID
        sessionId = $"{participantCode}_{System.DateTime.UtcNow.Ticks}";
        sessionStartTime = Time.time;

        try
        {
            // Upsert user document
            var userDocRef = Firestore.Collection("users").Document(participantCode);

            await userDocRef.SetAsync(new Dictionary<string, object>
            {
                { "participantCode", participantCode },
                { "groupAssignment", groupAssignment },
                { "createdAt", FieldValue.ServerTimestamp },
                { "lastActive", FieldValue.ServerTimestamp },
                { "currentSessionId", sessionId }
            }, SetOptions.MergeAll);

            // Create session document
            var sessionDoc = Firestore.Collection("users").Document(participantCode)
                .Collection("sessions").Document(sessionId);

            await sessionDoc.SetAsync(new Dictionary<string, object>
            {
                { "sessionId", sessionId },
                { "startTime", FieldValue.ServerTimestamp },
                { "groupAssignment", groupAssignment },
                { "platform", "Quest3" },
                { "unityVersion", Application.unityVersion }
            });

            Debug.Log($"[FirebaseManager] Session started: {sessionId}");
            return participantCode;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FirebaseManager] Login failed: {e.Message}");
            return null;
        }
    }

    public async Task EndSessionAsync()
    {
        if (!IsReady || string.IsNullOrEmpty(CurrentParticipantCode))
            return;

        try
        {
            float sessionDuration = Time.time - sessionStartTime;

            var sessionDoc = Firestore.Collection("users").Document(CurrentParticipantCode)
                .Collection("sessions").Document(sessionId);

            await sessionDoc.SetAsync(new Dictionary<string, object>
            {
                { "endTime", FieldValue.ServerTimestamp },
                { "duration", sessionDuration },
                { "completed", true }
            }, SetOptions.MergeAll);

            Debug.Log($"[FirebaseManager] Session ended: {sessionDuration:F1}s");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FirebaseManager] Failed to end session: {e.Message}");
        }
    }

    // ========================================================================
    // LEGACY COMPATIBILITY — Thin wrappers that delegate to FirebaseLogger
    //
    // These exist so callers that haven't been migrated yet still compile.
    // They can be removed once all callers use FirebaseLogger directly.
    // ========================================================================

    public async Task SaveDemographicsAsync(
        string userId, string age, string gender,
        string nationality, string computerSkills, string vrInterest)
    {
        var data = new Dictionary<string, object>
        {
            { "age", age },
            { "gender", gender },
            { "nationality", nationality },
            { "computerSkills", computerSkills },
            { "vrInterest", vrInterest }
        };
        await FirebaseLogger.MergeUserData("demographics", data, "info", "[FirebaseManager]");
    }

    public async Task SaveBadgeAsync(string userId, string badgeId, string badgeName, string description)
    {
        var data = new Dictionary<string, object>
        {
            { "badgeId", badgeId },
            { "badgeName", badgeName },
            { "description", description }
        };
        await FirebaseLogger.MergeUserData("badges", data, badgeId, "[FirebaseManager]");
    }

    public async Task SaveCardCollectedAsync(string userId, string cardId, int totalCardsCollected)
    {
        var cardData = new Dictionary<string, object>
        {
            { "cardId", cardId },
            { "found", true },
            { "sessionId", sessionId }
        };
        await FirebaseLogger.MergeUserData("cards", cardData, cardId, "[FirebaseManager]");

        var progressData = new Dictionary<string, object>
        {
            { "totalCardsCollected", totalCardsCollected },
            { "lastCardFound", cardId },
            { "lastCardTimestamp", FieldValue.ServerTimestamp }
        };
        await FirebaseLogger.MergeUserData("progress", progressData, "summary", "[FirebaseManager]");
    }

    public async Task<List<string>> LoadUserBadgesAsync(string userId)
    {
        if (!IsReady || Firestore == null)
            return new List<string>();

        try
        {
            var snapshot = await Firestore.Collection("users").Document(userId)
                .Collection("badges").GetSnapshotAsync();

            var list = new List<string>();
            foreach (var doc in snapshot.Documents)
                list.Add(doc.Id);

            return list;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FirebaseManager] Failed to load badges: {e.Message}");
            return new List<string>();
        }
    }

    public async Task<int> LoadUserCardsAsync(string userId)
    {
        if (!IsReady || Firestore == null)
            return 0;

        try
        {
            var snapshot = await Firestore.Collection("users").Document(userId)
                .Collection("cards").GetSnapshotAsync();

            return snapshot.Count;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FirebaseManager] Failed to load cards: {e.Message}");
            return 0;
        }
    }

    public async Task SaveRoomTimeAsync(string userId, string roomId, float timeSpent)
    {
        var data = new Dictionary<string, object>
        {
            { "timeSpent", FieldValue.Increment(timeSpent) },
            { "visitCount", FieldValue.Increment(1) }
        };
        await FirebaseLogger.MergeUserData("roomStats", data, roomId, "[FirebaseManager]");
    }

    public async Task SaveUserScoreAsync(string userId, int totalScore)
    {
        var data = new Dictionary<string, object>
        {
            { "totalScore", totalScore },
            { "lastScoreUpdate", FieldValue.ServerTimestamp }
        };
        await FirebaseLogger.MergeUserData("progress", data, "summary", "[FirebaseManager]");
    }

    public async Task SaveObjectInteractionAsync(
        string userId, string objectName, float duration,
        int totalInteractions, float averageTime)
    {
        // Per-event log (session-scoped)
        var eventData = new Dictionary<string, object>
        {
            { "objectName", objectName },
            { "duration", duration },
            { "totalInteractions", totalInteractions },
            { "averageTime", averageTime },
            { "sessionId", sessionId }
        };
        string docId = $"{objectName}_{System.DateTime.UtcNow.Ticks}";
        await FirebaseLogger.LogUserData("objectInteractions", eventData, docId, "[FirebaseManager]");

        // Aggregate stats (user-scoped, merged)
        var statsData = new Dictionary<string, object>
        {
            { "totalInteractions", totalInteractions },
            { "totalTimeSpent", FieldValue.Increment(duration) },
            { "averageTime", averageTime },
            { "lastInteraction", FieldValue.ServerTimestamp }
        };
        await FirebaseLogger.MergeUserData("objectStats", statsData, objectName, "[FirebaseManager]");
    }

    public async Task<bool> UserHasDemographicsAsync(string userId)
    {
        if (!IsReady || Firestore == null)
            return false;

        try
        {
            var snapshot = await Firestore.Collection("users").Document(userId)
                .Collection("demographics").Document("info")
                .GetSnapshotAsync();

            return snapshot.Exists;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FirebaseManager] Error checking demographics: {e.Message}");
            return false;
        }
    }

    // ========================================================================
    // NOTE: The following old methods are REMOVED (callers migrated to FirebaseLogger):
    //   LogHandProximityAsync, LogHandGestureAsync, LogHeadTrackingAsync,
    //   LogGazeEventAsync, LogMovementAsync, LogEngagementStateAsync,
    //   LogContentAdaptationAsync
    //
    // If a caller still references them, it should be updated to use
    //   FirebaseLogger.LogSessionData("collectionName", data);
    // ========================================================================
}
