using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Threading.Tasks;

public class QuizManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text questionText; // TextMeshPro text for the question
    public List<Button> answerButtons; // Buttons for answers

    private List<Question> questions = new List<Question>();
    private int currentQuestion = 0;
    private QuizDatabase quizDatabase;

    async void Start()
    {
        Debug.Log("[QuizManager] Start() called.");

        // Find the QuizDatabase in the scene
        quizDatabase = Object.FindFirstObjectByType<QuizDatabase>();
        if (quizDatabase == null)
        {
            Debug.LogError("[QuizManager] QuizDatabase not found in scene!");
            return;
        }

        Debug.Log("[QuizManager] Waiting for Firebase to initialize...");
        await WaitForFirebaseReady();
        Debug.Log("[QuizManager] Firebase ready! Now loading questions...");

        // Load quiz questions from Firestore
        questions = await quizDatabase.LoadQuestionsForRoom("room1");
        Debug.Log($"[QuizManager] Questions loaded: {questions.Count}");

        if (questions.Count > 0)
        {
            DisplayQuestion();
        }
        else
        {
            questionText.text = "No questions found!";
            Debug.LogWarning("[QuizManager] No questions found in Firestore for room1!");
        }
    }

    async Task WaitForFirebaseReady()
    {
        int checks = 0;
        while (!FirebaseInitializer.IsReady)
        {
            await Task.Delay(100);
            checks++;
            if (checks % 20 == 0)
                Debug.Log($"[QuizManager] Still waiting for Firebase... {checks * 100}ms elapsed");
        }
    }

    void DisplayQuestion()
    {
        Debug.Log("[QuizManager] DisplayQuestion() called.");

        if (questions == null || questions.Count == 0)
        {
            Debug.LogWarning("[QuizManager] No questions to display!");
            return;
        }

        if (currentQuestion >= questions.Count)
        {
            Debug.LogWarning("[QuizManager] Current question index out of range!");
            return;
        }

        var q = questions[currentQuestion];
        questionText.text = q.question;
        Debug.Log($"[QuizManager] Showing question {currentQuestion + 1}/{questions.Count}: {q.question}");

        for (int i = 0; i < answerButtons.Count; i++)
        {
            if (i < q.answers.Count)
            {
                answerButtons[i].gameObject.SetActive(true);

                TMP_Text btnText = answerButtons[i].GetComponentInChildren<TMP_Text>();
                if (btnText == null)
                {
                    Debug.LogWarning($"[QuizManager] Button {i} has no TMP_Text child!");
                    continue;
                }

                btnText.text = q.answers[i];
                Debug.Log($"[QuizManager] Set Button {i} text to: {q.answers[i]}");

                // Assign click listeners
                int index = i;
                answerButtons[i].onClick.RemoveAllListeners();
                answerButtons[i].onClick.AddListener(() => OnAnswerClicked(index));
                Debug.Log($"[QuizManager] Added listener to Button {i}");
            }
            else
            {
                answerButtons[i].gameObject.SetActive(false);
                Debug.Log($"[QuizManager] Hiding Button {i} (no corresponding answer)");
            }
        }
    }

    void OnAnswerClicked(int index)
    {
        Debug.Log($"[QuizManager] Button {index} clicked.");

        if (currentQuestion >= questions.Count)
        {
            Debug.LogError("[QuizManager] No valid question for this index!");
            return;
        }

        bool isCorrect = (index == questions[currentQuestion].correctIndex);
        Debug.Log(isCorrect ? "[QuizManager] ✅ Correct answer!" : "[QuizManager] ❌ Wrong answer!");

        // Optional: visual feedback
        var btnImage = answerButtons[index].GetComponent<Image>();
        if (btnImage != null)
        {
            btnImage.color = isCorrect ? Color.green : Color.red;
        }

        // Move to next question after a delay
        Debug.Log("[QuizManager] Moving to next question after delay...");
        StartCoroutine(NextQuestionAfterDelay(1.2f));
    }

    System.Collections.IEnumerator NextQuestionAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        currentQuestion++;

        Debug.Log($"[QuizManager] NextQuestionAfterDelay() → advancing to question index {currentQuestion}");

        if (currentQuestion < questions.Count)
        {
            DisplayQuestion();
        }
        else
        {
            questionText.text = "🎉 Quiz Complete!";
            foreach (var btn in answerButtons)
                btn.gameObject.SetActive(false);

            Debug.Log("[QuizManager] Quiz completed! All buttons hidden.");
        }
    }
}
