using System;
using System.Collections.Generic;
using System.Diagnostics;
#if !NET_DOTS
using System.Linq;
#endif
using Unity.Burst;
using System.Reflection;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Profiling;
using Unity.Core;
using System.Threading;

namespace Unity.Entities
{
    /// <summary>
    /// [WriteGroup] Can exclude components which are unknown at the time of creating the query that have been declared
    /// to write to the same component.
    ///
    /// This allows for extending systems of components safely without editing the previously existing systems.
    ///
    /// The goal is to have a way for systems that expect to transform data from one set of components (inputs) to
    /// another (output[s]) be able to declare that explicit transform, and they exclusively know about one set of
    /// inputs. If there are other inputs that want to write to the same output, the query shouldn't match because it's
    /// a nonsensical/unhandled setup. It's both a way to guard against nonsensical components (having two systems write
    /// to the same output value), and a way to "turn off" existing systems/queries by putting a component with the same
    /// write lock on an entity, letting another system handle it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public class WriteGroupAttribute : Attribute
    {
        public WriteGroupAttribute(Type targetType)
        {
            TargetType = targetType;
        }

        public Type TargetType;
    }

    /// <summary>
    /// [DisableAutoTypeRegistration] prevents a Component Type from being registered in the TypeManager
    /// during TypeManager.Initialize(). Types that are not registered will not be recognized by EntityManager.
    /// </summary>
    public class DisableAutoTypeRegistration : Attribute
    {
    }
    
#if NET_DOTS
    /// <summary>
    /// [GenerateFieldInfo] is used to signify the type or method this attribute is on should be scanned to replace
    /// GetFieldInfo<T>("xxx.xxx") calls with a compile-time generated FieldInfo struct. See the declaration of
    /// TypeManager.GetFieldInfo for more information
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public class GenerateFieldInfoAttribute : Attribute
    {
    }
#endif

    public static unsafe class TypeManager
    {
        [AttributeUsage(AttributeTargets.Struct)]
        public class ForcedMemoryOrderingAttribute : Attribute
        {
            public ForcedMemoryOrderingAttribute(ulong ordering)
            {
                MemoryOrdering = ordering;
            }

            public ulong MemoryOrdering;
        }

        [AttributeUsage(AttributeTargets.Struct)]
        public class TypeVersionAttribute : Attribute
        {
            public TypeVersionAttribute(int version)
            {
                TypeVersion = version;
            }

            public int TypeVersion;
        }

        public enum TypeCategory
        {
            /// <summary>
            /// Implements IComponentData (can be either a struct or a class)
            /// </summary>
            ComponentData,
            /// <summary>
            /// Implements IBufferElementData (struct only)
            /// </summary>
            BufferData,
            /// <summary>
            /// Implement ISharedComponentData (struct only)
            /// </summary>
            ISharedComponentData,
            /// <summary>
            /// Is an Entity
            /// </summary>
            EntityData,
            /// <summary>
            /// Inherits from UnityEngine.Object (class only)
            /// </summary>
            Class
        }

        public const int HasNoEntityReferencesFlag = 1 << 24; // this flag is inverted to ensure the type id of Entity can still be 1
        public const int SystemStateTypeFlag = 1 << 25;
        public const int BufferComponentTypeFlag = 1 << 26;
        public const int SharedComponentTypeFlag = 1 << 27;
        public const int ManagedComponentTypeFlag = 1 << 28; 
        public const int ChunkComponentTypeFlag = 1 << 29;
        public const int ZeroSizeInChunkTypeFlag = 1 << 30; // TODO: If we can ensure TypeIndex is unsigned we can use the top bit for this

        public const int ClearFlagsMask = 0x00FFFFFF;
        public const int SystemStateSharedComponentTypeFlag = SystemStateTypeFlag | SharedComponentTypeFlag;

        public const int MaximumChunkCapacity = int.MaxValue;
        public const int MaximumSupportedAlignment = 16;
        public const int MaximumTypesCount = 1024 * 10;
        /// <summary>
        /// BufferCapacity is by default calculated as DefaultBufferCapacityNumerator / sizeof(BufferElementDataType)
        /// thus for a 1 byte component, the maximum number of elements possible to be stored in chunk memory before
        /// the buffer is allocated separately from chunk data, is DefaultBufferCapacityNumerator elements.
        /// For a 2 byte sized component, (DefaultBufferCapacityNumerator / 2) elements can be stored, etc...  
        /// </summary>
        public const int DefaultBufferCapacityNumerator = 128;

        const int kInitialTypeCount = 2; // one for 'null' and one for 'Entity'
        static int s_TypeCount;
        static int s_SystemCount;
        static bool s_Initialized;
        static NativeArray<TypeInfo> s_TypeInfos;
        static NativeHashMap<ulong, int> s_StableTypeHashToTypeIndex;
        static NativeList<EntityOffsetInfo> s_EntityOffsetList;
        static NativeList<EntityOffsetInfo> s_BlobAssetRefOffsetList;
        static NativeList<int> s_WriteGroupList;
        static NativeList<bool> s_SystemIsGroupList;
        static List<FastEquality.TypeInfo> s_FastEqualityTypeInfoList;
        static List<Type> s_Types;
        static List<Type> s_SystemTypes;
        static List<string> s_TypeNames;
        static List<string> s_SystemTypeNames;
        
#if !UNITY_DOTSPLAYER
        static bool s_AppDomainUnloadRegistered;
        static double s_TypeInitializationTime;
        public static IEnumerable<TypeInfo> AllTypes { get { return Enumerable.Take(s_TypeInfos, s_TypeCount); } }
        static Dictionary<Type, int> s_ManagedTypeToIndex;
        public static int ObjectOffset;
        
        // TODO: this creates a dependency on UnityEngine, but makes splitting code in separate assemblies easier. We need to remove it during the biggere refactor.
        struct ObjectOffsetType
        {
            void* v0;
            void* v1;
        }
        
        internal static Type UnityEngineObjectType;
        internal static Type GameObjectEntityType;

        public static void RegisterUnityEngineObjectType(Type type)
        {
            if (type == null || !type.IsClass || type.IsInterface || type.FullName != "UnityEngine.Object")
                throw new ArgumentException($"{type} must be typeof(UnityEngine.Object).");
            UnityEngineObjectType = type;
        }
#endif

        public static TypeInfo[] GetAllTypes()
        {
            var res = new TypeInfo[s_TypeCount];

            for (var i = 0; i < s_TypeCount; i++)
            {
                res[i] = s_TypeInfos[i];
            }

            return res;
        }

        public struct EntityOffsetInfo
        {
            public int Offset;
        }

        internal struct StaticTypeLookup<T>
        {
            public static int typeIndex;
        }
        
        public struct TypeInfo
        {
            public TypeInfo(int typeIndex, TypeCategory category, int entityOffsetCount, int entityOffsetStartIndex,
                            ulong memoryOrdering, ulong stableTypeHash, int bufferCapacity, int sizeInChunk, int elementSize,
                            int alignmentInBytes, int maximumChunkCapacity, int writeGroupCount, int writeGroupStartIndex,
                            int blobAssetRefOffsetCount, int blobAssetRefOffsetStartIndex, int fastEqualityIndex, int typeSize)
            {
                TypeIndex = typeIndex;
                Category = category;
                EntityOffsetCount = entityOffsetCount;
                EntityOffsetStartIndex = entityOffsetStartIndex;
                MemoryOrdering = memoryOrdering;
                StableTypeHash = stableTypeHash;
                BufferCapacity = bufferCapacity;
                SizeInChunk = sizeInChunk;
                ElementSize = elementSize;
                AlignmentInBytes = alignmentInBytes;
                MaximumChunkCapacity = maximumChunkCapacity;
                WriteGroupCount = writeGroupCount;
                WriteGroupStartIndex = writeGroupStartIndex;
                BlobAssetRefOffsetCount = blobAssetRefOffsetCount;
                BlobAssetRefOffsetStartIndex = blobAssetRefOffsetStartIndex;
                FastEqualityIndex = fastEqualityIndex; // Only used for Hybrid types (should be removed once we code gen all equality cases)
                TypeSize = typeSize;
            }

            public int TypeIndex;
            // Note that this includes internal capacity and header overhead for buffers.
            public int SizeInChunk;
            // Sometimes we need to know not only the size, but the alignment.  For buffers this is the alignment
            // of an individual element.
            public int AlignmentInBytes;
            // Alignment of this type in a chunk.  Normally the same
            // as AlignmentInBytes, but that might be less than this
            // for buffer elements, whereas the buffer itself must
            // be aligned to the maximum.
            public int AlignmentInChunkInBytes
            {
                get
                {
                    if (Category == TypeCategory.BufferData)
                        return MaximumSupportedAlignment;
                    return AlignmentInBytes;
                }
                internal set
                {
                    AlignmentInChunkInBytes = value;
                }
            }
            // Normally the same as SizeInChunk (for components), but for buffers means size of an individual element.
            public int ElementSize;
            public int BufferCapacity;
            public TypeCategory Category;
            public ulong MemoryOrdering;
            public ulong StableTypeHash;
            public int EntityOffsetCount;
            internal int EntityOffsetStartIndex;
            public int BlobAssetRefOffsetCount;
            internal int BlobAssetRefOffsetStartIndex;
            public int WriteGroupCount;
            internal int WriteGroupStartIndex;
            public int MaximumChunkCapacity;
            internal int FastEqualityIndex;
            public int TypeSize;

