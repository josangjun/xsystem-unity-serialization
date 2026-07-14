using System.Collections.Generic;
using UnityEngine;

namespace XSystem
{
    [System.Serializable]
    public class SerializedDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<TKey> keys = new List<TKey>();
        [SerializeField] private List<TValue> values = new List<TValue>();

        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (var kvp in this)
            {
                keys.Add(kvp.Key);
                values.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            this.Clear();
            for (int i = 0; i < Mathf.Min(keys.Count, values.Count); i++)
            {
                this[keys[i]] = values[i];
            }
        }
    }
}


#if UNITY_EDITOR
namespace XSystem.Internal
{
    using System.Collections;
    using UnityEditor;
    using UnityEngine;

    [CustomPropertyDrawer(typeof(SerializedDictionary<,>), true)]
    public class SerializedDictionaryDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;
        private const float RemoveButtonWidth = 24f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var keysProperty = property.FindPropertyRelative("keys");
            var valuesProperty = property.FindPropertyRelative("values");

            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            if (keysProperty == null || valuesProperty == null)
                return EditorGUIUtility.singleLineHeight * 2f + Spacing;

            SyncArraySizes(keysProperty, valuesProperty);

            var height = EditorGUIUtility.singleLineHeight + Spacing;
            height += EditorGUIUtility.singleLineHeight + Spacing;

            if (keysProperty.arraySize == 0)
            {
                height += EditorGUIUtility.singleLineHeight + Spacing;
            }
            else
            {
                for (var i = 0; i < keysProperty.arraySize; i++)
                {
                    height += GetPairHeight(keysProperty, valuesProperty, i) + Spacing;
                }
            }

            height += EditorGUIUtility.singleLineHeight;
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var keysProperty = property.FindPropertyRelative("keys");
            var valuesProperty = property.FindPropertyRelative("values");

            EditorGUI.BeginProperty(position, label, property);

            var lineRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            if (keysProperty == null || valuesProperty == null)
            {
                lineRect.y += EditorGUIUtility.singleLineHeight + Spacing;
                EditorGUI.HelpBox(lineRect, "SerializedDictionary requires keys and values fields.", MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            SyncArraySizes(keysProperty, valuesProperty);

            EditorGUI.BeginChangeCheck();

            EditorGUI.indentLevel++;
            var contentRect = EditorGUI.IndentedRect(position);
            EditorGUI.indentLevel--;

            lineRect = new Rect(contentRect.x, lineRect.y + EditorGUIUtility.singleLineHeight + Spacing,
                contentRect.width, EditorGUIUtility.singleLineHeight);

            if (keysProperty.arraySize == 0)
            {
                EditorGUI.LabelField(lineRect, "Empty");
                lineRect.y += EditorGUIUtility.singleLineHeight + Spacing;
            }
            else
            {
                for (var i = 0; i < keysProperty.arraySize; i++)
                {
                    var rowHeight = GetPairHeight(keysProperty, valuesProperty, i);
                    var rowRect = new Rect(contentRect.x, lineRect.y, contentRect.width, rowHeight);
                    DrawPair(rowRect, keysProperty, valuesProperty, i);
                    lineRect.y += rowHeight + Spacing;
                }
            }

            var addRect = new Rect(contentRect.x, lineRect.y, contentRect.width, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(addRect, "Add Item"))
            {
                var index = keysProperty.arraySize;
                keysProperty.InsertArrayElementAtIndex(index);
                valuesProperty.InsertArrayElementAtIndex(index);

                ClearPropertyValue(keysProperty.GetArrayElementAtIndex(index));
                ClearPropertyValue(valuesProperty.GetArrayElementAtIndex(index));
            }

            if (EditorGUI.EndChangeCheck())
            {
                property.serializedObject.ApplyModifiedProperties();
                RebuildTargetDictionaries(property);
            }

            EditorGUI.EndProperty();
        }

        private static void DrawPair(Rect position, SerializedProperty keysProperty, SerializedProperty valuesProperty, int index)
        {
            var keyProperty = keysProperty.GetArrayElementAtIndex(index);
            var valueProperty = valuesProperty.GetArrayElementAtIndex(index);
            var keyHeight = EditorGUI.GetPropertyHeight(keyProperty, true);
            var valueHeight = EditorGUI.GetPropertyHeight(valueProperty, true);
            var fieldWidth = position.width - RemoveButtonWidth - Spacing;

            var keyRect = new Rect(position.x, position.y, fieldWidth, keyHeight);
            var valueRect = new Rect(position.x, keyRect.yMax + Spacing, fieldWidth, valueHeight);

            EditorGUI.PropertyField(keyRect, keyProperty, new GUIContent("Key"), true);
            EditorGUI.PropertyField(valueRect, valueProperty, new GUIContent("Value"), true);

            var removeRect = new Rect(position.xMax - RemoveButtonWidth, position.y, RemoveButtonWidth,
                EditorGUIUtility.singleLineHeight);
            if (GUI.Button(removeRect, "-"))
            {
                DeleteArrayElement(keysProperty, index);
                DeleteArrayElement(valuesProperty, index);
            }
        }

        private static float GetPairHeight(SerializedProperty keysProperty, SerializedProperty valuesProperty, int index)
        {
            var keyHeight = EditorGUI.GetPropertyHeight(keysProperty.GetArrayElementAtIndex(index), true);
            var valueHeight = EditorGUI.GetPropertyHeight(valuesProperty.GetArrayElementAtIndex(index), true);
            return keyHeight + Spacing + valueHeight;
        }

        private static void SyncArraySizes(SerializedProperty keysProperty, SerializedProperty valuesProperty)
        {
            while (valuesProperty.arraySize < keysProperty.arraySize)
                valuesProperty.InsertArrayElementAtIndex(valuesProperty.arraySize);

            while (keysProperty.arraySize < valuesProperty.arraySize)
                keysProperty.InsertArrayElementAtIndex(keysProperty.arraySize);
        }

        private static void DeleteArrayElement(SerializedProperty arrayProperty, int index)
        {
            var oldSize = arrayProperty.arraySize;
            arrayProperty.DeleteArrayElementAtIndex(index);

            if (arrayProperty.arraySize == oldSize)
                arrayProperty.DeleteArrayElementAtIndex(index);
        }

        private static void ClearPropertyValue(SerializedProperty property)
        {
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                property.arraySize = 0;
                return;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                    property.intValue = 0;
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = false;
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = 0f;
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = string.Empty;
                    break;
                case SerializedPropertyType.Color:
                    property.colorValue = Color.white;
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = null;
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = 0;
                    break;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = Vector2.zero;
                    break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = Vector3.zero;
                    break;
                case SerializedPropertyType.Vector4:
                    property.vector4Value = Vector4.zero;
                    break;
                case SerializedPropertyType.Rect:
                    property.rectValue = Rect.zero;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    property.animationCurveValue = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                    break;
                case SerializedPropertyType.Bounds:
                    property.boundsValue = new Bounds(Vector3.zero, Vector3.zero);
                    break;
                case SerializedPropertyType.Quaternion:
                    property.quaternionValue = Quaternion.identity;
                    break;
                case SerializedPropertyType.Vector2Int:
                    property.vector2IntValue = Vector2Int.zero;
                    break;
                case SerializedPropertyType.Vector3Int:
                    property.vector3IntValue = Vector3Int.zero;
                    break;
                case SerializedPropertyType.RectInt:
                    property.rectIntValue = new RectInt();
                    break;
                case SerializedPropertyType.BoundsInt:
                    property.boundsIntValue = new BoundsInt();
                    break;
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.ExposedReference:
                case SerializedPropertyType.FixedBufferSize:
                case SerializedPropertyType.Gradient:
                    break;
                case SerializedPropertyType.Generic:
                    ClearChildProperties(property);
                    break;
                case SerializedPropertyType.ManagedReference:
                    property.managedReferenceValue = null;
                    break;
            }
        }

