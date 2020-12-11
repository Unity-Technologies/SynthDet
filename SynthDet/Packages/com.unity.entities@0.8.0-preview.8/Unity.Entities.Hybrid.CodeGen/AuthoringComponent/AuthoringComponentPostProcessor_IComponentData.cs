using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.Entities.CodeGen;
using UnityEngine;

namespace Unity.Entities.Hybrid.CodeGen
{
    /*
 * Input C# code will be in the format of:
 *
 * using System;
 * using Unity.Entities;
 *     
 * [GenerateAuthoringComponent]
 * public struct BasicComponent : IComponentData
 * {
 *     public float RadiansPerSecond;
 * }
 * 
 * This code defines a standard Component, the difference being that it also informs the IL post processor that we
 * want a corresponding authoring component generated for us.  Currently, this component must live in its own C# file
 * (due to a limitation with how Unity processes MonoScripts during asset import).  With GenerateAuthoringComponent attribute
 * Unity will generate the following MonoBehaviour with a Convert method:
 * 
 * internal class BasicComponentAuthoring : MonoBehaviour, IConvertGameObjectToEntity
 * {
 *      public float RadiansPerSecond;
 * 
 *      public sealed override void Convert(Entity entity, EntityManager destinationManager, GameObjectConversionSystem conversionSystem)
 *      {
 *          BasicComponent componentData = default(BasicComponent);
 *          componentData.RadiansPerSecond = RadiansPerSecond;
 *      destinationManager.AddComponentData(entity, componentData);
 *      }
 * }
 * 
 * 
 * This process occurs through the following steps:
 * 1. Find all types that inherit from IComponentData and have the GenerateAuthoringComponent attribute.
 * 2. For each type found:
 *     1. Create a new authoring MonoBehaviour type that inherits from MonoBehaviour.
 *     2. For each field found in our component type, create a field in our authoring type.
 * 3. Create a convert method in our authoring component and inside:
 *     1. Initialize a local component type variable.
 *     2. Transfer every field in the authoring component over to the corresponding field in the component.
 *     3. Add a call to EntityManager.AddComponentData(entity, component).
 *
 * This also handles referenced GameObjects and adds those as referenced prefabs (by implementing the
 * IDeclareReferencedPrefabs interface in the authoring component).  GameObject fields are transformed
 * into entity fields that reference the primary entity of the referenced GameObject.
 */

    internal partial class AuthoringComponentPostProcessor
    {
        static (FieldDefinition authoringFieldDefinition, bool referencesPrefabs)
            CreateAuthoringFieldDefinitionForComponentDataField(TypeDefinition componentDataType, FieldDefinition componentDataField)
        {
            var moduleDefinition = componentDataType.Module;
            var entityTypeReference = moduleDefinition.ImportReference(typeof(Entity));
            var gameObjectTypeReference = moduleDefinition.ImportReference(typeof(GameObject));
           
            FieldDefinition authoringFieldDefinition;
            bool referencesPrefabs = false;
                    
            // If we have an entity reference we should expose this as a GameObject and add it to referenced prefabs
            if (componentDataField.FieldType.TypeReferenceEquals(entityTypeReference))
            {
                authoringFieldDefinition =
                    new FieldDefinition(componentDataField.Name, componentDataField.Attributes, gameObjectTypeReference);
                referencesPrefabs = true;
            }
            // Do the same thing for arrays of entities
            else if (componentDataField.FieldType.IsArray 
                     && componentDataField.FieldType.GetElementType().TypeReferenceEquals(entityTypeReference))
            {
                authoringFieldDefinition = 
                    new FieldDefinition(
                        componentDataField.Name,
                        componentDataField.Attributes,
                        moduleDefinition.ImportReference(gameObjectTypeReference.MakeArrayType()));
                
                referencesPrefabs = true;
            }
            else
            {
                authoringFieldDefinition = 
                    new FieldDefinition(componentDataField.Name, componentDataField.Attributes, componentDataField.FieldType);
            }

            if (componentDataField.HasCustomAttributes)
            {
                foreach (var attribute in componentDataField.CustomAttributes)
                {
                    authoringFieldDefinition.CustomAttributes.Add(attribute);
                }
            }
            
            return (authoringFieldDefinition, referencesPrefabs);
        }

