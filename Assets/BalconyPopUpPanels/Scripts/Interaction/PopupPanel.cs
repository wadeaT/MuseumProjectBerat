using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PopupPanel : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Image photo;
    [SerializeField] private Button closeButton;

    private Camera cam;

    void Awake()
    {
        cam = Camera.main;
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        HideInstant();
    }

    void LateUpdate()
    {
        // Make panel face the camera
        if (cam != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }

    public void SetContent(string title, string body, Sprite sprite)
    {
        if (titleText) titleText.text = title;
        if (bodyText) bodyText.text = body;

        if (photo)
        {
            if (sprite != null)
            {
                photo.sprite = sprite;
                photo.gameObject.SetActive(true);
            }
            else
            {
                photo.gameObject.SetActive(false);
            }
        }
    }

    public void ShowAt(Vector3 worldPos)
    {
        transform.position = worldPos;
        Show();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    public void Hide()
    {
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void HideInstant()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }
}
