using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// ScoreManager - WebGL-safe version using coroutines instead of async/await
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
    public int explorerBadgeBonus = 200;      // 3 cards
    public int curiousMindBonus = 400;        // 6 cards
    public int enthusiastBonus = 600;         // 9 cards
    public int scholarBonus = 800;            // 12 cards
    public int masterCollectorBonus = 1000;   // 15 cards
    public int museumExpertBonus = 1500;      // 18 cards

    [Header("UI References")]
    public TextMeshProUGUI scoreText;
    public GameObject scorePopupPrefab;

    [Header("Visual Feedback")]
    public AudioClip pointsEarnedSound;
    public Color normalPointsColor = Color.white;
    public Color bonusPointsColor = Color.yellow;

    private int currentScore = 0;
    private AudioSource audioSource;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    void Start()
    {
        LoadScore();
        UpdateScoreUI();
    }

    /// <summary>
    /// Add points to the score
    /// </summary>
    public void AddPoints(int points, string reason = "", bool isBonus = false)
    {
        currentScore += points;

        Debug.Log($"[ScoreManager] +{points} points! {reason} | Total: {currentScore}");

        // Visual feedback
        UpdateScoreUI();
        ShowPointsPopup(points, isBonus);

        // Play sound
        if (audioSource != null && pointsEarnedSound != null)
        {
            audioSource.PlayOneShot(pointsEarnedSound);
        }

        // Save to PlayerPrefs
        SaveScore();

        // Save to Firebase (non-blocking)
        SaveScoreToFirebase();
    }

    /// <summary>
    /// Called when a card is collected
    /// </summary>
    public void OnCardCollected(string cardID, string roomID, bool isFirstInRoom)
    {
        int points = cardCollectionPoints;

        // Bonus for first card in room
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
            AddPoints(bonusPoints, $"{badgeName} Badge!", true);
        }
    }

    /// <summary>
    /// Update the score UI text
    /// </summary>
    void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score: {currentScore:N0}";
        }
    }

    /// <summary>
    /// Show floating points popup (optional)
    /// </summary>
    void ShowPointsPopup(int points, bool isBonus)
    {
        if (scorePopupPrefab != null && scoreText != null)
        {
            // Instantiate popup near score text
            GameObject popup = Instantiate(scorePopupPrefab, scoreText.transform.position, Quaternion.identity, scoreText.transform.parent);

            TextMeshProUGUI popupText = popup.GetComponent<TextMeshProUGUI>();
            if (popupText != null)
            {
                popupText.text = $"+{points}";
                popupText.color = isBonus ? bonusPointsColor : normalPointsColor;
            }

            // Auto-destroy after animation
            Destroy(popup, 2f);
        }
    }

    /// <summary>
    /// Save score to PlayerPrefs
    /// </summary>
    void SaveScore()
    {
        PlayerPrefs.SetInt("TotalScore", currentScore);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Load score from PlayerPrefs
    /// </summary>
    void LoadScore()
    {
        currentScore = PlayerPrefs.GetInt("TotalScore", 0);
    }

    /// <summary>
    /// Reset score (for testing or new game)
    /// </summary>
    public void ResetScore()
    {
        currentScore = 0;
        SaveScore();
        UpdateScoreUI();
        Debug.Log("[ScoreManager] Score reset to 0");
    }

    /// <summary>
    /// Get current score
    /// </summary>
    public int GetScore()
    {
        return currentScore;
    }

    /// <summary>
    /// Save score to Firebase - non-blocking coroutine approach
    /// </summary>
    private void SaveScoreToFirebase()
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

        // Non-blocking Firebase save
        FirebaseManager.Instance.SaveUserScore(odId, currentScore, (success) =>
        {
            if (!success)
            {
                Debug.LogWarning("[ScoreManager] Failed to save score to Firebase");
            }
        });
    }
}