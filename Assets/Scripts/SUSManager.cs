using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Auth;
using Firebase.Firestore;

public class SUSManager : MonoBehaviour
{
    [Header("Setup")]
    public RectTransform contentParent;      // Content_Questions
    public SUSQuestion questionPrefab;       // QuestionItem prefab
    public Button submitButton;
    public TextMeshProUGUI errorText;

    private FirebaseAuth auth;
    private FirebaseFirestore db;
    private string testUserId = "TEST_USER_123";

    private readonly string[] susItems = new string[]
    {
        "1. I think that I would like to use this system frequently.",
        "2. I found the system unnecessarily complex.",
        "3. I thought the system was easy to use.",
        "4. I think that I would need the support of a technical person to be able to use this system.",
        "5. I found the various functions in this system were well integrated.",
        "6. I thought there was too much inconsistency in this system.",
        "7. I would imagine that most people would learn to use this system very quickly.",
        "8. I found the system very cumbersome to use.",
        "9. I felt very confident using the system.",
        "10. I needed to learn a lot of things before I could get going with this system."
    };

    private readonly List<SUSQuestion> _questions = new List<SUSQuestion>();

    void Awake()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;

        if (errorText != null)
            errorText.text = "";

        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmitClicked);

        BuildQuestions();
    }

    private void BuildQuestions()
    {
        if (contentParent == null || questionPrefab == null)
        {
            Debug.LogError("SUSManager not configured: missing contentParent or questionPrefab.");
            return;
        }

        // Clear old children (if any)
        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Destroy(contentParent.GetChild(i).gameObject);
        }

        _questions.Clear();

        // Instantiate one QuestionItem for each SUS item
        for (int i = 0; i < susItems.Length; i++)
        {
            SUSQuestion qInstance = Instantiate(questionPrefab, contentParent);
            qInstance.SetQuestionText(susItems[i]);
            _questions.Add(qInstance);
        }
    }

    private async void OnSubmitClicked()
    {
        if (_questions.Count == 0)
        {
            ShowError("Questions are not loaded.");
            return;
        }

        if (auth.CurrentUser == null)
        {
            ShowError("No user logged in.");
            return;
        }

        // 1) Collect answers
        List<int> answers = new List<int>();
        for (int i = 0; i < _questions.Count; i++)
        {
            int ans = _questions[i].GetAnswer();
            if (ans == 0)
            {
                ShowError($"Please answer question {i + 1}.");
                return;
            }
            answers.Add(ans);
        }

        // 2) Prepare data to save
        var data = new Dictionary<string, object>();
        for (int i = 0; i < answers.Count; i++)
        {
            data[$"Q{i + 1}"] = answers[i];
        }
        data["timestamp"] = Timestamp.GetCurrentTimestamp();

        //string userId = auth.CurrentUser.UserId;
        string userId = testUserId;

        try
        {
            DocumentReference docRef = db
                .Collection("users")
                .Document(userId)
                .Collection("questionnaires")
                .Document("SUS");

            await docRef.SetAsync(data, SetOptions.MergeAll);

            Debug.Log("SUS saved successfully!");

            // TODO: go to thank-you scene or exit
            // SceneManager.LoadScene("ThankYouScene");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save SUS: {e.Message}");
            ShowError("Error saving. Check your internet and try again.");
        }
    }

    private void ShowError(string msg)
    {
        if (errorText != null)
            errorText.text = msg;
        else
            Debug.LogWarning(msg);
    }
}
