using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Keeps serialized references to descendant TMP text components and applies the
/// font for the current game language when the locale changes.
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalizedFontText : MonoBehaviour
{
    private static Func<string> _selectedLanguageCodeProvider;
    private static Action<Action<TMP_FontAsset>> _fontLoader;
    private static bool _isConfigured;

    [SerializeField]
    private TMP_Text[] _texts = Array.Empty<TMP_Text>();
    private bool _isListening;
    private int _fontRequestVersion;

    public static event Action LocaleChanged;

    public static void Configure(
        Func<string> selectedLanguageCodeProvider,
        Action<Action<TMP_FontAsset>> fontLoader)
    {
        _selectedLanguageCodeProvider = selectedLanguageCodeProvider;
        _fontLoader = fontLoader;

        if (_isConfigured == false)
        {
            _isConfigured = true;
            NotifyLocaleChanged();
        }
    }

    public static void NotifyLocaleChanged()
    {
        LocaleChanged?.Invoke();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _texts = GetComponentsInChildren<TMP_Text>(true);
    }
#endif

    private void OnEnable()
    {
        _isListening = true;
        LocaleChanged += HandleLocaleChanged;
        HandleLocaleChanged();
    }

    private void OnDisable()
    {
        _isListening = false;
        _fontRequestVersion++;
        LocaleChanged -= HandleLocaleChanged;
    }

    private void OnDestroy()
    {
        _isListening = false;
        _fontRequestVersion++;
        LocaleChanged -= HandleLocaleChanged;
    }

    public static void Bind(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        if (!text.TryGetComponent(out LocalizedFontText localizedFontText))
        {
            localizedFontText = text.gameObject.AddComponent<LocalizedFontText>();
        }

        localizedFontText._texts = new[] { text };
        localizedFontText.HandleLocaleChanged();
    }

    private void HandleLocaleChanged()
    {
        if (_selectedLanguageCodeProvider == null || _fontLoader == null)
        {
            return;
        }

        string languageCode = _selectedLanguageCodeProvider();
        int requestVersion = ++_fontRequestVersion;
        _fontLoader(font =>
        {
            if (this == null ||
                !_isListening ||
                requestVersion != _fontRequestVersion ||
                _selectedLanguageCodeProvider == null ||
                _selectedLanguageCodeProvider() != languageCode)
            {
                return;
            }

            ApplyFont(font);
        });
    }

    private void ApplyFont(TMP_FontAsset font)
    {
        if (this == null ||
            !_isListening ||
            !isActiveAndEnabled ||
            font == null)
        {
            return;
        }

        for (int index = 0; index < _texts.Length; index++)
        {
            TMP_Text text = _texts[index];
            if (text == null || text.font == font)
            {
                continue;
            }

            text.font = font;
            text.SetVerticesDirty();
        }
    }
}
