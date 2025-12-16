using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public class WebGLFirebaseAuth : MonoBehaviour
{
    public static WebGLFirebaseAuth Instance { get; private set; }

    private TaskCompletionSource<string> authTaskSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("✅ WebGLFirebaseAuth initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void FirebaseRegisterUser(string email, string password, string objectName, string callbackMethod);

    [DllImport("__Internal")]
    private static extern void FirebaseLoginUser(string email, string password, string objectName, string callbackMethod);

    [DllImport("__Internal")]
    private static extern string FirebaseGetCurrentUserId();

    [DllImport("__Internal")]
    private static extern void FirebaseSignOut();
#endif

    public Task<string> RegisterAsync(string email, string password)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        authTaskSource = new TaskCompletionSource<string>();
        FirebaseRegisterUser(email, password, gameObject.name, "OnAuthCallback");
        Debug.Log($"[WebGL] Calling Firebase register for {email}");
        return authTaskSource.Task;
#else
        Debug.LogWarning("WebGLFirebaseAuth.RegisterAsync called outside WebGL build");
        return Task.FromResult<string>(null);
#endif
    }

    public Task<string> LoginAsync(string email, string password)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        authTaskSource = new TaskCompletionSource<string>();
        FirebaseLoginUser(email, password, gameObject.name, "OnAuthCallback");
        Debug.Log($"[WebGL] Calling Firebase login for {email}");
        return authTaskSource.Task;
#else
        Debug.LogWarning("WebGLFirebaseAuth.LoginAsync called outside WebGL build");
        return Task.FromResult<string>(null);
#endif
    }

    public string GetCurrentUserId()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string userId = FirebaseGetCurrentUserId();
        Debug.Log($"[WebGL] Current user ID: {userId ?? "null"}");
        return userId;
#else
        Debug.LogWarning("WebGLFirebaseAuth.GetCurrentUserId called outside WebGL build");
        return null;
#endif
    }

    public void SignOut()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        FirebaseSignOut();
        Debug.Log("[WebGL] Signed out");
#else
        Debug.LogWarning("WebGLFirebaseAuth.SignOut called outside WebGL build");
#endif
    }

    // This method is called from JavaScript
    public void OnAuthCallback(string result)
    {
        Debug.Log($"[WebGL] Auth callback received: {result}");

        if (authTaskSource == null)
        {
            Debug.LogWarning("[WebGL] authTaskSource is null in callback");
            return;
        }

        if (result.StartsWith("SUCCESS:"))
        {
            string uid = result.Substring(8);
            Debug.Log($"[WebGL] Auth success, UID: {uid}");
            authTaskSource.SetResult(uid);
        }
        else if (result.StartsWith("ERROR:"))
        {
            string error = result.Substring(6);
            Debug.LogError($"[WebGL] Auth error: {error}");
            authTaskSource.SetException(new Exception(error));
        }
        else
        {
            Debug.LogError($"[WebGL] Unknown callback result: {result}");
            authTaskSource.SetException(new Exception("Unknown callback result"));
        }

        authTaskSource = null;
    }
}