using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// MEASUREMENT STAGE: Tracks reading behavior from information panels
/// Provides smoothed reading engagement signals to the classification stage
/// 
/// Architecture:
/// [ObjectInfoUI/CardDiscoveryUI] → OnContentViewed() → [ReadingBehaviorTracker] → GetReadingEngagementScore() → [EngagementClassifier]
/// 
/// This component only MEASURES and SMOOTHS - it does not classify or adapt.
/// Separation ensures research validity and explainability.
/// </summary>
public class ReadingBehaviorTracker : MonoBehaviour
{
    public static ReadingBehaviorTracker Instance { get; private set; }

    // ============================================================================
    // CONFIGURATION
    // ============================================================================

    [Header("Smoothing Settings")]
    [Tooltip("Smoothing factor for exponential moving average (0-1). Higher = more responsive, Lower = more stable")]
    [Range(0.1f, 0.9f)]
    public float smoothingFactor = 0.3f;

    [Tooltip("How quickly reading engagement decays without new events (per second)")]
    [Range(0.001f, 0.05f)]
    public float decayRate = 0.01f;

    [Tooltip("Reading engagement decays toward this neutral value")]
    [Range(0f, 1f)]
    public float neutralBaseline = 0.5f;

    [Header("Reading Evaluation Thresholds")]
    [Tooltip("Reading engagement ratio above this = engaged reading")]
    [Range(0.4f, 0.8f)]
    public float engagedReadingThreshold = 0.5f;

    [Tooltip("Reading engagement ratio below this = skipped/skimmed")]
    [Range(0.1f, 0.4f)]
    public float skippedReadingThreshold = 0.25f;

    [Header("Event History")]
    [Tooltip("Maximum number of reading events to keep in history")]
    public int maxHistorySize = 20;

    [Tooltip("Events older than this (seconds) are weighted less")]
    public float eventRelevanceWindow = 120f;

    [Header("Signal Weights")]
    [Tooltip("Weight of reading duration relative to estimated time")]
    [Range(0f, 1f)]
    public float durationWeight = 0.6f;

    [Tooltip("Weight of word count (longer content = more weight if read)")]
    [Range(0f, 1f)]
    public float contentLengthWeight = 0.2f;

    [Tooltip("Weight of content type (cards weighted more than objects)")]
    [Range(0f, 1f)]
    public float contentTypeWeight = 0.2f;

    [Header("Debug")]
    public bool showDebugLogs = true;
    public bool showDetailedLogs = false;

    // ============================================================================
    // INTERNAL STATE
    // ============================================================================

    // Current smoothed reading engagement (0 = completely disengaged, 1 = highly engaged)
    private float currentReadingEngagement = 0.5f;

    // History of reading events for analysis
    private List<ReadingEvent> readingHistory = new List<ReadingEvent>();

    // Time tracking for decay
    private float lastEventTime = 0f;
    private float lastUpdateTime = 0f;

    // Statistics
    private int totalEventsReceived = 0;
    private int engagedReadEvents = 0;
    private int skippedReadEvents = 0;

    // ============================================================================
    // UNITY LIFECYCLE
    // ============================================================================

    void Awake()
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

    void Start()
    {
        lastEventTime = Time.time;
        lastUpdateTime = Time.time;

        if (showDebugLogs)
        {
            Debug.Log("✅ [ReadingBehaviorTracker] MEASUREMENT STAGE initialized");
            Debug.Log($"   Smoothing: {smoothingFactor}, Decay: {decayRate}/s, Baseline: {neutralBaseline}");
        }
    }

    void Update()
    {
        // Apply time decay to reading engagement
        ApplyTimeDecay();
    }

    // ============================================================================
    // MEASUREMENT API - Called by UI panels
    // ============================================================================

    /// <summary>
    /// Called when a content panel (object or card) is closed
    /// This is the primary input to the measurement stage
    /// </summary>
    public void OnContentViewed(ReadingEventData eventData)
    {
        totalEventsReceived++;
        lastEventTime = Time.time;

        // Calculate reading engagement for this event
        float eventEngagement = CalculateEventEngagement(eventData);

        // Classify the event
        ReadingOutcome outcome = ClassifyReadingOutcome(eventEngagement, eventData);

        // Update statistics
        if (outcome == ReadingOutcome.EngagedReading)
            engagedReadEvents++;
        else if (outcome == ReadingOutcome.Skipped)
            skippedReadEvents++;

        // Store in history
        ReadingEvent readingEvent = new ReadingEvent
        {
            timestamp = Time.time,
            eventData = eventData,
            calculatedEngagement = eventEngagement,
            outcome = outcome
        };

        readingHistory.Add(readingEvent);

        // Trim history if needed
        while (readingHistory.Count > maxHistorySize)
        {
            readingHistory.RemoveAt(0);
        }

        // Update smoothed engagement using exponential moving average
        UpdateSmoothedEngagement(eventEngagement, eventData);

        // Log to Firebase
        LogReadingEventToFirebase(readingEvent);

        if (showDebugLogs)
        {
            string outcomeEmoji = outcome == ReadingOutcome.EngagedReading ? "📖" :
                                  outcome == ReadingOutcome.Skipped ? "⏭️" : "👀";
            Debug.Log($"{outcomeEmoji} [ReadingBehaviorTracker] {eventData.contentType}: '{eventData.contentName}'");
            Debug.Log($"   Duration: {eventData.viewDuration:F1}s / {eventData.estimatedReadTime:F1}s estimated");
            Debug.Log($"   Event engagement: {eventEngagement:F2} → Outcome: {outcome}");
            Debug.Log($"   Smoothed reading engagement: {currentReadingEngagement:F2}");
        }
    }

