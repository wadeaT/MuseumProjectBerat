using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Firestore;

/// <summary>
/// Centralized Firebase logging service.
/// Replaces 20+ scattered copy-paste logging blocks with a single entry point.
///
/// All logging goes through this class, which provides:
/// - Unified readiness/auth guards
/// - Consistent Firestore path building
/// - Automatic timestamp and sessionTime injection
/// - Consistent error handling
/// - Session-scoped OR user-scoped writes via a single API
///
/// Usage:
///   FirebaseLogger.LogSessionData("engagementStates", data);           // → users/{uid}/sessions/{sid}/engagementStates/{auto}
///   FirebaseLogger.LogSessionData("engagementStates", data, "final");  // → users/{uid}/sessions/{sid}/engagementStates/final
///   FirebaseLogger.LogUserData("experimentInfo", data, "condition");   // → users/{uid}/experimentInfo/condition
/// </summary>
public static class FirebaseLogger
{
    // ========================================================================
    // READINESS CHECK
    // ========================================================================

    /// <summary>
    /// Returns true if Firebase is initialized, user is logged in, and session exists.
    /// All logging methods call this internally; callers don't need to check.
    /// </summary>
    public static bool IsReady
    {
        get
        {
            return FirebaseManager.Instance != null
                && FirebaseManager.Instance.IsReady
                && FirebaseManager.Instance.Firestore != null
                && PlayerManager.Instance != null
                && !string.IsNullOrEmpty(PlayerManager.Instance.userId);
        }
    }

    /// <summary>
    /// Returns true if session-scoped logging is available (sessionId exists).
    /// </summary>
    public static bool HasSession
    {
        get
        {
            return IsReady && !string.IsNullOrEmpty(FirebaseManager.Instance.CurrentSessionId);
        }
    }

    // ========================================================================
    // PRIMARY API — Session-Scoped Logging
    // ========================================================================

    /// <summary>
    /// Log data under the current session:
    ///   users/{userId}/sessions/{sessionId}/{collection}/{docId}
    ///
    /// Automatically injects "timestamp" (server) and "sessionTime" (local).
    /// If docId is null, generates one from DateTime.UtcNow.Ticks.
    /// </summary>
    /// <param name="collection">Subcollection name, e.g. "engagementStates"</param>
    /// <param name="data">Payload dictionary. Will be modified in-place to add metadata.</param>
    /// <param name="docId">Optional fixed document ID. Null = auto-generated.</param>
    /// <param name="callerTag">Tag for error logs, e.g. "[EngagementClassifier]"</param>
    public static async Task LogSessionData(
        string collection,
        Dictionary<string, object> data,
        string docId = null,
        string callerTag = "[FirebaseLogger]")
    {
        if (!HasSession)
        {
            // Silent skip — expected during startup or when session not yet created
            return;
        }

        InjectMetadata(data);

        if (string.IsNullOrEmpty(docId))
        {
            docId = GenerateDocId();
        }

        try
        {
            string userId = PlayerManager.Instance.userId;
            string sessionId = FirebaseManager.Instance.CurrentSessionId;

            await FirebaseManager.Instance.Firestore
                .Collection("users").Document(userId)
                .Collection("sessions").Document(sessionId)
                .Collection(collection).Document(docId)
                .SetAsync(data);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{callerTag} Firebase write failed ({collection}/{docId}): {e.Message}");
        }
    }

    /// <summary>
    /// Log data under the current session with SetOptions.MergeAll:
    ///   users/{userId}/sessions/{sessionId}/{collection}/{docId}
    ///
    /// Use this when updating an existing document (e.g. "summary" docs).
    /// </summary>
    public static async Task MergeSessionData(
        string collection,
        Dictionary<string, object> data,
        string docId,
        string callerTag = "[FirebaseLogger]")
    {
        if (!HasSession) return;

        InjectMetadata(data);

        try
        {
            string userId = PlayerManager.Instance.userId;
            string sessionId = FirebaseManager.Instance.CurrentSessionId;

            await FirebaseManager.Instance.Firestore
                .Collection("users").Document(userId)
                .Collection("sessions").Document(sessionId)
                .Collection(collection).Document(docId)
                .SetAsync(data, SetOptions.MergeAll);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{callerTag} Firebase merge failed ({collection}/{docId}): {e.Message}");
        }
    }

    // ========================================================================
    // SECONDARY API — User-Scoped Logging (no session nesting)
    // ========================================================================

    /// <summary>
    /// Log data directly under the user document (not session-scoped):
    ///   users/{userId}/{collection}/{docId}
    ///
    /// Use sparingly — prefer session-scoped logging for research data.
    /// </summary>
    public static async Task LogUserData(
        string collection,
        Dictionary<string, object> data,
        string docId = null,
        string callerTag = "[FirebaseLogger]")
    {
        if (!IsReady) return;

        InjectMetadata(data);

        if (string.IsNullOrEmpty(docId))
        {
            docId = GenerateDocId();
        }

        try
        {
            string userId = PlayerManager.Instance.userId;

            await FirebaseManager.Instance.Firestore
                .Collection("users").Document(userId)
                .Collection(collection).Document(docId)
                .SetAsync(data);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{callerTag} Firebase write failed (user/{collection}/{docId}): {e.Message}");
        }
    }

    /// <summary>
    /// Merge data directly under the user document:
    ///   users/{userId}/{collection}/{docId}
    /// </summary>
    public static async Task MergeUserData(
        string collection,
        Dictionary<string, object> data,
        string docId,
        string callerTag = "[FirebaseLogger]")
    {
        if (!IsReady) return;

        InjectMetadata(data);

        try
        {
            string userId = PlayerManager.Instance.userId;

            await FirebaseManager.Instance.Firestore
                .Collection("users").Document(userId)
                .Collection(collection).Document(docId)
                .SetAsync(data, SetOptions.MergeAll);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{callerTag} Firebase merge failed (user/{collection}/{docId}): {e.Message}");
        }
    }

    /// <summary>
    /// Merge fields directly on the root user document:
    ///   users/{userId}
    /// </summary>
    public static async Task MergeUserRoot(
        Dictionary<string, object> data,
        string callerTag = "[FirebaseLogger]")
    {
        if (!IsReady) return;

        try
        {
            string userId = PlayerManager.Instance.userId;

            await FirebaseManager.Instance.Firestore
                .Collection("users").Document(userId)
                .SetAsync(data, SetOptions.MergeAll);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{callerTag} Firebase root merge failed: {e.Message}");
        }
    }

    // ========================================================================
    // HELPERS
    // ========================================================================

    /// <summary>
    /// Inject standard metadata fields if not already present.
    /// </summary>
    private static void InjectMetadata(Dictionary<string, object> data)
    {
        if (!data.ContainsKey("timestamp"))
            data["timestamp"] = FieldValue.ServerTimestamp;

        if (!data.ContainsKey("sessionTime"))
            data["sessionTime"] = Time.time;
    }

    /// <summary>
    /// Generate a unique document ID based on UTC ticks.
    /// </summary>
    public static string GenerateDocId(string prefix = null)
    {
        string ticks = System.DateTime.UtcNow.Ticks.ToString();
        return string.IsNullOrEmpty(prefix) ? ticks : $"{prefix}_{ticks}";
    }
}