        private static void ClearChildProperties(SerializedProperty property)
        {
            var child = property.Copy();
            var end = property.GetEndProperty();
            var enterChildren = true;

            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                ClearPropertyValue(child);
                enterChildren = false;
            }
        }

        private void RebuildTargetDictionaries(SerializedProperty property)
        {
            foreach (var targetObject in property.serializedObject.targetObjects)
            {
                var dictionary = GetDictionaryValue(targetObject, property.propertyPath);
                dictionary?.OnAfterDeserialize();
                EditorUtility.SetDirty(targetObject);
            }
        }

        private ISerializationCallbackReceiver GetDictionaryValue(object targetObject, string propertyPath)
        {
            if (fieldInfo == null)
                return null;

            if (fieldInfo.DeclaringType != null && fieldInfo.DeclaringType.IsInstanceOfType(targetObject))
            {
                var value = fieldInfo.GetValue(targetObject);
                if (value is ISerializationCallbackReceiver dictionary)
                    return dictionary;
            }

            return ResolvePropertyPath(targetObject, propertyPath) as ISerializationCallbackReceiver;
        }

        private static object ResolvePropertyPath(object targetObject, string propertyPath)
        {
            var current = targetObject;
            var elements = propertyPath.Replace(".Array.data[", "[").Split('.');

            foreach (var element in elements)
            {
                if (current == null)
                    return null;

                var bracketIndex = element.IndexOf('[');
                if (bracketIndex >= 0)
                {
                    var fieldName = element.Substring(0, bracketIndex);
                    var indexText = element.Substring(bracketIndex + 1, element.Length - bracketIndex - 2);

                    current = GetFieldValue(current, fieldName);
                    if (current is IList list && int.TryParse(indexText, out var index) &&
                        index >= 0 && index < list.Count)
                    {
                        current = list[index];
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    current = GetFieldValue(current, element);
                }
            }

            return current;
        }

        private static object GetFieldValue(object source, string fieldName)
        {
            var type = source.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);

                if (field != null)
                    return field.GetValue(source);

                type = type.BaseType;
            }

            return null;
        }
    }
}
#endif
