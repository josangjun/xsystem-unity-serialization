#if UNITY_EDITOR
namespace XSystem.InternalEditor
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEditor;
    using UnityEditorInternal;
    using UnityEngine;
    
    internal static class ContainerListDrawer
    {
        private const float Spacing = 2f;
        private const float ButtonWidth = 28f;
        private const float PageLabelWidth = 36f;
        private const float PageFieldWidth = 44f;
        private static readonly Dictionary<string, string> SearchTexts = new Dictionary<string, string>();
        private static readonly Dictionary<string, int> PageIndices = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> SelectedIndices = new Dictionary<string, int>();

        public static float GetPropertyHeight(SerializedProperty property, GUIContent label, FieldInfo fieldInfo)
        {
            if (!IsContainerProperty(property))
                return EditorGUI.GetPropertyHeight(property, label, true);

            var options = DrawerOptions.FromField(fieldInfo);
            if (!options.HasAny)
                return EditorGUI.GetPropertyHeight(property, label, true);

            return CreateList(property, label, options).GetHeight();
        }

        public static void OnGUI(Rect position, SerializedProperty property, GUIContent label, FieldInfo fieldInfo)
        {
            if (!IsContainerProperty(property))
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var options = DrawerOptions.FromField(fieldInfo);
            if (!options.HasAny)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            CreateList(property, label, options).DoList(position);
            EditorGUI.EndProperty();
        }

        private static ReorderableList CreateList(SerializedProperty property, GUIContent label, DrawerOptions options)
        {
            var key = GetKey(property);
            var displayLabel = GetDisplayLabel(property, label);
            var searchText = options.Searchable ? GetSearchText(property) : string.Empty;
            var matchedIndices = GetMatchedIndices(property, searchText);
            var pageIndex = GetClampedPageIndex(property, options, matchedIndices.Count);
            var visibleIndices = GetVisibleIndices(matchedIndices, options, pageIndex);
            var list = new ReorderableList(visibleIndices, typeof(int), true, true, true, true);

            list.index = SelectedIndices.TryGetValue(key, out var selectedIndex) ? selectedIndex : -1;
            list.headerHeight = GetHeaderHeight(options);

            list.drawHeaderCallback = rect =>
            {
                var lineRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(lineRect, GetHeaderText(displayLabel, property, options, searchText, matchedIndices.Count,
                    pageIndex, visibleIndices.Count));

                if (options.Searchable)
                {
                    lineRect.y += EditorGUIUtility.singleLineHeight + Spacing;
                    EditorGUI.BeginChangeCheck();
                    var nextSearchText = EditorGUI.TextField(lineRect, "Search", searchText);
                    if (EditorGUI.EndChangeCheck())
                    {
                        SearchTexts[key] = nextSearchText;
                        PageIndices[key] = 0;
                        SelectedIndices[key] = -1;
                    }
                }

                if (options.Paged)
                {
                    lineRect.y += EditorGUIUtility.singleLineHeight + Spacing;
                    DrawPageControls(lineRect, property, options, pageIndex, matchedIndices.Count);
                }
            };

            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (index < 0 || index >= visibleIndices.Count)
                    return;

                var actualIndex = visibleIndices[index];
                if (actualIndex < 0 || actualIndex >= property.arraySize)
                    return;

                var element = property.GetArrayElementAtIndex(actualIndex);
                rect.y += Spacing;
                rect.height = EditorGUI.GetPropertyHeight(element, true);
                EditorGUI.PropertyField(rect, element, new GUIContent(GetElementLabel(element, actualIndex)), true);
            };

            list.elementHeightCallback = index =>
            {
                if (index < 0 || index >= visibleIndices.Count)
                    return EditorGUIUtility.singleLineHeight;

                var actualIndex = visibleIndices[index];
                if (actualIndex < 0 || actualIndex >= property.arraySize)
                    return EditorGUIUtility.singleLineHeight;

                return EditorGUI.GetPropertyHeight(property.GetArrayElementAtIndex(actualIndex), true) + Spacing * 2f;
            };

            list.onSelectCallback = selectedList =>
            {
                SelectedIndices[key] = selectedList.index;
            };

            list.onReorderCallbackWithDetails = (reorderedList, oldVisibleIndex, newVisibleIndex) =>
            {
                if (oldVisibleIndex < 0 || oldVisibleIndex >= visibleIndices.Count ||
                    newVisibleIndex < 0 || newVisibleIndex >= visibleIndices.Count)
                    return;

                property.MoveArrayElement(visibleIndices[oldVisibleIndex], visibleIndices[newVisibleIndex]);
                property.serializedObject.ApplyModifiedProperties();

                var updatedMatchedIndices = GetMatchedIndices(property, searchText);
                var updatedPageIndex = GetClampedPageIndex(property, options, updatedMatchedIndices.Count);
                var updatedVisibleCount = GetVisibleIndices(updatedMatchedIndices, options, updatedPageIndex).Count;
                SelectedIndices[key] = updatedVisibleCount > 0
                    ? Mathf.Clamp(newVisibleIndex, 0, updatedVisibleCount - 1)
                    : -1;
            };

            list.onAddCallback = _ =>
            {
                var insertIndex = GetInsertIndex(property, options, matchedIndices, pageIndex);
                property.InsertArrayElementAtIndex(insertIndex);
                property.serializedObject.ApplyModifiedProperties();

                if (options.Searchable)
                    SearchTexts[key] = string.Empty;

                if (options.Paged)
                    PageIndices[key] = insertIndex / options.ItemCount;

                SelectedIndices[key] = options.Paged ? insertIndex % options.ItemCount : insertIndex;
            };

            list.onRemoveCallback = reorderedList =>
            {
                if (reorderedList.index < 0 || reorderedList.index >= visibleIndices.Count)
                    return;

                DeleteArrayElement(property, visibleIndices[reorderedList.index]);
                property.serializedObject.ApplyModifiedProperties();

                var updatedSearchText = options.Searchable ? GetSearchText(property) : string.Empty;
                var updatedMatchedIndices = GetMatchedIndices(property, updatedSearchText);
                var updatedPageIndex = GetClampedPageIndex(property, options, updatedMatchedIndices.Count);
                var updatedVisibleCount = GetVisibleIndices(updatedMatchedIndices, options, updatedPageIndex).Count;
                SelectedIndices[key] = updatedVisibleCount > 0
                    ? Mathf.Min(reorderedList.index, updatedVisibleCount - 1)
                    : -1;
            };

            list.drawNoneElementCallback = rect =>
            {
                EditorGUI.LabelField(rect, GetEmptyText(property, options, searchText));
            };

            return list;
        }

        private static void DrawPageControls(Rect rect, SerializedProperty property, DrawerOptions options,
            int pageIndex, int itemCount)
        {
            var previousEnabled = GUI.enabled;
            var x = rect.x;
            var pageCount = GetPageCount(itemCount, options.ItemCount);
            var maxPageIndex = Mathf.Max(0, pageCount - 1);

            GUI.enabled = previousEnabled && pageIndex > 0;
            if (GUI.Button(new Rect(x, rect.y, ButtonWidth, rect.height), "<<"))
                SetPageIndex(property, 0);

            x += ButtonWidth + Spacing;
            if (GUI.Button(new Rect(x, rect.y, ButtonWidth, rect.height), "<"))
                SetPageIndex(property, pageIndex - 1);

            GUI.enabled = previousEnabled;
            x += ButtonWidth + Spacing;
            EditorGUI.LabelField(new Rect(x, rect.y, PageLabelWidth, rect.height), "Page");

            x += PageLabelWidth;
            EditorGUI.BeginChangeCheck();
            var nextPage = EditorGUI.IntField(new Rect(x, rect.y, PageFieldWidth, rect.height), pageIndex + 1);
            if (EditorGUI.EndChangeCheck())
                SetPageIndex(property, Mathf.Clamp(nextPage - 1, 0, maxPageIndex));

            x += PageFieldWidth + Spacing;
            EditorGUI.LabelField(new Rect(x, rect.y, PageFieldWidth, rect.height), $"/ {Mathf.Max(1, pageCount)}");

            x += PageFieldWidth + Spacing;
            GUI.enabled = previousEnabled && pageIndex < maxPageIndex;
            if (GUI.Button(new Rect(x, rect.y, ButtonWidth, rect.height), ">"))
                SetPageIndex(property, pageIndex + 1);

            x += ButtonWidth + Spacing;
            if (GUI.Button(new Rect(x, rect.y, ButtonWidth, rect.height), ">>"))
                SetPageIndex(property, maxPageIndex);

            GUI.enabled = previousEnabled;
        }

        private static string GetHeaderText(string labelText, SerializedProperty property, DrawerOptions options,
            string searchText, int matchedCount, int pageIndex, int visibleCount)
        {
            if (!options.Paged)
                return string.IsNullOrWhiteSpace(searchText) ? labelText : $"{labelText} ({matchedCount}/{property.arraySize})";

            var start = visibleCount == 0 ? 0 : pageIndex * options.ItemCount + 1;
            var end = Mathf.Min(matchedCount, pageIndex * options.ItemCount + visibleCount);
            if (options.Searchable && !string.IsNullOrWhiteSpace(searchText))
                return $"{labelText} ({start}-{end}/{matchedCount} matches, {property.arraySize} total)";

            return $"{labelText} ({start}-{end}/{property.arraySize})";
        }

        private static string GetDisplayLabel(SerializedProperty property, GUIContent label)
        {
            if (!string.IsNullOrWhiteSpace(property.displayName))
                return property.displayName;

            if (label != null && !string.IsNullOrWhiteSpace(label.text))
                return label.text;

            return "List";
        }

        private static string GetElementLabel(SerializedProperty element, int index)
        {
            var nameProperty = element.FindPropertyRelative("Name")
                ?? element.FindPropertyRelative("name")
                ?? element.FindPropertyRelative("_name");

            if (nameProperty == null && element.hasVisibleChildren)
            {
                var it = element.Copy();
                if (it.NextVisible(true))
                    nameProperty = it;
            }

            if (nameProperty != null)
            {
                var value = GetPropertySearchValue(nameProperty);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return $"Element {index}";
        }

        private static string GetEmptyText(SerializedProperty property, DrawerOptions options, string searchText)
        {
            if (property.arraySize == 0)
                return "List is Empty";

            if (options.Searchable && !string.IsNullOrWhiteSpace(searchText))
                return "No matching elements";

            return "No elements on this page";
        }

        private static int GetInsertIndex(SerializedProperty property, DrawerOptions options, List<int> matchedIndices,
            int pageIndex)
        {
            if (!options.Paged || matchedIndices.Count == 0)
                return property.arraySize;

            var pageEnd = Mathf.Min(matchedIndices.Count, (pageIndex + 1) * options.ItemCount);
            return pageEnd > 0 ? matchedIndices[pageEnd - 1] + 1 : property.arraySize;
        }

        private static float GetHeaderHeight(DrawerOptions options)
        {
            var lineCount = 1;
            if (options.Searchable)
                lineCount++;
            if (options.Paged)
                lineCount++;

            return EditorGUIUtility.singleLineHeight * lineCount + Spacing * lineCount;
        }

        private static bool IsContainerProperty(SerializedProperty property)
        {
            return property.isArray && property.propertyType != SerializedPropertyType.String;
        }

        private static List<int> GetMatchedIndices(SerializedProperty property, string searchText)
        {
            var visibleIndices = new List<int>();
            for (var i = 0; i < property.arraySize; i++)
            {
                if (MatchesSearch(property.GetArrayElementAtIndex(i), searchText))
                    visibleIndices.Add(i);
            }

            return visibleIndices;
        }

        private static List<int> GetVisibleIndices(List<int> matchedIndices, DrawerOptions options, int pageIndex)
        {
            if (!options.Paged)
                return matchedIndices;

            var visibleIndices = new List<int>();
            var start = Mathf.Clamp(pageIndex, 0, GetPageCount(matchedIndices.Count, options.ItemCount) - 1) *
                        options.ItemCount;
            var end = Mathf.Min(matchedIndices.Count, start + options.ItemCount);

            for (var i = start; i < end; i++)
                visibleIndices.Add(matchedIndices[i]);

            return visibleIndices;
        }

        private static int GetClampedPageIndex(SerializedProperty property, DrawerOptions options, int itemCount)
        {
            if (!options.Paged)
                return 0;

            PageIndices.TryGetValue(GetKey(property), out var pageIndex);
            pageIndex = Mathf.Clamp(pageIndex, 0, Mathf.Max(0, GetPageCount(itemCount, options.ItemCount) - 1));
            SetPageIndex(property, pageIndex);
            return pageIndex;
        }

        private static int GetPageCount(int itemCount, int pageItemCount)
        {
            if (itemCount == 0)
                return 1;

            return Mathf.CeilToInt(itemCount / (float)pageItemCount);
        }

        private static void SetPageIndex(SerializedProperty property, int pageIndex)
        {
            PageIndices[GetKey(property)] = pageIndex;
        }

        private static string GetSearchText(SerializedProperty property)
        {
            SearchTexts.TryGetValue(GetKey(property), out var searchText);
            return searchText ?? string.Empty;
        }

        private static string GetKey(SerializedProperty property)
        {
            return $"{property.serializedObject.targetObject.GetInstanceID()}:{property.propertyPath}";
        }

        private static void DeleteArrayElement(SerializedProperty arrayProperty, int index)
        {
            var oldSize = arrayProperty.arraySize;
            arrayProperty.DeleteArrayElementAtIndex(index);

            if (arrayProperty.arraySize == oldSize)
                arrayProperty.DeleteArrayElementAtIndex(index);
        }

        private static bool MatchesSearch(SerializedProperty property, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return true;

            var comparison = StringComparison.OrdinalIgnoreCase;
            if (PropertyValueContains(property, searchText, comparison))
                return true;

            var child = property.Copy();
            var end = property.GetEndProperty();
            var enterChildren = true;

            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                if (PropertyValueContains(child, searchText, comparison))
                    return true;

                enterChildren = false;
            }

            return false;
        }

        private static bool PropertyValueContains(SerializedProperty property, string searchText,
            StringComparison comparison)
        {
            var value = GetPropertySearchValue(property);
            return !string.IsNullOrEmpty(value) && value.IndexOf(searchText, comparison) >= 0;
        }

        private static string GetPropertySearchValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                    return property.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return property.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return property.floatValue.ToString();
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Color:
                    return property.colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue ? property.objectReferenceValue.name : string.Empty;
                case SerializedPropertyType.Enum:
                    return property.enumValueIndex >= 0 && property.enumValueIndex < property.enumDisplayNames.Length
                        ? property.enumDisplayNames[property.enumValueIndex]
                        : string.Empty;
                case SerializedPropertyType.Vector2:
                    return property.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return property.vector3Value.ToString();
                case SerializedPropertyType.Vector4:
                    return property.vector4Value.ToString();
                case SerializedPropertyType.Rect:
                    return property.rectValue.ToString();
                case SerializedPropertyType.Bounds:
                    return property.boundsValue.ToString();
                case SerializedPropertyType.Quaternion:
                    return property.quaternionValue.ToString();
                case SerializedPropertyType.Vector2Int:
                    return property.vector2IntValue.ToString();
                case SerializedPropertyType.Vector3Int:
                    return property.vector3IntValue.ToString();
                case SerializedPropertyType.RectInt:
                    return property.rectIntValue.ToString();
                case SerializedPropertyType.BoundsInt:
                    return property.boundsIntValue.ToString();
                case SerializedPropertyType.ManagedReference:
                    return property.managedReferenceFullTypename;
                default:
                    return string.Empty;
            }
        }

        private struct DrawerOptions
        {
            public bool Searchable;
            public bool Paged;
            public int ItemCount;
            public bool HasAny => Searchable || Paged;

            public static DrawerOptions FromField(FieldInfo fieldInfo)
            {
                var pageAttribute = fieldInfo?.GetCustomAttribute<PageAttribute>();
                return new DrawerOptions
                {
                    Searchable = fieldInfo?.GetCustomAttribute<SearchableAttribute>() != null,
                    Paged = pageAttribute != null,
                    ItemCount = pageAttribute != null ? Mathf.Max(1, pageAttribute.ItemCount) : 1
                };
            }
        }
    }
}
#endif
