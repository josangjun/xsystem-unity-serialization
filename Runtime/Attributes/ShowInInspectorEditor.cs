#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace XSystem.InternalEditor
{
    internal abstract class ShowInInspectorEditorBase : Editor
    {
        private static readonly Dictionary<Type, List<MemberInfo>> OrderedMembersCache = new Dictionary<Type, List<MemberInfo>>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var serializedProperties = GetSerializedProperties();
            var serializedNames = new HashSet<string>(serializedProperties);
            var drawnNames = new HashSet<string>();

            if (serializedNames.Contains("m_Script"))
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
                drawnNames.Add("m_Script");
            }

            foreach (var member in GetOrderedMembers(target))
            {
                if (drawnNames.Contains(member.Name)) continue;

                if (serializedNames.Contains(member.Name))
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(member.Name), true);
                    drawnNames.Add(member.Name);
                    continue;
                }

                if (!IsShowInInspectorMember(member)) continue;
                DrawMember(member);
                drawnNames.Add(member.Name);
            }

            // Preserve serialized properties that do not have a matching reflected member.
            foreach (var propertyName in serializedProperties)
            {
                if (drawnNames.Contains(propertyName)) continue;
                EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static bool IsShowInInspectorMember(MemberInfo member)
        {
            if (!member.IsDefined(typeof(ShowInInspectorAttribute), true)) return false;
            if (member is FieldInfo field)
                return !field.IsStatic && !field.IsDefined(typeof(HideInInspector), true);
            if (member is PropertyInfo property)
                return property.GetIndexParameters().Length == 0 && property.GetMethod != null;
            return false;
        }

        private List<string> GetSerializedProperties()
        {
            var result = new List<string>();
            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                result.Add(iterator.name);
            }

            return result;
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

        private static List<MemberInfo> GetOrderedMembers(UnityEngine.Object target)
        {
            var targetType = target.GetType();
            if (OrderedMembersCache.TryGetValue(targetType, out var cachedMembers))
                return cachedMembers;

            var result = new List<MemberInfo>();
            var hierarchy = new Stack<Type>();
            var sourceOrder = GetSourceOrder(target);
            for (var current = targetType; current != null && current != typeof(UnityEngine.Object); current = current.BaseType)
                hierarchy.Push(current);

            while (hierarchy.Count > 0)
            {
                var current = hierarchy.Pop();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                result.AddRange(current.GetMembers(flags)
                    .Where(member => member is FieldInfo || member is PropertyInfo)
                    .OrderBy(member => sourceOrder.TryGetValue(GetMemberKey(member), out var line) ? line : int.MaxValue)
                    .ThenBy(member => member.MetadataToken));
            }

            OrderedMembersCache[targetType] = result;
            return result;
        }

        private static Dictionary<string, int> GetSourceOrder(UnityEngine.Object target)
        {
            var result = new Dictionary<string, int>();
            var script = target is MonoBehaviour behaviour
                ? MonoScript.FromMonoBehaviour(behaviour)
                : target is ScriptableObject scriptableObject
                    ? MonoScript.FromScriptableObject(scriptableObject)
                    : null;
            if (script == null || string.IsNullOrEmpty(script.text)) return result;

            var lines = script.text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            var members = GetAllMembers(target.GetType());
            foreach (var member in members)
            {
                var pattern = $@"\b{Regex.Escape(member.Name)}\b\s*(?:[=;{{]|=>)";
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    if (!Regex.IsMatch(lines[lineIndex], pattern)) continue;
                    result[GetMemberKey(member)] = lineIndex;
                    break;
                }
            }

            return result;
        }

        private static string GetMemberKey(MemberInfo member)
        {
            return $"{member.DeclaringType?.AssemblyQualifiedName}:{member.Name}";
        }

        private static IEnumerable<MemberInfo> GetAllMembers(Type type)
        {
            for (var current = type; current != null && current != typeof(UnityEngine.Object); current = current.BaseType)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                foreach (var member in current.GetMembers(flags))
                    if (member is FieldInfo || member is PropertyInfo)
                        yield return member;
            }
        }
    }

    [CustomEditor(typeof(MonoBehaviour), true, isFallback = true)]
    internal sealed class ShowInInspectorMonoBehaviourEditor : ShowInInspectorEditorBase { }

    [CustomEditor(typeof(ScriptableObject), true, isFallback = true)]
    internal sealed class ShowInInspectorScriptableObjectEditor : ShowInInspectorEditorBase { }
}
#endif
