#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEditor.AddressableAssets;

namespace XSystem
{
    using System.Collections.Generic;
    using System.Linq;

    [CustomPropertyDrawer(typeof(SoftLink<>), true)]
    public class SoftLinkDrawer : PropertyDrawer
    {
        protected SerializedProperty nameProp;
        protected SerializedProperty pathProp;
        protected SerializedProperty guidProp;

        private ResourceAnchorAttribute anchorAttrb;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (anchorAttrb != null && anchorAttrb.height > 0f)
                return anchorAttrb.height;
            var type = GetFieldType();
            if (type.IsSubclassOf(typeof(Texture)))
                return 48f;
            return base.GetPropertyHeight(property, label);
        }

        protected virtual System.Type GetFieldType()
        {
            if (anchorAttrb != null)
                return anchorAttrb.type;
            if (fieldInfo == null)
                return typeof(Object);
            var type = fieldInfo.FieldType;
            if (type.IsArray)
            {
                return type.GetElementType().GenericTypeArguments[0];
            }
            if (type.GenericTypeArguments.Length > 0 &&
                 type.GenericTypeArguments[0].GenericTypeArguments.Length > 0 &&
                 typeof(List<>) == type.GetGenericTypeDefinition())
            {
                var elementType = type.GenericTypeArguments[0];
                var resourceType = elementType.GenericTypeArguments[0];
                return resourceType;
            }
            return type.GenericTypeArguments.Length > 0 ? type.GenericTypeArguments[0] : typeof(Object);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            anchorAttrb = attribute as ResourceAnchorAttribute;

            if (property.propertyType != SerializedPropertyType.Generic &&
                property.propertyType != SerializedPropertyType.ObjectReference)
            {
                base.OnGUI(position, property, label);
                return;
            }

            nameProp = property.FindPropertyRelative("name");
            pathProp = property.FindPropertyRelative("path");
            guidProp = property.FindPropertyRelative("guid");

            if (pathProp == null || guidProp == null || nameProp == null)
            {
                base.OnGUI(position, property, label);
                return;
            }
            EditorGUI.BeginProperty(position, label, property);
            //Debug.LogFormat("filedName:{0}, target:{1}, time:{2}",
            //    property.displayName, property.serializedObject.targetObject.GetType(), Time.time);

            DrawProperty(position, property, label);

            property.serializedObject.ApplyModifiedProperties();
            EditorGUI.EndProperty();
        }

        public T GetCachedObject<T>(string guid) where T : Object
        {
            if (anchorAttrb != null)
                return anchorAttrb.GetCachedObject<T>(guid);

            var path = string.IsNullOrEmpty(guid) ? pathProp.stringValue : AssetDatabase.GUIDToAssetPath(guid);
            var obj = AssetDatabase.LoadAssetAtPath<T>(path);
            return obj;
        }

        protected virtual void DrawProperty(Rect position, SerializedProperty property, GUIContent label)
        {
            try
            {
                /*
                var pt = GUIUtility.GUIToScreenPoint(position.position);
                if (pt.y < -EditorGUIUtility.singleLineHeight || pt.y > Screen.height)
                    return;
                */
                var fieldName = property.displayName;
                var fieldType = GetFieldType();
                var asset = GetCachedObject<Object>(
                    guidProp.stringValue);
                if (asset == null)
                {
                    pathProp.stringValue = AssetDatabase.GUIDToAssetPath(guidProp.stringValue);

                    asset = GetCachedObject<Object>(guidProp.stringValue);
                }

                var isComponent = fieldType.IsSubclassOf(typeof(Component));
                var objType = isComponent ? typeof(GameObject) : fieldType;

                EditorGUI.BeginChangeCheck();
                var alloc = EditorGUI.ObjectField(position, label, asset, objType, false);
                var changed = EditorGUI.EndChangeCheck();
                
                if (isComponent && alloc)
                {
                    var go = alloc as GameObject;
                    if (go.GetComponent(fieldType) == null)
                        return;
                }

                if (changed)
                {
                    if (alloc == null)
                    {
                        pathProp.stringValue = string.Empty;
                        guidProp.stringValue = string.Empty;
                        nameProp.stringValue = string.Empty;
                        property.serializedObject.ApplyModifiedProperties();
                        return;
                    }

                    var target = alloc;
                    if (alloc is Component comp)
                        target = comp.gameObject;

                    var assetPath = AssetDatabase.GetAssetPath(target);
                    if (string.IsNullOrEmpty(assetPath) && target is GameObject go)
                        assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);

                    if (string.IsNullOrEmpty(assetPath))
                    {
                        Debug.LogWarning($"SoftLink only accepts project assets. Field: {fieldName}, Object: {alloc.name}");
                        return;
                    }

                    pathProp.stringValue = assetPath;
                    guidProp.stringValue = AssetDatabase.AssetPathToGUID(assetPath);
                    nameProp.stringValue = alloc.name;
                    if (!string.IsNullOrEmpty(guidProp.stringValue))
                        EditorExtension.MakeAddressFromGUID(guidProp.stringValue);
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
            catch (ExitGUIException e)
            {
                throw e;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    public static class EditorExtension
    {
        public static bool MakeAddress(this UnityEngine.Object asset)
        {
            if (asset == null)
                return false;
            string path = AssetDatabase.GetAssetPath(asset);
            string guid = AssetDatabase.AssetPathToGUID(path);
            return MakeAddressFromGUID(guid);
        }

        public static bool HasAddress(string guid)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings.FindAssetEntry(guid);
            return entry != null;
        }

        public static bool MakeAddressFromGUID(string guid)
        {
            var h = Addressables.LoadResourceLocationsAsync(guid, null);
            var locations = h.WaitForCompletion();
            Addressables.Release(h);
            if (locations.Count > 0)
                return false;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings.FindAssetEntry(guid);
            if (entry != null)
                return false;
            settings.CreateAssetReference(guid);
            return true;
        }
    }
}
#endif
