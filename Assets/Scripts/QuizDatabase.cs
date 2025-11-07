using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using Firebase.Extensions;
using UnityEngine;

[System.Serializable]
public class Question
{
    public string question;
    public List<string> answers;
    public int correctIndex;
}

public class QuizDatabase : MonoBehaviour
{
    FirebaseFirestore db;

    void Start()
    {
        db = FirebaseFirestore.DefaultInstance;
    }

    public async Task<List<Question>> LoadQuestionsForRoom(string roomId)
    {
        List<Question> result = new List<Question>();

        Query query = db.Collection("rooms").Document(roomId).Collection("questions");
        QuerySnapshot snapshot = await query.GetSnapshotAsync();

        foreach (DocumentSnapshot doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            Question q = new Question();
            q.question = data["question"].ToString();
            q.answers = new List<string>();
            q.correctIndex = int.Parse(data["correctIndex"].ToString());

            // 🔹 Safely convert Firestore's array to strings
            if (data.TryGetValue("answers", out object answersObj) && answersObj is IEnumerable<object> rawAnswers)
            {
                foreach (var ans in rawAnswers)
                {
                    q.answers.Add(ans.ToString());
                }
            }
            else
            {
                Debug.LogWarning($"Question {q.question} has no answers array!");
            }

            result.Add(q);
        }

        Debug.Log($"Loaded {result.Count} questions for room: {roomId}");
        return result;
    }


}
