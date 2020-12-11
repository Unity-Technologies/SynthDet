using System;
using Mono.Cecil;
using NUnit.Framework;
using Unity.Entities.CodeGen.Tests;

namespace Unity.Entities.Hybrid.CodeGen.Tests
{
    [TestFixture]
    public abstract class AuthoringComponentIntegrationTest : IntegrationTest
    {
        protected override string ExpectedPath => 
            "Packages/com.unity.entities/Unity.Entities.Hybrid.CodeGen.Tests/AuthoringComponent/IntegrationTests";

        protected void RunAuthoringComponentDataTest(Type type)
        {
            TypeDefinition authoringType =
                AuthoringComponentPostProcessor.CreateComponentDataAuthoringType(TypeDefinitionFor(type));
            
            RunTest(authoringType);
        }

        protected void RunAuthoringBufferElementDataTest(Type type)
        {
            TypeDefinition authoringType =
                AuthoringComponentPostProcessor.CreateBufferElementDataAuthoringType(TypeDefinitionFor(type));
            
            RunTest(authoringType);
        }
    }
}