            public bool IsZeroSized => SizeInChunk == 0;
            public bool HasWriteGroups => WriteGroupCount > 0;

            // NOTE: We explicitly exclude Type as a member of TypeInfo so the type can remain a ValueType
            public Type Type => TypeManager.GetType(TypeIndex);
            public bool HasEntities => EntityOffsetCount > 0;

            /// <summary>
            /// Provides debug type information. This information may be stripped in non-debug builds
            /// </summary>
            /// Note: We create a new instance here since TypeInfoDebug relies on TypeInfo, thus if we were to
            /// cache a TypeInfoDebug field here we would have a cyclical definition. TypeInfoDebug should not be a class
            /// either as we explicitly want TypeInfo to remain a value type.
            public TypeInfoDebug Debug => new TypeInfoDebug(this);
        }

        public struct TypeInfoDebug
        {
            TypeInfo m_TypeInfo;

            public TypeInfoDebug(TypeInfo typeInfo)
            {
                m_TypeInfo = typeInfo;
            }

            public string TypeName
            {
                get
                {
#if !NET_DOTS
                    Type type = TypeManager.GetType(m_TypeInfo.TypeIndex);
                    if (type != null)
                        return type.FullName;
                    else
                        return "<unavailable>";
#else
                    int index = m_TypeInfo.TypeIndex & ClearFlagsMask;
                    if (index < s_TypeNames.Count)
                        return s_TypeNames[index];
                    else
                        return "<unavailable>";
#endif
                }
            }
        }

        internal static EntityOffsetInfo* GetEntityOffsetsPointer()
        {
            return (EntityOffsetInfo*) SharedEntityOffsetInfo.Ref.Data;
        }

        internal static EntityOffsetInfo* GetEntityOffsets(TypeInfo typeInfo)
        {
            if (!typeInfo.HasEntities)
                return null;
            return GetEntityOffsetsPointer() + typeInfo.EntityOffsetStartIndex;
        }

        public static EntityOffsetInfo* GetEntityOffsets(int typeIndex)
        {
            var typeInfo = s_TypeInfos[typeIndex & ClearFlagsMask];
            return GetEntityOffsets(typeInfo);
        }

        internal static EntityOffsetInfo* GetBlobAssetRefOffsetsPointer()
        {
            return (EntityOffsetInfo*) SharedBlobAssetRefOffset.Ref.Data;
        }

        internal static EntityOffsetInfo* GetBlobAssetRefOffsets(TypeInfo typeInfo)
        {
            if (typeInfo.BlobAssetRefOffsetCount == 0)
                return null;

            return GetBlobAssetRefOffsetsPointer() + typeInfo.BlobAssetRefOffsetStartIndex;
        }

        internal static int* GetWriteGroupsPointer()
        {
            return (int*) SharedWriteGroup.Ref.Data; 
        }

        internal static int* GetWriteGroups(TypeInfo typeInfo)
        {
            if (typeInfo.WriteGroupCount == 0)
                return null;

            return GetWriteGroupsPointer() + typeInfo.WriteGroupStartIndex;
        }

        public static TypeInfo GetTypeInfo(int typeIndex)
        {
            return GetTypeInfoPointer()[typeIndex & ClearFlagsMask];
        }

        public static TypeInfo GetTypeInfo<T>()
        {
            return GetTypeInfoPointer()[GetTypeIndex<T>() & ClearFlagsMask];
        }

        internal static TypeInfo* GetTypeInfoPointer()
        {
            return (TypeInfo*)SharedTypeInfo.Ref.Data;
        }

        public static Type GetType(int typeIndex)
        {
            var typeIndexNoFlags = typeIndex & ClearFlagsMask; 
            Assert.IsTrue(typeIndexNoFlags >= 0 && typeIndexNoFlags < s_Types.Count);
            return s_Types[typeIndexNoFlags];
        }

        public static int GetTypeCount()
        {
            return s_TypeCount;
        }

        public static FastEquality.TypeInfo GetFastEqualityTypeInfo(TypeInfo typeInfo)
        {
            return s_FastEqualityTypeInfoList[typeInfo.FastEqualityIndex];
        }

        public static bool IsBuffer(int typeIndex) => (typeIndex & BufferComponentTypeFlag) != 0;
        public static bool IsSystemStateComponent(int typeIndex) => (typeIndex & SystemStateTypeFlag) != 0;
        public static bool IsSystemStateSharedComponent(int typeIndex) => (typeIndex & SystemStateSharedComponentTypeFlag) == SystemStateSharedComponentTypeFlag;
        public static bool IsSharedComponent(int typeIndex) => (typeIndex & SharedComponentTypeFlag) != 0;
        public static bool IsManagedComponent(int typeIndex) => (typeIndex & (ManagedComponentTypeFlag | ChunkComponentTypeFlag)) == ManagedComponentTypeFlag; 
        public static bool IsZeroSized(int typeIndex) => (typeIndex & ZeroSizeInChunkTypeFlag) != 0;
        public static bool IsChunkComponent(int typeIndex) => (typeIndex & ChunkComponentTypeFlag) != 0;
        public static bool HasEntityReferences(int typeIndex) => (typeIndex & HasNoEntityReferencesFlag) == 0;

        public static int MakeChunkComponentTypeIndex(int typeIndex) => (typeIndex | ChunkComponentTypeFlag | ZeroSizeInChunkTypeFlag);

        private static void AddTypeInfoToTables(Type type, TypeInfo typeInfo, string typeName)
        {
            s_StableTypeHashToTypeIndex.TryAdd(typeInfo.StableTypeHash, typeInfo.TypeIndex);
            s_TypeInfos[typeInfo.TypeIndex & ClearFlagsMask] = typeInfo;
            s_Types.Add(type);
            s_TypeNames.Add(typeName);
            Assert.AreEqual(s_TypeCount, typeInfo.TypeIndex & ClearFlagsMask);
            s_TypeCount++;
            
#if !NET_DOTS
            if (type != null)
            {
                SharedTypeIndex.Get(type) = typeInfo.TypeIndex;
                s_ManagedTypeToIndex.Add(type, typeInfo.TypeIndex);
            }
#endif
        }

        /// <summary>
        /// Initializes the TypeManager with all ECS type information. May be called multiple times; only the first call
        /// will do any work. Always must be called from the main thread.
        /// </summary>
        public static void Initialize()
        {
#if UNITY_EDITOR
            if (!UnityEditorInternal.InternalEditorUtility.CurrentThreadIsMainThread())
                throw new InvalidOperationException("Must be called from the main thread");
#endif

            if (s_Initialized)
                return;
            s_Initialized = true;

#if !NET_DOTS
            if (!s_AppDomainUnloadRegistered)
            {
                // important: this will always be called from a special unload thread (main thread will be blocking on this)
                AppDomain.CurrentDomain.DomainUnload += (_, __) =>
                {
                    if (s_Initialized)
                        DisposeNative();
                };
                s_AppDomainUnloadRegistered = true;
            }
            
            ObjectOffset = UnsafeUtility.SizeOf<ObjectOffsetType>();
            s_ManagedTypeToIndex = new Dictionary<Type, int>(1000);
#endif

            s_TypeCount = 0;
            s_TypeInfos = new NativeArray<TypeInfo>(MaximumTypesCount, Allocator.Persistent);
            s_StableTypeHashToTypeIndex = new NativeHashMap<ulong, int>(MaximumTypesCount, Allocator.Persistent);
            s_EntityOffsetList = new NativeList<EntityOffsetInfo>(Allocator.Persistent);
            s_BlobAssetRefOffsetList = new NativeList<EntityOffsetInfo>(Allocator.Persistent);
            s_WriteGroupList = new NativeList<int>(Allocator.Persistent);
            s_SystemIsGroupList = new NativeList<bool>(Allocator.Persistent);
            s_FastEqualityTypeInfoList = new List<FastEquality.TypeInfo>();
            s_Types = new List<Type>();
            s_SystemTypes = new List<Type>();
            s_TypeNames = new List<string>();
            s_SystemTypeNames = new List<string>();

            // There are some types that must be registered first such as a null component and Entity
            RegisterSpecialComponents();
            Assert.IsTrue(kInitialTypeCount == s_TypeCount);
            
#if !NET_DOTS
            InitializeAllComponentTypes();
#else
            // Registers all types and their static info from the static type registry
            RegisterStaticAssemblyTypes();
#endif
            // Must occur after we've constructed s_TypeInfos
            SharedTypeInfo.Ref.Data = new IntPtr(s_TypeInfos.GetUnsafePtr());
            SharedEntityOffsetInfo.Ref.Data = new IntPtr(s_EntityOffsetList.GetUnsafePtr());
            SharedBlobAssetRefOffset.Ref.Data = new IntPtr(s_BlobAssetRefOffsetList.GetUnsafePtr());
            SharedWriteGroup.Ref.Data = new IntPtr(s_WriteGroupList.GetUnsafePtr());
        }

