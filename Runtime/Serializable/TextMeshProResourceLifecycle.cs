using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using System.Linq;


#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace XSystem.Internal
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public static class TextMeshProResourceLifecycle
    {
        static readonly FieldInfo settingsFieldInfo;
        static readonly FieldInfo fontAssetLookupField = typeof(MaterialReferenceManager).GetField(
            "m_FontAssetReferenceLookup",
            BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo fontMaterialLookupField = typeof(MaterialReferenceManager).GetField(
            "m_FontMaterialReferenceLookup",
            BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo spriteAssetLookupField = typeof(MaterialReferenceManager).GetField(
            "m_SpriteAssetReferenceLookup",
            BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo colorGradientLookupField = typeof(MaterialReferenceManager).GetField(
            "m_ColorGradientReferenceLookup",
            BindingFlags.NonPublic | BindingFlags.Instance);

#if UNITY_EDITOR
        static AddressableLoadHandle _editorHandle;
        static bool _editorInitializationScheduled;
#endif

        public sealed class AddressableLoadHandle : IDisposable
        {
            readonly AsyncOperationHandle<IList<AsyncOperationHandle>> _groupHandle;
            readonly AsyncOperationHandle<TMP_Settings> _settingsHandle;
            readonly AsyncOperationHandle<IList<TMP_FontAsset>> _fontAssetsHandle;
            readonly AsyncOperationHandle<IList<Material>> _materialsHandle;
            readonly AsyncOperationHandle<IList<TMP_SpriteAsset>> _spriteAssetsHandle;
            readonly AsyncOperationHandle<IList<TMP_ColorGradient>> _colorGradientsHandle;
            readonly List<UnityEngine.Object> _registeredAssets = new();

            TMP_Settings _loadedSettings;
            bool _released;
            bool _configured;

            internal AddressableLoadHandle(
                AsyncOperationHandle<IList<AsyncOperationHandle>> groupHandle,
                AsyncOperationHandle<TMP_Settings> settingsHandle,
                AsyncOperationHandle<IList<TMP_FontAsset>> fontAssetsHandle,
                AsyncOperationHandle<IList<Material>> materialsHandle,
                AsyncOperationHandle<IList<TMP_SpriteAsset>> spriteAssetsHandle,
                AsyncOperationHandle<IList<TMP_ColorGradient>> colorGradientsHandle)
            {
                _groupHandle = groupHandle;
                _settingsHandle = settingsHandle;
                _fontAssetsHandle = fontAssetsHandle;
                _materialsHandle = materialsHandle;
                _spriteAssetsHandle = spriteAssetsHandle;
                _colorGradientsHandle = colorGradientsHandle;
            }

            public AsyncOperationHandle<IList<AsyncOperationHandle>> OperationHandle => _groupHandle;

            internal static AddressableLoadHandle CreateEditor(
                TMP_Settings loadedSettings,
                IList<TMP_FontAsset> fontAssets,
                IList<Material> materials,
                IList<TMP_SpriteAsset> spriteAssets,
                IList<TMP_ColorGradient> colorGradients)
            {
                AddressableLoadHandle result = new(
                    default,
                    default,
                    default,
                    default,
                    default,
                    default);
                result.ConfigureAssets(loadedSettings, fontAssets, materials, spriteAssets, colorGradients);
                return result;
            }

            internal void Configure()
            {
                if (_released || _configured || _groupHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    return;
                }

                ConfigureAssets(
                    _settingsHandle.Result,
                    _fontAssetsHandle.IsValid() ? _fontAssetsHandle.Result : null,
                    _materialsHandle.IsValid() ? _materialsHandle.Result : null,
                    _spriteAssetsHandle.IsValid() ? _spriteAssetsHandle.Result : null,
                    _colorGradientsHandle.IsValid() ? _colorGradientsHandle.Result : null);
            }

            void ConfigureAssets(
                TMP_Settings loadedSettings,
                IList<TMP_FontAsset> fontAssets,
                IList<Material> materials,
                IList<TMP_SpriteAsset> spriteAssets,
                IList<TMP_ColorGradient> colorGradients)
            {
                if (_released || _configured)
                {
                    return;
                }

                _configured = true;
                _loadedSettings = loadedSettings;
                if (_loadedSettings != null)
                {
                    settings = _loadedSettings;
                    RegisterSettingsAssets();
                }

                RegisterFontAssets(fontAssets);
                RegisterMaterials(materials, _groupHandle.IsValid() == false);
                RegisterSpriteAssets(spriteAssets);
                RegisterColorGradients(colorGradients);
            }

            internal void RefreshEditorAssets(
                IList<TMP_FontAsset> fontAssets,
                IList<Material> materials,
                IList<TMP_SpriteAsset> spriteAssets,
                IList<TMP_ColorGradient> colorGradients)
            {
                if (_released)
                {
                    return;
                }

                RegisterSettingsAssets();
                RegisterFontAssets(fontAssets);
                RegisterMaterials(materials, true);
                RegisterSpriteAssets(spriteAssets);
                RegisterColorGradients(colorGradients);
            }

            public void Release()
            {
                Release(false, false);
            }

            public void ReleaseForEditorPlayMode()
            {
                Release(true, true);
            }

            void Release(bool preserveSettings, bool preserveRegisteredAssets)
            {
                if (_released)
                {
                    return;
                }

                _released = true;
                if (preserveRegisteredAssets == false)
                {
                    UnregisterAssets();
                }

                if (settings == _loadedSettings)
                {
                    if (preserveSettings == false)
                    {
                        settings = null;
                    }
                }

                if (_groupHandle.IsValid())
                {
                    Addressables.Release(_groupHandle);
                }

#if UNITY_EDITOR
                if (ReferenceEquals(_editorHandle, this))
                {
                    _editorHandle = null;
                }
#endif
            }

            public void Dispose()
            {
                Release();
            }

            void RegisterSettingsAssets()
            {
                RegisterFontAsset(TMP_Settings.defaultFontAsset);
                if (TMP_Settings.fallbackFontAssets != null)
                {
                    foreach (TMP_FontAsset fontAsset in TMP_Settings.fallbackFontAssets)
                    {
                        RegisterFontAsset(fontAsset);
                    }
                }

                RegisterSpriteAsset(TMP_Settings.defaultSpriteAsset);
                if (TMP_Settings.emojiFallbackTextAssets == null)
                {
                    return;
                }

                foreach (TMP_Asset fallbackAsset in TMP_Settings.emojiFallbackTextAssets)
                {
                    if (fallbackAsset is TMP_FontAsset fontAsset)
                    {
                        RegisterFontAsset(fontAsset);
                    }
                    else if (fallbackAsset is TMP_SpriteAsset spriteAsset)
                    {
                        RegisterSpriteAsset(spriteAsset);
                    }
                }
            }

            void RegisterFontAssets(IList<TMP_FontAsset> fontAssets)
            {
                if (fontAssets == null)
                {
                    return;
                }

                foreach (TMP_FontAsset fontAsset in fontAssets)
                {
                    RegisterFontAsset(fontAsset);
                }
            }

            void RegisterFontAsset(TMP_FontAsset fontAsset)
            {
                if (fontAsset == null || _registeredAssets.Contains(fontAsset))
                {
                    return;
                }

                MaterialReferenceManager.AddFontAsset(fontAsset);
                _registeredAssets.Add(fontAsset);
                if (fontAsset.material != null)
                {
                    _registeredAssets.Add(fontAsset.material);
                }
            }

            void RegisterMaterials(IList<Material> materials, bool replaceExisting)
            {
                if (materials == null)
                {
                    return;
                }

                HashSet<int> registeredMaterialHashes = new();
                foreach (Material material in materials)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    int hashCode = TMP_TextUtilities.GetHashCode(material.name);
                    if (registeredMaterialHashes.Add(hashCode) == false)
                    {
                        continue;
                    }

                    if (MaterialReferenceManager.TryGetMaterial(hashCode, out Material existingMaterial) == false)
                    {
                        MaterialReferenceManager.AddFontMaterial(hashCode, material);
                    }
                    else if (replaceExisting && existingMaterial != material)
                    {
                        RemoveKey(fontMaterialLookupField, hashCode);
                        MaterialReferenceManager.AddFontMaterial(hashCode, material);
                    }

                    if (_registeredAssets.Contains(material) == false)
                    {
                        _registeredAssets.Add(material);
                    }
                }
            }

            void RegisterSpriteAssets(IList<TMP_SpriteAsset> spriteAssets)
            {
                if (spriteAssets == null)
                {
                    return;
                }

                foreach (TMP_SpriteAsset spriteAsset in spriteAssets)
                {
                    RegisterSpriteAsset(spriteAsset);
                }
            }

            void RegisterSpriteAsset(TMP_SpriteAsset spriteAsset)
            {
                if (spriteAsset == null || _registeredAssets.Contains(spriteAsset))
                {
                    return;
                }

                MaterialReferenceManager.AddSpriteAsset(spriteAsset);
                _registeredAssets.Add(spriteAsset);
                if (spriteAsset.material != null)
                {
                    _registeredAssets.Add(spriteAsset.material);
                }
            }

            void RegisterColorGradients(IList<TMP_ColorGradient> colorGradients)
            {
                if (colorGradients == null)
                {
                    return;
                }

                foreach (TMP_ColorGradient colorGradient in colorGradients)
                {
                    if (colorGradient == null || _registeredAssets.Contains(colorGradient))
                    {
                        continue;
                    }

                    int hashCode = TMP_TextUtilities.GetHashCode(colorGradient.name);
                    if (MaterialReferenceManager.TryGetColorGradientPreset(hashCode, out TMP_ColorGradient existingGradient) == false)
                    {
                        MaterialReferenceManager.AddColorGradientPreset(hashCode, colorGradient);
                    }

                    _registeredAssets.Add(colorGradient);
                }
            }

            void UnregisterAssets()
            {
                foreach (UnityEngine.Object asset in _registeredAssets)
                {
                    if (asset is TMP_FontAsset fontAsset)
                    {
                        TMP_ResourceManager.RemoveFontAsset(fontAsset);
                        RemoveKey(fontAssetLookupField, fontAsset.hashCode);
                        RemoveMatchingValue(fontMaterialLookupField, fontAsset.material);
                    }
                    else if (asset is TMP_SpriteAsset spriteAsset)
                    {
                        RemoveKey(spriteAssetLookupField, spriteAsset.hashCode);
                        RemoveMatchingValue(fontMaterialLookupField, spriteAsset.material);
                    }
                    else if (asset is TMP_ColorGradient colorGradient)
                    {
                        RemoveKey(colorGradientLookupField, TMP_TextUtilities.GetHashCode(colorGradient.name));
                    }
                    else if (asset is Material material)
                    {
                        RemoveMatchingValue(fontMaterialLookupField, material);
                    }
                }

                _registeredAssets.Clear();
            }

            static void RemoveKey(FieldInfo lookupField, object key)
            {
                if (lookupField?.GetValue(MaterialReferenceManager.instance) is IDictionary lookup)
                {
                    lookup.Remove(key);
                }
            }

            static void RemoveMatchingValue(FieldInfo lookupField, UnityEngine.Object value)
            {
                if (value == null || lookupField?.GetValue(MaterialReferenceManager.instance) is not IDictionary lookup)
                {
                    return;
                }

                List<object> keysToRemove = new();
                foreach (DictionaryEntry entry in lookup)
                {
                    if (entry.Value is UnityEngine.Object existingValue && existingValue == value)
                    {
                        keysToRemove.Add(entry.Key);
                    }
                }

                foreach (object key in keysToRemove)
                {
                    lookup.Remove(key);
                }
            }
        }

        public static TMP_Settings settings
        {
            get => (TMP_Settings)settingsFieldInfo.GetValue(null);
            set => settingsFieldInfo.SetValue(null, value);
        }

#if XSYS_ADDRESSABLE_TMPRO
        static TextMeshProResourceLifecycle()
        {
            settingsFieldInfo = typeof(TMP_Settings).GetField("s_Instance", BindingFlags.NonPublic | BindingFlags.Static);
            Debug.Assert(settingsFieldInfo != null, nameof(settingsFieldInfo) + " != null");

#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                InitializeEditor();
            }
#endif
        }
#endif

        private static AddressableLoadHandle _textMeshProHandle;
        
        public static void ReleaseSettings()
        {
            if (_textMeshProHandle == null)
                return;
                
            #if UNITY_EDITOR
            _textMeshProHandle.ReleaseForEditorPlayMode();
            #else
            _textMeshProHandle.Release();
            #endif
            _textMeshProHandle = default;
        }
        
        public static AddressableLoadHandle InitializeAddressables(
            string settingsAddress,
            string fontAssetsLabel,
            string materialsLabel,
            string spriteAssetsLabel = null,
            string colorGradientsLabel = null)
        {
#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                return InitializeAddressablesInEditor(
                    settingsAddress,
                    fontAssetsLabel,
                    materialsLabel,
                    spriteAssetsLabel,
                    colorGradientsLabel);
            }
#endif

            AsyncOperationHandle<TMP_Settings> settingsHandle = Addressables.LoadAssetAsync<TMP_Settings>((object)settingsAddress);
            AsyncOperationHandle<IList<TMP_FontAsset>> fontAssetsHandle = LoadAssetsByLabel<TMP_FontAsset>(fontAssetsLabel);
            AsyncOperationHandle<IList<Material>> materialsHandle = LoadAssetsByLabel<Material>(materialsLabel);
            AsyncOperationHandle<IList<TMP_SpriteAsset>> spriteAssetsHandle = LoadAssetsByLabel<TMP_SpriteAsset>(spriteAssetsLabel);
            AsyncOperationHandle<IList<TMP_ColorGradient>> colorGradientsHandle = LoadAssetsByLabel<TMP_ColorGradient>(colorGradientsLabel);

            List<AsyncOperationHandle> operations = new() { settingsHandle };
            AddOperationIfValid(operations, fontAssetsHandle);
            AddOperationIfValid(operations, materialsHandle);
            AddOperationIfValid(operations, spriteAssetsHandle);
            AddOperationIfValid(operations, colorGradientsHandle);

            AsyncOperationHandle<IList<AsyncOperationHandle>> groupHandle =
                Addressables.ResourceManager.CreateGenericGroupOperation(operations);
            AddressableLoadHandle result = new(
                groupHandle,
                settingsHandle,
                fontAssetsHandle,
                materialsHandle,
                spriteAssetsHandle,
                colorGradientsHandle);
            groupHandle.Completed += handle =>
            {
                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogException(handle.OperationException);
                    return;
                }

                result.Configure();
            };
            ReleaseSettings();
            _textMeshProHandle = result;
            return result;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void InitializeEditorOnLoad()
        {
            #if XSYS_ADDRESSABLE_TMPRO
            EditorApplication.projectChanged -= OnEditorProjectChanged;
            EditorApplication.projectChanged += OnEditorProjectChanged;
            EditorApplication.playModeStateChanged -= OnEditorPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseEditorHandle;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseEditorHandle;
            EditorApplication.quitting -= ReleaseEditorHandle;
            EditorApplication.quitting += ReleaseEditorHandle;

            if (_editorHandle == null)
            {
                InitializeEditor();
                if (_editorHandle == null)
                {
                    ScheduleEditorInitialization();
                }
            }
            #endif
        }

        static void ScheduleEditorInitialization()
        {
            if (_editorInitializationScheduled)
            {
                return;
            }

            _editorInitializationScheduled = true;
            EditorApplication.delayCall += BeginEditorInitialization;
        }

        static void BeginEditorInitialization()
        {
            _editorInitializationScheduled = false;
            EditorApplication.update -= TryInitializeEditor;
            EditorApplication.update += TryInitializeEditor;
        }

        static void TryInitializeEditor()
        {
            if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            if (_editorHandle != null)
            {
                EditorApplication.update -= TryInitializeEditor;
                return;
            }

            TMP_Settings loadedSettings = FindEditorSettings();
            if (loadedSettings == null)
            {
                return;
            }

            EditorApplication.update -= TryInitializeEditor;
            InitializeEditor();
        }

        static void OnEditorProjectChanged()
        {
            if (_editorHandle != null)
            {
                RefreshEditorAssets();
                ScheduleEditorAssetRefresh();
                return;
            }

            ScheduleEditorInitialization();
        }

        static void OnEditorPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (_editorHandle == null)
                {
                    ScheduleEditorInitialization();
                }
                else
                {
                    ScheduleEditorStateRestore();
                }
            }
        }

        static void ScheduleEditorStateRestore()
        {
            EditorApplication.delayCall -= RestoreEditorState;
            EditorApplication.delayCall += RestoreEditorState;
        }

        static void RestoreEditorState()
        {
            if (Application.isPlaying || _editorHandle == null)
            {
                return;
            }

            TMP_Settings loadedSettings = FindEditorSettings();
            if (loadedSettings == null)
            {
                ScheduleEditorInitialization();
                return;
            }

            settings = loadedSettings;
            RefreshEditorAssets();
            ScheduleEditorTextRefresh();
        }

        static void ReleaseEditorHandle()
        {
            EditorApplication.update -= TryInitializeEditor;
            _editorHandle?.Release();
            _editorHandle = null;
        }

        static void RefreshEditorAssets()
        {
            if (Application.isPlaying || _editorHandle == null)
            {
                return;
            }

            _editorHandle.RefreshEditorAssets(
                LoadEditorResources<TMP_FontAsset>(),
                LoadEditorMaterials(),
                LoadEditorResources<TMP_SpriteAsset>(),
                LoadEditorResources<TMP_ColorGradient>());
        }

        internal static void ScheduleEditorAssetRefresh()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (_editorHandle == null)
            {
                ScheduleEditorInitialization();
                return;
            }

            EditorApplication.delayCall -= RefreshEditorAssets;
            EditorApplication.delayCall += RefreshEditorAssets;
            ScheduleEditorTextRefresh();
        }

        static void ScheduleEditorTextRefresh()
        {
            EditorApplication.delayCall -= RefreshEditorTextObjects;
            EditorApplication.delayCall += RefreshEditorTextObjects;
        }

        static void RefreshEditorTextObjects()
        {
            if (Application.isPlaying || _editorHandle == null)
            {
                return;
            }

            foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (text == null || EditorUtility.IsPersistent(text) || text.font == null)
                {
                    continue;
                }

                text.ForceMeshUpdate(true, true);
            }
        }

        static TMP_Settings FindEditorSettings()
        {
            TMP_Settings resourceSettings = Resources.Load<TMP_Settings>("TMP Settings");
            if (resourceSettings != null)
            {
                return resourceSettings;
            }

#if XSYS_ADDRESSABLE_TMPRO
            List<TMP_Settings> addressableSettings = LoadAllEditorAddressableAssets<TMP_Settings>();
            if (addressableSettings.Count > 0)
            {
                return addressableSettings[0];
            }
#endif

            return null;
        }

        static List<Material> LoadEditorMaterials()
        {
            List<Material> materials = new();
            
#if XSYS_ADDRESSABLE_TMPRO
            foreach (Material material in LoadAllEditorAddressableAssets<Material>())
            {
                AddTextMeshProMaterial(materials, material);
            }

            var materialPaths = AssetDatabase.FindAssets("t:Material").Select(AssetDatabase.GUIDToAssetPath);
            foreach (string path in materialPaths)
            {
                if (path.IndexOf("SDF", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                AddTextMeshProMaterial(
                    materials,
                    AssetDatabase.LoadAssetAtPath<Material>(path));
            }

            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                AddTextMeshProMaterialsAtPath(
                    materials,
                    AssetDatabase.GUIDToAssetPath(guid));
            }
#endif

            return materials;
        }

#if XSYS_ADDRESSABLE_TMPRO
        static void AddTextMeshProMaterialsAtPath(List<Material> materials, string assetPath)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is Material material)
                {
                    AddTextMeshProMaterial(materials, material);
                }
            }
        }
