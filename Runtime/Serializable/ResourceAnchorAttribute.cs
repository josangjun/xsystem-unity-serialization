using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XSystem
{
    public class ResourceAnchorAttribute : PropertyAttribute
    {
        private readonly string[] _labels;
        private readonly System.Type _type;
        private readonly float _height;
        private readonly int _cacheSize;

        public System.Type type => _type;

        public float height => _height;

        public string[] labels => _labels;

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

        private readonly List<string> _keyList = new List<string>();
        private readonly SortedList<string, Object> _objectDict = new SortedList<string, Object>();

        public T GetCachedObject<T>(string guid) where T : Object
        {
            if (_objectDict.TryGetValue(guid, out var value))
            {
                if (value != null)
                    return value as T;

                _objectDict.Remove(guid);
            }

            while (_keyList.Count > _cacheSize)
            {
                var key = _keyList[0];
                _keyList.RemoveAt(0);
                _objectDict.Remove(key);
            }

            if (string.IsNullOrEmpty(guid))
                return default;

            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            value = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            if (value == null)
            {
                Debug.LogErrorFormat("asset does not exist. guid:{0}, path:{1}", guid, path);
                return default;
            }

            _objectDict[guid] = value;
            _keyList.Add(guid);
            return value as T;
        }
#endif
    }
}
