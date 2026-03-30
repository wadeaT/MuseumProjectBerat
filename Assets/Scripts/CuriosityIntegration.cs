using UnityEngine;

/// <summary>
/// Add this component to InteractiveObject to integrate with CuriosityTracker
/// Automatically tracks object views for curiosity detection
/// </summary>
[RequireComponent(typeof(InteractiveObject))]
public class CuriosityIntegration : MonoBehaviour
{
    private InteractiveObject interactiveObject;

    void Start()
    {
        interactiveObject = GetComponent<InteractiveObject>();
    }

    /// <summary>
    /// Call this when object is examined
    /// Add this to InteractiveObject.ExamineObjectAsync() after line:
    /// string localizedTitle = titleTask.Result;
    /// 
    /// CuriosityIntegration curiosity = GetComponent<CuriosityIntegration>();
    /// if (curiosity != null) curiosity.OnObjectExamined();
    /// </summary>
    public async void OnObjectExamined()
    {
        if (CuriosityTracker.Instance == null) return;

        // Get localized name
        string objectName = gameObject.name;
        if (interactiveObject != null && interactiveObject.objectTitle != null)
        {
            var titleTask = interactiveObject.objectTitle.GetLocalizedStringAsync();
            await titleTask.Task;
            objectName = titleTask.Result;
        }

        // Track in curiosity system
        CuriosityTracker.Instance.OnObjectViewed(gameObject.name, objectName);
    }

    /// <summary>
    /// Check if this object has sparked curiosity
    /// </summary>
    public bool IsCuriousObject()
    {
        if (CuriosityTracker.Instance == null) return false;
        return CuriosityTracker.Instance.IsObjectOfCuriosity(gameObject.name);
    }
}
