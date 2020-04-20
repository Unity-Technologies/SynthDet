using NUnit.Framework;
using SimViz.TestHelpers;
using UnityEngine.TestTools;

namespace EditorTests.XodrImporterTests
{
    [TestFixture]
    public class ImportXodrTests : SimvizTestBaseSetup
    {
        TestHelpers testHelpers = new TestHelpers();

        [Test, TestCaseSource(typeof(XodrTestData), "TestCases")]
        public void VerifyXodrFilesImported(string file)
        {
            var fileImported = testHelpers.VerifyXodrFile(file);

            Assert.IsTrue(fileImported, string.Format("{0} was not found", file));
            LogAssert.NoUnexpectedReceived();
        }

        [Test, TestCaseSource(typeof(XodrTestData), "TestCases")]
        public void VerifyImportedXodrFiles(string file)
        {
            var xodrVerified = testHelpers.VerifyRoadsJunctionsInXodrFile(file);

            Assert.IsTrue(xodrVerified, string.Format("{0} failed verification", file));
            LogAssert.NoUnexpectedReceived();
        }
    }
}

