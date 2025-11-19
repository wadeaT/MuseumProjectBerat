using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;
using Firebase.Extensions;
using TMPro;
using UnityEngine.UI;

public class AchievementsManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject badgeUIPrefab;
    public GameObject cardUIPrefab;

    [Header("ScrollView Content Parents")]
    public Transform badgesContent;
    public Transform cardsContent;

    [Header("Summary UI")]
    public TMP_Text totalCardsText;
    public TMP_Text totalBadgesText;
    public TMP_Text lastCardFoundText;
    public TMP_Text lastBadgeUnlockedText;
    public TMP_Text roomStatsSummaryText;

    // -------------------------------------------------------
    // BADGE ICON SYSTEM
    // -------------------------------------------------------
    [System.Serializable]
    public class BadgeIconEntry
    {
        public string badgeId;   // e.g., “balcony_master”
        public Sprite icon;      // local sprite
    }

    [Header("Badge Icons (assign in Inspector)")]
    public List<BadgeIconEntry> badgeIcons;

    private FirebaseFirestore db;
    private string userId;

    // -------------------------------------------------------
    // UNITY FLOW
    // -------------------------------------------------------

    void Start()
    {
        StartCoroutine(LoadAchievementsFlow());
    }

    private IEnumerator LoadAchievementsFlow()
    {
        // TEMPORARY: Hardcoded user for testing
        userId = "B8w1cF06L3fJVBP7GaPuMpL9nFx1";

        db = FirebaseFirestore.DefaultInstance;

        LoadBadges();
        LoadCards();
        LoadSummary();
        LoadRoomStats();

        yield break;
    }

    // -------------------------------------------------------
    // BADGE ICON HELPER
    // -------------------------------------------------------
    private Sprite GetBadgeIcon(string badgeId)
    {
        foreach (var entry in badgeIcons)
        {
            if (entry.badgeId == badgeId)
                return entry.icon;
        }
        return null; // fallback if missing
    }

    // -------------------------------------------------------
    // LOAD BADGES
    // -------------------------------------------------------
    private void LoadBadges()
    {
        db.Collection("users").Document(userId)
            .Collection("badges")
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompletedSuccessfully) return;

                foreach (var doc in task.Result.Documents)
                {
                    bool unlocked = doc.GetValue<bool>("unlocked");
                    if (!unlocked) continue;

                    string badgeId = doc.Id;  // or doc.GetValue<string>("badgeId")
                    string name = doc.GetValue<string>("name");
                    string description = doc.GetValue<string>("description");

                    // Spawn UI item
                    GameObject item = Instantiate(badgeUIPrefab, badgesContent);

                    item.transform.Find("Title").GetComponent<TMP_Text>().text = name;
                    item.transform.Find("Desc").GetComponent<TMP_Text>().text = description;

                    // Assign Icon
                    Image iconImage = item.transform.Find("Icon").GetComponent<Image>();
                    Sprite iconSprite = GetBadgeIcon(badgeId);

                    if (iconSprite != null)
                    {
                        iconImage.sprite = iconSprite;
                    }
                    else
                    {
                        iconImage.enabled = false; // hide if no icon
                    }
                }
            });
    }

    // -------------------------------------------------------
    // LOAD CARDS
    // -------------------------------------------------------
    private void LoadCards()
    {
        db.Collection("users").Document(userId)
          .Collection("cards")
          .GetSnapshotAsync()
          .ContinueWithOnMainThread(task =>
          {
              if (!task.IsCompletedSuccessfully) return;

              foreach (var doc in task.Result.Documents)
              {
                  string cardId = doc.GetValue<string>("cardId");
                  bool found = doc.GetValue<bool>("found");

                  GameObject item = Instantiate(cardUIPrefab, cardsContent);

                  item.transform.Find("Title").GetComponent<TMP_Text>().text = cardId;
                  item.transform.Find("Status").GetComponent<TMP_Text>().text =
                      found ? "Collected ✓" : "Not Found ✗";
              }
          });
    }

    // -------------------------------------------------------
    // LOAD SUMMARY
    // -------------------------------------------------------
    private void LoadSummary()
    {
        db.Collection("users").Document(userId)
          .Collection("progress").Document("summary")
          .GetSnapshotAsync()
          .ContinueWithOnMainThread(task =>
          {
              if (!task.IsCompletedSuccessfully) return;

              var doc = task.Result;

              long totalCards = doc.GetValue<long>("totalCardsCollected");
              long totalBadges = doc.GetValue<long>("totalBadges");
              string lastCard = doc.GetValue<string>("lastCardFound");
              string lastBadge = doc.GetValue<string>("lastBadgeUnlocked");

              totalCardsText.text = "Total cards collected: " + totalCards;
              totalBadgesText.text = "Total badges unlocked: " + totalBadges;

              lastCardFoundText.text = "Last card found: " + lastCard;
              lastBadgeUnlockedText.text = "Last badge unlocked: " + lastBadge;
          });
    }


    // -------------------------------------------------------
    // LOAD ROOM STATS
    // -------------------------------------------------------
    private void LoadRoomStats()
    {
        db.Collection("users").Document(userId)
          .Collection("roomStats")
          .GetSnapshotAsync()
          .ContinueWithOnMainThread(task =>
          {
              if (!task.IsCompletedSuccessfully) return;

              string summary = "";

              foreach (var doc in task.Result.Documents)
              {
                  string room = doc.Id;

                  double timeSpent =
                      doc.ContainsField("timeSpent") ?
                      doc.GetValue<double>("timeSpent") : 0;

                  long visitCount =
                      doc.ContainsField("visitCount") ?
                      doc.GetValue<long>("visitCount") : 0;

                  summary += $"{room}: {timeSpent:F1}s, visits: {visitCount}\n";
              }

              roomStatsSummaryText.text = summary;
          });
    }
}
