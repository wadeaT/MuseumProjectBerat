using UnityEngine;

public class VRInteractionMode : MonoBehaviour
{
    public static bool IsVRMode = false; // Set this to true for Quest build

    void Awake()
    {
        // Auto-detect VR
#if UNITY_ANDROID && !UNITY_EDITOR
        IsVRMode = UnityEngine.XR.XRSettings.isDeviceActive;
#endif
    }
}