        static void RegisterSpecialComponents()
        {
            // Push Null TypeInfo -- index 0 is reserved for null/invalid in all arrays index by (TypeIndex & ClearFlagsMask)
            s_FastEqualityTypeInfoList.Add(FastEquality.TypeInfo.Null);
            AddTypeInfoToTables(null,
                new TypeInfo(0, TypeCategory.ComponentData, 0, -1, 
                    0, 0, -1, 0, 0, 0, 
                    int.MaxValue, 0, -1, 0, 
                    -1, 0, 0),
                "Null");

            // Push Entity TypeInfo
            var entityTypeIndex = 1;
            ulong entityStableTypeHash;
            int entityFastEqIndex = -1;
#if !NET_DOTS
            entityStableTypeHash = TypeHash.CalculateStableTypeHash(typeof(Entity));
            entityFastEqIndex = s_FastEqualityTypeInfoList.Count;
            s_FastEqualityTypeInfoList.Add(FastEquality.CreateTypeInfo(typeof(Entity)));
#else
            entityStableTypeHash = GetEntityStableTypeHash();
#endif
            // Entity is special and is treated as having an entity offset at 0 (itself)
            s_EntityOffsetList.Add(new EntityOffsetInfo() { Offset = 0 }); 
            AddTypeInfoToTables(typeof(Entity),
                new TypeInfo(1, TypeCategory.EntityData, entityTypeIndex, 0,
                    0, entityStableTypeHash, -1,UnsafeUtility.SizeOf<Entity>(),
                    UnsafeUtility.SizeOf<Entity>(), CalculateAlignmentInChunk(sizeof(Entity)),
                    int.MaxValue, 0, -1, 0,
                    -1, entityFastEqIndex, UnsafeUtility.SizeOf<Entity>()),
                "Unity.Entities.Entity");
            
            SharedTypeIndex<Entity>.Ref.Data = entityTypeIndex;
        }

        /// <summary>
        /// Removes all ECS type information and any allocated memory. May only be called once globally, and must be
        /// called from the main thread.
        /// </summary>
        public static void Shutdown()
        {
            // TODO, with module loaded type info, we cannot shutdown
#if UNITY_EDITOR
            if (!UnityEditorInternal.InternalEditorUtility.CurrentThreadIsMainThread())
                throw new InvalidOperationException("Must be called from the main thread");
#endif

            if (!s_Initialized)
                throw new InvalidOperationException($"{nameof(TypeManager)} cannot be double-freed");
            
            s_Initialized = false;
            s_TypeCount = 0;

            s_FastEqualityTypeInfoList.Clear();
            s_Types.Clear();
            s_SystemTypes.Clear();
            s_TypeNames.Clear();
            s_SystemTypeNames.Clear();
            
#if !NET_DOTS
            s_ManagedTypeToIndex.Clear();
#endif

            DisposeNative();
        }

        static void DisposeNative()
        {
            s_TypeInfos.Dispose();
            s_StableTypeHashToTypeIndex.Dispose();
            s_EntityOffsetList.Dispose();
            s_BlobAssetRefOffsetList.Dispose();
            s_WriteGroupList.Dispose();
            s_SystemIsGroupList.Dispose();
        }

        private static int FindTypeIndex(Type type)
        {
#if !NET_DOTS
            if (type == null)
                return 0;

            int res;
            if (s_ManagedTypeToIndex.TryGetValue(type, out res))
                return res;
            else
                return -1;
#else
            // skip 0 since it is always null
            for (var i = 1; i < s_Types.Count; i++)
                if (type == s_Types[i])
                    return s_TypeInfos[i].TypeIndex;

            throw new ArgumentException("Tried to GetTypeIndex for type that has not been set up by the static type registry.");
#endif
        }


        public static int GetTypeIndex<T>()
        {
            var result = SharedTypeIndex<T>.Ref.Data;
            
            if (result <= 0)
            {
                throw new ArgumentException($"Unknown Type:`{typeof(T)}` All ComponentType must be known at compile time. For generic components, each concrete type must be registered with [RegisterGenericComponentType].");
            }
            
            return result;
        }

        public static int GetTypeIndex(Type type)
        {
            var index = FindTypeIndex(type);

            if (index == -1)
                throw new ArgumentException($"Unknown Type:`{type}` All ComponentType must be known at compile time. For generic components, each concrete type must be registered with [RegisterGenericComponentType].");

            return index;
        }

        public static bool Equals<T>(ref T left, ref T right) where T : struct
        {
#if !NET_DOTS
            var typeInfo = s_FastEqualityTypeInfoList[GetTypeInfo<T>().FastEqualityIndex];
            if (typeInfo.Layouts != null || typeInfo.EqualFn != null)
                return FastEquality.Equals(ref left, ref right, typeInfo);
            else
                return left.Equals(right);
#else
            return UnsafeUtility.MemCmp(UnsafeUtility.AddressOf(ref left), UnsafeUtility.AddressOf(ref right), UnsafeUtility.SizeOf<T>()) == 0;
#endif
        }

        public static bool Equals(void* left, void* right, int typeIndex)
        {
#if !NET_DOTS
            var typeInfo = s_FastEqualityTypeInfoList[GetTypeInfo(typeIndex).FastEqualityIndex];
            return FastEquality.Equals(left, right, typeInfo);
#else
            var typeInfo = GetTypeInfo(typeIndex);
            return UnsafeUtility.MemCmp(left, right, typeInfo.TypeSize) == 0;
#endif
        }

        public static bool Equals(object left, object right, int typeIndex)
        {
#if !NET_DOTS
            if (left == null || right == null)
            {
                return left == right;
            }

            if (IsManagedComponent(typeIndex))
            {
                var typeInfo = s_FastEqualityTypeInfoList[GetTypeInfo(typeIndex).FastEqualityIndex];
                return FastEquality.ManagedEquals(left, right, typeInfo);
            }
            else
            {
                var leftptr = (byte*)UnsafeUtility.PinGCObjectAndGetAddress(left, out var lhandle) + ObjectOffset;
                var rightptr = (byte*)UnsafeUtility.PinGCObjectAndGetAddress(right, out var rhandle) + ObjectOffset;

                var typeInfo = s_FastEqualityTypeInfoList[GetTypeInfo(typeIndex).FastEqualityIndex];
                var result = FastEquality.Equals(leftptr, rightptr, typeInfo);

                UnsafeUtility.ReleaseGCObject(lhandle);
                UnsafeUtility.ReleaseGCObject(rhandle);
                return result;
            }
#else
            return GetBoxedEquals(left, right, typeIndex & ClearFlagsMask);
#endif
        }

        public static bool Equals(object left, void* right, int typeIndex)
        {
#if !NET_DOTS
            var leftptr = (byte*)UnsafeUtility.PinGCObjectAndGetAddress(left, out var lhandle) + ObjectOffset;

            var typeInfo = s_FastEqualityTypeInfoList[GetTypeInfo(typeIndex).FastEqualityIndex];
            var result = FastEquality.Equals(leftptr, right, typeInfo);

            UnsafeUtility.ReleaseGCObject(lhandle);
            return result;
#else
            return GetBoxedEquals(left, right, typeIndex & ClearFlagsMask);
#endif
        }

        public static int GetHashCode<T>(ref T val) where T : struct
        {
#if !NET_DOTS
            var typeInfo = s_FastEqualityTypeInfoList[GetTypeInfo<T>().FastEqualityIndex];
            return FastEquality.GetHashCode(ref val, typeInfo);
#else
            return (int) XXHash.Hash32((byte*)UnsafeUtility.AddressOf(ref val), UnsafeUtility.SizeOf<T>());
#endif
        }

        public static int GetHashCode(void* val, int typeIndex)
        {
#if !NET_DOTS
            var typeInfo = s_FastEqualityTypeInfoList[GetTypeInfo(typeIndex).FastEqualityIndex];
            return FastEquality.GetHashCode(val, typeInfo);
#else
            var typeInfo = GetTypeInfo(typeIndex);
            return (int)XXHash.Hash32((byte*)val, typeInfo.TypeSize);
#endif
        }

        public static int GetHashCode(object val, int typeIndex)
        {
#if !NET_DOTS
            if (IsManagedComponent(typeIndex))
            {
                var typeInfo = s_FastEqualityTypeInfoList[GetTypeInfo(typeIndex).FastEqualityIndex];
                return FastEquality.ManagedGetHashCode(val, typeInfo);
            }
            else
            {
                var ptr = (byte*)UnsafeUtility.PinGCObjectAndGetAddress(val, out var handle) + ObjectOffset;

                var typeInfo = s_FastEqualityTypeInfoList[GetTypeInfo(typeIndex).FastEqualityIndex];
                var result = FastEquality.GetHashCode(ptr, typeInfo);

                UnsafeUtility.ReleaseGCObject(handle);
                return result;
            }
#else
            return GetBoxedHashCode(val, typeIndex & ClearFlagsMask);
#endif
        }

        public static int GetTypeIndexFromStableTypeHash(ulong stableTypeHash)
        {
            if (s_StableTypeHashToTypeIndex.TryGetValue(stableTypeHash, out var typeIndex))
                return typeIndex;
            return -1;
        }
        
        /// <summary>
        /// Return an array of all the Systems in use. (They are found
        /// at compile time, and inserted by code generation.)
        /// </summary>
        public static Type[] GetSystems()
        {
            return s_SystemTypes.ToArray();
        }

        public static string GetSystemName(Type t)
        {
#if !NET_DOTS
            return t.FullName;
#else
            int index = GetSystemTypeIndex(t);
            if (index < 0 || index >= s_SystemTypeNames.Count) return "null";
            return s_SystemTypeNames[index];
#endif
        }

