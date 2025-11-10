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
    //  AUTHENTICATION
    // ------------------------------------------------------------------------

    public async Task<bool> RegisterUser(string email, string password)
    {
        if (FirebaseManager.Instance == null)
        {
            Debug.LogError("FirebaseManager not found in scene!");
            return false;
        }

        userId = await FirebaseManager.Instance.RegisterUserAsync(email, password);
        if (!string.IsNullOrEmpty(userId))
        {
            Debug.Log($"✅ Registered new user: {userId}");
            return true;
        }

        Debug.LogError("❌ Registration failed.");
        return false;
    }

    public async Task<bool> LoginUser(string email, string password)
    {
        if (FirebaseManager.Instance == null)
        {
            Debug.LogError("FirebaseManager not found in scene!");
            return false;
        }

        userId = await FirebaseManager.Instance.LoginUserAsync(email, password);
        if (!string.IsNullOrEmpty(userId))
        {
            Debug.Log($"✅ Logged in user: {userId}");
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

    public async void AddBadge(string badgeId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("User not logged in; badge not saved.");
            return;
        }

        if (!badges.Contains(badgeId))
        {
            badges.Add(badgeId);
            await FirebaseManager.Instance.SaveBadgeAsync(userId, badgeId);
            Debug.Log($"🏅 Badge added: {badgeId}");
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
        Debug.Log("🧹 Player data cleared.");
    }
}
