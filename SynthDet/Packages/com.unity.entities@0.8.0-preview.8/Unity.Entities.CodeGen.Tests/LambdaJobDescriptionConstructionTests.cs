using System;
using System.Linq;
using Mono.Cecil;
using NUnit.Framework;
using Unity.Entities.CodeGen.Tests.LambdaJobs.Infrastructure;
using Unity.Collections;
using Unity.Entities.CodeGeneratedJobForEach;
using Unity.Jobs;

namespace Unity.Entities.CodeGen.Tests
{
    [TestFixture]
    public class LambdaJobDescriptionConstructionTests : LambdaJobsPostProcessorTestBase
    {
        [Test]
        public void EntitiesForEachTest()
        {
            var methodToAnalyze = MethodDefinitionForOnlyMethodOf(typeof(WithForEachSystem));

            var forEachDescriptionConstruction = LambdaJobDescriptionConstruction.FindIn(methodToAnalyze).Single();

            var icm = forEachDescriptionConstruction.InvokedConstructionMethods;

            CollectionAssert.AreEqual(new[]
            {
            nameof(LambdaJobQueryConstructionMethods.WithEntityQueryOptions),
            nameof(LambdaJobDescriptionConstructionMethods.WithBurst),
            nameof(LambdaJobQueryConstructionMethods.WithNone),
            nameof(LambdaJobQueryConstructionMethods.WithChangeFilter),
            nameof(LambdaJobDescriptionConstructionMethods.WithName),
            nameof(LambdaForEachDescriptionConstructionMethods.ForEach),
            }, icm.Select(i => i.MethodName));


            CollectionAssert.AreEqual(new[]
            {
                1,
                3,
                0,
                0,
                1,
                1,
            }, icm.Select(i => i.Arguments.Length));

            Assert.AreEqual("MyJobName", icm[4].Arguments[0]);
            CollectionAssert.AreEqual(new[] {nameof(Translation), nameof(Velocity)},icm[3].TypeArguments.Select(t => t.Name));

            Assert.AreEqual(EntityQueryOptions.IncludePrefab, (EntityQueryOptions) icm[0].Arguments.Single());
        }

        class WithForEachSystem : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                float dt = 2.34f;

                return Entities
                    .WithEntityQueryOptions(EntityQueryOptions.IncludePrefab)
                    .WithBurst(synchronousCompilation:true)
                    .WithNone<Boid>()
                    .WithChangeFilter<Translation, Velocity>()
                    .WithName("MyJobName")
                    .ForEach(
                        (ref Translation translation, ref Boid boid,in Velocity velocity) =>
                        {
                            translation.Value += velocity.Value * dt;
                        })
                    .Schedule(inputDeps);
            }
        }
        
        [Test]
        public void SingleJobTest()
        {
            var methodToAnalyze = MethodDefinitionForOnlyMethodOf(typeof(SingleJobTestSystem));

            var forEachDescriptionConstruction = LambdaJobDescriptionConstruction.FindIn(methodToAnalyze).Single();

            var icm = forEachDescriptionConstruction.InvokedConstructionMethods;

            Assert.AreEqual( LambdaJobDescriptionKind.Job, forEachDescriptionConstruction.Kind);
            
            CollectionAssert.AreEqual(new[]
            {
                nameof(LambdaJobDescriptionConstructionMethods.WithBurst),
                nameof(LambdaJobDescriptionConstructionMethods.WithName),
                nameof(LambdaSingleJobDescriptionConstructionMethods.WithCode),
            }, icm.Select(i => i.MethodName));
            
            CollectionAssert.AreEqual(new[]
            {
                3,
                1,
                1,
            }, icm.Select(i => i.Arguments.Length));

            Assert.AreEqual("MyJobName", icm[1].Arguments[0]);
        }

        class SingleJobTestSystem : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                return Job
                    .WithBurst(synchronousCompilation:true)
                    .WithName("MyJobName")
                    .WithCode(()
                        =>
                        {
                        })
                    .Schedule(inputDeps);
            }
        }
        