        public static int GetSystemTypeIndex(Type t)
        {
            for (int i = 0; i < s_SystemTypes.Count; ++i)
            {
                if (t == s_SystemTypes[i]) return i;
            }
            throw new ArgumentException($"Could not find a matching system type for passed in type.");
        }

        public static bool IsSystemAGroup(Type t)
        {
#if !NET_DOTS
            return t.IsSubclassOf(typeof(ComponentSystemGroup));
#else
            int index = GetSystemTypeIndex(t);
            var isGroup = s_SystemIsGroupList[index];
            return isGroup;
#endif
        }

        /// <summary>
        /// Construct a System from a Type. Uses the same list in GetSystems()
        /// </summary>
        ///
        public static ComponentSystemBase ConstructSystem(Type systemType)
        {
#if !NET_DOTS
            return (ComponentSystemBase) Activator.CreateInstance(systemType);
#else
            var obj = CreateSystem(systemType);
            if (!(obj is ComponentSystemBase))
                throw new Exception("Null casting in Construct System. Bug in TypeManager.");
            return obj as ComponentSystemBase;
#endif
        }

        public static T ConstructSystem<T>(Type systemType) where T : ComponentSystemBase
        {
            return (T)ConstructSystem(systemType);
        }

        public static T ConstructSystem<T>() where T : ComponentSystemBase
        {
            return ConstructSystem<T>(typeof(T));
        }

        public static object ConstructComponentFromBuffer(int typeIndex, void* data)
        {
#if !NET_DOTS
            var tinfo = GetTypeInfo(typeIndex);
            Type type = GetType(typeIndex);
            object obj = Activator.CreateInstance(type);
            unsafe
            {
                var ptr = UnsafeUtility.PinGCObjectAndGetAddress(obj, out var handle);
                UnsafeUtility.MemCpy(ptr, data, tinfo.SizeInChunk);
                UnsafeUtility.ReleaseGCObject(handle);
            }

            return obj;
#else
            return ConstructComponentFromBuffer(data, typeIndex & ClearFlagsMask);
#endif
        }

