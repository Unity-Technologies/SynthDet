using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Mono.Cecil;
using NUnit.Framework;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace Unity.Entities.CodeGen.Tests.LambdaJobs.Infrastructure
{
    [TestFixture]
    public abstract class LambdaJobIntegrationTest : IntegrationTest
    {
        protected override string ExpectedPath
        {
            get { return "Packages/com.unity.entities/Unity.Entities.CodeGen.Tests/LambdaJobs/IntegrationTests"; }
        }
        
        StringBuilder _methodIL;
        protected override string AdditionalIL
        {
            get { return _methodIL.ToString(); }
        }
        
        protected void RunTest(Type type)
        {
            var methodToAnalyze = MethodDefinitionForOnlyMethodOf(type);
            var forEachDescriptionConstructions = LambdaJobDescriptionConstruction.FindIn(methodToAnalyze);
            JobStructForLambdaJob jobStructForLambdaJob = null;
            foreach (var forEachDescriptionConstruction in forEachDescriptionConstructions)
            {
                List<DiagnosticMessage> diagnosticMessages;
                (jobStructForLambdaJob, diagnosticMessages) = LambdaJobsPostProcessor.Rewrite(methodToAnalyze, forEachDescriptionConstruction);
            }

            _methodIL = new StringBuilder();
            if (methodToAnalyze != null)
            {
                foreach (var instruction in methodToAnalyze.Body.Instructions)
                    _methodIL.AppendLine(instruction.ToString());
            }

            RunTest(jobStructForLambdaJob.TypeDefinition.DeclaringType);
        }
    }
}