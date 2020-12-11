using NUnit.Framework;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    class LiveLinkToolbarTests
    {
        [Test]
        public void RepaintToolbarMethodMustBePresentOnToolbarInternalClass()
        {
            var @delegate = LiveLinkToolbar.BuildRepaintToolbarDelegate();
            Assert.That(@delegate, Is.Not.Null, $"Missing parameterless method RepaintToolbar() on UnityEditor.Toolbar");
        }
    }
}