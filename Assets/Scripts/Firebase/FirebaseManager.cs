using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

#if !UNITY_WEBGL || UNITY_EDITOR
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
#endif

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }

#if !UNITY_WEBGL || UNITY_EDITOR
    public FirebaseApp App { get; private set; }
    public FirebaseAuth Auth { get; private set; }
    public FirebaseFirestore Firestore { get; private set; }
#endif

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
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: Firebase JavaScript SDK is used instead
        IsReady = true;
        Debug.Log("✅ [FirebaseManager] WebGL mode: Firebase JavaScript SDK will be used");
        await Task.Yield();
#else
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
#endif
    }

    // ------------------------------------------------------------------------
    // USER AUTH (EMAIL + PASSWORD)
    // ------------------------------------------------------------------------

    public async Task<string> RegisterUserAsync(string email, string password)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!IsReady)
        {
            Debug.LogWarning("⚠️ [WebGL] FirebaseManager not ready yet.");
            return null;
        }

        if (WebGLFirebaseAuth.Instance == null)
        {
            Debug.LogError("❌ [WebGL] WebGLFirebaseAuth.Instance is null. Make sure WebGLFirebaseAuth GameObject exists in the scene!");
            return null;
        }

        try
        {
            string uid = await WebGLFirebaseAuth.Instance.RegisterAsync(email, password);
            Debug.Log($"✅ [WebGL] Registered new user: {uid}");
            return uid;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [WebGL] Register failed: {e.Message}");
            return null;
        }
#else
        if (!IsReady || Auth == null)
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
#endif
    }

    public async Task<string> LoginUserAsync(string email, string password)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!IsReady)
        {
            Debug.LogWarning("⚠️ [WebGL] FirebaseManager not ready yet.");
            return null;
        }

        if (WebGLFirebaseAuth.Instance == null)
        {
            Debug.LogError("❌ [WebGL] WebGLFirebaseAuth.Instance is null. Make sure WebGLFirebaseAuth GameObject exists in the scene!");
            return null;
        }

        try
        {
            string uid = await WebGLFirebaseAuth.Instance.LoginAsync(email, password);
            Debug.Log($"✅ [WebGL] Logged in: {uid}");
            return uid;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [WebGL] Login failed: {e.Message}");
            return null;
        }
#else
        if (!IsReady || Auth == null)
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
#endif
    }

    public string GetCurrentUserId()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (WebGLFirebaseAuth.Instance != null)
        {
            return WebGLFirebaseAuth.Instance.GetCurrentUserId();
        }
        return null;
#else
        if (Auth != null && Auth.CurrentUser != null)
        {
            return Auth.CurrentUser.UserId;
        }
        return null;
#endif
    }

    public void SignOut()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (WebGLFirebaseAuth.Instance != null)
        {
            WebGLFirebaseAuth.Instance.SignOut();
        }
#else
        if (Auth != null)
        {
            Auth.SignOut();
        }
#endif
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
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogWarning("⚠️ [WebGL] SaveDemographicsAsync - Firestore not yet implemented for WebGL");
        await Task.Yield();
        return;
#else
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
#endif
    }

    public async Task SaveBadgeAsync(string userId, string badgeId, string badgeName, string description)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogWarning("⚠️ [WebGL] SaveBadgeAsync - Firestore not yet implemented for WebGL");
        await Task.Yield();
        return;
#else
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ [FirebaseManager] Firestore not ready.");
            return;
        }

        try
        {
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
#endif
    }

    public async Task SaveCardCollectedAsync(string userId, string cardId, int totalCardsCollected)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogWarning("⚠️ [WebGL] SaveCardCollectedAsync - Firestore not yet implemented for WebGL");
        await Task.Yield();
        return;
#else
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ [FirebaseManager] Firestore not ready.");
            return;
        }

        try
        {
            var cardDoc = Firestore.Collection("users").Document(userId)
                .Collection("cards").Document(cardId);

            await cardDoc.SetAsync(new Dictionary<string, object>
            {
                { "cardId", cardId },
                { "found", true },
                { "timestamp", FieldValue.ServerTimestamp }
            }, SetOptions.MergeAll);

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
#endif
    }

    public async Task<List<string>> LoadUserBadgesAsync(string userId)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogWarning("⚠️ [WebGL] LoadUserBadgesAsync - Firestore not yet implemented for WebGL");
        await Task.Yield();
        return new List<string>();
#else
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
                badgeList.Add(doc.Id);
            }

            Debug.Log($"✅ [FirebaseManager] Loaded {badgeList.Count} badges for user {userId}");
            return badgeList;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [FirebaseManager] Failed to load badges: {e.Message}");
            return new List<string>();
        }
#endif
    }

    public async Task<int> LoadUserCardsAsync(string userId)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogWarning("⚠️ [WebGL] LoadUserCardsAsync - Firestore not yet implemented for WebGL");
        await Task.Yield();
        return 0;
#else
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
#endif
    }

    public async Task SaveRoomTimeAsync(string userId, string roomId, float timeSpent)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogWarning("⚠️ [WebGL] SaveRoomTimeAsync - Firestore not yet implemented for WebGL");
        await Task.Yield();
        return;
#else
        if (!IsReady || Firestore == null) return;

        try
        {
            var docRef = Firestore.Collection("users").Document(userId)
                .Collection("roomStats").Document(roomId);

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
#endif
    }

    public async Task SaveUserScoreAsync(string userId, int totalScore)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogWarning("⚠️ [WebGL] SaveUserScoreAsync - Firestore not yet implemented for WebGL");
        await Task.Yield();
        return;
#else
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ Firestore not ready in SaveUserScoreAsync.");
            return;
        }

        try
        {
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
#endif
    }

    public async Task SaveObjectInteractionAsync(
        string userId,
        string objectName,
        float duration,
        int totalInteractions,
        float averageTime)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogWarning("⚠️ [WebGL] SaveObjectInteractionAsync - Firestore not yet implemented for WebGL");
        await Task.Yield();
        return;
#else
        if (!IsReady || Firestore == null)
        {
            Debug.LogError("❌ [FirebaseManager] Firestore not ready.");
            return;
        }

        try
        {
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
#endif
    }

    public async Task<bool> UserHasDemographicsAsync(string userId)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogWarning("⚠️ [WebGL] UserHasDemographicsAsync - Firestore not yet implemented for WebGL");
        await Task.Yield();
        return false;
#else
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
            return false;
        }
#endif
    }
}