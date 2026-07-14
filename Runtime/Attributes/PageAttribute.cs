
using System;
using UnityEngine;

namespace XSystem
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class PageAttribute : PropertyAttribute
    {
        public int ItemCount { get; private set; }
        
        public PageAttribute(int itemCount = 10)
            : base(true)
        {
            ItemCount = Mathf.Max(1, itemCount);
        }
    }
}

#if UNITY_EDITOR
namespace XSystem.InternalEditor
{
    using UnityEditor;
    using UnityEngine;

    [CustomPropertyDrawer(typeof(PageAttribute), true)]
    public class PageDrawer : PropertyDrawer
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