#endif

        static void AddTextMeshProMaterial(List<Material> materials, Material material)
        {
            if (material != null && material.shader != null &&
                material.shader.name.IndexOf("TextMeshPro", StringComparison.OrdinalIgnoreCase) >= 0 &&
                materials.Contains(material) == false)
            {
                materials.Add(material);
            }
        }

#if XSYS_ADDRESSABLE_TMPRO
        static List<T> LoadAllEditorAddressableAssets<T>() where T : UnityEngine.Object
        {
            List<T> assets = new();
            AddressableAssetSettings addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (addressableSettings == null)
            {
                return assets;
            }

            foreach (AddressableAssetGroup group in addressableSettings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (AddressableAssetEntry entry in group.entries)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                    foreach (UnityEngine.Object loadedAsset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                    {
                        if (loadedAsset is T asset && assets.Contains(asset) == false)
                        {
                            assets.Add(asset);
                        }
                    }
                }
            }

            return assets;
        }
#endif

        public static AddressableLoadHandle InitializeEditor()
        {
            if (Application.isPlaying)
            {
                return null;
            }

            ReleaseEditorHandle();
            TMP_Settings loadedSettings = FindEditorSettings();
            if (loadedSettings == null)
            {
                return null;
            }

            _editorHandle = AddressableLoadHandle.CreateEditor(
                loadedSettings,
                LoadEditorResources<TMP_FontAsset>(),
                LoadEditorMaterials(),
                LoadEditorResources<TMP_SpriteAsset>(),
                LoadEditorResources<TMP_ColorGradient>());
            ScheduleEditorTextRefresh();
            return _editorHandle;
        }

        static List<T> LoadEditorResources<T>() where T : UnityEngine.Object
        {
            List<T> assets = new();
            foreach (T asset in Resources.LoadAll<T>(string.Empty))
            {
                if (asset != null && assets.Contains(asset) == false)
                {
                    assets.Add(asset);
                }
            }

#if XSYS_ADDRESSABLE_TMPRO
            foreach (T asset in LoadAllEditorAddressableAssets<T>())
            {
                if (asset != null && assets.Contains(asset) == false)
                {
                    assets.Add(asset);
                }
            }
#endif

            return assets;
        }

        static AddressableLoadHandle InitializeAddressablesInEditor(
            string settingsAddress,
            string fontAssetsLabel,
            string materialsLabel,
            string spriteAssetsLabel,
            string colorGradientsLabel)
        {
            ReleaseEditorHandle();
            TMP_Settings loadedSettings = LoadEditorAddressableAsset<TMP_Settings>(settingsAddress);
            if (loadedSettings == null)
            {
                Debug.LogError($"Failed to load TMP_Settings addressable '{settingsAddress}' in the Unity Editor.");
            }

            AddressableLoadHandle result = AddressableLoadHandle.CreateEditor(
                loadedSettings,
                LoadEditorAddressableAssets<TMP_FontAsset>(fontAssetsLabel),
                LoadEditorAddressableAssets<Material>(materialsLabel),
                LoadEditorAddressableAssets<TMP_SpriteAsset>(spriteAssetsLabel),
                LoadEditorAddressableAssets<TMP_ColorGradient>(colorGradientsLabel));
            _editorHandle = result;
            return result;
        }

        static T LoadEditorAddressableAsset<T>(string key) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            T asset = AssetDatabase.LoadAssetAtPath<T>(key);
            if (asset != null)
            {
                return asset;
            }

            AddressableAssetSettings addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (addressableSettings == null)
            {
                return null;
            }

            foreach (AddressableAssetGroup group in addressableSettings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (entry.address != key && entry.guid != key)
                    {
                        continue;
                    }

                    string assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                    return AssetDatabase.LoadAssetAtPath<T>(assetPath);
                }
            }

            return null;
        }

        static List<T> LoadEditorAddressableAssets<T>(string label) where T : UnityEngine.Object
        {
            List<T> assets = new();
            if (string.IsNullOrWhiteSpace(label))
            {
                return assets;
            }

            AddressableAssetSettings addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (addressableSettings == null)
            {
                return assets;
            }

            foreach (AddressableAssetGroup group in addressableSettings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (entry.labels.Contains(label) == false)
                    {
                        continue;
                    }

                    string assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                    foreach (UnityEngine.Object loadedAsset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                    {
                        if (loadedAsset is T asset && assets.Contains(asset) == false)
                        {
                            assets.Add(asset);
                        }
                    }
                }
            }

            return assets;
        }
