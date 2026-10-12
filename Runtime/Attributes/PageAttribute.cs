
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