    /// <summary>
    /// Simplified overload for direct calls
    /// </summary>
    public void OnContentViewed(
        string contentName,
        string contentType,
        float viewDuration,
        float estimatedReadTime,
        int wordCount,
        bool wasLikelyRead)
    {
        OnContentViewed(new ReadingEventData
        {
            contentName = contentName,
            contentType = contentType,
            viewDuration = viewDuration,
            estimatedReadTime = estimatedReadTime,
            wordCount = wordCount,
            wasLikelyRead = wasLikelyRead
        });
    }

    // ============================================================================
    // CLASSIFICATION STAGE API - Called by EngagementClassifier
    // ============================================================================

    /// <summary>
    /// Get current smoothed reading engagement score (0-1)
    /// Used by EngagementClassifier as one of its criteria
    /// </summary>
    public float GetReadingEngagementScore()
    {
        return currentReadingEngagement;
    }

    /// <summary>
    /// Check if reading behavior indicates engagement
    /// Simple boolean for criteria-based classification
    /// </summary>
    public bool IsReadingEngaged()
    {
        return currentReadingEngagement >= engagedReadingThreshold;
    }

    /// <summary>
    /// Check if reading behavior indicates disengagement
    /// </summary>
    public bool IsReadingDisengaged()
    {
        return currentReadingEngagement <= skippedReadingThreshold;
    }

    /// <summary>
    /// Get reading engagement as a 3-level classification
    /// </summary>
    public ReadingEngagementLevel GetReadingEngagementLevel()
    {
        if (currentReadingEngagement >= engagedReadingThreshold)
            return ReadingEngagementLevel.Engaged;
        else if (currentReadingEngagement <= skippedReadingThreshold)
            return ReadingEngagementLevel.Disengaged;
        else
            return ReadingEngagementLevel.Neutral;
    }

    /// <summary>
    /// Get confidence in the current reading engagement estimate
    /// Based on recency and quantity of events
    /// </summary>
    public float GetConfidence()
    {
        if (readingHistory.Count == 0)
            return 0f;

        // Factor 1: Number of recent events
        float currentTime = Time.time;
        int recentEvents = 0;
        for (int i = 0; i < readingHistory.Count; i++)
        {
            if (currentTime - readingHistory[i].timestamp < eventRelevanceWindow)
                recentEvents++;
        }
        float quantityConfidence = Mathf.Clamp01(recentEvents / 5f); // Max confidence at 5+ recent events

        // Factor 2: Recency of last event
        float timeSinceLastEvent = Time.time - lastEventTime;
        float recencyConfidence = Mathf.Clamp01(1f - (timeSinceLastEvent / eventRelevanceWindow));

        return (quantityConfidence + recencyConfidence) / 2f;
    }

    // ============================================================================
    // INTERNAL CALCULATIONS
    // ============================================================================

    /// <summary>
    /// Calculate engagement score for a single reading event
    /// </summary>
    float CalculateEventEngagement(ReadingEventData eventData)
    {
        float engagement = 0f;

        // Component 1: Duration ratio (primary signal)
        float durationRatio = 0f;
        if (eventData.estimatedReadTime > 0)
        {
            durationRatio = Mathf.Clamp01(eventData.viewDuration / eventData.estimatedReadTime);
        }
        engagement += durationRatio * durationWeight;

        // Component 2: Content length bonus (reading longer content = more engaged)
        float lengthBonus = 0f;
        if (eventData.wordCount > 0 && eventData.wasLikelyRead)
        {
            // Normalize word count (50 words = 0.5, 150 words = 1.0)
            lengthBonus = Mathf.Clamp01((eventData.wordCount - 25f) / 125f);
        }
        engagement += lengthBonus * contentLengthWeight;

        // Component 3: Content type weight (cards = more detailed, worth more)
        float typeBonus = 0f;
        if (eventData.wasLikelyRead)
        {
            typeBonus = eventData.contentType == "Card" ? 1f : 0.7f;
        }
        engagement += typeBonus * contentTypeWeight;

        // Normalize to 0-1
        float totalWeight = durationWeight + contentLengthWeight + contentTypeWeight;
        if (totalWeight > 0)
        {
            engagement /= totalWeight;
        }

        return Mathf.Clamp01(engagement);
    }