        internal static TypeDefinition CreateComponentDataAuthoringType(TypeDefinition componentDataType)
        {
            ModuleDefinition moduleDefinition = componentDataType.Module;
            string authoringTypeNameSpace = componentDataType.Namespace;
            
            var authoringType =
                new TypeDefinition(
                    authoringTypeNameSpace,
                    componentDataType.Name + "Authoring",
                    TypeAttributes.Class)
                {
                    Scope = componentDataType.Scope,
                    BaseType = moduleDefinition.ImportReference(typeof(UnityEngine.MonoBehaviour))
                };
            
            authoringType.Interfaces.Add(new InterfaceImplementation(moduleDefinition.ImportReference(typeof(IConvertGameObjectToEntity))));

            var dotsCompilerGeneratedAttribute = 
                moduleDefinition.ImportReference(typeof(DOTSCompilerGeneratedAttribute).GetConstructors().Single());
            authoringType.CustomAttributes.Add(new CustomAttribute(dotsCompilerGeneratedAttribute));
            
            var DisallowMultipleComponentsAttribute = 
                moduleDefinition.ImportReference(typeof(UnityEngine.DisallowMultipleComponent).GetConstructors().Single(c=>!c.GetParameters().Any()));
            authoringType.CustomAttributes.Add(new CustomAttribute(DisallowMultipleComponentsAttribute));

            var dataFieldsToAuthoringFields = new Dictionary<FieldDefinition, FieldDefinition>();

            bool hasReferencedPrefabs = false;
            
            foreach (var field in componentDataType.Fields.Where(f => !f.IsStatic && f.IsPublic && !f.IsPrivate))
            {
                var (authoringFieldDefinition, referencesPrefabs) =
                    CreateAuthoringFieldDefinitionForComponentDataField(componentDataType, field);
                
                authoringType.Fields.Add(authoringFieldDefinition);

                if (referencesPrefabs)
                {
                    hasReferencedPrefabs = true;
                }
                dataFieldsToAuthoringFields.Add(field, authoringFieldDefinition);
            }

            CreateConvertMethodForComponentDataTypes(authoringType, componentDataType);
            
            if (hasReferencedPrefabs)
            {
                authoringType.Interfaces.Add(
                    new InterfaceImplementation(moduleDefinition.ImportReference(typeof(IDeclareReferencedPrefabs))));
                CreateDeclareReferencedPrefabsMethod(authoringType, componentDataType);
            }
            componentDataType.Module.Types.Add(authoringType);
            
            return authoringType;
        }

        static void CreateDeclareReferencedPrefabsMethod(TypeDefinition authoringType, TypeDefinition componentDataType)
        {
            var interfaceMethodInfo = typeof(IDeclareReferencedPrefabs).GetMethod(nameof(IDeclareReferencedPrefabs.DeclareReferencedPrefabs));
            var declareMethod = CecilHelpers.AddMethodImplementingInterfaceMethod(componentDataType.Module, authoringType, interfaceMethodInfo);
            var gameObjectTypeReference = componentDataType.Module.ImportReference(typeof(UnityEngine.GameObject));
            
            var ilProcessor = declareMethod.Body.GetILProcessor();
            var addGameObjectToListMethod = componentDataType.Module.ImportReference(typeof(List<GameObject>).GetMethod("Add"));
            var addRangeOfGameObjectsToListMethod = componentDataType.Module.ImportReference(typeof(List<GameObject>).GetMethod("AddRange"));
            
            void EmitILToAddGameObjectFieldToReferencedPrefabsList(FieldDefinition field, bool isFieldArray)
            {
                ilProcessor.Emit(OpCodes.Ldarg_1);                             // referencedPrefabs (List<GameObject>)
                ilProcessor.Emit(OpCodes.Ldarg_0);                             // this (our MB)
                ilProcessor.Emit(OpCodes.Ldfld, field);                        // load prefab field from MB
                ilProcessor.Emit(OpCodes.Callvirt, isFieldArray ? addRangeOfGameObjectsToListMethod : addGameObjectToListMethod); // call List<GameObject>.Add
            }

            // Let's add every element that is a GameObject
            foreach (var field in authoringType.Fields.Where(f => f.FieldType.TypeReferenceEquals(gameObjectTypeReference)))
                EmitILToAddGameObjectFieldToReferencedPrefabsList(field, false);
            
            // Also add elements that are an array of GameObject (use helper method for this)
            foreach (var field in authoringType.Fields.Where(f => f.FieldType.IsArray && f.FieldType.GetElementType().TypeReferenceEquals(gameObjectTypeReference)))
                EmitILToAddGameObjectFieldToReferencedPrefabsList(field, true);

            ilProcessor.Emit(OpCodes.Ret);
        }

