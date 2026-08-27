using System.Text;
using UnityEngine;

namespace XSystem
{
    public static class SerializationExt
    {
        public static string GetHierarchy(this GameObject go)
        {
            var builder = new StringBuilder();
            var transform = go.transform;
            while (transform != null)
            {
                builder.Insert(0, transform.name);
                builder.Insert(0, '/');
                transform = transform.parent;
            }

            return builder.ToString();
        }
    }
}
