#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEditor.AddressableAssets;
using Object = UnityEngine.Object;

namespace XSystem
{
    [CustomPropertyDrawer(typeof(AssetLink<>), true)]
    public class AssetLinkDrawer : PropertyDrawer
    {
        protected SerializedProperty assetGuidProp;
        protected SerializedProperty subObjectNameProp;
        protected SerializedProperty subObjectTypeProp;

        private ResourceAnchorAttribute _anchorAttribute;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var type = GetFieldType();
            if (_anchorAttribute != null && _anchorAttribute.height > 0f)
                return _anchorAttribute.height;

            if (type != null && type.IsSubclassOf(typeof(Texture)))
                return 48f;

            return base.GetPropertyHeight(property, label);
        }

        protected virtual Type GetFieldType()
        {
            _anchorAttribute = attribute as ResourceAnchorAttribute;
            if (_anchorAttribute != null)
                return _anchorAttribute.type;

            return GetAssetType(fieldInfo?.FieldType) ?? typeof(Object);
        }

        private static Type GetAssetType(Type fieldType)
        {
            if (fieldType == null)
                return null;

            if (fieldType.IsArray)
                fieldType = fieldType.GetElementType();
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
                fieldType = fieldType.GetGenericArguments()[0];

            while (fieldType != null)
            {
                if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(AssetLink<>))
                    return fieldType.GetGenericArguments()[0];

                fieldType = fieldType.BaseType;
            }

            return null;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Generic)
            {
                base.OnGUI(position, property, label);
                return;
            }

            assetGuidProp = property.FindPropertyRelative("m_AssetGUID");
            subObjectNameProp = property.FindPropertyRelative("m_SubObjectName");
            subObjectTypeProp = property.FindPropertyRelative("m_SubObjectType");

            if (assetGuidProp == null)
            {
                base.OnGUI(position, property, label);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            DrawProperty(position, property, label);
            property.serializedObject.ApplyModifiedProperties();
            EditorGUI.EndProperty();
        }

        protected virtual void DrawProperty(Rect position, SerializedProperty property, GUIContent label)
        {
            var fieldType = GetFieldType();
            var asset = LoadEditorAsset(fieldType, assetGuidProp.stringValue);

            EditorGUI.BeginChangeCheck();
            var selected = EditorGUI.ObjectField(position, label, asset, fieldType, false);
            if (!EditorGUI.EndChangeCheck())
                return;

            if (selected == null)
            {
                ClearReference();
                return;
            }

            var path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"AssetLink only accepts project assets. Field: {property.displayName}, Object: {selected.name}");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogWarning($"AssetLink could not resolve a GUID. Field: {property.displayName}, Path: {path}");
                return;
            }

            assetGuidProp.stringValue = guid;
            if (subObjectNameProp != null)
                subObjectNameProp.stringValue = AssetDatabase.IsSubAsset(selected) ? selected.name : string.Empty;
            if (subObjectTypeProp != null)
                subObjectTypeProp.stringValue = AssetDatabase.IsSubAsset(selected) ? selected.GetType().AssemblyQualifiedName : string.Empty;

            EnsureAddressable(guid);
        }

        protected string GetAssetPath()
        {
            return assetGuidProp == null
                ? string.Empty
                : AssetDatabase.GUIDToAssetPath(assetGuidProp.stringValue);
        }

        private static Object LoadEditorAsset(Type fieldType, string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return null;

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return null;

            return AssetDatabase.LoadAssetAtPath(path, fieldType ?? typeof(Object));
        }

        private void ClearReference()
        {
            assetGuidProp.stringValue = string.Empty;
            if (subObjectNameProp != null)
                subObjectNameProp.stringValue = string.Empty;
            if (subObjectTypeProp != null)
                subObjectTypeProp.stringValue = string.Empty;
        }

        private static void EnsureAddressable(string guid)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || settings.FindAssetEntry(guid) != null)
                return;

            settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
        }
    }
}
#endif
