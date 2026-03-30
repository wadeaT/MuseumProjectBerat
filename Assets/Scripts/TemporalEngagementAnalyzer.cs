using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Temporal Engagement Analyzer - Tracks engagement patterns over time
/// Detects fatigue, engagement curves, and optimal learning windows
/// </summary>
public class TemporalEngagementAnalyzer : MonoBehaviour
{
    public static TemporalEngagementAnalyzer Instance { get; private set; }

    [Header("Tracking Settings")]
    [Tooltip("How often to sample engagement (seconds)")]
    public float sampleInterval = 5f;

    [Tooltip("Time window for trend analysis (seconds)")]
    public float trendWindowSize = 120f; // 2 minutes

    [Header("Pattern Detection")]
    [Tooltip("Engagement drop threshold for fatigue detection")]
    public float fatigueThreshold = 1.5f; // 1.5 level drop

    [Tooltip("Engagement rise threshold for interest spike")]
    public float interestSpikeThreshold = 1.5f; // 1.5 level rise

    [Tooltip("Minimum samples for pattern detection")]
    public int minSamplesForPattern = 10;

    [Header("Debug")]
    public bool showDebugLogs = true;
    public bool showDetailedLogs = false;

    // Tracking data
    private List<EngagementDataPoint> engagementTimeline = new List<EngagementDataPoint>();
    private float lastSampleTime = 0f;

