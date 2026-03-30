using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class VRKeyboard : MonoBehaviour, IPointerClickHandler
{
    private TMP_InputField inputField;
    private TouchScreenKeyboard keyboard;

    void Start()
    {
        inputField = GetComponent<TMP_InputField>();

        if (inputField == null)
        {
            Debug.LogError("[VRKeyboard] No TMP_InputField found on this object!");
        }
    }

    // Called when user clicks/points at the input field
    public void OnPointerClick(PointerEventData eventData)
    {
        OpenKeyboard();
    }

    public void OpenKeyboard()
    {
        if (inputField == null) return;

        if (TouchScreenKeyboard.isSupported)
        {
            Debug.Log("[VRKeyboard] Opening keyboard...");
            keyboard = TouchScreenKeyboard.Open(
                inputField.text,
                TouchScreenKeyboardType.Default,
                false,  // autocorrect
                false,  // multiline
                false   // secure
            );
        }
        else
        {
            Debug.LogWarning("[VRKeyboard] TouchScreenKeyboard not supported on this platform");
        }
    }

    void Update()
    {
        if (keyboard == null) return;

        // Update input field with keyboard text
        if (keyboard.status == TouchScreenKeyboard.Status.Visible ||
            keyboard.status == TouchScreenKeyboard.Status.Done)
        {
            inputField.text = keyboard.text;
        }

        // Clear reference when done
        if (keyboard.status == TouchScreenKeyboard.Status.Done ||
            keyboard.status == TouchScreenKeyboard.Status.Canceled)
        {
            Debug.Log($"[VRKeyboard] Keyboard closed. Final text: {inputField.text}");
            keyboard = null;
        }
    }
}