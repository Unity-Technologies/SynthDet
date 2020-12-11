#if UNITY_DOTSPLAYER
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using static Unity.Entities.CodeGen.ILHelper;
using static Unity.Entities.TypeManager;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;
using TypeGenInfoList = System.Collections.Generic.List<Unity.Entities.CodeGen.StaticTypeRegistryPostProcessor.TypeGenInfo>;
using SystemList = System.Collections.Generic.List<Mono.Cecil.TypeReference>;

namespace Unity.Entities.CodeGen
{
    /// <summary>
    /// This PostProcessor will generate type information for all types inheriting from a component type interface such
    /// as IComponentData, as well as generate the appropriate type information for ComponentSystems for all types
    /// with ComponentSystemBase in their type hierarchy. This information is then collected into a TypeRegistry class
    /// and injected into the assembly. At boot each TypeRegistry generated will be read to register all ECS type
    /// information upfront.
    ///
    /// TypeRegistry types in essence contain the TypeInfo struct that will be consumed at runtime, however some
    /// information in TypeInfo cannot be fully determined until we have all other assemblies' TypeRegistry instances.
    /// (e.g. runtime type indicies cannot be resolved until a TypeRegistry has been registered, and a type's WriteGroup
    /// list, which uses typeIndices, requires global knowledge about all the types declaring an dependency on a given
    /// component type, which again can't be resolved until we globally register TypeRegistry instances). As such, a
    /// TypeRegistry instance contains extra information to be used during registration type as well as at runtime to
    /// allow per assembly type information to be provided. Such information includes managed type information such as
    /// the array of `Type`s each TypeInfo is for, but as well as generated functions (and the list of delegates for
    /// said functions) to handle the case when we need to perform a GetHashCode(object obj) or
    /// Equals (object obj, void* someComponent)
    ///
    /// The processor will take the following flow:
    /// - Find all relevant types (component and system types. We look for both at the same time
    ///     to avoid scanning all types in the assembly more often than we need to
    /// - For found types, generate the appropriate type info and inject it into a TypeRegistry struct
    ///     - For Types this means:
    ///         - TypeInfo fields like alignment, size in chunk, actual type size, type category, entity offsets etc...
    ///         - Generate equality functions for each component. For pure value types this points to XXHash32 but for
    ///             managed types we need to generate a function specific to the type.
    ///         - Debug information such as the TypeName as that isn't available in DOTSRuntime dynamically
    ///     - For Systems this means:
    ///         - System info such as it's attributes for schedule order
    ///         - Sorting systems based on their attribute order
    ///         - Debug information such as SystemName as this isn't available in DOTSRuntime dynamically
    /// - For DOTSRuntime we support compiletime fieldoffsets via the GetFieldInfo API. As such we scan all methods
    ///     for calls to GetFieldInfo() and patch in the correct FieldInfo information
    ///
    /// For DOTSRuntime, we still need the runtime to find all generated TypeRegistry instances for all assemblies and
    /// register them with the TypeManager at runtime. We would do this via ModuleInitializers however there is no
    /// support for those in il2cpp yet. So instead this registration is done elsewhere via TypeRegGen (part of the
    /// DOTS Runtime compilation pipeline). 
    /// </summary>
    internal partial class StaticTypeRegistryPostProcessor : EntitiesILPostProcessor
    {
#if UNITY_DOTSPLAYER64
        const int kArchBits = 64;
#else
        const int kArchBits = 32;
#endif

        TypeReference m_SystemTypeRef;
        TypeReference m_TypeInfoRef;
        MethodReference m_TypeInfoConstructorRef;
        MethodReference m_GetTypeFromHandleFnRef;
        MethodReference m_MemCmpFnRef;
        MethodReference m_MemCpyFnRef;
        MethodReference m_Hash32FnRef;
        MethodReference m_SystemGuidHashFn;

