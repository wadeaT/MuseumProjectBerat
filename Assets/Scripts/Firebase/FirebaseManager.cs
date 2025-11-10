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
            Debug.LogError($"❌ Firebase dependencies missing: {status}");
            return;
        }

        App = FirebaseApp.DefaultInstance;
        Auth = FirebaseAuth.DefaultInstance;
        Firestore = FirebaseFirestore.DefaultInstance;

        IsReady = true;
        Debug.Log("✅ Firebase initialized successfully!");
    }

    // ------------------------------------------------------------------------
    // USER AUTH (EMAIL + PASSWORD)
    // ------------------------------------------------------------------------

    public async Task<string> RegisterUserAsync(string email, string password)
    {
        if (!IsReady)
        {
            Debug.LogWarning("Firebase not ready yet.");
            return null;
        }

        try
        {
            var result = await Auth.CreateUserWithEmailAndPasswordAsync(email, password);
            Debug.Log($"✅ Registered new user: {result.User.UserId}");
            return result.User.UserId;
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ Register failed: " + e.Message);
            return null;
        }
    }

    public async Task<string> LoginUserAsync(string email, string password)
    {
        if (!IsReady)
        {
            Debug.LogWarning("Firebase not ready yet.");
            return null;
        }

        try
        {
            var result = await Auth.SignInWithEmailAndPasswordAsync(email, password);
            Debug.Log($"✅ Logged in: {result.User.UserId}");
            return result.User.UserId;
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ Login failed: " + e.Message);
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
            Debug.LogError("❌ Firestore not ready or null.");
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
            Debug.Log("✅ Demographics saved successfully in Firestore!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Failed to save demographics: {e.Message}\n{e.StackTrace}");
        }
    }



    public async Task SaveBadgeAsync(string userId, string badgeId)
    {
        if (!IsReady || Firestore == null) return;

        try
        {
            var doc = Firestore.Collection("users").Document(userId)
                .Collection("progress").Document("badges");

            await doc.UpdateAsync(new Dictionary<string, object>
            {
                { "badgeList", FieldValue.ArrayUnion(badgeId) }
            });
            Debug.Log($"✅ Badge '{badgeId}' saved for user {userId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ Failed to save badge: " + e.Message);
        }
    }

    public async Task SaveRoomTimeAsync(string userId, string roomId, float timeSpent)
    {
        if (!IsReady || Firestore == null) return;

        try
        {
            var data = new Dictionary<string, object>
            {
                { "timeSpent", timeSpent },
                { "updatedAt", FieldValue.ServerTimestamp }
            };

            var doc = Firestore.Collection("users").Document(userId)
                .Collection("roomStats").Document(roomId);

            await doc.SetAsync(data, SetOptions.MergeAll);
            Debug.Log($"✅ Room time saved ({roomId}: {timeSpent:F2}s)");
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ Failed to save room time: " + e.Message);
        }
    }
}
