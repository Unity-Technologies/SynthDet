using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    static class UIElementTestHelpers
    {
        internal static VisualElement GetItem(this VisualElement e, string name = null, string className = null)
            => GetItem<VisualElement>(e, name, className);

        internal static List<VisualElement> GetItems(this VisualElement e, string name = null, string className = null)
            => GetItems<VisualElement>(e, name, className);

        internal static T GetItem<T>(this VisualElement e, string name = null, string className = null) where T : VisualElement
        {
            var el = e.Q<T>(name, className);
            Assert.That(el, Is.Not.Null);
            return el;
        }

        internal static List<T> GetItems<T>(this VisualElement e, string name = null, string className = null) where T : VisualElement
        {
            var el = e.Query<T>(name, className).ToList();
            Assert.That(el, Is.Not.Empty);
            return el;
        }
    }
}