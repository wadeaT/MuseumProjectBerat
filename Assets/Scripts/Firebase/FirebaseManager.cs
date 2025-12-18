using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;

/// <summary>
/// Simplified FirebaseManager for research studies.
/// Uses participant code (e.g., "P001") directly as document ID.
/// No authentication needed - just Firestore.
/// </summary>
public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }

    public FirebaseApp App { get; private set; }
    public FirebaseFirestore Firestore { get; private set; }
    public FirebaseAuth Auth { get; private set; }  // Kept for compatibility

    public bool IsReady { get; private set; } = false;

    // Current participant code (used as document ID)
    public string CurrentParticipantCode { get; private set; }

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
            Debug.LogError($"❌ [FirebaseManager] Firebase dependencies missing: {status}");
            return;
        }

        App = FirebaseApp.DefaultInstance;
        Firestore = FirebaseFirestore.DefaultInstance;
        Auth = FirebaseAuth.DefaultInstance;  // Kept for compatibility

        // No Auth needed!
        IsReady = true;
        Debug.Log("✅ [FirebaseManager] Firebase initialized successfully!");
    }

    // ------------------------------------------------------------------------
    // PARTICIPANT LOGIN (No auth - just set the code)
    // ------------------------------------------------------------------------

    /// <summary>
    /// "Logs in" a participant by setting their code.
    /// Creates their user document in Firestore.
    /// The code becomes the document ID (e.g., users/P001)
    /// </summary>
    public async Task<string> LoginWithParticipantCodeAsync(string participantCode)
    {
        if (!IsReady)
        {
            Debug.LogWarning("⚠️ Firebase not ready yet.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(participantCode))
        {
            Debug.LogError("❌ [FirebaseManager] Participant code cannot be empty.");
            return null;
        }

        // Clean up the code (trim whitespace, uppercase)
        participantCode = participantCode.Trim().ToUpper();
        CurrentParticipantCode = participantCode;

        try
        {
            // Create user document with participant code as the ID
            var userDocRef = Firestore.Collection("users").Document(participantCode);

            await userDocRef.SetAsync(new Dictionary<string, object>
            {
                { "participantCode", participantCode },
                { "createdAt", FieldValue.ServerTimestamp },
                { "lastActive", FieldValue.ServerTimestamp }
            }, SetOptions.MergeAll);

            Debug.Log($"✅ [FirebaseManager] Participant {participantCode} logged in!");

            // Return the participant code (this is now the "userId")
            return participantCode;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Login failed: {e.Message}");
            return null;
        }
    }

    // ------------------------------------------------------------------------
    // FIRESTORE WRITES (using participant code as document ID)
    // ------------------------------------------------------------------------

    public async Task SaveDemographicsAsync(
        string odId,  // This is now the participant code (e.g., "P001")
        string age,
        string gender,
        string nationality,
        string computerSkills,
        string vrInterest)
    {
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ [FirebaseManager] Firestore not ready or null.");
            return;
        }

        Debug.Log($"[FirebaseManager] Saving demographics for {odId}...");

        try
        {
            var data = new Dictionary<string, object>
            {
                { "age", age },
                { "gender", gender },
                { "nationality", nationality },
                { "computerSkills", computerSkills },
                { "vrInterest", vrInterest },
                { "timestamp", FieldValue.ServerTimestamp }
            };

            var doc = Firestore.Collection("users").Document(odId)
                .Collection("demographics").Document("info");

            await doc.SetAsync(data, SetOptions.MergeAll);
            Debug.Log($"✅ [FirebaseManager] Demographics saved for {odId}!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Failed to save demographics: {e.Message}");
        }
    }

    public async Task SaveBadgeAsync(string odId, string badgeId, string badgeName, string description)
    {
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ [FirebaseManager] Firestore not ready.");
            return;
        }

        try
        {
            var badgeDoc = Firestore.Collection("users").Document(odId)
                .Collection("badges").Document(badgeId);

            await badgeDoc.SetAsync(new Dictionary<string, object>
            {
                { "badgeId", badgeId },
                { "badgeName", badgeName },
                { "description", description },
                { "unlockedAt", FieldValue.ServerTimestamp }
            }, SetOptions.MergeAll);

            Debug.Log($"✅ [FirebaseManager] Badge '{badgeName}' saved for {odId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Failed to save badge: {e.Message}");
        }
    }

    public async Task SaveCardCollectedAsync(string odId, string cardId, int totalCardsCollected)
    {
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ [FirebaseManager] Firestore not ready.");
            return;
        }

        try
        {
            var cardDoc = Firestore.Collection("users").Document(odId)
                .Collection("cards").Document(cardId);

            await cardDoc.SetAsync(new Dictionary<string, object>
            {
                { "cardId", cardId },
                { "found", true },
                { "timestamp", FieldValue.ServerTimestamp }
            }, SetOptions.MergeAll);

            var progressDoc = Firestore.Collection("users").Document(odId)
                .Collection("progress").Document("summary");

            await progressDoc.SetAsync(new Dictionary<string, object>
            {
                { "totalCardsCollected", totalCardsCollected },
                { "lastCardFound", cardId },
                { "lastCardTimestamp", FieldValue.ServerTimestamp }
            }, SetOptions.MergeAll);

            Debug.Log($"✅ [FirebaseManager] Card '{cardId}' saved for {odId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Failed to save card: {e.Message}");
        }
    }

    public async Task<List<string>> LoadUserBadgesAsync(string odId)
    {
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ [FirebaseManager] Firestore not ready.");
            return new List<string>();
        }

        try
        {
            var badgesSnapshot = await Firestore.Collection("users").Document(odId)
                .Collection("badges").GetSnapshotAsync();

            var badgeList = new List<string>();
            foreach (var doc in badgesSnapshot.Documents)
            {
                badgeList.Add(doc.Id);
            }

            Debug.Log($"✅ [FirebaseManager] Loaded {badgeList.Count} badges for {odId}");
            return badgeList;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Failed to load badges: {e.Message}");
            return new List<string>();
        }
    }

    public async Task<int> LoadUserCardsAsync(string odId)
    {
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ [FirebaseManager] Firestore not ready.");
            return 0;
        }

        try
        {
            var cardsSnapshot = await Firestore.Collection("users").Document(odId)
                .Collection("cards").GetSnapshotAsync();

            Debug.Log($"✅ [FirebaseManager] Loaded {cardsSnapshot.Count} cards for {odId}");
            return cardsSnapshot.Count;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Failed to load cards: {e.Message}");
            return 0;
        }
    }

    public async Task SaveRoomTimeAsync(string odId, string roomId, float timeSpent)
    {
        if (!IsReady || Firestore == null) return;

        try
        {
            var docRef = Firestore.Collection("users").Document(odId)
                .Collection("roomStats").Document(roomId);

            await docRef.SetAsync(new Dictionary<string, object>
            {
                { "timeSpent", FieldValue.Increment(timeSpent) },
                { "visitCount", FieldValue.Increment(1) }
            }, SetOptions.MergeAll);

            Debug.Log($"✅ [FirebaseManager] Room '{roomId}' updated for {odId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Failed to save room time: {e.Message}");
        }
    }

    public async Task SaveUserScoreAsync(string odId, int totalScore)
    {
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ Firestore not ready.");
            return;
        }

        try
        {
            var progressDoc = Firestore.Collection("users").Document(odId)
                .Collection("progress").Document("summary");

            await progressDoc.SetAsync(new Dictionary<string, object>
            {
                { "totalScore", totalScore },
                { "lastScoreUpdate", FieldValue.ServerTimestamp }
            }, SetOptions.MergeAll);

            Debug.Log($"✅ Score {totalScore} saved for {odId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Failed to save user score: {e.Message}");
        }
    }

    public async Task SaveObjectInteractionAsync(
        string odId,
        string objectName,
        float duration,
        int totalInteractions,
        float averageTime)
    {
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ [FirebaseManager] Firestore not ready.");
            return;
        }

        try
        {
            string docId = $"{objectName}_{System.DateTime.UtcNow.Ticks}";

            var interactionDoc = Firestore.Collection("users").Document(odId)
                .Collection("objectInteractions").Document(docId);

            await interactionDoc.SetAsync(new Dictionary<string, object>
            {
                { "objectName", objectName },
                { "duration", duration },
                { "totalInteractions", totalInteractions },
                { "averageTime", averageTime },
                { "timestamp", FieldValue.ServerTimestamp }
            });

            var statsDoc = Firestore.Collection("users").Document(odId)
                .Collection("objectStats").Document(objectName);

            await statsDoc.SetAsync(new Dictionary<string, object>
            {
                { "totalInteractions", totalInteractions },
                { "totalTimeSpent", FieldValue.Increment(duration) },
                { "averageTime", averageTime },
                { "lastInteraction", FieldValue.ServerTimestamp }
            }, SetOptions.MergeAll);

            Debug.Log($"✅ [FirebaseManager] Object '{objectName}' saved for {odId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Failed to save object interaction: {e.Message}");
        }
    }

    public async Task<bool> UserHasDemographicsAsync(string odId)
    {
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ Firestore not ready.");
            return false;
        }

        try
        {
            var docRef = Firestore.Collection("users").Document(odId)
                .Collection("demographics").Document("info");

            var snapshot = await docRef.GetSnapshotAsync();
            bool exists = snapshot.Exists;

            Debug.Log($"[FirebaseManager] {odId} has demographics: {exists}");
            return exists;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error checking demographics for {odId}: {e.Message}");
            return false;
        }
    }
}