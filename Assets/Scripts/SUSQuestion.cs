using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SUSQuestion : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI questionText;
    public Transform optionsParent; // parent of all toggles, including default

    private List<Toggle> _toggles = new List<Toggle>();
    private ToggleGroup _toggleGroup;
    private Toggle _defaultToggle;

    void Awake()
    {
        if (optionsParent == null)
            optionsParent = transform.Find("Options");

        _toggles.Clear();

        if (optionsParent != null)
        {
            _toggleGroup = optionsParent.GetComponent<ToggleGroup>();

            foreach (var toggle in optionsParent.GetComponentsInChildren<Toggle>(true))
            {
                _toggles.Add(toggle);

                // link to toggle group
                if (_toggleGroup != null)
                    toggle.group = _toggleGroup;

                // detect default toggle by tag
                if (toggle.CompareTag("DefaultToggle"))
                    _defaultToggle = toggle;
            }
        }

        // Safety: ensure default toggle exists
        if (_defaultToggle == null)
            Debug.LogWarning($"{name}: No default toggle found! Add one with tag 'DefaultToggle'.");
    }

    void Start()
    {
        // Make sure default toggle is selected at the beginning
        if (_defaultToggle != null)
        {
            _defaultToggle.isOn = true;

            // Ensure all other toggles start OFF
            foreach (var t in _toggles)
                if (t != _defaultToggle)
                    t.isOn = false;
        }
    }

    // Called by external UI to fill the question text
    public void SetQuestionText(string text)
    {
        if (questionText != null)
            questionText.text = text;
    }

    /// <summary>
    /// Returns:
    ///   1-5 → if a valid real answer is selected
    ///   0   → if default is selected or no answer
    /// </summary>
    public int GetAnswer()
    {
        for (int i = 0; i < _toggles.Count; i++)
        {
            Toggle toggle = _toggles[i];

            if (toggle != null && toggle.isOn)
            {
                // If this is the default toggle → invalid
                if (toggle == _defaultToggle)
                    return 0;

                // Otherwise return answer (skip default)
                int answerIndex = _defaultToggle == null ? i : (i - 1);
                return answerIndex + 1; // return 1–5
            }
        }

        return 0; // no real answer selected
    }

    /// <summary>
    /// Returns true only if a real answer (1–5) is selected.
    /// </summary>
    public bool IsAnswered()
    {
        return GetAnswer() != 0;
    }
}
