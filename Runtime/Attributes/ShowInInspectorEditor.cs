#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XSystem.InternalEditor
{
    internal abstract class ShowInInspectorEditorBase : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            base.OnInspectorGUI();
            var members = GetMembers(target.GetType());
            if (members.Count == 0) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shown In Inspector", EditorStyles.boldLabel);
            foreach (var member in members) DrawMember(member);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMember(MemberInfo member)
        {
            var field = member as FieldInfo;
            var property = member as PropertyInfo;
            if (field != null && serializedObject.FindProperty(field.Name) != null) return;
            var value = field != null ? field.GetValue(target) : property.GetValue(target);
            var valueType = field != null ? field.FieldType : property.PropertyType;
            var canWrite = field != null ? !field.IsInitOnly : property.SetMethod != null;
            EditorGUI.BeginChangeCheck();
            var newValue = DrawValue(ObjectNames.NicifyVariableName(member.Name), value, valueType, canWrite);
            var changed = EditorGUI.EndChangeCheck();
            if (!canWrite || !changed) return;
            Undo.RecordObject(target, $"Change {member.Name}");
            if (field != null) field.SetValue(target, newValue);
            else property.SetValue(target, newValue);
            EditorUtility.SetDirty(target);
        }

        private static object DrawValue(string label, object value, Type type, bool enabled)
        {
            using (new EditorGUI.DisabledScope(!enabled))
            {
                if (type == typeof(bool)) return EditorGUILayout.Toggle(label, value is bool v && v);
                if (type == typeof(int)) return EditorGUILayout.IntField(label, value is int v ? v : 0);
                if (type == typeof(float)) return EditorGUILayout.FloatField(label, value is float v ? v : 0f);
                if (type == typeof(double)) return EditorGUILayout.DoubleField(label, value is double v ? v : 0d);
                if (type == typeof(string)) return EditorGUILayout.TextField(label, value as string ?? string.Empty);
                if (type == typeof(long)) return EditorGUILayout.LongField(label, value is long v ? v : 0L);
                if (type.IsEnum) return EditorGUILayout.EnumPopup(label, value as Enum ?? (Enum)Enum.GetValues(type).GetValue(0));
                if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return EditorGUILayout.ObjectField(label, value as UnityEngine.Object, type, true);
                if (type == typeof(Vector2)) return EditorGUILayout.Vector2Field(label, value is Vector2 v ? v : default);
                if (type == typeof(Vector3)) return EditorGUILayout.Vector3Field(label, value is Vector3 v ? v : default);
                if (type == typeof(Vector4)) return EditorGUILayout.Vector4Field(label, value is Vector4 v ? v : default);
                if (type == typeof(Color)) return EditorGUILayout.ColorField(label, value is Color v ? v : Color.white);
                EditorGUILayout.LabelField(label, value?.ToString() ?? "null");
                return value;
            }
        }

        private static List<MemberInfo> GetMembers(Type type)
        {
            var result = new List<MemberInfo>();
            var names = new HashSet<string>();
            for (var current = type; current != null && current != typeof(UnityEngine.Object); current = current.BaseType)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                foreach (var field in current.GetFields(flags))
                {
                    if (field.IsStatic || field.IsDefined(typeof(HideInInspector), true) || !field.IsDefined(typeof(ShowInInspectorAttribute), true) || !names.Add(field.Name)) continue;
                    result.Add(field);
                }
                foreach (var property in current.GetProperties(flags))
                {
                    if (property.GetIndexParameters().Length != 0 || property.GetMethod == null || !property.IsDefined(typeof(ShowInInspectorAttribute), true) || !names.Add(property.Name)) continue;
                    result.Add(property);
                }
            }
            return result.OrderBy(member => member.MetadataToken).ToList();
        }
    }

    [CustomEditor(typeof(MonoBehaviour), true, isFallback = true)]
    internal sealed class ShowInInspectorMonoBehaviourEditor : ShowInInspectorEditorBase { }

    [CustomEditor(typeof(ScriptableObject), true, isFallback = true)]
    internal sealed class ShowInInspectorScriptableObjectEditor : ShowInInspectorEditorBase { }
}
#endif
