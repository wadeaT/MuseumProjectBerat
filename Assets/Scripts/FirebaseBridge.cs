using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// Bridge between Unity C# and Firebase JavaScript SDK for WebGL builds.
/// All methods call into JavaScript via DllImport.
/// </summary>
public class FirebaseBridge : MonoBehaviour
{
    public static FirebaseBridge Instance { get; private set; }

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

    // ========================================================================
    // JAVASCRIPT INTEROP - Firebase Auth
    // ========================================================================

    /// <summary>
    /// Sign in anonymously with Firebase Auth
    /// </summary>
    [DllImport("__Internal")]
    public static extern void SignInAnonymously(string objectName, string callbackSuccess, string callbackError);

    /// <summary>
    /// Get the current authenticated user's ID
    /// </summary>
    [DllImport("__Internal")]
    public static extern string GetCurrentUserId();

    // ========================================================================
    // JAVASCRIPT INTEROP - Firestore Operations
    // ========================================================================

    /// <summary>
    /// Set/update a document in a Firestore collection
    /// </summary>
    [DllImport("__Internal")]
    public static extern void SetDocument(string collectionPath, string documentId, string jsonData,
        string objectName, string callbackSuccess, string callbackError);

    /// <summary>
    /// Get a document from a Firestore collection
    /// </summary>
    [DllImport("__Internal")]
    public static extern void GetDocument(string collectionPath, string documentId,
        string objectName, string callbackSuccess, string callbackError);

    /// <summary>
    /// Add a new document to a collection (auto-generated ID)
    /// </summary>
    [DllImport("__Internal")]
    public static extern void AddDocument(string collectionPath, string jsonData,
        string objectName, string callbackSuccess, string callbackError);

    /// <summary>
    /// Set/update a document in a subcollection
    /// </summary>
    [DllImport("__Internal")]
    public static extern void SetDocumentInSubcollection(string path, string documentId, string jsonData,
        string objectName, string callbackSuccess, string callbackError);

    /// <summary>
    /// Get all documents from a collection
    /// </summary>
    [DllImport("__Internal")]
    public static extern void GetCollection(string collectionPath,
        string objectName, string callbackSuccess, string callbackError);

    /// <summary>
    /// Check if a document exists
    /// </summary>
    [DllImport("__Internal")]
    public static extern void DocumentExists(string collectionPath, string documentId,
        string objectName, string callbackSuccess, string callbackError);

    /// <summary>
    /// Get all documents from a collection with their full data
    /// </summary>
    [DllImport("__Internal")]
    public static extern void GetCollectionWithData(string collectionPath,
        string objectName, string callbackSuccess, string callbackError);

    // ========================================================================
    // UTILITY METHODS
    // ========================================================================

    /// <summary>
    /// Helper method to safely send a message to a GameObject
    /// </summary>
    public static void SafeSendMessage(string objectName, string methodName, string message)
    {
        var obj = GameObject.Find(objectName);
        if (obj != null)
        {
            obj.SendMessage(methodName, message, SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            Debug.LogWarning($"[FirebaseBridge] GameObject '{objectName}' not found for method '{methodName}'");
        }
    }

    /// <summary>
    /// Log platform info for debugging
    /// </summary>
    public void LogPlatform()
    {
        Debug.Log("[FirebaseBridge] Running in WebGL build");
    }
}