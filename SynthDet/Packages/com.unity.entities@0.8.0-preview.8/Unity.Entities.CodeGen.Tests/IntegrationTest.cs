using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using NUnit.Framework;
using Unity.Entities.Editor;
using Unity.Entities.Hybrid;

namespace Unity.Entities.CodeGen.Tests
{
    [TestFixture]
    public abstract class IntegrationTest : LambdaJobsPostProcessorTestBase
    {
        // Make sure to not check this in with true or your tests will always pass!
        public static bool overwriteExpectationWithReality = false;
        
        protected abstract string ExpectedPath { get; }
        protected virtual string AdditionalIL => string.Empty;

        static bool IsAssemblyBuiltAsDebug()
        {
            var debuggableAttributes = typeof(IntegrationTest).Assembly.GetCustomAttributes(typeof(DebuggableAttribute), false);
            return debuggableAttributes.Any(debuggableAttribute => ((DebuggableAttribute) debuggableAttribute).IsJITTrackingEnabled);
        }
       
        protected void RunTest(TypeReference type)
        {
            // Ideally these tests to run in Release codegen or otherwise the generated IL won't be deterministic (due to differences between /optimize+ and /optimize-. 
            // We attempt to make the tests generate the same decompiled C# in any case (by making sure all variables are used).
            if (IsAssemblyBuiltAsDebug())
                UnityEngine.Debug.LogWarning("Integration tests should only be run with release code optimizations turned on for consistent codegen.  Switch your settings in Preferences->External Tools->Editor Attaching (in 2019.3) or Preferences->General->Code Optimization On Startup (in 2020.1+) to be able to run these tests.");

            var expectationFile = Path.GetFullPath($"{ExpectedPath}/{GetType().Name}.expectation.txt");
            var jobCSharp = Decompiler.DecompileIntoCSharpAndIL(type, DecompiledLanguage.CSharpOnly).CSharpCode;
            var actualLines = jobCSharp.Split('\n').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

            var shouldOverWrite = overwriteExpectationWithReality || !File.Exists(expectationFile);

            if (shouldOverWrite)
            {
                File.WriteAllText(expectationFile, jobCSharp);
            }
            string expected = File.ReadAllText(expectationFile);
            var expectedLines = expected.Split('\n').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

            var attributeRegex = new Regex(@"^[\t, ]*\[[\w]+(\(.*\))*\][\s]*$");
            var actualAttributes = new List<string>();
            var expectedAttributes = new List<string>();

            bool success = expectedLines.Length == actualLines.Length;
            if (!success)
                Console.WriteLine($"Incorrect number of lines.  Expected lines: {expectedLines.Length}, actual lines: {actualLines.Length}");
            if (success)
            {
                for (int i = 0; i < actualLines.Length; ++i)
                {
                    string actualLine = actualLines[i];
                    string expectedLine = expectedLines[i];

                    if (attributeRegex.IsMatch(actualLine))
                    {
                        actualAttributes.Add(actualLine.Trim());
                        expectedAttributes.Add(expectedLine.Trim());
                        continue;
                    }

                    if (expectedLine != actualLine)
                    {
                        success = false;
                        Console.WriteLine($"Mismatched line at {i}.\nExpected line:\n\n{expectedLine}\n\nActual line:\n\n{actualLine}\n\n");
                        break;
                    }
                }

                actualAttributes.Sort();
                expectedAttributes.Sort();
                if (success && !expectedAttributes.SequenceEqual(actualAttributes))
                {
                    success = false;
                    var expectedAttributesStr = String.Join("\n", expectedAttributes);
                    var actualAttributesStr = String.Join("\n", actualAttributes);
                    Console.WriteLine($"Mismatched attributes.\nExpected attributes:\n\n{expectedAttributesStr}\n\nActual attributes:\n\n {actualAttributesStr}\n\n");
                }
            }

            if (!success || overwriteExpectationWithReality)
            {
                var tempFolder = Path.GetTempPath();
                var path = $@"{tempFolder}decompiled.cs";
                File.WriteAllText(path, jobCSharp + Environment.NewLine + Environment.NewLine + AdditionalIL);
                Console.WriteLine("Actual Decompiled C#: ");
                Console.WriteLine((string)jobCSharp);
                if (!String.IsNullOrEmpty(AdditionalIL))
                {
                    Console.WriteLine("Addition IL: ");
                    Console.WriteLine(AdditionalIL);
                }
                UnityEngine.Debug.Log($"Wrote expected csharp to editor log and to {path}");
            }

            if (shouldOverWrite)
                return;

            Assert.IsTrue(success);
        }
    }
}
