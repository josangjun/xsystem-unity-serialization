using System;
using TMPro;
using UnityEngine;
using XSystem.Internal;

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

    private const string SourceMaterialCacheSuffix = ".__LocalizedFontTextSource";

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

            TMP_FontAsset previousFont = text.font;
            Material previousBaseMaterial = text.fontSharedMaterial;
            Material[] previousMaterials = text.fontSharedMaterials;

            Material compatibleBaseMaterial = GetCompatibleMaterial(
                previousFont,
                font,
                previousBaseMaterial);

            if (previousMaterials != null)
            {
                for (int materialIndex = 0; materialIndex < previousMaterials.Length; materialIndex++)
                {
                    if (previousMaterials[materialIndex] == null ||
                        previousBaseMaterial != null &&
                        previousMaterials[materialIndex].GetInstanceID() == previousBaseMaterial.GetInstanceID())
                    {
                        continue;
                    }

                    GetCompatibleMaterial(previousFont, font, previousMaterials[materialIndex]);
                }
            }

            text.font = font;

            if (compatibleBaseMaterial != null)
            {
                text.fontSharedMaterial = compatibleBaseMaterial;
            }

            text.SetVerticesDirty();
        }
    }

    private static bool IsCustomMaterial(TMP_FontAsset font, Material material)
    {
        return font != null &&
               font.material != null &&
               material != null &&
               material.GetInstanceID() != font.material.GetInstanceID();
    }

    private static Material GetCompatibleMaterial(
        TMP_FontAsset previousFont,
        TMP_FontAsset targetFont,
        Material currentMaterial)
    {
        if (!IsCustomMaterial(previousFont, currentMaterial) ||
            targetFont == null ||
            targetFont.material == null)
        {
            return null;
        }

        string tagName = currentMaterial.name;
        if (string.IsNullOrEmpty(tagName))
        {
            return null;
        }

        Material sourceMaterial = GetSourceMaterial(tagName, currentMaterial);
        if (sourceMaterial == null)
        {
            return null;
        }

        // TMP_MaterialManager checks its own source-material/atlas cache before
        // creating a new material instance.
        Material compatibleMaterial = TMP_MaterialManager.GetFallbackMaterial(
            sourceMaterial,
            targetFont.material);
        if (compatibleMaterial == null)
        {
            return null;
        }

        // Keep the original material name so the existing <material=...> tag
        // continues to resolve after the font asset changes.
        compatibleMaterial.name = tagName;
        TextMeshProResourceLifecycle.RegisterFontMaterial(tagName, compatibleMaterial, true);
        return compatibleMaterial;
    }

    private static Material GetSourceMaterial(string tagName, Material currentMaterial)
    {
        string sourceMaterialKey = tagName + SourceMaterialCacheSuffix;
        int sourceMaterialHash = TMP_TextUtilities.GetHashCode(sourceMaterialKey);
        if (MaterialReferenceManager.TryGetMaterial(sourceMaterialHash, out Material cachedSource) &&
            cachedSource != null)
        {
            return cachedSource;
        }

        // Keep the original source in TMP's own material registry so a later
        // locale switch can reuse TMP_MaterialManager's existing fallback entry
        // instead of using the previously generated material as a new source.
        TextMeshProResourceLifecycle.RegisterFontMaterial(sourceMaterialKey, currentMaterial, false);
        return currentMaterial;
    }
}