    // Analysis results
    private EngagementTrend currentTrend = EngagementTrend.Stable;
    private float peakEngagementTime = 0f;
    private float lowestEngagementTime = 0f;
    private EngagementLevel peakEngagementLevel = EngagementLevel.Neutral;
    private EngagementLevel lowestEngagementLevel = EngagementLevel.Neutral;
    private bool fatigueDetected = false;
    private bool interestSpikeDetected = false;

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
        if (showDebugLogs)
        {
            Debug.Log("âœ… [TemporalEngagementAnalyzer] Initialized - Tracking engagement patterns over time");
        }
    }

    void Update()
    {
        // FIX: Track engagement for BOTH groups (needed for research comparison)
        // Only content adaptation checks condition, not data collection!

        // Sample at intervals
        if (Time.time - lastSampleTime >= sampleInterval)
        {
            SampleEngagement();
            lastSampleTime = Time.time;

            // Analyze patterns periodically (every 30 seconds)
            if (engagementTimeline.Count % 6 == 0 && engagementTimeline.Count > 0)
            {
                AnalyzeEngagementPatterns();
            }
        }
    }

    /// <summary>
    /// Sample current engagement state
    /// </summary>
    void SampleEngagement()
    {
        if (EngagementClassifier.Instance == null) return;

        EngagementLevel currentLevel = EngagementClassifier.Instance.GetEngagementLevel();
        int criteriaCount = EngagementClassifier.Instance.GetPhysicalCriteriaCount();

        // Create data point
        EngagementDataPoint dataPoint = new EngagementDataPoint
        {
            timestamp = Time.time,
            engagementLevel = currentLevel,
            engagementScore = (int)currentLevel,
            criteriaCount = criteriaCount
        };

        engagementTimeline.Add(dataPoint);

        // Track peaks and lows
        if (currentLevel > peakEngagementLevel)
        {
            peakEngagementLevel = currentLevel;
            peakEngagementTime = Time.time;
        }

        if (engagementTimeline.Count == 1 || currentLevel < lowestEngagementLevel)
        {
            lowestEngagementLevel = currentLevel;
            lowestEngagementTime = Time.time;
        }

        if (showDetailedLogs)
        {
            Debug.Log($"[TemporalEngagementAnalyzer] Sampled: {currentLevel} (Score: {(int)currentLevel}, Criteria: {criteriaCount}/4) at t={Time.time:F1}s");
        }
    }

    /// <summary>
    /// Analyze engagement patterns over time
    /// </summary>
    void AnalyzeEngagementPatterns()
    {
        if (engagementTimeline.Count < minSamplesForPattern) return;

        // Get recent samples within trend window
        float cutoffTime = Time.time - trendWindowSize;
        List<EngagementDataPoint> recentSamples = new List<EngagementDataPoint>();
        for (int i = 0; i < engagementTimeline.Count; i++)
        {
            if (engagementTimeline[i].timestamp > cutoffTime)
                recentSamples.Add(engagementTimeline[i]);
        }

        if (recentSamples.Count < minSamplesForPattern) return;

        // Split into early and late periods
        int halfPoint = recentSamples.Count / 2;

        // Calculate average engagement for each period
        float earlySum = 0f;
        for (int i = 0; i < halfPoint; i++)
            earlySum += recentSamples[i].engagementScore;
        float earlyAvg = earlySum / halfPoint;

        float lateSum = 0f;
        int lateCount = recentSamples.Count - halfPoint;
        for (int i = halfPoint; i < recentSamples.Count; i++)
            lateSum += recentSamples[i].engagementScore;
        float lateAvg = lateCount > 0 ? lateSum / lateCount : 0f;
        float changeMagnitude = lateAvg - earlyAvg;

        // Determine trend
        EngagementTrend newTrend;
        if (changeMagnitude < -fatigueThreshold)
        {
            newTrend = EngagementTrend.Declining;
            interestSpikeDetected = false;
            if (!fatigueDetected)
            {
                fatigueDetected = true;
                OnFatigueDetected(earlyAvg, lateAvg);
            }
        }
        else if (changeMagnitude > interestSpikeThreshold)
        {
            newTrend = EngagementTrend.Rising;
            fatigueDetected = false;
            if (!interestSpikeDetected)
            {
                interestSpikeDetected = true;
                OnInterestSpikeDetected(earlyAvg, lateAvg);
            }
        }
        else
        {
            newTrend = EngagementTrend.Stable;
            fatigueDetected = false;
            interestSpikeDetected = false;
        }

        // Log if trend changed
        if (newTrend != currentTrend)
        {
            currentTrend = newTrend;
            if (showDebugLogs)
            {
                Debug.Log($"[TemporalEngagementAnalyzer] Trend changed: <b>{currentTrend}</b> (Early: {earlyAvg:F1}, Late: {lateAvg:F1}, Change: {changeMagnitude:F1})");
            }
        }

        if (showDetailedLogs)
        {
            Debug.Log($"[TemporalEngagementAnalyzer] Analysis: {recentSamples.Count} samples, Trend: {currentTrend}, Change: {changeMagnitude:F2}");
        }
    }

    /// <summary>
    /// Called when fatigue is detected
    /// </summary>
    void OnFatigueDetected(float earlyAvg, float lateAvg)
    {
        if (showDebugLogs)
        {
            Debug.Log($"âš ï¸ [TemporalEngagementAnalyzer] FATIGUE DETECTED - Engagement declining ({earlyAvg:F1} â†’ {lateAvg:F1})");
            Debug.Log($"   â†’ Suggestion: Switch to shorter, simpler content");
        }

        // Log to Firebase
        LogPatternToFirebase("Fatigue", earlyAvg, lateAvg);
    }

    /// <summary>
    /// Called when interest spike is detected
    /// </summary>
    void OnInterestSpikeDetected(float earlyAvg, float lateAvg)
    {
        if (showDebugLogs)
        {
            Debug.Log($"ðŸ“ˆ [TemporalEngagementAnalyzer] INTEREST SPIKE - Engagement increasing ({earlyAvg:F1} â†’ {lateAvg:F1})");
            Debug.Log($"   â†’ Suggestion: Provide richer, more detailed content");
        }

        // Log to Firebase
        LogPatternToFirebase("InterestSpike", earlyAvg, lateAvg);
    }

    // ============================================================================
    // PUBLIC API
    // ============================================================================

    /// <summary>
    /// Get current engagement trend
    /// </summary>
    public EngagementTrend GetCurrentTrend()
    {
        return currentTrend;
    }

    /// <summary>
    /// Check if fatigue is currently detected
    /// </summary>
    public bool IsFatigueDetected()
    {
        return fatigueDetected;
    }

    /// <summary>
    /// Check if interest spike is currently detected
    /// </summary>
    public bool IsInterestSpikeDetected()
    {
        return interestSpikeDetected;
    }

    /// <summary>
    /// Get time of peak engagement
    /// </summary>
    public float GetPeakEngagementTime()
    {
        return peakEngagementTime;
    }

    /// <summary>
    /// Get peak engagement level achieved
    /// </summary>
    public EngagementLevel GetPeakEngagementLevel()
    {
        return peakEngagementLevel;
    }

    /// <summary>
    /// Get average engagement over entire session
    /// </summary>
    public float GetAverageEngagement()
    {
        if (engagementTimeline.Count == 0) return 0f;
        float sum = 0f;
        for (int i = 0; i < engagementTimeline.Count; i++)
            sum += engagementTimeline[i].engagementScore;
        return sum / engagementTimeline.Count;
    }

    /// <summary>
    /// Get engagement score at specific time point
    /// </summary>
    public float GetEngagementAtTime(float time)
    {
        if (engagementTimeline.Count == 0) return 0f;
        int closestIdx = 0;
        float closestDiff = Mathf.Abs(engagementTimeline[0].timestamp - time);
        for (int i = 1; i < engagementTimeline.Count; i++)
        {
            float diff = Mathf.Abs(engagementTimeline[i].timestamp - time);
            if (diff < closestDiff) { closestDiff = diff; closestIdx = i; }
        }
        return engagementTimeline[closestIdx].engagementScore;
    }

    /// <summary>
    /// Get engagement curve data (for visualization/analysis)
    /// </summary>
    public List<EngagementDataPoint> GetEngagementCurve()
    {
        return new List<EngagementDataPoint>(engagementTimeline);
    }

    /// <summary>
    /// Calculate engagement variance (how much it fluctuates)
    /// </summary>
    public float GetEngagementVariance()
    {
        if (engagementTimeline.Count < 2) return 0f;

        float meanSum = 0f;
        for (int i = 0; i < engagementTimeline.Count; i++)
            meanSum += engagementTimeline[i].engagementScore;
        float mean = meanSum / engagementTimeline.Count;

        float varianceSum = 0f;
        for (int i = 0; i < engagementTimeline.Count; i++)
        {
            float diff = engagementTimeline[i].engagementScore - mean;
            varianceSum += diff * diff;
        }
        return Mathf.Sqrt(varianceSum / engagementTimeline.Count);
    }

    /// <summary>
    /// Get optimal learning window (time when engagement was highest)
    /// </summary>
    public TimeWindow GetOptimalLearningWindow()
    {
        if (engagementTimeline.Count < minSamplesForPattern)
            return new TimeWindow { start = 0f, end = 0f, avgEngagement = 0f };

        // Find 2-minute window with highest average engagement
        float bestWindowStart = 0f;
        float bestWindowAvg = 0f;
        float windowSize = 120f; // 2 minutes

        for (int i = 0; i < engagementTimeline.Count; i++)
        {
            float windowStart = engagementTimeline[i].timestamp;
            float windowEnd = windowStart + windowSize;

            float windowSum = 0f;
            int windowCount = 0;
            for (int j = 0; j < engagementTimeline.Count; j++)
            {
                float ts = engagementTimeline[j].timestamp;
                if (ts >= windowStart && ts <= windowEnd)
                {
                    windowSum += engagementTimeline[j].engagementScore;
                    windowCount++;
                }
            }

            if (windowCount < 5) continue;

            float windowAvg = windowSum / windowCount;

            if (windowAvg > bestWindowAvg)
            {
                bestWindowAvg = windowAvg;
                bestWindowStart = windowStart;
            }
        }

        return new TimeWindow
        {
            start = bestWindowStart,
            end = bestWindowStart + windowSize,
            avgEngagement = bestWindowAvg
        };
    }

    /// <summary>
    /// Get comprehensive temporal summary
    /// </summary>
    public TemporalEngagementSummary GetTemporalSummary()
    {
        return new TemporalEngagementSummary
        {
            currentTrend = currentTrend,
            averageEngagement = GetAverageEngagement(),
            peakEngagement = peakEngagementLevel,
            peakEngagementTime = peakEngagementTime,
            lowestEngagement = lowestEngagementLevel,
            lowestEngagementTime = lowestEngagementTime,
            engagementVariance = GetEngagementVariance(),
            fatigueDetected = fatigueDetected,
            interestSpikeDetected = interestSpikeDetected,
            totalSamples = engagementTimeline.Count,
            sessionDuration = Time.time
        };
    }

    // ============================================================================
    // FIREBASE LOGGING
    // ============================================================================

    /// <summary>
    /// Log detected pattern to Firebase
    /// </summary>
    async void LogPatternToFirebase(string patternType, float earlyAvg, float lateAvg)
    {
        if (!FirebaseLogger.HasSession)
            return;

        try
        {
            var data = new Dictionary<string, object>
            {
                { "patternType", patternType },
                { "earlyAvgEngagement", earlyAvg },
                { "lateAvgEngagement", lateAvg },
                { "changeMagnitude", lateAvg - earlyAvg }
            };

            string docId = FirebaseLogger.GenerateDocId($"pattern_{patternType}");
            await FirebaseLogger.LogSessionData("temporalPatterns", data, docId, "[TemporalEngagementAnalyzer]");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TemporalEngagementAnalyzer] Failed to log pattern: {e.Message}");
        }
    }

    /// <summary>
    /// Log complete temporal summary to Firebase
    /// </summary>
    public async void LogTemporalSummaryToFirebase()
    {
        if (!FirebaseLogger.HasSession)
            return;

        try
        {
            var summary = GetTemporalSummary();
            var optimalWindow = GetOptimalLearningWindow();

            var data = new Dictionary<string, object>
            {
                { "averageEngagement", summary.averageEngagement },
                { "peakEngagement", summary.peakEngagement.ToString() },
                { "peakEngagementTime", summary.peakEngagementTime },
                { "lowestEngagement", summary.lowestEngagement.ToString() },
                { "engagementVariance", summary.engagementVariance },
                { "optimalWindowStart", optimalWindow.start },
                { "optimalWindowEnd", optimalWindow.end },
                { "optimalWindowAvgEngagement", optimalWindow.avgEngagement },
                { "fatigueDetected", summary.fatigueDetected },
                { "interestSpikeDetected", summary.interestSpikeDetected },
                { "totalSamples", summary.totalSamples },
                { "sessionDuration", summary.sessionDuration }
            };

            await FirebaseLogger.MergeSessionData("temporalAnalysis", data, "summary", "[TemporalEngagementAnalyzer]");

            if (showDebugLogs)
            {
                Debug.Log("[TemporalEngagementAnalyzer] Logged temporal summary to Firebase");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TemporalEngagementAnalyzer] Failed to log summary: {e.Message}");
        }
    }
}

// ============================================================================
// DATA STRUCTURES
// ============================================================================

public class EngagementDataPoint
{
    public float timestamp;
    public EngagementLevel engagementLevel;
    public int engagementScore;
    public int criteriaCount;
}

public enum EngagementTrend
{
    Declining,   // Engagement decreasing (fatigue)
    Stable,      // Engagement consistent
    Rising       // Engagement increasing (interest spike)
}

public struct TimeWindow
{
    public float start;
    public float end;
    public float avgEngagement;
}

public struct TemporalEngagementSummary
{
    public EngagementTrend currentTrend;
    public float averageEngagement;
    public EngagementLevel peakEngagement;
    public float peakEngagementTime;
    public EngagementLevel lowestEngagement;
    public float lowestEngagementTime;
    public float engagementVariance;
    public bool fatigueDetected;
    public bool interestSpikeDetected;
    public int totalSamples;
    public float sessionDuration;
}