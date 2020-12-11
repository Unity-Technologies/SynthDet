using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.Entities.CodeGen;

namespace Unity.Entities.CodeGen.Tests
{
    public abstract class PostProcessorTestBase
    {
        class FailResolver : IAssemblyResolver
        {
            public void Dispose() { }
            public AssemblyDefinition Resolve(AssemblyNameReference name) { return null; }
            public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) { return null; }
        }
        
        protected AssemblyDefinition AssemblyDefinitionFor(Type type, bool useFailResolver = false)
        {
            var assemblyLocation = type.Assembly.Location;

            IAssemblyResolver resolver;
            if (useFailResolver)
                resolver = new FailResolver();
            else
                resolver = new LambdaJobsPostProcessorTestBase.OnDemandResolver();
            
            var ad = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(assemblyLocation)), 
                new ReaderParameters(ReadingMode.Immediate)
                {
                    ReadSymbols = true,
                    ThrowIfSymbolsAreNotMatching = true,
                    SymbolReaderProvider = new PortablePdbReaderProvider(),
                    AssemblyResolver = resolver,
                    SymbolStream = PdbStreamFor(assemblyLocation)
                }
            );

            if (!ad.MainModule.HasSymbols)
                throw new Exception("NoSymbols");
            return ad;
        }

        protected TypeDefinition TypeDefinitionFor(Type type, bool useFailResolver = false)
        {
            var ad = AssemblyDefinitionFor(type, useFailResolver);
            var fullName = type.FullName.Replace("+", "/");
            return ad.MainModule.GetType(fullName).Resolve();
        }

        protected TypeDefinition TypeDefinitionFor(string typeName, Type nextToType, bool useFailResolver = false)
        {
            var ad = AssemblyDefinitionFor(nextToType, useFailResolver);
            var fullName = nextToType.FullName.Replace("+", "/");
            fullName = fullName.Replace(nextToType.Name, typeName);
            return ad.MainModule.GetType(fullName).Resolve();
        }

        protected MethodDefinition MethodDefinitionForOnlyMethodOf(Type type, bool useFailResolver = false)
        {
            return MethodDefinitionForOnlyMethodOfDefinition(TypeDefinitionFor(type, useFailResolver));
        }

        protected MethodDefinition MethodDefinitionForOnlyMethodOfDefinition(TypeDefinition typeDefinition)
        {
            var a = typeDefinition.GetMethods().Where(m => !m.IsConstructor && !m.IsStatic && !m.IsCompilerControlled && 
                                                           !m.CustomAttributes.Any(c => c.AttributeType.Name == nameof(CompilerGeneratedAttribute))).ToList();
            return a.Count == 1 ? a.Single() : a.Single(m => m.Name == "Test");
        }

        static MemoryStream PdbStreamFor(string assemblyLocation)
        {
            var file = Path.ChangeExtension(assemblyLocation, ".pdb");
            if (!File.Exists(file))
                return null;
            return new MemoryStream(File.ReadAllBytes(file));
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        protected static T EnsureNotOptimizedAway<T>(T x) { return x; }

        private class OnDemandResolver : IAssemblyResolver
        {
            public void Dispose()
            {
            }

            public AssemblyDefinition Resolve(AssemblyNameReference name)
            {
                return Resolve(name, new ReaderParameters(ReadingMode.Deferred));
            }

            public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == name.Name);
                var fileName = assembly.Location;
                parameters.AssemblyResolver = this;
                parameters.SymbolStream = PdbStreamFor(fileName);
                var bytes = File.ReadAllBytes(fileName);
                return AssemblyDefinition.ReadAssembly(new MemoryStream(bytes), parameters);
            }
        }

        protected abstract void AssertProducesInternal(Type systemType, DiagnosticType type, string[] shouldContains, bool useFailResolver = false);
        
        protected void AssertProducesWarning(Type systemType, params string[] shouldContainErrors)
        {
            AssertProducesInternal(systemType, DiagnosticType.Warning, shouldContainErrors);
        }

        protected void AssertProducesError(Type systemType, params string[] shouldContainErrors)
        {
            AssertProducesInternal(systemType, DiagnosticType.Error, shouldContainErrors);
        }

        protected static void AssertDiagnosticHasSufficientFileAndLineInfo(List<DiagnosticMessage> errors)
        {
            string diagnostic = errors.Select(dm=>dm.MessageData).SeparateByComma();
            if (!diagnostic.Contains(".cs"))
                Assert.Fail("Diagnostic message had no file info: " + diagnostic);

            var match = Regex.Match(diagnostic, "\\.cs:?\\((?<line>.*?),(?<column>.*?)\\)");
            if (!match.Success)
                Assert.Fail("Diagnostic message had no line info: " + diagnostic);

            var line = int.Parse(match.Groups["line"].Value);
            if (line > 2000)
                Assert.Fail("Unreasonable line number in errormessage: " + diagnostic);
        }
    }

    public class LambdaJobsPostProcessorTestBase : PostProcessorTestBase
    {
        protected void AssertProducesNoError(Type systemType)
        {
            Assert.DoesNotThrow(() =>
            {
                var assemblyDefinition = AssemblyDefinitionFor(systemType);
                var testSystemType = assemblyDefinition.MainModule
                    .GetAllTypes()
                    .Where(TypeDefinitionExtensions.IsComponentSystem)
                    .FirstOrDefault(t => t.Name == systemType.Name);

                foreach (var methodToAnalyze in testSystemType.Methods.ToList())
                {
                    foreach (var forEachDescriptionConstruction in LambdaJobDescriptionConstruction.FindIn(methodToAnalyze))
                    {
                        var (jobStructForLambdaJob, diagnosticMessages) = LambdaJobsPostProcessor.Rewrite(methodToAnalyze, forEachDescriptionConstruction);
                        foreach (var diagnosticMessage in diagnosticMessages)
                            Assert.AreNotEqual(DiagnosticType.Error, diagnosticMessage.DiagnosticType);
                    }
                }

                // Write out assembly to memory stream
                // Missing ImportReference errors for types only happens here. 
                var pe = new MemoryStream();
                var pdb = new MemoryStream();
                var writerParameters = new WriterParameters
                {
                    SymbolWriterProvider = new PortablePdbWriterProvider(), SymbolStream = pdb, WriteSymbols = true
                };
                assemblyDefinition.Write(pe, writerParameters);
            });
        }

        protected override void AssertProducesInternal(
            Type systemType, 
            DiagnosticType type, 
            string[] shouldContains,
            bool useFailResolver = false)
        {
            var methodToAnalyze = MethodDefinitionForOnlyMethodOf(systemType);
            var diagnosticMessages = new List<DiagnosticMessage>();

            try
            {
                foreach (var forEachDescriptionConstruction in LambdaJobDescriptionConstruction.FindIn(methodToAnalyze))
                {
                    var (_, rewriteMessages) = LambdaJobsPostProcessor.Rewrite(methodToAnalyze, forEachDescriptionConstruction);
                    diagnosticMessages.AddRange(rewriteMessages);
                }
            }
            catch (FoundErrorInUserCodeException exc)
            {
                diagnosticMessages.AddRange(exc.DiagnosticMessages);
            }

            Assert.AreEqual(1, diagnosticMessages.Count);
            Assert.AreEqual(type, diagnosticMessages[0].DiagnosticType);

            foreach (var str in shouldContains)
                Assert.That(diagnosticMessages[0].MessageData.Contains(str), $"Error message \"{diagnosticMessages[0].MessageData}\" does not contain \"{str}\".");

            AssertDiagnosticHasSufficientFileAndLineInfo(diagnosticMessages);
        }
    }
}