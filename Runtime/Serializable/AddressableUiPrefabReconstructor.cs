using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>Replaces scene-only UI placeholders with their Addressables instances at runtime.</summary>
public sealed class AddressableUiPrefabReconstructor : MonoBehaviour
{
    [Serializable]
    private sealed class RectTransformSnapshot
    {
        [SerializeField] private Vector2 _anchorMin;
        [SerializeField] private Vector2 _anchorMax;
        [SerializeField] private Vector2 _anchoredPosition;
        [SerializeField] private Vector2 _sizeDelta;
        [SerializeField] private Vector2 _pivot;
        [SerializeField] private Quaternion _localRotation = Quaternion.identity;
        [SerializeField] private Vector3 _localScale = Vector3.one;

        public void Capture(RectTransform source)
        {
            _anchorMin = source.anchorMin;
            _anchorMax = source.anchorMax;
            _anchoredPosition = source.anchoredPosition;
            _sizeDelta = source.sizeDelta;
            _pivot = source.pivot;
            _localRotation = source.localRotation;
            _localScale = source.localScale;
        }

        public void Apply(RectTransform target)
        {
            target.anchorMin = _anchorMin;
            target.anchorMax = _anchorMax;
            target.pivot = _pivot;
            target.sizeDelta = _sizeDelta;
            target.anchoredPosition = _anchoredPosition;
            target.localRotation = _localRotation;
            target.localScale = _localScale;
        }
    }

    [Serializable]
    private sealed class UiPrefabSlot
    {
        [SerializeField] private string _id;
        [SerializeField] private string _address;
        [SerializeField] private RectTransform _editorOnlyInstance;
        [SerializeField] private Transform _parent;
        [SerializeField] private int _siblingIndex;
        [SerializeField] private RectTransformSnapshot _transform = new();
        [SerializeField] private bool active = true;

        public RectTransform EditorOnlyInstance => _editorOnlyInstance;
        public string Id => _id;

        public void CaptureEditorPlacement()
        {
            if (_editorOnlyInstance == null)
            {
                return;
            }

            _parent ??= _editorOnlyInstance.parent;
            _siblingIndex = _editorOnlyInstance.GetSiblingIndex();
            _transform ??= new RectTransformSnapshot();
            _transform.Capture(_editorOnlyInstance);
        }

        public IEnumerator Instantiate(Action<GameObject, AsyncOperationHandle<GameObject>> onCreated, MonoBehaviour owner)
        {
            if (string.IsNullOrWhiteSpace(_address))
            {
                Debug.LogError("Addressable UI slot has no address.", owner);
                yield break;
            }

            AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(_address);
            yield return handle;

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                Debug.LogError($"Failed to instantiate Addressable UI prefab '{_address}'.", owner);
                Addressables.Release(handle);
                yield break;
            }

            GameObject instance = UnityEngine.Object.Instantiate(handle.Result, _parent, false);
            instance.name = handle.Result.name;
            instance.SetActive(false);
            RectTransform rectTransform = instance.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                _transform?.Apply(rectTransform);
                rectTransform.SetSiblingIndex(Mathf.Clamp(_siblingIndex, 0, _parent != null ? _parent.childCount - 1 : 0));
            }

            onCreated?.Invoke(instance, handle);
            instance.SetActive(active);
        }
    }

    [SerializeField] private List<UiPrefabSlot> _slots = new();
    
    private readonly Dictionary<string, GameObject> _runtimeInstances = new();
    private readonly List<AsyncOperationHandle<GameObject>> _runtimeAssetHandles = new();

    public bool IsComplete { get; private set; }

    public bool TryGetRuntimeInstances(out IReadOnlyDictionary<string, GameObject> runtimeInstances)
    {
        runtimeInstances = _runtimeInstances;
        return IsComplete;
    }

    private void OnValidate()
    {
        foreach (UiPrefabSlot slot in _slots)
        {
            slot?.CaptureEditorPlacement();
        }
    }

    private void Awake()
    {
        foreach (UiPrefabSlot slot in _slots)
        {
            if (slot?.EditorOnlyInstance != null)
            {
                slot.EditorOnlyInstance.gameObject.SetActive(false);
            }
        }
    }

    private void Start()
    {
        StartCoroutine(ReconstructUi());
    }

    private IEnumerator ReconstructUi()
    {
        foreach (UiPrefabSlot slot in _slots)
        {
            if (slot?.EditorOnlyInstance != null)
            {
                Destroy(slot.EditorOnlyInstance.gameObject);
            }
        }

        foreach (UiPrefabSlot slot in _slots)
        {
            if (slot != null)
            {
                yield return StartCoroutine(slot.Instantiate(
                    (instance, handle) => AssignRuntimeInstance(slot, instance, handle), this));
            }
        }

        IsComplete = true;
    }

    private void AssignRuntimeInstance(
        UiPrefabSlot slot,
        GameObject instance,
        AsyncOperationHandle<GameObject> handle)
    {
        _runtimeAssetHandles.Add(handle);
        if (!string.IsNullOrWhiteSpace(slot.Id))
        {
            _runtimeInstances[slot.Id] = instance;
        }
    }

    private void OnDestroy()
    {
        foreach (GameObject instance in _runtimeInstances.Values)
        {
            if (instance != null)
            {
                Destroy(instance);
            }
        }

        _runtimeInstances.Clear();
        foreach (AsyncOperationHandle<GameObject> handle in _runtimeAssetHandles)
        {
            Addressables.Release(handle);
        }

        _runtimeAssetHandles.Clear();
    }
}