#if ENABLE_DOTS_COMPILER_CHUNKS
        [Test]
        public void JobChunkTest()
        {
            var methodToAnalyze = MethodDefinitionForOnlyMethodOf(typeof(JobChunkTestSystem));

            var forEachDescriptionConstruction = LambdaJobDescriptionConstruction.FindIn(methodToAnalyze).Single();

            var icm = forEachDescriptionConstruction.InvokedConstructionMethods;
            Assert.AreEqual( LambdaJobDescriptionKind.Chunk, forEachDescriptionConstruction.Kind);
            CollectionAssert.AreEqual(new[]
            {
                nameof(LambdaJobDescriptionConstructionMethods.WithBurst),
                nameof(LambdaJobDescriptionConstructionMethods.WithName),
                nameof(LambdaJobChunkDescriptionConstructionMethods.ForEach),
            }, icm.Select(i => i.MethodName));
            
            CollectionAssert.AreEqual(new[]
            {
                4,
                1,
                1,
            }, icm.Select(i => i.Arguments.Length));

            Assert.AreEqual("MyJobName", icm[1].Arguments[0]);
        }

        class JobChunkTestSystem : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                return Chunks
                    .WithName("MyJobName")
                    .ForEach(delegate(ArchetypeChunk chunk, int index, int query) {  })
                    .Schedule(inputDeps);
            }
        }
