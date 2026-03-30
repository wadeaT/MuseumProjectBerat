using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// ScoreManager - FIXED VERSION
/// 
/// ✅ FIX: Now resets score when a different user logs in
/// Uses user-specific PlayerPrefs keys to prevent cross-user contamination
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Score Settings")]
    [Tooltip("Points awarded for collecting a card")]
    public int cardCollectionPoints = 100;

    [Tooltip("Points for first card in a new room")]
    public int firstCardInRoomBonus = 50;

    [Tooltip("Points for examining an object")]
    public int objectExaminationPoints = 10;

    [Header("Badge Bonus Points")]
    public int explorerBadgeBonus = 200;
    public int curiousMindBonus = 400;
    public int enthusiastBonus = 600;
    public int scholarBonus = 800;
    public int masterCollectorBonus = 1000;
    public int museumExpertBonus = 1500;

    [Header("UI References")]
    public TextMeshProUGUI scoreText;
    public GameObject scorePopupPrefab;

    [Header("Visual Feedback")]
    public AudioClip pointsEarnedSound;
    public Color normalPointsColor = Color.white;
    public Color bonusPointsColor = Color.yellow;

    private int currentScore = 0;
    private AudioSource audioSource;

    // ✅ Track which user's score we're managing
    private string currentUserId = "";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    void Start()
    {
        // ✅ FIX: Load score for CURRENT user (or start fresh)
        InitializeForCurrentUser();
        UpdateScoreUI();
    }

    /// <summary>
    /// ✅ NEW: Initialize score for the current user
    /// Called on Start and when user changes
    /// </summary>
    public void InitializeForCurrentUser()
    {
        string userId = GetCurrentUserId();

        if (string.IsNullOrEmpty(userId))
        {
            // No user logged in - start with 0
            currentScore = 0;
            currentUserId = "";
            Debug.Log("[ScoreManager] No user logged in, starting with score 0");
        }
        else if (userId != currentUserId)
        {
            // Different user - load their score or start fresh
            currentUserId = userId;
            LoadScoreForUser(userId);
            Debug.Log($"[ScoreManager] Initialized for user: {userId}, Score: {currentScore}");
        }

        UpdateScoreUI();
    }

    /// <summary>
    /// Get current user ID from PlayerManager
    /// </summary>
    private string GetCurrentUserId()
    {
        if (PlayerManager.Instance != null && !string.IsNullOrEmpty(PlayerManager.Instance.userId))
        {
            return PlayerManager.Instance.userId;
        }
        return "";
    }

    /// <summary>
    /// ✅ NEW: Load score for a specific user
    /// </summary>
    private void LoadScoreForUser(string userId)
    {
        // Use user-specific key
        string key = $"Score_{userId}";
        currentScore = PlayerPrefs.GetInt(key, 0);
        Debug.Log($"[ScoreManager] Loaded score {currentScore} for user {userId}");
    }

    /// <summary>
    /// ✅ NEW: Save score for current user
    /// </summary>
    private void SaveScoreForUser()
    {
        string userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("[ScoreManager] Cannot save score - no user logged in");
            return;
        }

        string key = $"Score_{userId}";
        PlayerPrefs.SetInt(key, currentScore);
        PlayerPrefs.Save();
        Debug.Log($"[ScoreManager] Saved score {currentScore} for user {userId}");
    }

    /// <summary>
    /// Add points to the score
    /// </summary>
    public void AddPoints(int points, string reason = "", bool isBonus = false)
    {
        // ✅ Verify we're tracking the right user
        string currentUser = GetCurrentUserId();
        if (!string.IsNullOrEmpty(currentUser) && currentUser != currentUserId)
        {
            Debug.LogWarning($"[ScoreManager] User changed from {currentUserId} to {currentUser}, reinitializing...");
            InitializeForCurrentUser();
        }

        currentScore += points;

        Debug.Log($"🎯 +{points} points! {reason} | Total: {currentScore}");

        UpdateScoreUI();
        ShowPointsPopup(points, isBonus);

        if (audioSource != null && pointsEarnedSound != null)
        {
            audioSource.PlayOneShot(pointsEarnedSound);
        }

        // ✅ Save with user-specific key
        SaveScoreForUser();
        SaveScoreToFirebase();
    }

    /// <summary>
    /// Called when a card is collected
    /// </summary>
    public void OnCardCollected(string cardID, string roomID, bool isFirstInRoom)
    {
        int points = cardCollectionPoints;

        if (isFirstInRoom)
        {
            points += firstCardInRoomBonus;
        }

        AddPoints(points, $"Card collected: {cardID}", false);
    }

    /// <summary>
    /// Called when an object is examined
    /// </summary>
    public void OnObjectExamined(string objectName)
    {
        AddPoints(objectExaminationPoints, $"Examined: {objectName}", false);
    }

    /// <summary>
    /// Called when a badge is unlocked
    /// </summary>
    public void OnBadgeUnlocked(int cardCount)
    {
        int bonusPoints = 0;
        string badgeName = "";

        switch (cardCount)
        {
            case 3:
                bonusPoints = explorerBadgeBonus;
                badgeName = "Explorer";
                break;
            case 6:
                bonusPoints = curiousMindBonus;
                badgeName = "Curious Mind";
                break;
            case 9:
                bonusPoints = enthusiastBonus;
                badgeName = "Culture Enthusiast";
                break;
            case 12:
                bonusPoints = scholarBonus;
                badgeName = "Heritage Scholar";
                break;
            case 15:
                bonusPoints = masterCollectorBonus;
                badgeName = "Master Collector";
                break;
            case 18:
                bonusPoints = museumExpertBonus;
                badgeName = "Museum Expert";
                break;
        }

        if (bonusPoints > 0)
        {
            AddPoints(bonusPoints, $"🏅 {badgeName} Badge!", true);
        }
    }

    void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score: {currentScore:N0}";
        }
    }

    void ShowPointsPopup(int points, bool isBonus)
    {
        if (scorePopupPrefab != null && scoreText != null)
        {
            GameObject popup = Instantiate(scorePopupPrefab, scoreText.transform.position, Quaternion.identity, scoreText.transform.parent);

            TextMeshProUGUI popupText = popup.GetComponent<TextMeshProUGUI>();
            if (popupText != null)
            {
                popupText.text = $"+{points}";
                popupText.color = isBonus ? bonusPointsColor : normalPointsColor;
            }

            Destroy(popup, 2f);
        }
    }

    /// <summary>
    /// ✅ DEPRECATED: Use InitializeForCurrentUser() instead
    /// Kept for backward compatibility
    /// </summary>
    void LoadScore()
    {
        InitializeForCurrentUser();
    }

    /// <summary>
    /// ✅ DEPRECATED: Use SaveScoreForUser() instead  
    /// </summary>
    void SaveScore()
    {
        SaveScoreForUser();
    }

    /// <summary>
    /// Reset score (for testing or new game)
    /// </summary>
    public void ResetScore()
    {
        currentScore = 0;
        SaveScoreForUser();
        UpdateScoreUI();
        Debug.Log("[ScoreManager] Score reset to 0");
    }

    /// <summary>
    /// ✅ NEW: Called when a new user logs in to start fresh
    /// </summary>
    public void OnUserChanged()
    {
        InitializeForCurrentUser();
    }

    /// <summary>
    /// Get current score
    /// </summary>
    public int GetScore()
    {
        return currentScore;
    }

    /// <summary>
    /// Save score to Firebase
    /// </summary>
    private async void SaveScoreToFirebase()
    {
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
        {
            Debug.LogWarning("[ScoreManager] FirebaseManager not ready, score not saved to cloud");
            return;
        }

        if (PlayerManager.Instance == null || string.IsNullOrEmpty(PlayerManager.Instance.userId))
        {
            Debug.LogWarning("[ScoreManager] User not logged in, score not saved to Firebase");
            return;
        }

        string odId = PlayerManager.Instance.userId;

        try
        {
            await FirebaseManager.Instance.SaveUserScoreAsync(odId, currentScore);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ScoreManager] Error saving score to Firebase: {e.Message}");
        }
    }
}