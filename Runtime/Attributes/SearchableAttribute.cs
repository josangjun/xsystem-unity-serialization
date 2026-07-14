using System;
using UnityEngine;

namespace XSystem
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class SearchableAttribute : PropertyAttribute
    {
        public SearchableAttribute() : base(true)
        {
        }
    }
}

#if UNITY_EDITOR
namespace XSystem.InternalEditor
{
    using UnityEditor;

    [CustomPropertyDrawer(typeof(SearchableAttribute), true)]
    public class SearchableDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return ContainerListDrawer.GetPropertyHeight(property, label, fieldInfo);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ContainerListDrawer.OnGUI(position, property, label, fieldInfo);
        }
    }
}
#endif