        static void CreateConvertMethodForComponentDataTypes(TypeDefinition authoringType, TypeDefinition componentDataType)
        {
            var moduleDefinition = componentDataType.Module;
            var entityTypeReference = moduleDefinition.ImportReference(typeof(Unity.Entities.Entity));
            var entityManagerTypeReference = moduleDefinition.ImportReference(typeof(Unity.Entities.EntityManager));
            var gameObjectTypeReference = moduleDefinition.ImportReference(typeof(UnityEngine.GameObject));
            
            MethodDefinition convertMethod = CreateEmptyConvertMethod(moduleDefinition, authoringType);

            // Make a local variable which we'll populate with the values stored in the MonoBehaviour
            var variableDefinition = new VariableDefinition(componentDataType);
            convertMethod.Body.Variables.Add(variableDefinition);

            var ilProcessor = convertMethod.Body.GetILProcessor();

            // Initialize the local variable.  (we might not need this, but all c# compilers emit it, so let's play it safe for now)
            if (componentDataType.IsValueType())
            {
                ilProcessor.Emit(OpCodes.Ldloca, variableDefinition);
                ilProcessor.Emit(OpCodes.Initobj, componentDataType);
            }
            else
            {
                var componentDataConstructor = componentDataType.GetConstructors().FirstOrDefault(c => c.Parameters.Count == 0);
                if (componentDataConstructor == null)
                    UserError.DC0030(componentDataType).Throw();
                ilProcessor.Emit(OpCodes.Newobj, moduleDefinition.ImportReference(componentDataConstructor));
                ilProcessor.Emit(OpCodes.Stloc, variableDefinition);
            }

            var getPrimaryEntityGameObjectMethod = moduleDefinition.ImportReference(
                typeof(GameObjectConversionSystem).GetMethod("GetPrimaryEntity", new Type[] { typeof(UnityEngine.GameObject) }));
            var getPrimaryEntityComponentMethod = moduleDefinition.ImportReference(
                typeof(GameObjectConversionSystem).GetMethod("GetPrimaryEntity", new Type[] { typeof(UnityEngine.Component) }));
            var convertGameObjectsToEntitiesFieldMethod = moduleDefinition.ImportReference(
                typeof(GameObjectConversionUtility).GetMethod(nameof(GameObjectConversionUtility.ConvertGameObjectsToEntitiesField)));

            // Let's transfer every field in the MonoBehaviour over to the corresponding field in the IComponentData
            foreach (var field in authoringType.Fields)
            {
                var destinationField = componentDataType.Fields.Single(f => f.Name == field.Name);

                // Special case when destination field is a array of entities, we need to emit IL to create our destination array
                // and convert from array of GameObjects
                // https://sharplab.io/#v2:C4LglgNgNAJiDUAfAAgJgIwFgBQyAMABMugCwDcOOyAzEagQKIB2wYwAngQN4C+VtaAgHEAhgFsApgHkARgCsJAY2Dc+uAfWasOAWRFMRAcwkAnVfzrDx0+UuABhAPZMAbqYDOYZ8QI4uOAkCiWi02TiEJYAAFEzAxERN2UI4AClFJWQVlAkNHAEoAoP9sINKiAHYCJgBXCAgyIMLAtTULQQBJJzEAB2cJFgAREWARc3VLACVHEdZnAGVuiQkYAH0AMUcTBhFFAAsCEAJOxx6+weGRPybgggAzCEdhggmRGDB9dyjTOaVnGAoSkEaIwWGEANoAXQI/W0YAk7gBrXGxAAbJYABISCCLEzuK6AwLA1FEEgEJyuUzAdI2LLAdwAFUcyTh7g2JgAgtVgLtNmAmIY0tZMnZyW5cV4mD5FM4xZ5vOgoFYMrZlJCckKVXTFSYJLcQdp2GqYWwWQUCdxrqVjax4QQALxVCQAd314MMGtp7gAdAAZfqGbkQgFlIK3TYEFJ8lRgO14BpgAA81pZvv93Ia8HgYDNIZDyfhYLAUId0op4vlXoi0Vi8USyXYKXdys9hYheWDQTUpWuRPQaOQpPZMBg7JMJhE7Ckt2pwuU7nZ7gmutM/UUyxiupEMncgubdjVTZpdncip9YHcwATM81AD4CDrbiumGuYBvblv3DmipbQ5sJDt9hSFwEnVPdsj5UCjznL8Q2KXMygfJ8XzfD8vSHGBGw9Ox2x/Zpri7RpzV7ftB2HLpeiYGEhhGFJuXPV1dH0IxTAIGB4VYAxZiYPQDGMExFXraFQQ4RUphmCUFiWVY2W2PYCGlU5KJYGCLXNFpKGRegxOGCTFmWdZNlk3ZOW5Xl+XxUpgXuR4VBeN4Pi+EwflLf4e1oa9aSNYSWQ7QI3JJMkZUpFJBOtdgBOE9geOYsw2IvPkdOcaK+MVDyRSC8tJXQeSMrlLKzQASGKAqCu0rjJP0mSAPvaZEqYCrpMM6qHUol0yt0qSDK2ACUhw7ASpMWryr0xrur2L07PeJhPm+X4mBge1nleKaZqcubXPNUpMWxDwvVFSk0rnRlmXhNkTJ5WJ+RSUtZQlYhFXzE9711GrxPmEauqMr1Hr6krtpxb10PIs5gGokQUjiji6uS0wHsi7Uho6yqmr2X6kQKolSQGJQIASCQl0fHVn3XB8PxSM8LyvLDlDvRCieQ0nt0KuDc3+3b0NHcdJ2nam6QXAmkJJzdtxSR7tWXemhffJmAQK9TASAA===
                if (destinationField.FieldType.IsArray && destinationField.FieldType.GetElementType().TypeReferenceEquals(entityTypeReference))
                {
                    ilProcessor.Emit(OpCodes.Ldarg_3);
                    ilProcessor.Emit(OpCodes.Ldarg_0);
                    ilProcessor.Emit(OpCodes.Ldfld, field);
                    ilProcessor.Emit(OpCodes.Ldloc, variableDefinition);
                    ilProcessor.Emit(OpCodes.Ldflda, destinationField);
                    ilProcessor.Emit(OpCodes.Callvirt, convertGameObjectsToEntitiesFieldMethod);
                }
                else
                {
                    // Load the local iComponentData we are populating, so we can later write to it
                    ilProcessor.Emit(componentDataType.IsValueType() ? OpCodes.Ldloca : OpCodes.Ldloc, variableDefinition);

                    // Special case when destination is an entity and we our converting from a GameObject
                    if (destinationField.FieldType.TypeReferenceEquals(entityTypeReference))
                    {
                        ilProcessor.Emit(OpCodes.Ldarg_3);
                        ilProcessor.Emit(OpCodes.Ldarg_0);
                        ilProcessor.Emit(OpCodes.Ldfld, field);

                        var methodToCall = field.FieldType.TypeReferenceEquals(gameObjectTypeReference)
                            ? getPrimaryEntityGameObjectMethod
                            : getPrimaryEntityComponentMethod;
                        ilProcessor.Emit(OpCodes.Callvirt, methodToCall);
                    }
                    else
                    {
                        ilProcessor.Emit(OpCodes.Ldarg_0);
                        ilProcessor.Emit(OpCodes.Ldfld, field);
                    }
                    
                    // Store it to the IComponentData we already placed on the stack
                    ilProcessor.Emit(OpCodes.Stfld, destinationField);
                }
            }

            // Now that our local IComponentData is properly setup, the only thing left for us is to call:
            // entityManager.AddComponentData(entity, myPopulatedIComponentData). 
            // IL method arguments go on the stack from first to last so:
            ilProcessor.Emit(OpCodes.Ldarg_2); //entityManager
            ilProcessor.Emit(OpCodes.Ldarg_1); //entity
            ilProcessor.Emit(OpCodes.Ldloc_0); //myPopulatedIComponentData

            // Build a MethodReference to EntityManager.AddComponentData
            MethodReference addComponentDataMethodReference = null;
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            // For managed components this is void EntityManagerManagedComponentExtensions.AddComponentData<T>(this EntityMananger mananger, Entity target, T payload);
            var entityManagerManagedComponentExtensionsTypeReference = moduleDefinition.ImportReference(typeof(Unity.Entities.EntityManagerManagedComponentExtensions));
            if (!componentDataType.IsValueType())
            {
                addComponentDataMethodReference =
                    new MethodReference("AddComponentData", moduleDefinition.TypeSystem.Void, entityManagerManagedComponentExtensionsTypeReference)
                    {
                        Parameters =
                        {
                            new ParameterDefinition("manager", ParameterAttributes.None, entityManagerTypeReference),
                            new ParameterDefinition("entity", ParameterAttributes.None, entityTypeReference),
                        },
                        ReturnType = moduleDefinition.TypeSystem.Void
                    };
            }
#endif
            // For non-managed components this is EntityManager.AddComponentData<T>(Entity target, T payload);
            if (componentDataType.IsValueType())
            {
                addComponentDataMethodReference =
                    new MethodReference("AddComponentData", moduleDefinition.TypeSystem.Void, entityManagerTypeReference)
                    {   
                        HasThis = true,
                        Parameters =
                        {
                            new ParameterDefinition("entity", ParameterAttributes.None, entityTypeReference),
                        },
                        ReturnType = moduleDefinition.TypeSystem.Boolean
                    };
            }

            var genericParameter = new GenericParameter("T", addComponentDataMethodReference);
            addComponentDataMethodReference.GenericParameters.Add(genericParameter);
            addComponentDataMethodReference.Parameters.Add(new ParameterDefinition("payload", ParameterAttributes.None, genericParameter));

            // Since AddComponentData<T> is a generic method, we cannot call it super easily.  
            // We have to wrap the generic method reference into a GenericInstanceMethod, 
            // which let's us specify what we want to use for T for this specific invocation. 
            // In our case T is the IComponentData we're operating on
            var genericInstanceMethod = new GenericInstanceMethod(addComponentDataMethodReference)
            {
                GenericArguments = {componentDataType},
            };
            ilProcessor.Emit(OpCodes.Callvirt, genericInstanceMethod);
            
            // Pop off return value since AddComponentData returns a bool (managed AddComponentData strangely does not however)
            if (componentDataType.IsValueType())
                ilProcessor.Emit(OpCodes.Pop);

            // We're done already!  Easy peasy.
            ilProcessor.Emit(OpCodes.Ret);
            
            // Cecil has a bug where it will not emit a method debug information table if no methods have debug information.
            // This causes a crash in Mono in the rare case where the only methods emitted into a module are from Cecil.
            convertMethod.DebugInformation.Scope = new ScopeDebugInformation(convertMethod.Body.Instructions.First(), convertMethod.Body.Instructions.Last());
        }
    }
}