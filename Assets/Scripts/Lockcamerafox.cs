using UnityEngine;

/// <summary>
/// Forces the camera to maintain a specific Field of View in VR
/// Attach this to Main Camera
/// </summary>
public class LockCameraFOV : MonoBehaviour
{
    [Header("FOV Settings")]
    [Tooltip("Desired Field of View (60-90 recommended for VR)")]
    [Range(60f, 120f)]
    public float targetFOV = 85f;

    [Tooltip("Apply FOV every frame (recommended for VR)")]
    public bool continuousUpdate = true;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("LockCameraFOV: No Camera component found!");
            enabled = false;
            return;
        }

        // Set FOV immediately
        SetFOV();
        Debug.Log($"[LockCameraFOV] Camera FOV locked to {targetFOV}");
    }

    void LateUpdate()
    {
        if (continuousUpdate && cam != null)
        {
            // Keep forcing FOV in case XR system tries to override
            if (Mathf.Abs(cam.fieldOfView - targetFOV) > 0.1f)
            {
                SetFOV();
                Debug.Log($"[LockCameraFOV] FOV reset to {targetFOV} (was {cam.fieldOfView})");
            }
        }
    }

    void SetFOV()
    {
        if (cam != null)
        {
            cam.fieldOfView = targetFOV;
        }
    }

    // Allow changing FOV at runtime from other scripts
    public void SetTargetFOV(float newFOV)
    {
        targetFOV = Mathf.Clamp(newFOV, 60f, 120f);
        SetFOV();
    }
}