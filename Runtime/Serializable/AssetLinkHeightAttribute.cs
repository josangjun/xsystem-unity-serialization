using System;
using UnityEngine;

namespace XSystem
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class AssetLinkHeightAttribute : PropertyAttribute
    {
        public float Height { get; }

        public AssetLinkHeightAttribute(float height)
        {
            Height = Mathf.Max(0f, height);
        }
    }
}
