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