        /// <summary>
        /// Get all the attribute objects of Type attributeType for a System.
        /// </summary>
        public static Attribute[] GetSystemAttributes(Type systemType, Type attributeType)
        {
#if !NET_DOTS
            var objArr = systemType.GetCustomAttributes(attributeType, true);
            var attr = new Attribute[objArr.Length];
            for (int i = 0; i < objArr.Length; i++)
            {
                attr[i] = objArr[i] as Attribute;
            }
            return attr;
#else
            Attribute[] attr = GetSystemAttributes(systemType);
            int count = 0;
            for (int i = 0; i < attr.Length; ++i)
            {
                if (attr[i].GetType() == attributeType)
                {
                    ++count;
                }
            }
            Attribute[] result = new Attribute[count];
            count = 0;
            for (int i = 0; i < attr.Length; ++i)
            {
                if (attr[i].GetType() == attributeType)
                {
                    result[count++] = attr[i];
                }
            }
            return result;
#endif
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private static readonly Type[] s_SingularInterfaces =
        {
            typeof(IComponentData),
            typeof(IBufferElementData),
            typeof(ISharedComponentData),
        };

        internal static void CheckComponentType(Type type)
        {
            int typeCount = 0;
            foreach (Type t in s_SingularInterfaces)
            {
                if (t.IsAssignableFrom(type))
                    ++typeCount;
            }

            if (typeCount > 1)
                throw new ArgumentException($"Component {type} can only implement one of IComponentData, ISharedComponentData and IBufferElementData");
        }

#endif

        public static NativeArray<int> GetWriteGroupTypes(int typeIndex)
        {
            var type = GetTypeInfo(typeIndex);
            var writeGroups = GetWriteGroups(type);
            var writeGroupCount = type.WriteGroupCount;
            var arr = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<int>(writeGroups, writeGroupCount, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref arr, AtomicSafetyHandle.Create());
#endif
            return arr;
        }

        // TODO: Fix our wild alignment requirements for chunk memory (easier said than done)
        /// <summary>
        /// Our alignment calculations for types are taken from the perspective of the alignment of the type _specifically_ when
        /// stored in chunk memory. This means a type's natural alignment may not match the AlignmentInChunk value. Our current scheme is such that
        /// an alignment of 'MaximumSupportedAlignment' is assumed unless the size of the type is smaller than 'MaximumSupportedAlignment' and is a power of 2.
        /// In such cases we use the type size directly, thus if you have a type that naturally aligns to 4 bytes and has a size of 8, the AlignmentInChunk will be 8
        /// as long as 8 is less than 'MaximumSupportedAlignment'.
        /// </summary>
        /// <param name="sizeOfTypeInBytes"></param>
        /// <returns></returns>
        internal static int CalculateAlignmentInChunk(int sizeOfTypeInBytes)
        {
            int alignmentInBytes = MaximumSupportedAlignment;
            if (sizeOfTypeInBytes < alignmentInBytes && CollectionHelper.IsPowerOfTwo(sizeOfTypeInBytes))
                alignmentInBytes = sizeOfTypeInBytes;

            return alignmentInBytes;
        }

#if !NET_DOTS

        private static bool IsSupportedComponentType(Type type)
        {
            return typeof(IComponentData).IsAssignableFrom(type)
                || typeof(ISharedComponentData).IsAssignableFrom(type)
                || typeof(IBufferElementData).IsAssignableFrom(type);
        }

        static void InitializeAllComponentTypes()
        {
            try
            {
                Profiler.BeginSample("InitializeAllComponentTypes");

                double start = (new TimeSpan(DateTime.Now.Ticks)).TotalMilliseconds;

                var componentTypeSet = new HashSet<Type>();
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                // Inject types needed for Hybrid
                foreach (var assembly in assemblies)
                {
                    if (assembly.GetName().Name == "Unity.Entities.Hybrid")
                    {
                        GameObjectEntityType = assembly.GetType("Unity.Entities.GameObjectEntity");
                    }

                    if (assembly.GetName().Name == "UnityEngine")
                    {
                        UnityEngineObjectType = assembly.GetType("UnityEngine.Object");
                    }
                }
                if ((UnityEngineObjectType == null) || (GameObjectEntityType == null))
                {
                    throw new Exception("Required UnityEngine and Unity.Entities.Hybrid types not found.");
                }

                foreach (var assembly in assemblies)
                {
                    var isAssemblyReferencingUnityEngine = IsAssemblyReferencingUnityEngine(assembly);
                    var isAssemblyReferencingEntities = IsAssemblyReferencingEntities(assembly);
                    var isAssemblyRelevant = isAssemblyReferencingEntities || isAssemblyReferencingUnityEngine;

                    if (!isAssemblyRelevant)
                        continue;

                    var assemblyTypes = assembly.GetTypes();

                    // Register UnityEngine types (Hybrid)
                    if (isAssemblyReferencingUnityEngine)
                    {
                        foreach (var type in assemblyTypes)
                        {
                            if (type.ContainsGenericParameters)
                                continue;

                            if (type.IsAbstract)
                                continue;

                            if (type.IsClass)
                            {
                                if (type == GameObjectEntityType)
                                    continue;

                                if (!UnityEngineObjectType.IsAssignableFrom(type))
                                    continue;

                                componentTypeSet.Add(type);
                            }
                        }
                    }

                    if (isAssemblyReferencingEntities)
                    {
                        // Register ComponentData types
                        foreach (var type in assemblyTypes)
                        {
                            if (type.IsAbstract)
                                continue;

#if !UNITY_DISABLE_MANAGED_COMPONENTS
                            if (!type.IsValueType && !typeof(IComponentData).IsAssignableFrom(type))
                                continue;
#else
                            if (!type.IsValueType && typeof(IComponentData).IsAssignableFrom(type))
                                throw new ArgumentException($"Type '{type.FullName}' inherits from IComponentData but has been defined as a managed type. " +
                                    $"Managed component support has been explicitly disabled via the 'UNITY_DISABLE_MANAGED_COMPONENTS' define. " +
                                    $"Change the offending type to be a value type or re-enable managed component support.");

                            if (!type.IsValueType)
                                continue;
#endif

                            // Don't register open generics here.  It's an open question
                            // on whether we should support them for components at all,
                            // as with them we can't ever see a full set of component types
                            // in use.
                            if (type.ContainsGenericParameters)
                                continue;

                            if (type.GetCustomAttribute(typeof(DisableAutoTypeRegistration)) != null)
                                continue;

                            // XXX There's a bug in the Unity Mono scripting backend where if the
                            // Mono type hasn't been initialized, the IsUnmanaged result is wrong.
                            // We force it to be fully initialized by creating an instance until
                            // that bug is fixed.
                            try
                            {
                                var inst = Activator.CreateInstance(type);
                            }
                            catch (Exception)
                            {
                                // ignored
                            }

                            if (IsSupportedComponentType(type))
                            {
                                componentTypeSet.Add(type);
                            }
                        }

                        // Register ComponentData concrete generics
                        foreach (var registerGenericComponentTypeAttribute in
                                 assembly.GetCustomAttributes<RegisterGenericComponentTypeAttribute>())
                        {
                            var type = registerGenericComponentTypeAttribute.ConcreteType;

                            if (IsSupportedComponentType(type))
                            {
                                componentTypeSet.Add(type);
                            }
                        }
                    }
                }

                var componentTypeCount = componentTypeSet.Count;
                var componentTypes = new Type[componentTypeCount];
                componentTypeSet.CopyTo(componentTypes);

                var typeIndexByType = new Dictionary<Type, int>();
                var writeGroupByType = new Dictionary<int, HashSet<int>>();
                var startTypeIndex = s_TypeCount;

                for (int i = 0; i < componentTypes.Length; i++)
                {
                    typeIndexByType[componentTypes[i]] = startTypeIndex + i;
                }

                GatherWriteGroups(componentTypes, startTypeIndex, typeIndexByType, writeGroupByType);
                AddAllComponentTypes(componentTypes, startTypeIndex, writeGroupByType);

                double end = (new TimeSpan(DateTime.Now.Ticks)).TotalMilliseconds;

                // Save the time since profiler might not catch the first frame.
                s_TypeInitializationTime = end - start;
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        private static void AddAllComponentTypes(Type[] componentTypes, int startTypeIndex, Dictionary<int, HashSet<int>> writeGroupByType)
        {
            var expectedTypeIndex = startTypeIndex;

            for (int i = 0; i < componentTypes.Length; i++)
            {
                try
                {
                    var type = componentTypes[i];
                    var index = FindTypeIndex(type);
                    if (index != -1)
                        throw new InvalidOperationException("ComponentType cannot be initialized more than once.");

                    TypeInfo typeInfo;
                    if (writeGroupByType.ContainsKey(expectedTypeIndex))
                    {
                        var writeGroupSet = writeGroupByType[expectedTypeIndex];
                        var writeGroupCount = writeGroupSet.Count;
                        var writeGroupArray = new int[writeGroupCount];
                        writeGroupSet.CopyTo(writeGroupArray);

                        typeInfo = BuildComponentType(type, writeGroupArray);
                    }
                    else
                    {
                        typeInfo = BuildComponentType(type);
                    }

                    var typeIndex = typeInfo.TypeIndex & TypeManager.ClearFlagsMask;
                    if (expectedTypeIndex != typeIndex)
                        throw new InvalidOperationException("ComponentType.TypeIndex does not match precalculated index.");

                    AddTypeInfoToTables(type, typeInfo, type.FullName);
                    expectedTypeIndex += 1;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private static void GatherWriteGroups(Type[] componentTypes, int startTypeIndex, Dictionary<Type, int> typeIndexByType,
            Dictionary<int, HashSet<int>> writeGroupByType)
        {
            for (int i = 0; i < componentTypes.Length; i++)
            {
                var type = componentTypes[i];
                var typeIndex = startTypeIndex + i;

                foreach (var attribute in type.GetCustomAttributes(typeof(WriteGroupAttribute)))
                {
                    var attr = (WriteGroupAttribute)attribute;
                    if (!typeIndexByType.ContainsKey(attr.TargetType))
                    {
                        Debug.LogError($"GatherWriteGroups: looking for {attr.TargetType} but it hasn't been set up yet");
                    }

                    int targetTypeIndex = typeIndexByType[attr.TargetType];

                    if (!writeGroupByType.ContainsKey(targetTypeIndex))
                    {
                        var targetList = new HashSet<int>();
                        writeGroupByType.Add(targetTypeIndex, targetList);
                    }

                    writeGroupByType[targetTypeIndex].Add(typeIndex);
                }
            }
        }

        public static bool IsAssemblyReferencingEntities(Assembly assembly)
        {
            const string entitiesAssemblyName = "Unity.Entities";
            if (assembly.GetName().Name.Contains(entitiesAssemblyName))
                return true;

            var referencedAssemblies = assembly.GetReferencedAssemblies();
            foreach (var referenced in referencedAssemblies)
                if (referenced.Name.Contains(entitiesAssemblyName))
                    return true;
            return false;
        }

        public static bool IsAssemblyReferencingUnityEngine(Assembly assembly)
        {
            const string entitiesAssemblyName = "UnityEngine";
            if (assembly.GetName().Name.Contains(entitiesAssemblyName))
                return true;

            var referencedAssemblies = assembly.GetReferencedAssemblies();
            foreach (var referenced in referencedAssemblies)
                if (referenced.Name.Contains(entitiesAssemblyName))
                    return true;
            return false;
        }
        
        // The reflection-based type registration path that we can't support with tiny csharp profile.
        // A generics compile-time path is temporarily used (see later in the file) until we have
        // full static type info generation working.
        static EntityOffsetInfo[] CalculateBlobAssetRefOffsets(Type type)
        {
            var offsets = new List<EntityOffsetInfo>();
            CalculateBlobAssetRefOffsetsRecurse(ref offsets, type, 0);
            if (offsets.Count > 0)
                return offsets.ToArray();
            else
                return null;
        }

        static void CalculateBlobAssetRefOffsetsRecurse(ref List<EntityOffsetInfo> offsets, Type type, int baseOffset)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BlobAssetReference<>))
            {
                offsets.Add(new EntityOffsetInfo { Offset = baseOffset });
            }
            else
            {
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (var field in fields)
                {
                    if (field.FieldType.IsValueType && !field.FieldType.IsPrimitive)
                        CalculateBlobAssetRefOffsetsRecurse(ref offsets, field.FieldType, baseOffset + UnsafeUtility.GetFieldOffset(field));
                }
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void CheckIsAllowedAsComponentData(Type type, string baseTypeDesc)
        {
            if (UnsafeUtility.IsUnmanaged(type))
                return;

            // it can't be used -- so we expect this to find and throw
            ThrowOnDisallowedComponentData(type, type, baseTypeDesc);

            // if something went wrong and the above didn't throw, then throw
            throw new ArgumentException($"{type} cannot be used as component data for unknown reasons (BUG)");
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void CheckIsAllowedAsManagedComponentData(Type type, string baseTypeDesc)
        {
            if (type.IsClass && typeof(IComponentData).IsAssignableFrom(type))
            {
                ThrowOnDisallowedManagedComponentData(type, type, baseTypeDesc);
                return;
            }

            // if something went wrong and the above didn't throw, then throw
            throw new ArgumentException($"{type} cannot be used as managed component data for unknown reasons (BUG)");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void ThrowOnDisallowedManagedComponentData(Type type, Type baseType, string baseTypeDesc)
        {
            // Validate the class IComponentData is usable:
            // - Has a default constructor
            if (type.GetConstructor(Type.EmptyTypes) == null)
                throw new ArgumentException($"{type} is a class based IComponentData. Class based IComponentData must implement a default constructor.");
        }

#endif

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void ThrowOnDisallowedComponentData(Type type, Type baseType, string baseTypeDesc)
        {
            if (type.IsPrimitive)
                return;

            // if it's a pointer, we assume you know what you're doing
            if (type.IsPointer)
                return;

            if (!type.IsValueType || type.IsByRef || type.IsClass || type.IsInterface || type.IsArray)
            {
                if (type == baseType)
                    throw new ArgumentException(
                        $"{type} is a {baseTypeDesc} and thus must be a struct containing only primitive or blittable members.");

                throw new ArgumentException($"{baseType} contains a field of {type}, which is neither primitive nor blittable.");
            }

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                ThrowOnDisallowedComponentData(field.FieldType, baseType, baseTypeDesc);
            }
        }

        // https://stackoverflow.com/a/27851610
        static bool IsZeroSizeStruct(Type t)
        {
            return t.IsValueType && !t.IsPrimitive &&
                t.GetFields((BindingFlags)0x34).All(fi => IsZeroSizeStruct(fi.FieldType));
        }
        
        internal static TypeInfo BuildComponentType(Type type)
        {
            return BuildComponentType(type, null);
        }

        internal static TypeInfo BuildComponentType(Type type, int[] writeGroups)
        {
            var sizeInChunk = 0;
            TypeCategory category;
            var typeInfo = FastEquality.TypeInfo.Null;
            EntityOffsetInfo[] entityOffsets = null;
            EntityOffsetInfo[] blobAssetRefOffsets = null;
            int bufferCapacity = -1;
            var memoryOrdering = TypeHash.CalculateMemoryOrdering(type);
            var stableTypeHash = TypeHash.CalculateStableTypeHash(type);
            bool isManaged = type.IsClass;
            var maxChunkCapacity = MaximumChunkCapacity;
            var valueTypeSize = 0;

            var maxCapacityAttribute = type.GetCustomAttribute<MaximumChunkCapacityAttribute>();
            if (maxCapacityAttribute != null)
                maxChunkCapacity = maxCapacityAttribute.Capacity;

            int elementSize = 0;
            int alignmentInBytes = 0;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (type.IsInterface)
                throw new ArgumentException($"{type} is an interface. It must be a concrete type.");
#endif
            if (typeof(IComponentData).IsAssignableFrom(type) && !isManaged)
            {
                CheckIsAllowedAsComponentData(type, nameof(IComponentData));

                category = TypeCategory.ComponentData;

                valueTypeSize = UnsafeUtility.SizeOf(type);
                alignmentInBytes = CalculateAlignmentInChunk(valueTypeSize);

                if (TypeManager.IsZeroSizeStruct(type))
                    sizeInChunk = 0;
                else
                    sizeInChunk = valueTypeSize;

                typeInfo = FastEquality.CreateTypeInfo(type);
                entityOffsets = EntityRemapUtility.CalculateEntityOffsets(type);
                blobAssetRefOffsets = CalculateBlobAssetRefOffsets(type);
            }
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            else if (typeof(IComponentData).IsAssignableFrom(type) && isManaged)
            {
                CheckIsAllowedAsManagedComponentData(type, nameof(IComponentData));

                category = TypeCategory.ComponentData;
                sizeInChunk = sizeof(int);
                typeInfo = FastEquality.CreateTypeInfo(type);
                entityOffsets = EntityRemapUtility.CalculateEntityOffsets(type);
                blobAssetRefOffsets = CalculateBlobAssetRefOffsets(type);
            }
#endif
            else if (typeof(IBufferElementData).IsAssignableFrom(type))
            {
                CheckIsAllowedAsComponentData(type, nameof(IBufferElementData));

                category = TypeCategory.BufferData;

                valueTypeSize = UnsafeUtility.SizeOf(type);
                // TODO: Implement UnsafeUtility.AlignOf(type)
                alignmentInBytes = CalculateAlignmentInChunk(valueTypeSize);

                elementSize = valueTypeSize;

                var capacityAttribute = (InternalBufferCapacityAttribute)type.GetCustomAttribute(typeof(InternalBufferCapacityAttribute));
                if (capacityAttribute != null)
                    bufferCapacity = capacityAttribute.Capacity;
                else
                    bufferCapacity = DefaultBufferCapacityNumerator / elementSize; // Rather than 2*cachelinesize, to make it cross platform deterministic

                sizeInChunk = sizeof(BufferHeader) + bufferCapacity * elementSize;
                typeInfo = FastEquality.CreateTypeInfo(type);
                entityOffsets = EntityRemapUtility.CalculateEntityOffsets(type);
                blobAssetRefOffsets = CalculateBlobAssetRefOffsets(type);
            }
            else if (typeof(ISharedComponentData).IsAssignableFrom(type))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (!type.IsValueType)
                    throw new ArgumentException($"{type} is an ISharedComponentData, and thus must be a struct.");
#endif
                valueTypeSize = UnsafeUtility.SizeOf(type);
                entityOffsets = EntityRemapUtility.CalculateEntityOffsets(type);
                category = TypeCategory.ISharedComponentData;
                typeInfo = FastEquality.CreateTypeInfo(type);
            }
            else if (type.IsClass)
            {
                category = TypeCategory.Class;
                sizeInChunk = sizeof(int);
                alignmentInBytes = sizeof(int);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (type.FullName == "Unity.Entities.GameObjectEntity")
                    throw new ArgumentException(
                        "GameObjectEntity cannot be used from EntityManager. The component is ignored when creating entities for a GameObject.");
                if (UnityEngineObjectType == null)
                    throw new ArgumentException(
                        $"{type} cannot be used from EntityManager. If it inherits UnityEngine.Component, you must first register TypeManager.UnityEngineObjectType or include the Unity.Entities.Hybrid assembly in your build.");
                if (!UnityEngineObjectType.IsAssignableFrom(type))
                    throw new ArgumentException($"{type} must inherit {UnityEngineObjectType}.");
#endif
            }
            else
            {
                throw new ArgumentException($"{type} is not a valid component.");
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckComponentType(type);
#endif
            int fastEqIndex = 0;
            if (!FastEquality.TypeInfo.Null.Equals(typeInfo))
            {
                fastEqIndex = s_FastEqualityTypeInfoList.Count;
                s_FastEqualityTypeInfoList.Add(typeInfo);
            }

            int entityOffsetIndex = s_EntityOffsetList.Length;
            int entityOffsetCount = entityOffsets == null ? 0 : entityOffsets.Length;
            if (entityOffsets != null)
            {
                foreach (var offset in entityOffsets)
                    s_EntityOffsetList.Add(offset);
            }

            int blobAssetRefOffsetIndex = s_BlobAssetRefOffsetList.Length;
            int blobAssetRefOffsetCount = blobAssetRefOffsets == null ? 0 : blobAssetRefOffsets.Length;
            if (blobAssetRefOffsets != null)
            {
                foreach (var offset in blobAssetRefOffsets)
                    s_BlobAssetRefOffsetList.Add(offset);
            }

            int writeGroupIndex = s_WriteGroupList.Length;
            int writeGroupCount = writeGroups == null ? 0 : writeGroups.Length;
            if (writeGroups != null)
            {
                foreach (var wgTypeIndex in writeGroups)
                    s_WriteGroupList.Add(wgTypeIndex);
            }

            int typeIndex = s_TypeCount;
            // System state shared components are also considered system state components
            bool isSystemStateSharedComponent = typeof(ISystemStateSharedComponentData).IsAssignableFrom(type);
            bool isSystemStateBufferElement = typeof(ISystemStateBufferElementData).IsAssignableFrom(type);
            bool isSystemStateComponent = isSystemStateSharedComponent || isSystemStateBufferElement || typeof(ISystemStateComponentData).IsAssignableFrom(type);
            
            if (typeIndex != 0)
            {
                if (sizeInChunk == 0)
                    typeIndex |= ZeroSizeInChunkTypeFlag;

                if (category == TypeCategory.ISharedComponentData)
                    typeIndex |= SharedComponentTypeFlag;

                if (isSystemStateComponent)
                    typeIndex |= SystemStateTypeFlag;

                if (isSystemStateSharedComponent)
                    typeIndex |= SystemStateSharedComponentTypeFlag;

                if (bufferCapacity >= 0)
                    typeIndex |= BufferComponentTypeFlag;

                if (entityOffsetCount == 0)
                    typeIndex |= HasNoEntityReferencesFlag;

                if (isManaged)
                    typeIndex |= ManagedComponentTypeFlag;
            }
            
            return new TypeInfo(typeIndex, category, entityOffsetCount, entityOffsetIndex, 
                memoryOrdering, stableTypeHash, bufferCapacity, sizeInChunk, 
                elementSize > 0 ? elementSize : sizeInChunk, alignmentInBytes, 
                maxChunkCapacity, writeGroupCount, writeGroupIndex,
                blobAssetRefOffsetCount, blobAssetRefOffsetIndex, fastEqIndex,
                valueTypeSize);
        }

        [Obsolete("CreateTypeIndexForComponent is deprecated. TypeIndices can be created for new Types using AddNewComponentTypes (editor only) (RemovedAfter 2020-03-03)", false)] 
        public static int CreateTypeIndexForComponent<T>() where T : IComponentData
        {
            return GetTypeIndex<T>();
        }

        [Obsolete("CreateTypeIndexForSharedComponent is deprecated. TypeIndices can be created for new Types using AddNewComponentTypes (editor only) (RemovedAfter 2020-03-03)", false)]
        public static int CreateTypeIndexForSharedComponent<T>() where T : struct, ISharedComponentData
        {
            return GetTypeIndex<T>();
        }

        [Obsolete("CreateTypeIndexForBufferElement is deprecated. TypeIndices can be created for new Types using AddNewComponentTypes (editor only) (RemovedAfter 2020-03-03)", false)]
        public static int CreateTypeIndexForBufferElement<T>() where T : struct, IBufferElementData
        {
            return GetTypeIndex<T>();
        }

 #if UNITY_EDITOR
        /// <summary>
        /// This function allows for unregistered component types to be added to the TypeManager allowing for their use
        /// across the ECS apis _after_ TypeManager.Initialize() may have been called. Importantly, this function must
        /// be called from the main thread and will create a synchronization point across all worlds. If a type which
        /// is already registered with the TypeManager is passed in, this function will throw.
        /// </summary>
        /// <remarks>Types with [WriteGroup] attributes will be accepted for registration however their
        /// write group information will be ignored.</remarks>
        /// <param name="types"></param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static void AddNewComponentTypes(Type[] types)
        {
            if (!UnityEditorInternal.InternalEditorUtility.CurrentThreadIsMainThread())
                throw new InvalidOperationException("Must be called from the main thread");

            // We might invalidate the SharedStatics ptr so we must synchronize all jobs that might be using those ptrs
            foreach (var world in World.All)
                world.EntityManager.BeforeStructuralChange();

            // Is this a new type, or are we replacing an existing one?
            foreach (var type in types)
            {
                if(s_ManagedTypeToIndex.ContainsKey(type))
                    throw new ArgumentException($"Type '{type.FullName}' has already been added to TypeManager.");

                var typeInfo = BuildComponentType(type);
                AddTypeInfoToTables(type, typeInfo, type.FullName);
            }

            // We may have added enough types to cause the underlying containers to resize so re-fetch their ptrs
            SharedEntityOffsetInfo.Ref.Data = new IntPtr(s_EntityOffsetList.GetUnsafePtr());
            SharedBlobAssetRefOffset.Ref.Data = new IntPtr(s_BlobAssetRefOffsetList.GetUnsafePtr());
            SharedWriteGroup.Ref.Data = new IntPtr(s_WriteGroupList.GetUnsafePtr());

            // Since the ptrs may have changed we need to ensure all entity component stores are using the correct ones
            foreach (var w in World.All)
            {
                w.EntityManager.EntityComponentStore->InitializeTypeManagerPointers();
            }
        }
#endif         

        private sealed class SharedTypeIndex
        {
            public static ref int Get(Type componentType)
            {
                return ref SharedStatic<int>.GetOrCreate(typeof(TypeManagerKeyContext), componentType).Data;
            }
        }
#endif
        private sealed class TypeManagerKeyContext
        {
            private TypeManagerKeyContext()
            {
            }
        }

        private sealed class SharedTypeInfo
        {
            private SharedTypeInfo()
            {
            }

            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedTypeInfo>();
        }

        private sealed class SharedEntityOffsetInfo
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedEntityOffsetInfo>();
        }

        private sealed class SharedBlobAssetRefOffset
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedBlobAssetRefOffset>();
        }

        private sealed class SharedWriteGroup
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedWriteGroup>();
        }

        // Marked as internal as this is used by StaticTypeRegistryILPostProcessor
        internal sealed class SharedTypeIndex<TComponent>
        {
            public static readonly SharedStatic<int> Ref = SharedStatic<int>.GetOrCreate<TypeManagerKeyContext, TComponent>();
        }
        
#if NET_DOTS
        public struct FieldInfo
        {
            public Type BaseType;
            public Type FieldType;
            public int  FieldOffset;
        }

        /// <summary>
        /// Specify the full name of the field you'd like FieldInfo for. This method call will be replaced
        /// at compilation time to provide the FieldInfo statically. As such, fieldFullName must be a string literal
        /// and not a runtime string. The value of the string literal must be the 'path' to the field you would like
        /// FieldInfo for, as if accessing the field from a local variable of type <T>. In order for FieldInfo to be
        /// generated, you must ensure the method (or its containing type) using GetFieldInfo<T>() has been given the
        /// [GenerateFieldInfo] attribute. Failure to do so will result in a runtime exception.  
        ///
        /// Example usage:
        /// var fieldInfo = new FieldInfo<MyType>("m_fieldName");
        /// var nestedFieldInfo = new FieldInfo<MyType>("m_fieldName.m_InnerTypeFieldName");
        /// var moreNestedPrivateFieldInfo = new FieldInfo<MyType>("m_fieldName.m_InnerTypeFieldName.m_somePrivateField");
        ///
        /// The following will produce a errors:
        /// var fieldName = "m_FieldName";
        /// var fieldInfo = new FieldInfo<MyType>(fieldName);
        /// var innerFieldInfo = new FieldInfo<MyType>("m_fieldName" + ".m_InnerTypeFieldName");
        /// </summary>
        /// <param name="fieldPath"></param>
        public static FieldInfo GetFieldInfo<T>(string fieldPath) where T : struct
        {
            throw new CodegenShouldReplaceException("This call should have been replaced by codegen. " +
                "Ensure your method or type contains the [GenerateFieldInfo] attribute. ");
        }
        
        static SpinLock sLock = new SpinLock();

        internal static void RegisterStaticAssemblyTypes()
        {
            throw new CodegenShouldReplaceException("To be replaced by codegen");
        }

        static List<int> s_TypeDelegateIndexRanges = new List<int>();
        static List<int> s_SystemTypeDelegateIndexRanges = new List<int>();
        static List<TypeRegistry.GetBoxedEqualsFn> s_AssemblyBoxedEqualsFn = new List<TypeRegistry.GetBoxedEqualsFn>();
        static List<TypeRegistry.GetBoxedEqualsPtrFn> s_AssemblyBoxedEqualsPtrFn = new List<TypeRegistry.GetBoxedEqualsPtrFn>();
        static List<TypeRegistry.BoxedGetHashCodeFn> s_AssemblyBoxedGetHashCodeFn = new List<TypeRegistry.BoxedGetHashCodeFn>();
        static List<TypeRegistry.ConstructComponentFromBufferFn> s_AssemblyConstructComponentFromBufferFn = new List<TypeRegistry.ConstructComponentFromBufferFn>();
        static List<TypeRegistry.CreateSystemFn> s_AssemblyCreateSystemFn = new List<TypeRegistry.CreateSystemFn>();
        static List<TypeRegistry.GetSystemAttributesFn> s_AssemblyGetSystemAttributesFn = new List<TypeRegistry.GetSystemAttributesFn>();

        internal static bool GetBoxedEquals(object lhs, object rhs, int typeIndexNoFlags)
        {
            int offset = 0;
            for (int i = 0; i < s_TypeDelegateIndexRanges.Count; ++i)
            {
                if (typeIndexNoFlags < s_TypeDelegateIndexRanges[i])
                    return s_AssemblyBoxedEqualsFn[i](lhs, rhs, typeIndexNoFlags - offset);
                offset = s_TypeDelegateIndexRanges[i];
            }

            throw new ArgumentException("No function was generated for the provided type.");
        }

        internal static bool GetBoxedEquals(object lhs, void* rhs, int typeIndexNoFlags)
        {
            int offset = 0;
            for (int i = 0; i < s_TypeDelegateIndexRanges.Count; ++i)
            {
                if (typeIndexNoFlags < s_TypeDelegateIndexRanges[i])
                    return s_AssemblyBoxedEqualsPtrFn[i](lhs, rhs, typeIndexNoFlags - offset);
                offset = s_TypeDelegateIndexRanges[i];
            }

            throw new ArgumentException("No function was generated for the provided type.");
        }

        internal static int GetBoxedHashCode(object obj, int typeIndexNoFlags)
        {
            int offset = 0;
            for (int i = 0; i < s_TypeDelegateIndexRanges.Count; ++i)
            {
                if (typeIndexNoFlags < s_TypeDelegateIndexRanges[i])
                    return s_AssemblyBoxedGetHashCodeFn[i](obj, typeIndexNoFlags - offset);
                offset = s_TypeDelegateIndexRanges[i];
            }

            throw new ArgumentException("No function was generated for the provided type.");
        }

        internal static object ConstructComponentFromBuffer(void* buffer, int typeIndexNoFlags)
        {
            int offset = 0;
            for (int i = 0; i < s_TypeDelegateIndexRanges.Count; ++i)
            {
                if (typeIndexNoFlags < s_TypeDelegateIndexRanges[i])
                    return s_AssemblyConstructComponentFromBufferFn[i](buffer, typeIndexNoFlags - offset);
                offset = s_TypeDelegateIndexRanges[i];
            }

            throw new ArgumentException("No function was generated for the provided type.");
        }

        internal static object CreateSystem(Type systemType)
        {
            int systemIndex = 0;
            for(; systemIndex < s_SystemTypes.Count; ++systemIndex)
            {
                if (s_SystemTypes[systemIndex] == systemType)
                    break;
            }

            for (int i = 0; i < s_SystemTypeDelegateIndexRanges.Count; ++i)
            {
                if (systemIndex < s_SystemTypeDelegateIndexRanges[i])
                    return s_AssemblyCreateSystemFn[i](systemType);
            }

            throw new ArgumentException("No function was generated for the provided type.");
        }

        internal static Attribute[] GetSystemAttributes(Type system)
        {
            int typeIndexNoFlags = 0;
            for (; typeIndexNoFlags < s_SystemTypes.Count; ++typeIndexNoFlags)
            {
                if (s_SystemTypes[typeIndexNoFlags] == system)
                    break;
            }

            for (int i = 0; i < s_SystemTypeDelegateIndexRanges.Count; ++i)
            {
                if (typeIndexNoFlags < s_SystemTypeDelegateIndexRanges[i])
                    return s_AssemblyGetSystemAttributesFn[i](system);
            }

            throw new ArgumentException("No function was generated for the provided type.");
        }

        static bool EntityBoxedEquals(object lhs, object rhs, int typeIndexNoFlags)
        {
            Assert.IsTrue(typeIndexNoFlags == 1);
            Entity e0 = (Entity)lhs;
            Entity e1 = (Entity)rhs;
            return e0.Equals(e1);
        }

        static bool EntityBoxedEqualsPtr(object lhs, void* rhs, int typeIndexNoFlags)
        {
            Assert.IsTrue(typeIndexNoFlags == 1);
            Entity e0 = (Entity)lhs;
            Entity e1 = *(Entity*)rhs;
            return e0.Equals(e1);
        }

        static int EntityBoxedGetHashCode(object obj, int typeIndexNoFlags)
        {
            Assert.IsTrue(typeIndexNoFlags == 1);
            Entity e0 = (Entity)obj;
            return e0.GetHashCode();
        }

        static object EntityConstructComponentFromBuffer(void* obj, int typeIndexNoFlags)
        {
            Assert.IsTrue(typeIndexNoFlags == 1);
            return *(Entity*)obj;
        }       
        
        /// <summary>
        /// Registers, all at once, the type registry information generated for each assembly.
        /// </summary>
        /// <param name="registries"></param>
        internal static void RegisterAssemblyTypes(TypeRegistry[] registries)
        {
            // The standard doesn't guarantee that we will not call this method concurrently so we need to 
            // ensure the code here can handle multiple modules registering their types at once
            bool lockTaken = false;
            try
            {
                sLock.Enter(ref lockTaken);
                int initializeTypeIndexOffset = s_TypeCount;
                s_TypeDelegateIndexRanges.Add(s_TypeCount);

                s_AssemblyBoxedEqualsFn.Add(EntityBoxedEquals);
                s_AssemblyBoxedEqualsPtrFn.Add(EntityBoxedEqualsPtr);
                s_AssemblyBoxedGetHashCodeFn.Add(EntityBoxedGetHashCode);
                s_AssemblyConstructComponentFromBufferFn.Add(EntityConstructComponentFromBuffer);
                foreach (var typeRegistry in registries)
                { 
                    int typeIndexOffset = s_TypeCount;
                    int entityOffsetsOffset = s_EntityOffsetList.Length;
                    int blobOffsetsOffset = s_BlobAssetRefOffsetList.Length;

                    foreach (var type in typeRegistry.Types)
                    {
                        s_Types.Add(type);
                    }

                    foreach (var offset in typeRegistry.EntityOffsets)
                    {
                        s_EntityOffsetList.Add(new EntityOffsetInfo() { Offset = offset });
                    }

                    foreach (var offset in typeRegistry.BlobAssetReferenceOffsets)
                    {
                        s_BlobAssetRefOffsetList.Add(new EntityOffsetInfo() { Offset = offset });
                    }

                    foreach (var typeName in typeRegistry.TypeNames)
                    {
                        s_TypeNames.Add(typeName);
                    }

                    // TODO: Replace this with a memcpy of ILPP'd TypeInfos and then fixup the values inline in s_TypeInfos
                    // once IL2CPP supports loading RVAs from static fields (see commit ce866b25a2ff1cdfae941b498bfa315f0c870e00)
                    {
                        int* newTypeIndices = stackalloc int[typeRegistry.TypeInfos.Length];
                        for (int i = 0; i < typeRegistry.TypeInfos.Length; ++i)
                        {
                            var typeInfo = typeRegistry.TypeInfos[i];
                            var newTypeIndex = typeInfo.TypeIndex + typeIndexOffset;
                            newTypeIndices[i] = newTypeIndex;

                            var newTypeInfo = new TypeInfo(
                                typeIndex: newTypeIndex,
                                category: typeInfo.Category,
                                entityOffsetCount: typeInfo.EntityOffsetCount,
                                entityOffsetStartIndex: typeInfo.EntityOffsetStartIndex + entityOffsetsOffset,
                                memoryOrdering: typeInfo.MemoryOrdering,
                                stableTypeHash: typeInfo.StableTypeHash,
                                bufferCapacity: typeInfo.BufferCapacity,
                                sizeInChunk: typeInfo.SizeInChunk,
                                elementSize: typeInfo.ElementSize,
                                alignmentInBytes: typeInfo.AlignmentInBytes,
                                maximumChunkCapacity: typeInfo.MaximumChunkCapacity,
                                writeGroupCount: 0, // we will adjust this value when we recalculate the writegroups below
                                writeGroupStartIndex: -1,
                                blobAssetRefOffsetCount: typeInfo.BlobAssetRefOffsetCount,
                                blobAssetRefOffsetStartIndex: typeInfo.BlobAssetRefOffsetStartIndex + blobOffsetsOffset,
                                fastEqualityIndex: 0,
                                typeSize: typeInfo.TypeSize);

                            s_TypeInfos[s_TypeCount++] = newTypeInfo;
                            s_StableTypeHashToTypeIndex.Add(newTypeInfo.StableTypeHash, newTypeInfo.TypeIndex);
                        }

                        // Setup our new TypeIndices into the appropriately types SharedTypeIndex<TComponent> shared static
                        typeRegistry.SetSharedTypeIndices(newTypeIndices, typeRegistry.TypeInfos.Length);
                    }

                    if (typeRegistry.Types.Length > 0)
                    {
                        s_TypeDelegateIndexRanges.Add(s_TypeCount);

                        s_AssemblyBoxedEqualsFn.Add(typeRegistry.BoxedEquals);
                        s_AssemblyBoxedEqualsPtrFn.Add(typeRegistry.BoxedEqualsPtr);
                        s_AssemblyBoxedGetHashCodeFn.Add(typeRegistry.BoxedGetHashCode);
                        s_AssemblyConstructComponentFromBufferFn.Add(typeRegistry.ConstructComponentFromBuffer);
                    }

                    // Register system info
                    int systemTypeIndexOffset = s_SystemCount;
                    foreach (var type in typeRegistry.SystemTypes)
                    {
                        s_SystemTypes.Add(type);
                        s_SystemCount++;
                    }

                    foreach (var typeName in typeRegistry.SystemTypeNames)
                    {
                        s_SystemTypeNames.Add(typeName);
                    }

                    foreach (var isSystemGroup in typeRegistry.IsSystemGroup)
                    {
                        s_SystemIsGroupList.Add(isSystemGroup);
                    }

                    if (typeRegistry.SystemTypes.Length > 0)
                    {
                        s_SystemTypeDelegateIndexRanges.Add(s_SystemCount);

                        s_AssemblyCreateSystemFn.Add(typeRegistry.CreateSystem);
                        s_AssemblyGetSystemAttributesFn.Add(typeRegistry.GetSystemAttributes);
                    }
                }
                GatherAndInitializeWriteGroups(initializeTypeIndexOffset, registries);
            }
            finally
            {
                if (lockTaken)
                {
                    sLock.Exit(true);
                }
            }
        }

        static void GatherAndInitializeWriteGroups(int typeIndexOffset, TypeRegistry[] registries)
        {
            // A this point we have loaded all Types and know all TypeInfos. Now we need to
            // go back through each assembly, determine if a type has a write group, and if so
            // translate the Type of the writegroup component to a TypeIndex. But, we must do this incrementally
            // for all assemblies since AssemblyA can add to the writegroup list of a type defined in AssemblyB.
            // Once we have a complete mapping, generate the s_WriteGroup array and fixup all writegroupStart
            // indices in our type infos

            // We create a list of hashmaps here since we can't put a NativeHashMap inside of a NativeHashMap in debug builds due to DisposeSentinels being managed
            var hashSetList = new List<NativeHashMap<int, byte>>();
            NativeHashMap<int, int> writeGroupMap = new NativeHashMap<int, int>(1024, Allocator.Temp);
            foreach (var typeRegistry in registries)
            {
                foreach (var typeInfo in typeRegistry.TypeInfos)
                {
                    if (typeInfo.WriteGroupCount > 0)
                    {
                        var typeIndex = typeInfo.TypeIndex + typeIndexOffset;

                        for (int wgIndex = 0; wgIndex < typeInfo.WriteGroupCount; ++wgIndex)
                        {
                            var targetType = typeRegistry.WriteGroups[typeInfo.WriteGroupStartIndex + wgIndex];
                            // targetType isn't necessarily from this assembly (it could be from one of its references)
                            // so lookup the actual typeIndex since we loaded all assembly types above
                            var targetTypeIndex = GetTypeIndex(targetType);

                            if (!writeGroupMap.TryGetValue(targetTypeIndex, out var targetSetIndex))
                            {
                                targetSetIndex = hashSetList.Count;
                                writeGroupMap.Add(targetTypeIndex, targetSetIndex);
                                hashSetList.Add(new NativeHashMap<int, byte>(typeInfo.WriteGroupCount, Allocator.Temp));
                            }
                            var targetSet = hashSetList[targetSetIndex];
                            targetSet.TryAdd(typeIndex, 0); // We don't have a NativeSet, so just push 0
                        }
                    }
                }
                typeIndexOffset += typeRegistry.TypeInfos.Length;
            }

            using (var keys = writeGroupMap.GetKeyArray(Allocator.Temp))
            {
                foreach (var typeIndex in keys)
                {
                    var index = typeIndex & ClearFlagsMask;
                    var typeInfo = s_TypeInfos[index];

                    var valueIndex = writeGroupMap[typeIndex];
                    var valueSet = hashSetList[valueIndex];
                    using (var values = valueSet.GetKeyArray(Allocator.Temp))
                    {
                        typeInfo.WriteGroupStartIndex = s_WriteGroupList.Length;
                        typeInfo.WriteGroupCount = values.Length;
                        foreach (var ti in values)
                            s_WriteGroupList.Add(ti);
                    }

                    s_TypeInfos[index] = typeInfo;
                    valueSet.Dispose();
                }
            }
            writeGroupMap.Dispose();
        }

        static ulong GetEntityStableTypeHash()
        {
            throw new CodegenShouldReplaceException("This call should have been replaced by codegen");
        }
#endif
    }
    
#if NET_DOTS
    public class TypeRegistry
    {
        // TODO: Have Burst generate a native function ptr we can invoke instead of using a delegate
        public delegate bool GetBoxedEqualsFn(object lhs, object rhs, int typeIndexNoFlags);
        public unsafe delegate bool GetBoxedEqualsPtrFn(object lhs, void* rhs, int typeIndexNoFlags);
        public delegate int BoxedGetHashCodeFn(object obj, int typeIndexNoFlags);
        public unsafe delegate object ConstructComponentFromBufferFn(void* buffer, int typeIndexNoFlags);
        public unsafe delegate void SetSharedTypeIndicesFn(int* typeInfoArray, int count);
        public delegate Attribute[] GetSystemAttributesFn(Type system);
        public delegate object CreateSystemFn(Type system);

        public GetBoxedEqualsFn BoxedEquals;
        public GetBoxedEqualsPtrFn BoxedEqualsPtr;
        public BoxedGetHashCodeFn BoxedGetHashCode;
        public ConstructComponentFromBufferFn ConstructComponentFromBuffer;
        public SetSharedTypeIndicesFn SetSharedTypeIndices;
        public GetSystemAttributesFn GetSystemAttributes;
        public CreateSystemFn CreateSystem;

#pragma warning disable 0649
        public string AssemblyName;
        public TypeManager.TypeInfo[] TypeInfos;
        public Type[] Types;
        public string[] TypeNames;
        public int[] EntityOffsets;
        public int[] BlobAssetReferenceOffsets;
        public Type[] WriteGroups;

        public Type[] SystemTypes;
        public string[] SystemTypeNames;
        public bool[] IsSystemGroup;
#pragma warning restore 0649
    }
#endif
}