#endif

        static AsyncOperationHandle<IList<T>> LoadAssetsByLabel<T>(string label) where T : UnityEngine.Object
        {
            AsyncOperationHandle<IList<T>> loadHandle = default;
            try
            {
                if (string.IsNullOrWhiteSpace(label))
                {
                    return default;
                }

                AsyncOperationHandle<IList<IResourceLocation>> locationsHandle =
                    Addressables.LoadResourceLocationsAsync((object)label, typeof(T));
                loadHandle = Addressables.ResourceManager.CreateChainOperation<IList<T>, IList<IResourceLocation>>(
                    locationsHandle,
                    locationsOperation =>
                    {
                        if (locationsOperation.Status != AsyncOperationStatus.Succeeded ||
                            locationsOperation.Result == null ||
                            locationsOperation.Result.Count == 0)
                        {
                            return Addressables.ResourceManager.CreateCompletedOperation<IList<T>>(
                                new List<T>(),
                                null);
                        }

                        return Addressables.LoadAssetsAsync<T>(locationsOperation.Result, null);
                    });
                return loadHandle;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load assets by label '{label}': {ex}");
                if (loadHandle.IsValid())
                {
                    Addressables.Release(loadHandle);
                }
                return default;
            }
        }

        static void AddOperationIfValid(List<AsyncOperationHandle> operations, AsyncOperationHandle operation)
        {
            if (operation.IsValid())
            {
                operations.Add(operation);
            }
        }
    }

#if UNITY_EDITOR
    sealed class TextMeshProAssetPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsTextMeshProAsset(importedAssets) ||
                ContainsTextMeshProAsset(deletedAssets) ||
                ContainsTextMeshProAsset(movedAssets) ||
                ContainsTextMeshProAsset(movedFromAssetPaths))
            {
                TextMeshProResourceLifecycle.ScheduleEditorAssetRefresh();
            }
        }

        static bool ContainsTextMeshProAsset(string[] assetPaths)
        {
            if (assetPaths == null)
            {
                return false;
            }

            foreach (string assetPath in assetPaths)
            {
                if (assetPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) ||
                    assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
#endif
}