    /// <summary>
    /// Classify reading outcome based on engagement score
    /// </summary>
    ReadingOutcome ClassifyReadingOutcome(float eventEngagement, ReadingEventData eventData)
    {
        // Quick close = definitely skipped
        if (eventData.viewDuration < 1.5f)
            return ReadingOutcome.Skipped;

        // High engagement = engaged reading
        if (eventEngagement >= engagedReadingThreshold)
            return ReadingOutcome.EngagedReading;

        // Low engagement = skipped
        if (eventEngagement <= skippedReadingThreshold)
            return ReadingOutcome.Skipped;

        // Middle ground = skimmed
        return ReadingOutcome.Skimmed;
    }

    /// <summary>
    /// Update smoothed engagement using exponential moving average
    /// </summary>
    void UpdateSmoothedEngagement(float eventEngagement, ReadingEventData eventData)
    {
        // Adjust smoothing based on how different this event is from current state
        // Larger differences = slightly more responsive (but still smoothed)
        float difference = Mathf.Abs(eventEngagement - currentReadingEngagement);
        float adaptiveSmoothingFactor = smoothingFactor + (difference * 0.1f);
        adaptiveSmoothingFactor = Mathf.Clamp(adaptiveSmoothingFactor, 0.1f, 0.5f);

        // Apply exponential moving average
        // newValue = alpha * newSample + (1 - alpha) * oldValue
        currentReadingEngagement = adaptiveSmoothingFactor * eventEngagement +
                                   (1f - adaptiveSmoothingFactor) * currentReadingEngagement;

        // Clamp to valid range
        currentReadingEngagement = Mathf.Clamp01(currentReadingEngagement);

        if (showDetailedLogs)
        {
            Debug.Log($"[ReadingBehaviorTracker] EMA update: {eventEngagement:F2} → smoothed: {currentReadingEngagement:F2} (α={adaptiveSmoothingFactor:F2})");
        }
    }

    /// <summary>
    /// Apply time decay when no reading events occur
    /// Gradually returns to neutral baseline
    /// </summary>
    void ApplyTimeDecay()
    {
        float deltaTime = Time.time - lastUpdateTime;
        lastUpdateTime = Time.time;

        // Only decay if we haven't had a recent event
        float timeSinceLastEvent = Time.time - lastEventTime;
        if (timeSinceLastEvent < 5f) // Grace period of 5 seconds
            return;

        // Exponential decay toward neutral baseline
        float decayAmount = decayRate * deltaTime;

        if (currentReadingEngagement > neutralBaseline)
        {
            currentReadingEngagement = Mathf.Max(neutralBaseline, currentReadingEngagement - decayAmount);
        }
        else if (currentReadingEngagement < neutralBaseline)
        {
            currentReadingEngagement = Mathf.Min(neutralBaseline, currentReadingEngagement + decayAmount);
        }
    }

    // ============================================================================
    // STATISTICS & ANALYSIS
    // ============================================================================

    /// <summary>
    /// Get summary of reading behavior for research analysis
    /// </summary>
    public ReadingBehaviorSummary GetSummary()
    {
        float avgEngagement = readingHistory.Count > 0
            ? readingHistory.Average(e => e.calculatedEngagement)
            : neutralBaseline;

        float avgDuration = readingHistory.Count > 0
            ? (float)readingHistory.Average(e => e.eventData.viewDuration)
            : 0f;

        return new ReadingBehaviorSummary
        {
            currentSmoothedEngagement = currentReadingEngagement,
            totalEventsReceived = totalEventsReceived,
            engagedReadEvents = engagedReadEvents,
            skippedReadEvents = skippedReadEvents,
            averageEventEngagement = avgEngagement,
            averageViewDuration = avgDuration,
            engagementLevel = GetReadingEngagementLevel(),
            confidence = GetConfidence()
        };
    }

    /// <summary>
    /// Get recent reading events for detailed analysis
    /// </summary>
    public List<ReadingEvent> GetRecentEvents(int count = 10)
    {
        return readingHistory.TakeLast(Mathf.Min(count, readingHistory.Count)).ToList();
    }

