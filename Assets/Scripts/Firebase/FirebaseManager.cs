using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }

    public FirebaseApp App { get; private set; }
    public FirebaseAuth Auth { get; private set; }
    public FirebaseFirestore Firestore { get; private set; }

    public bool IsReady { get; private set; } = false;

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
        Auth = FirebaseAuth.DefaultInstance;
        Firestore = FirebaseFirestore.DefaultInstance;

        IsReady = true;
        Debug.Log("✅ [FirebaseManager] Firebase initialized successfully!");
    }

    // ------------------------------------------------------------------------
    // USER AUTH (EMAIL + PASSWORD)
    // ------------------------------------------------------------------------

    public async Task<string> RegisterUserAsync(string email, string password)
    {
        if (!IsReady)
        {
            Debug.LogWarning("⚠️ Firebase not ready yet.");
            return null;
        }

        try
        {
            var result = await Auth.CreateUserWithEmailAndPasswordAsync(email, password);
            Debug.Log($"✅ [FirebaseManager] Registered new user: {result.User.UserId}");
            return result.User.UserId;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Register failed: {e.Message}");
            return null;
        }
    }

    public async Task<string> LoginUserAsync(string email, string password)
    {
        if (!IsReady)
        {
            Debug.LogWarning("⚠️ Firebase not ready yet.");
            return null;
        }

        try
        {
            var result = await Auth.SignInWithEmailAndPasswordAsync(email, password);
            Debug.Log($"✅ [FirebaseManager] Logged in: {result.User.UserId}");
            return result.User.UserId;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Login failed: {e.Message}");
            return null;
        }
    }

    // ------------------------------------------------------------------------
    // FIRESTORE WRITES
    // ------------------------------------------------------------------------

    public async Task SaveDemographicsAsync(
    string userId,
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

        Debug.Log($"[FirebaseManager] Saving demographics for user {userId}...");

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

            var doc = Firestore.Collection("users").Document(userId)
                .Collection("demographics").Document("info");

            Debug.Log("[FirebaseManager] Calling SetAsync...");
            await doc.SetAsync(data, SetOptions.MergeAll);
            Debug.Log("✅ [FirebaseManager] Demographics saved successfully in Firestore!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Failed to save demographics: {e.Message}\n{e.StackTrace}");
        }
    }



    /// <summary>
    /// Saves a badge with full details to Firestore
    /// </summary>
    public async Task SaveBadgeAsync(string userId, string badgeId, string badgeName, string description)
    {
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ [FirebaseManager] Firestore not ready.");
            return;
        }

        try
        {
            // Save to badges subcollection with full details
            var badgeDoc = Firestore.Collection("users").Document(userId)
                .Collection("badges").Document(badgeId);

            var badgeData = new Dictionary<string, object>
        {
            { "badgeId", badgeId },
            { "name", badgeName },
            { "description", description },
            { "unlocked", true },
            { "timestamp", FieldValue.ServerTimestamp }
        };

            await badgeDoc.SetAsync(badgeData, SetOptions.MergeAll);

            // Also update the progress document with total count
            var progressDoc = Firestore.Collection("users").Document(userId)
                .Collection("progress").Document("summary");

            await progressDoc.SetAsync(new Dictionary<string, object>
        {
            { "totalBadges", FieldValue.Increment(1) },
            { "lastBadgeUnlocked", badgeId },
            { "lastBadgeTimestamp", FieldValue.ServerTimestamp }
        }, SetOptions.MergeAll);

            Debug.Log($"✅ [FirebaseManager] Badge '{badgeName}' saved for user {userId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Failed to save badge: {e.Message}");
        }
    }

    /// <summary>
    /// Saves card collection progress
    /// </summary>
    public async Task SaveCardCollectedAsync(string userId, string cardId, int totalCardsCollected)
    {
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ [FirebaseManager] Firestore not ready.");
            return;
        }

        try
        {
            // Save individual card
            var cardDoc = Firestore.Collection("users").Document(userId)
                .Collection("cards").Document(cardId);

            await cardDoc.SetAsync(new Dictionary<string, object>
        {
            { "cardId", cardId },
            { "found", true },
            { "timestamp", FieldValue.ServerTimestamp }
        }, SetOptions.MergeAll);

            // Update progress summary
            var progressDoc = Firestore.Collection("users").Document(userId)
                .Collection("progress").Document("summary");

            await progressDoc.SetAsync(new Dictionary<string, object>
        {
            { "totalCardsCollected", totalCardsCollected },
            { "lastCardFound", cardId },
            { "lastCardTimestamp", FieldValue.ServerTimestamp }
        }, SetOptions.MergeAll);

            Debug.Log($"✅ [FirebaseManager] Card '{cardId}' saved for user {userId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Failed to save card: {e.Message}");
        }
    }

    /// <summary>
    /// Loads user's badge progress from Firestore
    /// </summary>
    public async Task<List<string>> LoadUserBadgesAsync(string userId)
    {
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ [FirebaseManager] Firestore not ready.");
            return new List<string>();
        }

        try
        {
            var badgesSnapshot = await Firestore.Collection("users").Document(userId)
                .Collection("badges").GetSnapshotAsync();

            var badgeList = new List<string>();
            foreach (var doc in badgesSnapshot.Documents)
            {
                badgeList.Add(doc.Id); // Badge ID
            }

            Debug.Log($"✅ [FirebaseManager] Loaded {badgeList.Count} badges for user {userId}");
            return badgeList;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Failed to load badges: {e.Message}");
            return new List<string>();
        }
    }

    /// <summary>
    /// Loads user's card collection progress
    /// </summary>
    public async Task<int> LoadUserCardsAsync(string userId)
    {
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ [FirebaseManager] Firestore not ready.");
            return 0;
        }

        try
        {
            var cardsSnapshot = await Firestore.Collection("users").Document(userId)
                .Collection("cards").GetSnapshotAsync();

            Debug.Log($"✅ [FirebaseManager] Loaded {cardsSnapshot.Count} cards for user {userId}");
            return cardsSnapshot.Count;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Failed to load cards: {e.Message}");
            return 0;
        }
    }

    public async Task SaveRoomTimeAsync(string userId, string roomId, float timeSpent)
    {
        if (!IsReady || Firestore == null) return;

        try
        {
            var docRef = Firestore.Collection("users").Document(userId)
                .Collection("roomStats").Document(roomId);

            // Add time to total, and increment visit count by 1
            await docRef.SetAsync(new Dictionary<string, object>
        {
            { "timeSpent", FieldValue.Increment(timeSpent) },
            { "visitCount", FieldValue.Increment(1) }
        }, SetOptions.MergeAll);

            Debug.Log($"✅ [FirebaseManager] Room '{roomId}' updated → +{timeSpent:F2}s, +1 visit");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Failed to save room time: {e.Message}");
        }
    }
    /// <summary>
    /// Save total user score to Firestore
    /// </summary>
    public async Task SaveUserScoreAsync(string userId, int totalScore)
    {
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ Firestore not ready in SaveUserScoreAsync.");
            return;
        }

        try
        {
            // We store score in the same "progress/summary" document
            var progressDoc = Firestore.Collection("users").Document(userId)
                .Collection("progress").Document("summary");

            var data = new Dictionary<string, object>
            {
                { "totalScore", totalScore },
                { "lastScoreUpdate", FieldValue.ServerTimestamp }
            };

            await progressDoc.SetAsync(data, SetOptions.MergeAll);

            Debug.Log($"✅ Score {totalScore} saved for user {userId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Failed to save user score: {e.Message}");
        }
    }

    /// <summary>
    /// Saves object interaction data to Firestore
    /// </summary>
    public async Task SaveObjectInteractionAsync(
        string userId,
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
            // Create unique document ID with timestamp
            string docId = $"{objectName}_{System.DateTime.UtcNow.Ticks}";

            var interactionDoc = Firestore.Collection("users").Document(userId)
                .Collection("objectInteractions").Document(docId);

            var interactionData = new Dictionary<string, object>
        {
            { "objectName", objectName },
            { "duration", duration },
            { "totalInteractions", totalInteractions },
            { "averageTime", averageTime },
            { "timestamp", FieldValue.ServerTimestamp }
        };

            await interactionDoc.SetAsync(interactionData);

            // Also update summary statistics
            var statsDoc = Firestore.Collection("users").Document(userId)
                .Collection("objectStats").Document(objectName);

            await statsDoc.SetAsync(new Dictionary<string, object>
        {
            { "totalInteractions", totalInteractions },
            { "totalTimeSpent", FieldValue.Increment(duration) },
            { "averageTime", averageTime },
            { "lastInteraction", FieldValue.ServerTimestamp }
        }, SetOptions.MergeAll);

            Debug.Log($"✅ [FirebaseManager] Object interaction '{objectName}' saved for user {userId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Failed to save object interaction: {e.Message}");
        }
    }
    public async Task<bool> UserHasDemographicsAsync(string userId)
    {
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ Firestore not ready in UserHasDemographicsAsync.");
            return false;
        }

        try
        {
            var docRef = Firestore.Collection("users").Document(userId)
                .Collection("demographics").Document("info");

            var snapshot = await docRef.GetSnapshotAsync();
            bool exists = snapshot.Exists;

            Debug.Log($"[FirebaseManager] User {userId} has demographics: {exists}");
            return exists;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error checking demographics for user {userId}: {e.Message}");
            // On error, be safe and treat as "no demographics"
            return false;
        }
    }
}
