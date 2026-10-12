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