        TypeDefinition GeneratedRegistryDef;
        MethodDefinition GeneratedRegistryCCTORDef;
        bool IsReleaseConfig;
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected override bool PostProcessImpl()
        {
            bool madeChange = false;
            
            (var typeGenInfoList, var systemList) = GatherTypeInformation();
            if (typeGenInfoList.Count > 0 || systemList.Count > 0)
            {
                madeChange = true;
                InjectAssemblyTypeRegistry(typeGenInfoList, systemList);
            }

            madeChange |= InjectFieldInfo();
            
            // We are modifying the TypeManager in these functions so only
            // do so if we are modifying the Entities assembly
            if (AssemblyDefinition.Name.Name == "Unity.Entities")
            {
                // Promote the SharedTypeIndex as we use it in our injected AssemblyRegistries
                var sharedTypeIndex = AssemblyDefinition.MainModule.ImportReference(typeof(SharedTypeIndex<>)).Resolve();
                sharedTypeIndex.MakeTypePublic();

                InjectEntityStableTypeHash();
                madeChange = true;
            }

            // Disabled for now as IL2CPP doesn't support module initializers
            //InjectModuleInitializer(assemblyTypeRegistryField);

            return madeChange;
        }

        void InitializeReferences()
        {
            m_SystemTypeRef = AssemblyDefinition.MainModule.ImportReference(typeof(Type));
            m_GetTypeFromHandleFnRef = AssemblyDefinition.MainModule.ImportReference(typeof(Type).GetMethod("GetTypeFromHandle"));

            // I think we can remove this and make the GetHashCode generation less dumb
            m_SystemGuidHashFn = AssemblyDefinition.MainModule.ImportReference(typeof(System.Guid).GetMethod("GetHashCode"));

            m_MemCmpFnRef = AssemblyDefinition.MainModule.ImportReference(typeof(UnsafeUtility).GetMethod("MemCmp"));
            m_MemCpyFnRef = AssemblyDefinition.MainModule.ImportReference(typeof(UnsafeUtility).GetMethod("MemCpy"));
            m_Hash32FnRef = AssemblyDefinition.MainModule.ImportReference(typeof(XXHash).GetMethod("Hash32"));

            m_TypeInfoRef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeManager.TypeInfo));
            m_TypeInfoConstructorRef = m_TypeInfoRef.Module.ImportReference(typeof(TypeManager.TypeInfo).GetConstructor(new Type[]
            { 
                typeof(int), typeof(TypeCategory), typeof(int), typeof(int), 
                typeof(ulong), typeof(ulong), typeof(int), typeof(int), typeof(int),
                typeof(int), typeof(int), typeof(int), typeof(int), 
                typeof(int), typeof(int), typeof(int), typeof(int) 
            }));  
            