#endif
        
        [Test]
        public void DoesNotCaptureTest()
        {
            var methodToAnalyze = MethodDefinitionForOnlyMethodOf(typeof(WithCodeThatDoesNotCaptureSystem));

            var icm = LambdaJobDescriptionConstruction.FindIn(methodToAnalyze).Single().InvokedConstructionMethods;

            CollectionAssert.AreEqual(new[]
            {
                nameof(LambdaForEachDescriptionConstructionMethods.ForEach),
            }, icm.Select(i => i.MethodName));
        }

        class WithCodeThatDoesNotCaptureSystem : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDependency)
            {
                return Entities
                    .ForEach((ref Velocity e1) => { e1.Value += 1f;})
                    .Schedule(inputDependency);
            }
        }
        
        [Test]
        public void ControlFlowInsideWithChainTest()
        {
            AssertProducesError(typeof(ControlFlowInsideWithChainSystem), nameof(UserError.DC0010));
        }

        public class ControlFlowInsideWithChainSystem : JobComponentSystem
        {
            public bool maybe;

            protected override JobHandle OnUpdate(JobHandle inputDependencies)
            {
                return Entities
                    .WithName(maybe ? "One" : "Two")
                    .ForEach(
                        (ref Translation translation, ref Boid boid, in Velocity velocity) =>
                        {
                            translation.Value += velocity.Value;
                        })
                    .Schedule(inputDependencies);
            }
        }

        [Test]
        public void UsingConstructionMultipleTimesThrows()
        {
            AssertProducesError(typeof(UseConstructionMethodMultipleTimes), nameof(UserError.DC0009), "WithName");
        }
        
        public class UseConstructionMethodMultipleTimes : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                return Entities
                    .WithName("Cannot")
                    .WithName("Make up my mind")
                    .ForEach(
                        (ref Translation translation, ref Boid boid, in Velocity velocity) =>
                        {
                            translation.Value += velocity.Value;
                        })
                    .Schedule(inputDeps);
            }
        }

        [Test]
        public void InvalidJobNamesThrow()
        {
            AssertProducesError(typeof(InvalidJobNameWithSpaces), nameof(UserError.DC0043), "WithName");
            AssertProducesError(typeof(InvalidJobNameStartsWithDigit), nameof(UserError.DC0043), "WithName");
            AssertProducesError(typeof(InvalidJobNameCompilerReservedName), nameof(UserError.DC0043), "WithName");
        }

        public class InvalidJobNameWithSpaces : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                return Entities
                    .WithName("This name may not contain spaces")
                    .ForEach(
                        (ref Translation translation, ref Boid boid, in Velocity velocity) =>
                        {
                            translation.Value += velocity.Value;
                        })
                    .Schedule(inputDeps);
            }
        }
        
        public class InvalidJobNameStartsWithDigit : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                return Entities
                    .WithName("1job")
                    .ForEach(
                        (ref Translation translation, ref Boid boid, in Velocity velocity) =>
                        {
                            translation.Value += velocity.Value;
                        })
                    .Schedule(inputDeps);
            }
        }
        
        public class InvalidJobNameCompilerReservedName : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                return Entities
                    .WithName("__job")
                    .ForEach(
                        (ref Translation translation, ref Boid boid, in Velocity velocity) =>
                        {
                            translation.Value += velocity.Value;
                        })
                    .Schedule(inputDeps);
            }
        }

        [Test]
        public void ForgotToAddForEachTest()
        {
            AssertProducesError(typeof(ForgotToAddForEach), nameof(UserError.DC0006));
        }
        
        class ForgotToAddForEach : TestJobComponentSystem
        {
            void Test()
            {
                Entities
                    .WithAny<Translation>()
                    .Schedule(default);
            }
        }
        
        [Test]
        public void WithReadOnlyCapturedVariableTest()
        {
            var methodToAnalyze = MethodDefinitionForOnlyMethodOf(typeof(WithReadOnlyCapturedVariable));
            var description = LambdaJobDescriptionConstruction.FindIn(methodToAnalyze).Single();
            var withReadOnly = description.InvokedConstructionMethods.Single(m => m.MethodName == nameof(LambdaJobDescriptionConstructionMethods.WithReadOnly));
            Assert.IsInstanceOf<FieldDefinition>(withReadOnly.Arguments.Single());
        }
        
        class WithReadOnlyCapturedVariable : TestJobComponentSystem
        {
            void Test()
            {
                NativeArray<int> myarray = new NativeArray<int>();
                
                Entities
                    .WithReadOnly(myarray)
                    .ForEach((ref Translation translation) => translation.Value += myarray[0])
                    .Schedule(default);
            }
        }
        
        [Test]
        public void WithReadOnlyCapturedVariableFromTwoScopesTest()
        {
            var methodToAnalyze = MethodDefinitionForOnlyMethodOf(typeof(WithReadOnlyCapturedVariableFromTwoScopes));
            var description = LambdaJobDescriptionConstruction.FindIn(methodToAnalyze).Single();

            var withReadOnlyMethods = description.InvokedConstructionMethods.Where(m => m.MethodName == nameof(LambdaJobDescriptionConstructionMethods.WithReadOnly));
            Assert.AreEqual(2, withReadOnlyMethods.Count());
            foreach (var withReadOnly in withReadOnlyMethods)
                Assert.IsInstanceOf<FieldDefinition>(withReadOnly.Arguments.Single());
        }

        class WithReadOnlyCapturedVariableFromTwoScopes : TestJobComponentSystem
        {
            void Test()
            {
                NativeArray<int> outerScopeArray = new NativeArray<int>();
                {
                    NativeArray<int> innerScopeArray = new NativeArray<int>();
                    Entities
                        .WithReadOnly(outerScopeArray)
                        .WithReadOnly(innerScopeArray)
                        .ForEach((ref Translation translation) => translation.Value += outerScopeArray[0] + innerScopeArray[0])
                        .Schedule(default);
                }
            }
        }

        [Test]
        public void WithoutScheduleInvocationTest()
        {
            AssertProducesError(typeof(WithoutScheduleInvocation), nameof(UserError.DC0011));
        }

        public class WithoutScheduleInvocation : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                Entities.ForEach(
                        (ref Translation translation, ref Boid boid, in Velocity velocity) =>
                        {
                            translation.Value += velocity.Value;
                        });
                return default;
            }
        }

        [Test]
        public void RunInsideLoopCapturingLoopConditionTest()
        {
            var methodToAnalyze = MethodDefinitionForOnlyMethodOf(typeof(RunInsideLoopCapturingLoopCondition));

            LambdaJobDescriptionConstruction.FindIn(methodToAnalyze).Single();
        }

        public class RunInsideLoopCapturingLoopCondition : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                int variable = 10;
                for (int i = 0; i != variable; i++)
                {
                    Entities
                        .ForEach((ref Translation e1) => { e1.Value += variable; })
                        .Run();
                }

                return default;
            }
        }
        
        [Test]
        public void WithLambdaStoredInFieldTest()
        {
            AssertProducesError(typeof(WithLambdaStoredInFieldSystem), nameof(UserError.DC0044));
        }

        public class WithLambdaStoredInFieldSystem : JobComponentSystem
        {
            UniversalDelegates.R<Translation> _translationAction;
            
            protected override JobHandle OnUpdate(JobHandle inputDependencies)
            {
                _translationAction = (ref Translation t) => { };
                return Entities.ForEach(_translationAction).Schedule(inputDependencies);
            }
        }
        
        [Test]
        public void WithLambdaStoredInVariableTest()
        {
            AssertProducesError(typeof(WithLambdaStoredInVariableSystem), nameof(UserError.DC0044));
        }

        public class WithLambdaStoredInVariableSystem : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDependencies)
            {
                UniversalDelegates.R<Translation> translationAction = (ref Translation t) => { };
                return Entities.ForEach(translationAction).Schedule(inputDependencies);
            }
        }
        
        [Test]
        public void WithLambdaStoredInArgTest()
        {
            AssertProducesError(typeof(WithLambdaStoredInArgSystem), nameof(UserError.DC0044));
        }

        public class WithLambdaStoredInArgSystem : JobComponentSystem
        {
            JobHandle Test(UniversalDelegates.R<Translation> action)
            {
                return Entities.ForEach(action).Schedule(default);
            }
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                return Test((ref Translation t) => { });
            }
        }
        
        [Test]
        public void WithLambdaReturnedFromMethodTest()
        {
            AssertProducesError(typeof(WithLambdaReturnedFromMethodSystem), nameof(UserError.DC0044));
        }

        public class WithLambdaReturnedFromMethodSystem : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                return Entities.ForEach(GetAction()).Schedule(default);
            }
            
            static UniversalDelegates.R<Translation> GetAction()
            {
                return (ref Translation t) => { };
            }
        }
        
        [Test]
        public void WithGetComponentAndCaptureOfThisTest()
        {
            AssertProducesError(typeof(WithGetComponentAndCaptureOfThis), nameof(UserError.DC0001), "someField");
        }

        public class WithGetComponentAndCaptureOfThis : SystemBase
        {
            float someField = 3.0f;
            
            protected override void OnUpdate()
            {
                Entities
                    .ForEach(
                        (ref Translation translation) =>
                        {
                            var vel = GetComponent<Velocity>(default);
                            translation = new Translation() {Value = someField * vel.Value};
                        })
                    .Schedule();
            }
        }
        
        [Test]
        public void WithGetComponentAndCaptureOfThisAndVarTest()
        {
            AssertProducesError(typeof(WithGetComponentAndCaptureOfThisAndVar), nameof(UserError.DC0001), "someField");
        }

        public class WithGetComponentAndCaptureOfThisAndVar : SystemBase
        {
            float someField = 3.0f;
            
            protected override void OnUpdate()
            {
                float someVar = 2.0f;
                Entities
                    .ForEach(
                        (ref Translation translation) =>
                        {
                            var vel = GetComponent<Velocity>(default);
                            translation = new Translation() {Value = someField * vel.Value * someVar};
                        })
                    .Schedule();
            }
        }
        
        [Test]
        public void GetComponentWithConditionTest()
        {
            AssertProducesError(typeof(GetComponentWithCondition), nameof(UserError.DC0045), "GetComponent");
        }
        
        public class GetComponentWithCondition : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities.ForEach((Entity entity, ref Translation tde) =>
                {
                    Entity e1 = default, e2 = default;
                    tde.Value += GetComponent<Velocity>(tde.Value > 1 ? e1 : e2).Value;
                }).Schedule();
            }
        }
        
        [Test]
        public void SetComponentWithPermittedAliasTest()
        {
            AssertProducesNoError(typeof(SetComponentWithPermittedAlias));
        }
        
        public class SetComponentWithPermittedAlias : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities.ForEach((Entity e, in Translation data) => {
                    GetComponent<Translation>(e);
                }).Run();
            }
        }
        
        [Test]
        public void SetComponentWithNotPermittedParameterThatAliasesTestTest()
        {
            AssertProducesError(typeof(SetComponentWithNotPermittedParameterThatAliasesTest), nameof(UserError.DC0047), "Translation");
        }
        
        public class SetComponentWithNotPermittedParameterThatAliasesTest : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities.ForEach((Entity e, ref Translation data) => {
                    var translation = GetComponent<Translation>(e);
                }).Run();
            }
        }
        
        [Test]
        public void SetComponentWithNotPermittedComponentAccessThatAliasesTest()
        {
            AssertProducesError(typeof(SetComponentWithNotPermittedComponentAccessThatAliases), nameof(UserError.DC0046), "SetComponent");
        }
        
        public class SetComponentWithNotPermittedComponentAccessThatAliases : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities.ForEach((Entity e, in Translation data) => {
                    SetComponent(e, new Translation());
                }).Run();
            }
        }
    }
}
