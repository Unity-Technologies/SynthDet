using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Unity.Entities.CodeGen;

namespace Unity.Entities.Hybrid.CodeGen
{ 
/*
 * Input C# code will be in the format of:
 *
 * [GenerateAuthoringComponent]
 * public struct IntBufferElementData : IBufferElementData
 * {
 *     public int Value;
 * }
 * 
 * This code implements the IBufferElementData interface, and also informs the IL post processor that we
 * want a corresponding authoring component generated for us.  Currently, this component must live in its own C# file
 * (due to a limitation with how Unity processes MonoScripts during asset import).  With the GenerateAuthoringComponent attribute,
 * Unity will generate a class that inherits from the following abstract base class:
 *
 * [DisallowMultipleComponent]
 * internal abstract class BufferElementAuthoring<TBufferElementData, TWrappedValue> :
 *     MonoBehaviour, IConvertGameObjectToEntity
 *     where TBufferElementData : struct, IBufferElementData
 *     where TWrappedValue : unmanaged
 * {
 *     public TWrappedValue[] Values;
 * 
 *     public unsafe void Convert(
 *         Entity entity, 
 *         EntityManager destinationManager,
 *         GameObjectConversionSystem _)
 *     {
 *         DynamicBuffer<TBufferElementData> dynamicBuffer = destinationManager.AddBuffer<TBufferElementData>(entity);
 *         dynamicBuffer.ResizeUninitialized(Values.Length);
 * 
 *         if (Values.Length == 0)
 *         {
 *             return;
 *         }
 * 
 *         fixed (void* sourcePtr = &Values[0])
 *         {
 *             UnsafeUtility.MemCpy(
 *                 destination: dynamicBuffer.GetUnsafePtr(),
 *                 sourcePtr,
 *                 size: Values.Length * UnsafeUtility.SizeOf<TBufferElementData>());
 *         }
 *     }
 * }
 * 
 * In our example, the generated derived class will be:
 *
 * internal class IntBufferElementAuthoring : BufferElementAuthoring<IntBufferElement, int>
 * {
 * }
 * 
 * This process occurs through the following steps:
 * 1. Find all types that implement IBufferElementData and have the GenerateAuthoringComponent attribute.
 * 2. For each type found, create a new class inheriting from BufferElementAuthoring, with the type parameters being 
 *    a. the type that implements IBufferElementData and b. the type that it wraps.
 */
    internal partial class AuthoringComponentPostProcessor
    {
        internal static TypeDefinition CreateBufferElementDataAuthoringType(TypeDefinition bufferElementDataType)
        {
            if (!bufferElementDataType.IsValueType())
            {
                UserError.DC0041(bufferElementDataType).Throw();
            }
            
            if (bufferElementDataType.Fields.Count != 1)
            {
                UserError.DC0039(bufferElementDataType, bufferElementDataType.Fields.Count).Throw();
            }

            FieldDefinition field = bufferElementDataType.Fields.Single();

            if (!field.FieldType.IsValueType())
            {
                UserError.DC0040(bufferElementDataType).Throw();
            }

            if ((bufferElementDataType.Attributes & TypeAttributes.ExplicitLayout) != 0)
            {
                UserError.DC0042(bufferElementDataType).Throw();
            }
            
            ModuleDefinition moduleDefinition = bufferElementDataType.Module;

            var authoringType = new TypeDefinition(
                bufferElementDataType.Namespace,
                name: $"{bufferElementDataType.Name}Authoring",
                TypeAttributes.Class)
            {
                Scope = bufferElementDataType.Scope,
                BaseType =
                    moduleDefinition.ImportReference(typeof(BufferElementAuthoring<,>))
                                    .MakeGenericInstanceType(moduleDefinition.ImportReference(bufferElementDataType), field.FieldType)
            };
            moduleDefinition.Types.Add(authoringType);
            
            return authoringType;
        }
    }
}