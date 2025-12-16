using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class LanguageDropdownController : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;
    // We'll allow language families, not only exact codes.
    private readonly string[] allowedPrefixes = { "en", "sq" };
    private bool isInitializing;

    private void Awake()  // Changed from IEnumerator to void
    {
        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();

        // Optional: remove placeholder options in edit mode feel
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string> { "Loading..." });
        dropdown.RefreshShownValue();
    }

    private IEnumerator Start()
    {
        isInitializing = true;
        yield return LocalizationSettings.InitializationOperation;

        var locales = LocalizationSettings.AvailableLocales.Locales;
        var filtered = new List<Locale>();

        foreach (var loc in locales)
        {
            var code = loc.Identifier.Code; // e.g., "en", "en-US", "sq", "sq-AL"
            foreach (var prefix in allowedPrefixes)
            {
                if (code.StartsWith(prefix))
                {
                    filtered.Add(loc);
                    break;
                }
            }
        }

        // If filtered ended up empty (shouldn't), fallback to all
        if (filtered.Count == 0)
            filtered = new List<Locale>(locales);

        dropdown.ClearOptions();
        var options = new List<string>();
        int selectedIndex = 0;

        for (int i = 0; i < filtered.Count; i++)
        {
            var code = filtered[i].Identifier.Code;
            // Friendly display names
            string name =
                code.StartsWith("en") ? "English" :
                code.StartsWith("sq") ? "Shqip" :
                filtered[i].LocaleName;
            options.Add(name);

            if (LocalizationSettings.SelectedLocale == filtered[i])
                selectedIndex = i;
        }

        dropdown.AddOptions(options);
        dropdown.value = selectedIndex;
        dropdown.RefreshShownValue();

        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(index =>
        {
            if (isInitializing) return;
            StartCoroutine(SetLocale(filtered[index]));
        });

        isInitializing = false;
    }

    private IEnumerator SetLocale(Locale locale)
    {
        yield return LocalizationSettings.InitializationOperation;
        LocalizationSettings.SelectedLocale = locale;
    }
}