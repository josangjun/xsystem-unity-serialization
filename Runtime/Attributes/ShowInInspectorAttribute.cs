using System;

namespace XSystem
{
    /// <summary>
    /// Displays a field or property in the Unity Inspector.
    /// Properties are displayed read-only unless they have a setter.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ShowInInspectorAttribute : Attribute
    {
    }
}
