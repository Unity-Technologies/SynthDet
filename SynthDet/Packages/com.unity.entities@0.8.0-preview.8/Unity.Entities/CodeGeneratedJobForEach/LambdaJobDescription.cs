using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.CodeGeneratedJobForEach;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using AllowMultipleInvocationsAttribute = Unity.Entities.LambdaJobDescriptionConstructionMethods.AllowMultipleInvocationsAttribute;

[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public delegate System.Object Invalid_ForEach_Signature_See_ForEach_Documentation_For_Rules_And_Restrictions(System.Object o);

namespace Unity.Entities.CodeGeneratedJobForEach
{
    [AttributeUsage(AttributeTargets.Parameter)]
    internal class AllowDynamicValueAttribute : Attribute
    {
    }

    public interface ILambdaJobDescription
    {
    }
    
    // Deprecated with JobComponentSystem
    public interface ILambdaJobExecutionDescriptionJCS
    {
    }

    public interface ILambdaJobExecutionDescription
    {
    }
    
    public interface ILambdaSingleJobExecutionDescription
    {
    }
    
    // Deprecated with JobComponentSystem
    public interface ILambdaSingleJobExecutionDescriptionJCS
    {
    }
    
    public interface ISupportForEachWithUniversalDelegate
    {
    }
    
    // Deprecate with JobComponentSystem
    public struct ForEachLambdaJobDescriptionJCS : ILambdaJobDescription, ILambdaJobExecutionDescriptionJCS, ISupportForEachWithUniversalDelegate
    {
        //this overload exists here with the sole purpose of being able to give the user a not-totally-horrible
        //experience when they try to use an unsupported lambda signature. When this happens, the C# compiler
        //will go through its overload resolution, take the first candidate, and explain the user why the users
        //lambda is incompatible with that first candidates' parametertype.  We put this method here, instead
        //of with the other .ForEach overloads, to make sure this is the overload that the c# compiler will pick
        //when generating its compiler error.  If we didn't do that, it might pick a completely unrelated .ForEach
        //extention method, like the one for IJobChunk.
        //
        //The only communication channel we have to the user to guide them to figuring out what their problem is
        //is the name of the expected delegate type, as the c# compiler will put that in its compiler error message.
        //so we take this very unconventional approach here of encoding a message for the user in that type name,
        //that is easily googlable, so they will end up at a documentation page that describes why some lambda
        //signatures are compatible, and why some aren't, and what to do about that.
        //
        //the reason the delegate type is in the global namespace, is that it makes for a cleaner error message
        //it's marked with an attribute to prevent it from showing up in intellisense.
        public void ForEach(Invalid_ForEach_Signature_See_ForEach_Documentation_For_Rules_And_Restrictions ed)
        {
        }
    }

    public interface ISingleJobDescription { }

    public struct LambdaSingleJobDescriptionJCS : ILambdaJobDescription, ILambdaSingleJobExecutionDescriptionJCS, ISingleJobDescription
    {
    }

    public struct ForEachLambdaJobDescription : ILambdaJobDescription, ILambdaJobExecutionDescription, ISupportForEachWithUniversalDelegate
    {
        //this overload exists here with the sole purpose of being able to give the user a not-totally-horrible
        //experience when they try to use an unsupported lambda signature. When this happens, the C# compiler
        //will go through its overload resolution, take the first candidate, and explain the user why the users
        //lambda is incompatible with that first candidates' parametertype.  We put this method here, instead
        //of with the other .ForEach overloads, to make sure this is the overload that the c# compiler will pick
        //when generating its compiler error.  If we didn't do that, it might pick a completely unrelated .ForEach
        //extention method, like the one for IJobChunk.
        //
        //The only communication channel we have to the user to guide them to figuring out what their problem is
        //is the name of the expected delegate type, as the c# compiler will put that in its compiler error message.
        //so we take this very unconventional approach here of encoding a message for the user in that type name,
        //that is easily googlable, so they will end up at a documentation page that describes why some lambda
        //signatures are compatible, and why some aren't, and what to do about that.
        //
        //the reason the delegate type is in the global namespace, is that it makes for a cleaner error message
        //it's marked with an attribute to prevent it from showing up in intellisense.
        public void ForEach(Invalid_ForEach_Signature_See_ForEach_Documentation_For_Rules_And_Restrictions ed)
        {
        }
    }
    public struct LambdaSingleJobDescription : ILambdaJobDescription, ILambdaSingleJobExecutionDescription, ISingleJobDescription
    {
    }
    public struct LambdaJobChunkDescription : ILambdaJobDescription, ILambdaJobExecutionDescription
    {
    }
}

namespace Unity.Entities
{
    public static class LambdaJobDescriptionConstructionMethods
    {
        [AttributeUsage(AttributeTargets.Method)]
        internal class AllowMultipleInvocationsAttribute : Attribute { }
        
        public static TDescription WithoutBurst<TDescription>(this TDescription description) where TDescription : ILambdaJobDescription => description;
        
        [Obsolete("To turn off burst, please use .WithoutBurst() instead of .WithBurst(false). (RemovedAfter 2020-04-09)")]
        public static TDescription WithBurst<TDescription>(this TDescription description, bool enabled) where TDescription : ILambdaJobDescription => description;
        
        public static TDescription WithBurst<TDescription>(this TDescription description, FloatMode floatMode = FloatMode.Default, FloatPrecision floatPrecision = FloatPrecision.Standard, bool synchronousCompilation = false) where TDescription : ILambdaJobDescription => description;
        public static TDescription WithName<TDescription>(this TDescription description, string name) where TDescription : ILambdaJobDescription => description;
        public static TDescription WithStructuralChanges<TDescription>(this TDescription description) where TDescription : ILambdaJobDescription => description;
        
        [AllowMultipleInvocations]
        public static TDescription WithReadOnly<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;
        [AllowMultipleInvocations]
        public static TDescription WithDeallocateOnJobCompletion<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;
        [AllowMultipleInvocations]
        public static TDescription WithNativeDisableContainerSafetyRestriction<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;
        [AllowMultipleInvocations]
        public static unsafe TDescription WithNativeDisableUnsafePtrRestriction<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType* capturedVariable) where TDescription : ILambdaJobDescription where TCapturedVariableType : unmanaged => description;
        [Obsolete("Use WithNativeDisableUnsafePtrRestriction instead. (RemovedAfter 2020-04-09)", true)] //<-- remove soon, never shipped, only used in a2-dots-shooter
        public static TDescription WithNativeDisableUnsafePtrRestrictionAttribute<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;
        [AllowMultipleInvocations]
        public static TDescription WithNativeDisableParallelForRestriction<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;
    }

    // Deprecate with JobComponentSystem
    public static class LambdaJobDescriptionExecutionMethodsJCS
    {
        //do not remove this obsolete method. It is not really obsolete, it never existed, but it is created to give a better error message for when you try to use .Schedule() without argument.  Without this method signature,
        //c#'s overload resolution will try to match a completely different Schedule extension method, and explain why that one doesn't work, which results in an error message that sends the user in a wrong direction.
        [Obsolete("You must provide a JobHandle argument to .Schedule(). (DoNotRemove)", true)]
        public static JobHandle Schedule<TDescription>(this TDescription description) where TDescription : ILambdaJobExecutionDescriptionJCS => ThrowCodeGenException();
        
        public static JobHandle Schedule<TDescription>(this TDescription description, [AllowDynamicValue] JobHandle dependency) where TDescription : ILambdaJobExecutionDescriptionJCS => ThrowCodeGenException();
        
        public static void Run<TDescription>(this TDescription description) where TDescription : ILambdaJobExecutionDescriptionJCS => ThrowCodeGenException();

        static JobHandle ThrowCodeGenException() => throw new Exception("This JobComponentSystem method should have been replaced by codegen");
    }
        
    public static class LambdaJobDescriptionExecutionMethods
    {
        public static JobHandle Schedule<TDescription>(this TDescription description, [AllowDynamicValue] JobHandle dependency) where TDescription : ILambdaJobExecutionDescription => ThrowCodeGenException();
        public static JobHandle ScheduleParallel<TDescription>(this TDescription description, [AllowDynamicValue] JobHandle dependency) where TDescription : ILambdaJobExecutionDescription => ThrowCodeGenException();
        
        public static void Schedule<TDescription>(this TDescription description) where TDescription : ILambdaJobExecutionDescription => ThrowCodeGenException();
        public static void ScheduleParallel<TDescription>(this TDescription description) where TDescription : ILambdaJobExecutionDescription => ThrowCodeGenException();
        public static void Run<TDescription>(this TDescription description) where TDescription : ILambdaJobExecutionDescription => ThrowCodeGenException();

        static JobHandle ThrowCodeGenException() => throw new Exception("This SystemBase method should have been replaced by codegen");
    }
    
    public static class LambdaSingleJobDescriptionExecutionMethodsJCS
    {
        public static JobHandle Schedule<TDescription>(this TDescription description, [AllowDynamicValue] JobHandle dependency) where TDescription : ILambdaSingleJobExecutionDescriptionJCS => ThrowCodeGenException();
        
        public static void Run<TDescription>(this TDescription description) where TDescription : ILambdaSingleJobExecutionDescriptionJCS => ThrowCodeGenException();

        static JobHandle ThrowCodeGenException() => throw new Exception("This SystemBase method should have been replaced by codegen");
    }
    
    public static class LambdaSingleJobDescriptionExecutionMethods
    {
        public static JobHandle Schedule<TDescription>(this TDescription description, [AllowDynamicValue] JobHandle dependency) where TDescription : ILambdaSingleJobExecutionDescription => ThrowCodeGenException();
        
        public static void Schedule<TDescription>(this TDescription description) where TDescription : ILambdaSingleJobExecutionDescription => ThrowCodeGenException();
        public static void Run<TDescription>(this TDescription description) where TDescription : ILambdaSingleJobExecutionDescription => ThrowCodeGenException();

        static JobHandle ThrowCodeGenException() => throw new Exception("This SystemBase method should have been replaced by codegen");
    }

    public static class LambdaSingleJobDescriptionConstructionMethods
    {
        public delegate void WithCodeAction();
        public static TDescription WithCode<TDescription>(this TDescription description,  [AllowDynamicValue] WithCodeAction code) 
            where TDescription : ISingleJobDescription => description;
    }
    
    public static class LambdaJobChunkDescriptionConstructionMethods
    {
        public delegate void JobChunkDelegate(ArchetypeChunk chunk, int chunkIndex, int queryIndexOfFirstEntityInChunk);
        public static LambdaJobChunkDescription ForEach(this LambdaJobChunkDescription description,  [AllowDynamicValue] JobChunkDelegate code) => description;
    }
    
    public static class LambdaJobChunkDescription_SetSharedComponent
    {
        public static LambdaJobChunkDescription SetSharedComponentFilterOnQuery<T>(LambdaJobChunkDescription description, T sharedComponent, EntityQuery query) where T : struct, ISharedComponentData
        {
            query.SetSharedComponentFilter(sharedComponent);
            return description;
        }
    }
    
    public static class ForEachLambdaJobDescription_SetSharedComponent
    {
        public static TDescription SetSharedComponentFilterOnQuery<TDescription, T>(this TDescription description, T sharedComponent, EntityQuery query)
            where TDescription : struct, ISupportForEachWithUniversalDelegate
            where T : struct, ISharedComponentData
        {
            query.SetSharedComponentFilter(sharedComponent);
            return description;
        }
    }

    public static class InternalCompilerInterface
    {
        public static JobRunWithoutJobSystemDelegate BurstCompile(JobRunWithoutJobSystemDelegate d) => 
#if !NET_DOTS
            BurstCompiler.CompileFunctionPointer(d).Invoke;
#else
            d;
#endif
        
        public static JobChunkRunWithoutJobSystemDelegate BurstCompile(JobChunkRunWithoutJobSystemDelegate d) =>
#if !NET_DOTS
            BurstCompiler.CompileFunctionPointer(d).Invoke;
#else
            d;
#endif
        
        public unsafe delegate void JobChunkRunWithoutJobSystemDelegate(ArchetypeChunkIterator* iterator, void* job);
        public unsafe delegate void JobRunWithoutJobSystemDelegate(void* job);

#if NET_DOTS && ENABLE_UNITY_COLLECTIONS_CHECKS && !UNITY_DOTSPLAYER_IL2CPP
        // DOTS Runtime always compiles against the .Net Framework which will re-order structs if they contain non-blittable data (unlike mono which
        // will keep structs as Layout.Sequential). However, Burst will always assume a struct layout as if Layout.Sequential was used which presents 
        // a data layout mismatch that must be accounted for. The DOTS Runtime job system handles this problem by marshalling jobData structs already 
        // but in the case where we are calling RunJob/RunJobChunk we bypass the job system data marshalling by executing the bursted static function directly 
        // passing the jobData as a void*. So we must account for this marshalling here. Note we only need to do this when collection checks are on since
        // job structs must be non-blittable for bursting however collection checks add reference types which Burst internally treates as IntPtr which 
        // allows collection checks enabled code to still burst compile.
        struct JobMarshalFnLookup<T> where T : struct, IJobBase
        {
            static IntPtr MarshalToBurstFn;
            static IntPtr MarshalFromBurstFn;

            public static IntPtr GetMarshalToBurstFn()
            {
                if(MarshalToBurstFn == IntPtr.Zero)
                {
                    var job = default(T);
                    var fn = job.GetMarshalToBurstMethod_Gen();
                    // Pin the delegate so it can be used for the lifetime of the app. Never de-alloc
                    var handle = GCHandle.Alloc(fn);
                    MarshalToBurstFn = Marshal.GetFunctionPointerForDelegate(fn);
                }
                return MarshalToBurstFn;
            }

            public static IntPtr GetMarshalFromBurstFn()
            {
                if (MarshalFromBurstFn == IntPtr.Zero)
                {
                    var job = default(T);
                    var fn = job.GetMarshalFromBurstMethod_Gen();
                    // Pin the delegate so it can be used for the lifetime of the app. Never de-alloc
                    var handle = GCHandle.Alloc(fn);
                    MarshalFromBurstFn = Marshal.GetFunctionPointerForDelegate(fn);
                }
                return MarshalFromBurstFn;
            }
        }

        public static unsafe void RunIJob<T>(ref T jobData, JobRunWithoutJobSystemDelegate functionPointer) where T : unmanaged, IJob, IJobBase
        {
            var managedJobDataPtr = UnsafeUtility.AddressOf(ref jobData);
            var unmanagedSize = jobData.GetUnmanagedJobSize_Gen();
            if (unmanagedSize != -1)
            {
                const int kAlignment = 16;
                int alignedSize = (unmanagedSize + kAlignment - 1) & ~(kAlignment - 1);
                byte* unmanagedJobData = stackalloc byte[alignedSize];
                byte* alignedUnmanagedJobData = (byte*)((UInt64)(unmanagedJobData + kAlignment - 1) & ~(UInt64)(kAlignment - 1));

                // DOTS Runtime job marshalling code assumes the job is wrapped so create the wrapper and assign the jobData

                IJobExtensions.JobProducer<T> jobStructData = default;
                jobStructData.JobData = jobData;
                byte* jobStructDataPtr = (byte*)UnsafeUtility.AddressOf(ref jobStructData);

                byte* dst = (byte*)alignedUnmanagedJobData;
                byte* src = (byte*)jobStructDataPtr;
                var marshalToBurstFnPtr = JobMarshalFnLookup<T>.GetMarshalToBurstFn();
                UnsafeUtility.CallFunctionPtr_pp(marshalToBurstFnPtr.ToPointer(), dst, src);

                // In the case of JobStruct we know the jobwrapper doesn't add 
                // anything to the jobData so just pass it along, no offset required unlike JobChunk
                functionPointer(alignedUnmanagedJobData);

                // Since Run can capture locals for write back, we must write back the marshalled jobData after the job executes
                var marshalFromBurstFnPtr = JobMarshalFnLookup<T>.GetMarshalFromBurstFn();
                UnsafeUtility.CallFunctionPtr_pp(marshalFromBurstFnPtr.ToPointer(), src, dst);
                jobData = jobStructData.JobData;
            }
            else
            {
                functionPointer(managedJobDataPtr);
            }
        }

        public static unsafe void RunJobChunk<T>(ref T jobData, EntityQuery query, JobChunkRunWithoutJobSystemDelegate functionPointer) where T : unmanaged, IJobChunk, IJobBase
        {
            var myIterator = query.GetArchetypeChunkIterator();

            try
            {
                query._DependencyManager->IsInForEachDisallowStructuralChange++;

                var managedJobDataPtr = UnsafeUtility.AddressOf(ref jobData);
                var unmanagedSize = jobData.GetUnmanagedJobSize_Gen();
                if (unmanagedSize != -1)
                {
                    const int kAlignment = 16;
                    int alignedSize = (unmanagedSize + kAlignment - 1) & ~(kAlignment - 1);
                    byte* unmanagedJobData = stackalloc byte[alignedSize];
                    byte* alignedUnmanagedJobData = (byte*)((UInt64)(unmanagedJobData + kAlignment - 1) & ~(UInt64)(kAlignment - 1));

                    // DOTS Runtime job marshalling code assumes the job is wrapped so create the wrapper and assign the jobData
                    JobChunkExtensions.JobChunkWrapper<T> jobChunkWrapper = default;
                    jobChunkWrapper.JobData = jobData;
                    byte* jobChunkDataPtr = (byte*)UnsafeUtility.AddressOf(ref jobChunkWrapper);

                    byte* dst = (byte*)alignedUnmanagedJobData;
                    byte* src = (byte*)jobChunkDataPtr;
                    var marshalToBurstFnPtr = JobMarshalFnLookup<T>.GetMarshalToBurstFn();
                    UnsafeUtility.CallFunctionPtr_pp(marshalToBurstFnPtr.ToPointer(), dst, src);

                    // Since we are running inline, normally the outer job scheduling code would
                    // reference jobWrapper.Data however we can't do that since if we are in this code it means
                    // we are dealing with a job/jobwrapper that is burst compiled and is non-blittable. Thus any
                    // type-safe offset we calculate here will be based on the managed data layout which is not useful.
                    // Instead we can at least know that for a sequential layout (which is what we know we must be using
                    // since we are burst compiled) our JobChunkData contains a safety field as its first member. Skipping over this will
                    // provide the necessary offset to jobChunkData.Data
                    var DataOffset = UnsafeUtility.SizeOf<JobChunkExtensions.EntitySafetyHandle>();
                    Assertions.Assert.AreEqual(jobChunkWrapper.safety.GetType(), typeof(JobChunkExtensions.EntitySafetyHandle));
                    functionPointer(&myIterator, alignedUnmanagedJobData + DataOffset);

                    // Since Run can capture locals for write back, we must write back the marshalled jobData after the job executes
                    var marshalFromBurstFnPtr = JobMarshalFnLookup<T>.GetMarshalFromBurstFn();
                    UnsafeUtility.CallFunctionPtr_pp(marshalFromBurstFnPtr.ToPointer(), src, dst);
                    jobData = jobChunkWrapper.JobData;
                }
                else
                {
                    functionPointer(&myIterator, managedJobDataPtr);
                }
            }
            finally
            {
                query._DependencyManager->IsInForEachDisallowStructuralChange--;
            }
        }
#else
        public static unsafe void RunIJob<T>(ref T jobData, JobRunWithoutJobSystemDelegate functionPointer) where T : unmanaged, IJob
        {
            functionPointer(UnsafeUtility.AddressOf(ref jobData));
        }

        public static unsafe void RunJobChunk<T>(ref T jobData, EntityQuery query, JobChunkRunWithoutJobSystemDelegate functionPointer) where T : unmanaged, IJobChunk
        {
            var myIterator = query.GetArchetypeChunkIterator();

            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            try
            {
                query._DependencyManager->IsInForEachDisallowStructuralChange++;
                functionPointer(&myIterator, UnsafeUtility.AddressOf(ref jobData));
            }
            finally
            {
                query._DependencyManager->IsInForEachDisallowStructuralChange--;
            }
            #else
            functionPointer(&myIterator, UnsafeUtility.AddressOf(ref jobData));
            #endif
        }
#endif
    }
}

public static partial class LambdaForEachDescriptionConstructionMethods
{
    static TDescription ThrowCodeGenException<TDescription>() => throw new Exception("This method should have been replaced by codegen");
}