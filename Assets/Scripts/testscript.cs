using UnityEngine;
using Firebase;
using Firebase.Firestore;

public class testscript : MonoBehaviour
{
    async void Start()
    {
        Debug.Log("Checking Firebase...");
        var status = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (status == DependencyStatus.Available)
        {
            Debug.Log("✅ Firebase core OK");
            try
            {
                var db = FirebaseFirestore.DefaultInstance;
                Debug.Log("✅ Firestore instance created");
            }
            catch (System.Exception e)
            {
                Debug.LogError("❌ Firestore failed: " + e);
            }
        }
        else
        {
            Debug.LogError("Missing dependencies: " + status);
        }
    }
}
