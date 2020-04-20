using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.SimViz.Scenarios
{
    internal class SerializedPropertyEnumerable : IEnumerable<SerializedProperty>
    {
        private SerializedProperty m_Property;

        public SerializedPropertyEnumerable(SerializedProperty property)
        {
            m_Property = property;
        }

        public IEnumerator<SerializedProperty> GetEnumerator()
        {
            for (int i = 0; i < m_Property.arraySize; i++)
            {
                yield return m_Property.GetArrayElementAtIndex(i);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal static class Extensions
    {
        public static IEnumerable<SerializedProperty> ToEnumerable(this SerializedProperty property)
        {
            return new SerializedPropertyEnumerable(property);
        }

        public static IEnumerable<SerializedProperty> GetChildren(this SerializedProperty property)
        {
            property = property.Copy();
            var nextElement = property.Copy();
            bool hasNextElement = nextElement.NextVisible(false);
            if (!hasNextElement)
            {
                nextElement = null;
            }
 
            property.NextVisible(true);
            while (true)
            {
                if ((SerializedProperty.EqualContents(property, nextElement)))
                {
                    yield break;
                }
 
                yield return property;
 
                bool hasNext = property.NextVisible(false);
                if (!hasNext)
                {
                    break;
                }
            }
        }
    }

}