    /// <summary>
    /// Calculate trend in reading engagement (positive = improving, negative = declining)
    /// </summary>
    public float GetEngagementTrend()
    {
        if (readingHistory.Count < 3)
            return 0f;

        var recentEvents = readingHistory.TakeLast(6).ToList();
        if (recentEvents.Count < 3)
            return 0f;

        int half = recentEvents.Count / 2;
        float firstHalfAvg = recentEvents.Take(half).Average(e => e.calculatedEngagement);
        float secondHalfAvg = recentEvents.Skip(half).Average(e => e.calculatedEngagement);

        return secondHalfAvg - firstHalfAvg;
    }

    // ============================================================================
    // FIREBASE LOGGING
    // ============================================================================

    async void LogReadingEventToFirebase(ReadingEvent readingEvent)
    {
        if (!FirebaseLogger.HasSession)
            return;

        try
        {
            var data = new Dictionary<string, object>
            {
                { "contentName", readingEvent.eventData.contentName },
                { "contentType", readingEvent.eventData.contentType },
                { "viewDuration", readingEvent.eventData.viewDuration },
                { "estimatedReadTime", readingEvent.eventData.estimatedReadTime },
                { "wordCount", readingEvent.eventData.wordCount },
                { "wasLikelyRead", readingEvent.eventData.wasLikelyRead },
                { "calculatedEngagement", readingEvent.calculatedEngagement },
                { "outcome", readingEvent.outcome.ToString() },
                { "smoothedEngagementAfter", currentReadingEngagement },
                { "confidence", GetConfidence() },
                { "eventNumber", totalEventsReceived }
            };

            string docId = FirebaseLogger.GenerateDocId($"reading_event_{totalEventsReceived}");
            await FirebaseLogger.LogSessionData("readingTime", data, docId, "[ReadingBehaviorTracker]");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ReadingBehaviorTracker] Firebase logging failed: {e.Message}");
        }
    }

    /// <summary>
    /// Log final summary to Firebase (call at end of session)
    /// </summary>
    public async void LogFinalSummaryToFirebase()
    {
        if (!FirebaseLogger.HasSession)
            return;

        try
        {
            var summary = GetSummary();
            var data = new Dictionary<string, object>
            {
                { "finalSmoothedEngagement", summary.currentSmoothedEngagement },
                { "totalEvents", summary.totalEventsReceived },
                { "engagedReadEvents", summary.engagedReadEvents },
                { "skippedReadEvents", summary.skippedReadEvents },
                { "averageEventEngagement", summary.averageEventEngagement },
                { "averageViewDuration", summary.averageViewDuration },
                { "engagementTrend", GetEngagementTrend() },
                { "finalConfidence", summary.confidence }
            };

            await FirebaseLogger.MergeSessionData("readingTime", data, "summary", "[ReadingBehaviorTracker]");

            if (showDebugLogs)
            {
                Debug.Log("📊 [ReadingBehaviorTracker] Final summary logged to Firebase");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ReadingBehaviorTracker] Failed to log summary: {e.Message}");
        }
    }

    // ============================================================================
    // RESET & UTILITY
    // ============================================================================

    /// <summary>
    /// Reset tracker state (for testing or new session)
    /// </summary>
    public void Reset()
    {
        currentReadingEngagement = neutralBaseline;
        readingHistory.Clear();
        totalEventsReceived = 0;
        engagedReadEvents = 0;
        skippedReadEvents = 0;
        lastEventTime = Time.time;

        if (showDebugLogs)
        {
            Debug.Log("[ReadingBehaviorTracker] State reset");
        }
    }
}

// ============================================================================
// DATA STRUCTURES
// ============================================================================

/// <summary>
/// Data passed when a content panel closes
/// </summary>
[System.Serializable]
public struct ReadingEventData
{
    public string contentName;
    public string contentType; // "Object" or "Card"
    public float viewDuration;
    public float estimatedReadTime;
    public int wordCount;
    public bool wasLikelyRead;
}

/// <summary>
/// Stored reading event with calculated metrics
/// </summary>
public class ReadingEvent
{
    public float timestamp;
    public ReadingEventData eventData;
    public float calculatedEngagement;
    public ReadingOutcome outcome;
}

/// <summary>
/// Classification of reading behavior for a single event
/// </summary>
public enum ReadingOutcome
{
    Skipped,        // Quick close, didn't read
    Skimmed,        // Partial reading
    EngagedReading  // Full or thorough reading
}

/// <summary>
/// Overall reading engagement classification
/// </summary>
public enum ReadingEngagementLevel
{
    Disengaged,
    Neutral,
    Engaged
}

/// <summary>
/// Summary statistics for research analysis
/// </summary>
public struct ReadingBehaviorSummary
{
    public float currentSmoothedEngagement;
    public int totalEventsReceived;
    public int engagedReadEvents;
    public int skippedReadEvents;
    public float averageEventEngagement;
    public float averageViewDuration;
    public ReadingEngagementLevel engagementLevel;
    public float confidence;
}