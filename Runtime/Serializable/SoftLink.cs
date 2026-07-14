using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;

namespace XSystem
{
    public interface IAddressLink<T>
    {
        AsyncOperationHandle<T> LoadAssetAsync();
        T Asset { get; }
        AsyncOperationHandle OperationHandle { get; }
        bool IsValid();
        bool IsDone { get; }
        void ReleaseAsset();
    }

    [System.Serializable]
    public class AssetLink<T> : AssetReferenceT<T>, IAddressLink<T> where T : Object
    {
        public AssetLink(string guid) : base(guid) { }

        T IAddressLink<T>.Asset => (T)base.Asset;
    }

    [System.Serializable]
    [Preserve]
    public class SoftLink<T> : IAddressLink<T>, IEquatable<SoftLink<T>>
#if !UNITY_EDITOR
   , ISerializationCallbackReceiver where T : UnityEngine.Object
   {
       void ISerializationCallbackReceiver.OnAfterDeserialize() { }
       void ISerializationCallbackReceiver.OnBeforeSerialize()
       {
           SyncPath();
       }
#else
    where T : UnityEngine.Object
    {
#endif
        public string name;
        public string guid;
        public string path;

        public string SubObjectName => name;

        public AsyncOperationHandle OperationHandle { get; private set; }

        public T Asset { get; private set; }

        public SoftLink() { }

        public SoftLink(string guid)
        {
            this.guid = guid;
            SyncPath();
        }

        public SoftLink(string guid, string path)
        {
            this.guid = guid;
            this.path = path;
            SyncPath();
        }

        public bool IsValid() => OperationHandle.IsValid();

        public bool IsDone => OperationHandle.IsValid() && OperationHandle.IsDone;

        public void SyncPath()
        {
            name = System.IO.Path.GetFileNameWithoutExtension(path);
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(guid))
            {
                path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                EditorExtension.MakeAddressFromGUID(guid);
            }
#endif
        }

        [System.NonSerialized]
        public bool muteWarning = false;

        public AsyncOperationHandle<T> LoadAssetAsync()
        {
            try
            {
                if (OperationHandle.IsValid())
                    return OperationHandle.Convert<T>();
                var key = string.IsNullOrEmpty(guid) ? path : guid;
                if (string.IsNullOrEmpty(key))
                {
#if UNITY_EDITOR
                    Debug.LogWarning($"key is empty.");
#endif
                    return default;
                }
                var asyncOp = Addressables.LoadAssetAsync<T>(key);
                Action<AsyncOperationHandle<T>> onComplete = h =>
                {
                    if (h.IsValid() && h.Status != AsyncOperationStatus.Succeeded)
                    {
                        if (!muteWarning)
                        {
                            Debug.LogWarning($"key:{key}, {h.OperationException}");
                            muteWarning = true;
                        }
                    }
                    Asset = h.Result;
                };
                if (asyncOp.IsDone)
                    onComplete.Invoke(asyncOp);
                else
                    asyncOp.Completed += onComplete;

                OperationHandle = asyncOp;
                return asyncOp;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{ex.Message}: {name}, {path}");
                throw ex;
            }
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(Transform parent)
        {
            try
            {
                var key = !string.IsNullOrEmpty(path) ? path : guid;
                if (string.IsNullOrEmpty(key))
                {
#if UNITY_EDITOR
                    Debug.LogWarning($"key is empty.");
#endif
                    return default;
                }
                var asyncOp = Addressables.InstantiateAsync(key, parent);
                Action<AsyncOperationHandle<GameObject>> onComplete = h =>
                {
                    if (h.IsValid() && h.Status != AsyncOperationStatus.Succeeded)
                    {
                        if (!muteWarning)
                        {
                            Debug.LogWarning($"key:{key}, {h.OperationException}");
                            muteWarning = true;
                        }
                    }
                    var go = h.Result;
                    go.name = name;
                    Asset = go.GetComponent<T>();
                };
                if (asyncOp.IsDone)
                {
                    onComplete.Invoke(asyncOp);
                }
                else
                    asyncOp.Completed += onComplete;

                OperationHandle = asyncOp;
                return asyncOp;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{ex.Message}: {name}, {path}");
                throw ex;
            }
        }

        public void ReleaseInstance()
        {
            if (OperationHandle.IsValid())
            {
                Addressables.ReleaseInstance(OperationHandle);
                OperationHandle = default;
            }
            Asset = null;
        }

        public void ReleaseAsset()
        {
            if (OperationHandle.IsValid())
            {
                Addressables.Release(OperationHandle);
                OperationHandle = default;
            }
            Asset = null;
        }

        public bool Equals(SoftLink<T> other)
        {
            return guid == other.guid;
        }
    }

    public class ResourceAnchorAttribute : PropertyAttribute
    {
        private string[] _labels;
        private System.Type _type;
        private float _height;

        public System.Type type
        {
            get { return _type; }
            private set { _type = value; }
        }

        public float height => _height;
        public string[] labels => _labels;

        private int _cacheSize = 0;

        public ResourceAnchorAttribute(System.Type type, int cacheSize = 1, float height = 0f)
        {
            _type = type;
            _cacheSize = Mathf.Max(cacheSize, 0);
            _height = height;
        }

#if UNITY_EDITOR
        public void Clear()
        {
            _keyList.Clear();
            _objectDict.Clear();
        }

        private List<string> _keyList = new List<string>();
        private SortedList<string, Object> _objectDict = new SortedList<string, Object>();
        public T GetCachedObject<T>(string guid) where T : Object
        {
            Object val;
            if (_objectDict.TryGetValue(guid, out val))
            {
                if (val != null)
                    return val as T;
                _objectDict.Remove(guid);
            }
            while (_keyList.Count > _cacheSize)
            {
                var key = _keyList[0];
                _keyList.RemoveAt(0);
                _objectDict.Remove(key);
            }
            if (string.IsNullOrEmpty(guid))
            {
                return default(T);
            }
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            val = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            if (val == null)
            {
                Debug.LogErrorFormat("asset does not exist. guid:{0}, path:{1}", guid, path);
                return default(T);
            }
            _objectDict[guid] = val;
            _keyList.Add(guid);
            return val as T;
        }
#endif
    }

    public static class SerializationExt
    {
        public static string GetHierarchy(this GameObject go)
        {
            var sb = new System.Text.StringBuilder();
            var tm = go.transform;
            while (tm != null)
            {
                sb.Insert(0, tm.name);
                sb.Insert(0, '/');
                tm = tm.parent;
            }
            return sb.ToString();
        }

    }
}