            IsReleaseConfig = !EntitiesILPostProcessors.Defines.Contains("DEBUG");
        }

        TypeCategory FindTypeCategoryForType(TypeDefinition typeDef)
        {
            var interfaces = typeDef.Interfaces;
            foreach (var iface in interfaces)
            {
                if (iface.InterfaceType.Name == nameof(IComponentData) && iface.InterfaceType.Namespace == "Unity.Entities")
                    return TypeCategory.ComponentData;
                if (iface.InterfaceType.Name == nameof(ISharedComponentData) && iface.InterfaceType.Namespace == "Unity.Entities")
                    return TypeCategory.ISharedComponentData;
                if (iface.InterfaceType.Name == nameof(IBufferElementData) && iface.InterfaceType.Namespace == "Unity.Entities")
                    return TypeCategory.BufferData;
            }

            return TypeCategory.Class;
        }

        TypeCategory FindTypeCategoryForTypeRecursive(TypeDefinition typeDef)
        {
            var typeCategory = FindTypeCategoryForType(typeDef);
            if (typeCategory == TypeCategory.Class && typeDef.BaseType != null) 
            {
                typeCategory = FindTypeCategoryForTypeRecursive(typeDef.BaseType.Resolve());
            }

            return typeCategory;
        }
        
        /// <summary>
        /// Generates a list of type information for all component types in the assembly
        /// </summary>
        /// <returns></returns>
        (TypeGenInfoList, SystemList) GatherTypeInformation()
        {
            var components = new List<(TypeReference Type, TypeCategory Category)>();
            var typeGenInfoList = new TypeGenInfoList();
            var systemList = new SystemList();
            var invalidAutoSystems = new List<TypeDefinition>();

            // It's possible for the whole assembly to disable auto creation so check for it
            var disableAsmAutoCreation = AssemblyDefinition.CustomAttributes.Any(attr => attr.AttributeType.Name == nameof(DisableAutoCreationAttribute));
            var componentSystemBaseClass = AssemblyDefinition.MainModule.ImportReference(typeof(ComponentSystemBase)).Resolve();

            foreach (var type in AssemblyDefinition.MainModule.GetAllTypes())
            {
                if ((type.IsValueType && !type.IsInterface && type.Interfaces.Count > 0) || (!type.IsAbstract && type.IsClass))
                {
                    // Generic components are handled below
                    if (!type.HasGenericParameters)
                    {
                        TypeCategory typeCategory;
                        if (type.IsClass)
                            typeCategory = FindTypeCategoryForTypeRecursive(type);
                        else
                            typeCategory = FindTypeCategoryForType(type);

                        if (typeCategory != TypeCategory.Class)
                        {
                            components.Add((type, typeCategory));
                            continue;
                        }
                    }

                    // If we're here the type isn't a component so see if it's a system

                    // these types obviously cannot be instantiated
                    if (type.IsAbstract || type.HasGenericParameters)
                        continue;

                    // only derivatives of ComponentSystemBase are systems
                    if (!type.IsChildTypeOf(componentSystemBaseClass))
                        continue;

                    // the auto-creation system instantiates using the default ctor, so if we can't find one, exclude from list
                    if (type.GetConstructors().All(c => c.HasParameters))
                    {
                        var disableTypeAutoCreation = type.CustomAttributes.Any(attr => attr.AttributeType.Name == nameof(DisableAutoCreationAttribute));

                        // we want users to be explicit system creation
                        if (!disableAsmAutoCreation && !disableTypeAutoCreation)
                            invalidAutoSystems.Add(type);

                        continue;
                    }

                    // We will be referencing this type in generated functions in this assembly so make
                    // the type internal if it's private
                    type.MakeTypeInternal();
                    systemList.Add(type);
                }
            }

            // For any found generic components, validate the user has registered the closed form with the assembly
            var genericComponents = AssemblyDefinition.CustomAttributes
                .Where(ca => ca.AttributeType.Name == nameof(RegisterGenericComponentTypeAttribute))
                .Select(ca=>ca.ConstructorArguments.First().Value as TypeReference)
                .Distinct();
            foreach (var genericComponent in genericComponents)
            {
                TypeCategory typeCategory;
                if (!genericComponent.IsValueType)
                    typeCategory = FindTypeCategoryForTypeRecursive(genericComponent.Resolve());
                else
                    typeCategory = FindTypeCategoryForType(genericComponent.Resolve());

                if (typeCategory != TypeCategory.Class)
                {
                    components.Add((genericComponent, typeCategory));
                    continue;
                }
            }

            if (invalidAutoSystems.Any())
            {
                throw new ArgumentException(
                    "A default constructor is necessary for automatic system scheduling for Component Systems not marked with [DisableAutoCreation]: "
                    + string.Join(", ", invalidAutoSystems.Select(cs => cs.FullName)));
            }

            // Move the CreateTypeGenInfo here so we can keep assemblies with no components quick to process
            if (components.Count > 0 || systemList.Count > 0)
            {
                InitializeReferences();
                foreach (var typePair in components)
                {
                    typeGenInfoList.Add(CreateTypeGenInfo(typePair.Type, typePair.Category));
                }
                
                PopulateWriteGroups(typeGenInfoList);
            }

            return (typeGenInfoList, systemList);
        }

        FieldReference InjectAssemblyTypeRegistry(TypeGenInfoList typeGenInfoList, SystemList systemList)
        {
            var typeRegistryDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry));

            GeneratedRegistryDef = new TypeDefinition("Unity.Entities.CodeGeneratedRegistry", "AssemblyTypeRegistry", TypeAttributes.Class | TypeAttributes.Public, AssemblyDefinition.MainModule.ImportReference(typeof(object)));
            AssemblyDefinition.MainModule.Types.Add(GeneratedRegistryDef);

            // Declares: "static TypeRegistry() { }" (i.e. the static ctor / .cctor)
            GeneratedRegistryCCTORDef = new MethodDefinition(".cctor", MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, AssemblyDefinition.MainModule.ImportReference(typeof(void)));
            GeneratedRegistryDef.Methods.Add(GeneratedRegistryCCTORDef);
            GeneratedRegistryDef.IsBeforeFieldInit = true;
            GeneratedRegistryCCTORDef.Body.InitLocals = true;

            // Defines: class Unity.Entities.StaticTypeRegistry { public static readonly TypeRegistry TypeRegistry; }
            var assemblyTypeRegistryFieldDef = new FieldDefinition("TypeRegistry", Mono.Cecil.FieldAttributes.Static | Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.InitOnly, typeRegistryDef);
            GeneratedRegistryDef.Fields.Add(assemblyTypeRegistryFieldDef);

            var assemblyTypeRegistryLocal = new VariableDefinition(typeRegistryDef);
            GeneratedRegistryCCTORDef.Body.Variables.Add(assemblyTypeRegistryLocal);

            var il = GeneratedRegistryCCTORDef.Body.GetILProcessor();

            // Create a new TypeRegistry type
            var assemblyTypeRegistryCtorRef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetConstructor(new Type[] { }));
            il.Emit(OpCodes.Newobj, assemblyTypeRegistryCtorRef);
            il.Emit(OpCodes.Stloc_0);

            // Store TypeRegistry.AssemblyName
            il.Emit(OpCodes.Ldloc_0);
            var assemblyNameFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("AssemblyName", BindingFlags.Public | BindingFlags.Instance));
            il.Emit(OpCodes.Ldstr, AssemblyDefinition.Name.Name);
            il.Emit(OpCodes.Stfld, assemblyNameFieldDef);

            // Store TypeRegistry.TypeInfos[]
            il.Emit(OpCodes.Ldloc_0);
            var typeInfosFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("TypeInfos", BindingFlags.Public | BindingFlags.Instance));
            GenerateTypeInfoArray(il, typeGenInfoList, typeInfosFieldDef, false);

            // Store TypeRegistry.Types[]
            il.Emit(OpCodes.Ldloc_0);
            var typesFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("Types", BindingFlags.Public | BindingFlags.Instance));
            GenerateTypeArray(il, typeGenInfoList.Select(tgi => tgi.TypeReference).ToList(), typesFieldDef, false);

            // Store TypeRegistry.TypeNamess[]
            il.Emit(OpCodes.Ldloc_0);
            var typeNamesFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("TypeNames", BindingFlags.Public | BindingFlags.Instance));
            var typeNames = IsReleaseConfig ? new List<string>() : typeGenInfoList.Select(t => t.TypeReference.FullName).ToList();
            StoreStringArrayInField(il, typeNames, typeNamesFieldDef, false);

            // Store TypeRegistry.EntityOffsets[]
            il.Emit(OpCodes.Ldloc_0);
            var entityOffsetsFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("EntityOffsets", BindingFlags.Public | BindingFlags.Instance));
            GenerateEntityOffsetInfoArray(il, typeGenInfoList, entityOffsetsFieldDef, false);

            // Store TypeRegistry.BlobAssetReferenceOffsets[]
            il.Emit(OpCodes.Ldloc_0);
            var blobOffsetsFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("BlobAssetReferenceOffsets", BindingFlags.Public | BindingFlags.Instance));
            GenerateBlobAssetReferenceArray(il, typeGenInfoList, blobOffsetsFieldDef, false);

            // Store TypeRegistry.WriteGroups[]
            il.Emit(OpCodes.Ldloc_0);
            var writeGroupsFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("WriteGroups", BindingFlags.Public | BindingFlags.Instance));
            GenerateWriteGroupArray(il, typeGenInfoList, writeGroupsFieldDef, false);

            // Store TypeRegistry.SystemTypes[]
            il.Emit(OpCodes.Ldloc_0);
            var systemTypesFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("SystemTypes", BindingFlags.Public | BindingFlags.Instance));
            GenerateTypeArray(il, systemList, systemTypesFieldDef, false);

            // Store TypeRegistry.SystemTypeNames[]
            il.Emit(OpCodes.Ldloc_0);
            var systemTypeNamesFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("SystemTypeNames", BindingFlags.Public | BindingFlags.Instance));
            // TODO: SystemNames are currently _required_ for runtime component sorting. This should be replaced with a systemid at which point we can remove systemnames
            //var systemTypeNames = IsReleaseConfig ? new List<string>() : systemList.Select(t => t.FullName).ToList();
            var systemTypeNames = systemList.Select(t => t.FullName).ToList();
            StoreStringArrayInField(il, systemTypeNames, systemTypeNamesFieldDef, false);

            // Store TypeRegistry.IsSystemGroup[]
            var isSystemGroupList = GetSystemIsGroupList(systemList);
            il.Emit(OpCodes.Ldloc_0);
            var isSystemGroupFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("IsSystemGroup", BindingFlags.Public | BindingFlags.Instance));
            StoreBoolArrayInField(il, isSystemGroupList, isSystemGroupFieldDef, false);

            // Store Delegates
            ///////////////////
            (var boxedEqualsFn, var boxedEqualsPtrFn, var boxedGetHashCodeFn) = InjectEqualityFunctions(typeGenInfoList);

            // Store TypeRegistry.BoxedEquals
            il.Emit(OpCodes.Ldloc_0);
            var boxedEqualsFnCtor = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry.GetBoxedEqualsFn).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            var boxedEqualsFnFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("BoxedEquals", BindingFlags.Public | BindingFlags.Instance));
            il.Emit(OpCodes.Ldnull); // no this ptr
            il.Emit(OpCodes.Ldftn, boxedEqualsFn);
            il.Emit(OpCodes.Newobj, boxedEqualsFnCtor);
            il.Emit(OpCodes.Stfld, boxedEqualsFnFieldDef);

            // Store TypeRegistry.BoxedEqualsPtr
            il.Emit(OpCodes.Ldloc_0);
            var boxedEqualsPtrFnCtor = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry.GetBoxedEqualsPtrFn).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            var boxedEqualsPtrFnFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("BoxedEqualsPtr", BindingFlags.Public | BindingFlags.Instance));
            il.Emit(OpCodes.Ldnull); // no this ptr
            il.Emit(OpCodes.Ldftn, boxedEqualsPtrFn);
            il.Emit(OpCodes.Newobj, boxedEqualsPtrFnCtor);
            il.Emit(OpCodes.Stfld, boxedEqualsPtrFnFieldDef);

            // Store TypeRegistry.BoxedGetHashCode
            il.Emit(OpCodes.Ldloc_0);
            var boxedGetHashCodeFnCtor = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry.BoxedGetHashCodeFn).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            var boxedGetHashCodeFnFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("BoxedGetHashCode", BindingFlags.Public | BindingFlags.Instance));
            il.Emit(OpCodes.Ldnull); // no this ptr
            il.Emit(OpCodes.Ldftn, boxedGetHashCodeFn);
            il.Emit(OpCodes.Newobj, boxedGetHashCodeFnCtor);
            il.Emit(OpCodes.Stfld, boxedGetHashCodeFnFieldDef);

            // Store TypeRegistry.ConstructComponentFromBuffer
            var constructComponentFn = InjectConstructComponentFunction(typeGenInfoList);
            il.Emit(OpCodes.Ldloc_0);
            var constructComponentFnCtor = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry.ConstructComponentFromBufferFn).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            var constructComponentFnFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("ConstructComponentFromBuffer", BindingFlags.Public | BindingFlags.Instance));
            il.Emit(OpCodes.Ldnull); // no this ptr
            il.Emit(OpCodes.Ldftn, constructComponentFn);
            il.Emit(OpCodes.Newobj, constructComponentFnCtor);
            il.Emit(OpCodes.Stfld, constructComponentFnFieldDef);

            // Store TypeRegistry.GetSystemAttributes
            var getSystemAttributesFn = InjectGetSystemAttributes(systemList);
            GeneratedRegistryDef.Methods.Add(getSystemAttributesFn);
            il.Emit(OpCodes.Ldloc_0);
            var getSystemAttributesFnCtor = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry.GetSystemAttributesFn).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            var getSystemAttributesFnFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("GetSystemAttributes", BindingFlags.Public | BindingFlags.Instance));
            il.Emit(OpCodes.Ldnull); // no this ptr
            il.Emit(OpCodes.Ldftn, getSystemAttributesFn);
            il.Emit(OpCodes.Newobj, getSystemAttributesFnCtor);
            il.Emit(OpCodes.Stfld, getSystemAttributesFnFieldDef);

            // Store TypeRegistry.CreateSystem
            var createSystemFn = InjectCreateSystem(systemList);
            GeneratedRegistryDef.Methods.Add(createSystemFn);
            il.Emit(OpCodes.Ldloc_0);
            var getSystemFnCtor = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry.CreateSystemFn).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            var createSystemFnFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("CreateSystem", BindingFlags.Public | BindingFlags.Instance));
            il.Emit(OpCodes.Ldnull); // no this ptr
            il.Emit(OpCodes.Ldftn, createSystemFn);
            il.Emit(OpCodes.Newobj, getSystemFnCtor);
            il.Emit(OpCodes.Stfld, createSystemFnFieldDef);

            // Store TypeRegistry.SetSharedTypeIndices
            var setSharedTypeIndicesFn = InjectSetSharedStaticTypeIndices(typeGenInfoList);
            GeneratedRegistryDef.Methods.Add(setSharedTypeIndicesFn);
            il.Emit(OpCodes.Ldloc_0);
            var setSharedStaticTypeIndicesFnCtor = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry.SetSharedTypeIndicesFn).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            var setSharedStaticTypeIndicesFnFnFieldDef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeRegistry).GetField("SetSharedTypeIndices", BindingFlags.Public | BindingFlags.Instance));
            il.Emit(OpCodes.Ldnull); // no this ptr
            il.Emit(OpCodes.Ldftn, setSharedTypeIndicesFn);
            il.Emit(OpCodes.Newobj, setSharedStaticTypeIndicesFnCtor);
            il.Emit(OpCodes.Stfld, setSharedStaticTypeIndicesFnFnFieldDef);

            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Stsfld, assemblyTypeRegistryFieldDef);

            il.Emit(OpCodes.Ret);

            return assemblyTypeRegistryFieldDef;
        }

        MethodDefinition InjectSetSharedStaticTypeIndices(TypeGenInfoList typeGenInfos)
        {
            var setSharedStaticTypeIndicesFn = new MethodDefinition("SetSharedStaticTypeIndices", 
                MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig,
                AssemblyDefinition.MainModule.ImportReference(typeof(void)));

            var typeInfosPtrArg =
                new ParameterDefinition("pTypeInfos",
                Mono.Cecil.ParameterAttributes.None,
                AssemblyDefinition.MainModule.ImportReference(typeof(int*)));
            setSharedStaticTypeIndicesFn.Parameters.Add(typeInfosPtrArg);

            var countArg = new ParameterDefinition("count",
                Mono.Cecil.ParameterAttributes.None,
                AssemblyDefinition.MainModule.ImportReference(typeof(int)));
            setSharedStaticTypeIndicesFn.Parameters.Add(countArg);

            setSharedStaticTypeIndicesFn.Body.InitLocals = true;
            var il = setSharedStaticTypeIndicesFn.Body.GetILProcessor();

            if (!IsReleaseConfig)
            {
                // Check if the count != the number of Components types we expect, as this means the runtime code is passing
                // the wrong data into this function
                var branchEndOp = Instruction.Create(OpCodes.Nop);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, typeGenInfos.Count);
                il.Emit(OpCodes.Beq, branchEndOp);
                var argumentExceptionConstructor = AssemblyDefinition.MainModule.ImportReference(typeof(ArgumentException)).Resolve().GetConstructors()
                        .Single(c => c.Parameters.Count == 1 && c.Parameters[0].ParameterType.MetadataType == MetadataType.String);
                il.Emit(OpCodes.Ldstr, $"The passed in 'count' does not match the expected count of '{typeGenInfos.Count}' component types");
                il.Emit(OpCodes.Newobj, AssemblyDefinition.MainModule.ImportReference(argumentExceptionConstructor));
                il.Emit(OpCodes.Throw);
                il.Append(branchEndOp);
            }

            // Burst needs to hae some IL that statically declares a SharedStatic for all component types
            // so it can map a hash to the type name which DOTS Runtime cannot do at runtime
            var openSharedTypeIndex = AssemblyDefinition.MainModule.ImportReference(typeof(SharedTypeIndex<>));
            var sharedTypeIndexRefField = AssemblyDefinition.MainModule.ImportReference(typeof(SharedTypeIndex<>).GetField(nameof(SharedTypeIndex<int>.Ref)));
            var sharedStaticGetDataFn = AssemblyDefinition.MainModule.ImportReference(typeof(SharedStatic<int>).GetProperty("Data").GetMethod);
            
            for(int i = 0; i < typeGenInfos.Count; ++i)
            {
                var closedSharedTypeIndex = AssemblyDefinition.MainModule.ImportReference(openSharedTypeIndex.MakeGenericInstanceType(typeGenInfos[i].TypeReference));
                var closeSharedTypeIndexRefField = new FieldReference(sharedTypeIndexRefField.Name, sharedTypeIndexRefField.FieldType, closedSharedTypeIndex);

                // SharedTypeIndex<typeGenInfo.TypeReference>.Ref.Data = 0;
                il.Emit(OpCodes.Ldsflda, closeSharedTypeIndexRefField);
                il.Emit(OpCodes.Call, sharedStaticGetDataFn);

                // Fetch TypeIndex from the array
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldc_I4_4); // sizeof(int)
                il.Emit(OpCodes.Mul);
                il.Emit(OpCodes.Add);
                il.Emit(OpCodes.Ldind_I4);

                // Store loaded int to our SharedStatic's Ref.Data
                il.Emit(OpCodes.Stind_I4);
            }

            il.Emit(OpCodes.Ret);

            return setSharedStaticTypeIndicesFn;
        }
        
        void InjectEntityStableTypeHash()
        {
            var entityStableTypeHash = TypeHash.CalculateStableTypeHashRefl(typeof(Entity));
            var typeManagerDef = AssemblyDefinition.MainModule.GetType("Unity.Entities.TypeManager");
            var getEntityStableTypeHashFn = typeManagerDef.GetMethods().First(m => m.Parameters.Count == 0 && m.Name == "GetEntityStableTypeHash");
            var il = getEntityStableTypeHashFn.Body.GetILProcessor();
            il.Body.Instructions.Clear();
            
            il.Emit(OpCodes.Ldc_I8, (long) entityStableTypeHash);
            il.Emit(OpCodes.Ret);
        }
        
        /// <summary>
        /// Customizes the <Module> initializer for our assembly to call out to TypeManager.RegisterAssemblyTypes
        /// </summary>
        /// <param name="typeRegistryField"></param>
        /// <exception cref="ArgumentException"></exception>
        void InjectModuleInitializer(FieldReference typeRegistryField)
        {
            const MethodAttributes Attributes = MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            var initializerReturnType = AssemblyDefinition.MainModule.ImportReference(typeof(void));

            var moduleClass = AssemblyDefinition.MainModule.Types.FirstOrDefault(t => t.Name == "<Module>");
            if (moduleClass == null)
            {
                throw new ArgumentException($"Failed to find the module class for '{AssemblyDefinition.Name.Name}'");
            }

            var cctor = moduleClass.Methods.FirstOrDefault(m => m.Name == ".cctor");
            ILProcessor il = null;
            if (cctor == null)
            {
                // Create a blank cctor that simply returns (we'll add to it below)
                cctor = new MethodDefinition(".cctor", Attributes, initializerReturnType);
                il = cctor.Body.GetILProcessor();
                il.Append(il.Create(OpCodes.Ret));
                moduleClass.Methods.Add(cctor);
            }

            // Insert ourselves as the first thing performed in module initialization

            var loadAssemblyTypeRegistry = il.Create(OpCodes.Ldsfld, typeRegistryField);
            var callRegisterAssemblyTypes = il.Create(OpCodes.Call, AssemblyDefinition.MainModule.ImportReference(typeof(TypeManager).GetMethod(nameof(TypeManager.RegisterAssemblyTypes))));
            il.InsertBefore(il.Body.Instructions[0], new[] { loadAssemblyTypeRegistry, callRegisterAssemblyTypes });
        }
    }
}
#endif