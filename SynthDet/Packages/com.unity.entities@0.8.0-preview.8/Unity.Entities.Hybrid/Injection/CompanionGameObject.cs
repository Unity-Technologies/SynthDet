#if !UNITY_DISABLE_MANAGED_COMPONENTS
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities
{
    struct EditorCompanionInPreviewSceneTag : IComponentData { }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad] // ensures type manager is initialized on domain reload when not playing
#endif
    static unsafe class AttachToEntityClonerInjection
    {
        // Injection is used to keep everything GameObject related outside of Unity.Entities

        static AttachToEntityClonerInjection()
        {
            Initialize();
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            TypeManager.Initialize();
            ManagedComponentStore.CompanionLinkTypeIndex = TypeManager.GetTypeIndex(typeof(CompanionLink));
            ManagedComponentStore.InstantiateHybridComponent = InstantiateHybridComponentDelegate;
            ManagedComponentStore.AssignHybridComponentsToCompanionGameObjects = AssignHybridComponentsToCompanionGameObjectsDelegate;
        }

        /// <summary>
        /// This method will handle the cloning of Hybrid Components (if any) during the batched instantiation of an Entity
        /// </summary>
        /// <param name="srcArray">Array of source managed component indices. One per <paramref name="componentCount"/></param>
        /// <param name="componentCount">Number of component being instantiated</param>
        /// <param name="dstEntities">Array of destination entities. One per <paramref name="instanceCount"/></param>
        /// <param name="dstCompanionLinkIndices">Array of destination CompanionLink indices, can be null if the hybrid components are not owned</param>
        /// <param name="dstArray">Array of destination managed component indices. One per <paramref name="componentCount"/>*<paramref name="instanceCount"/>. All indices for the first component stored first etc.</param>
        /// <param name="instanceCount">Number of instances being created</param>
        /// <param name="managedComponentStore">Managed Store that owns the instances we create</param>
        static void InstantiateHybridComponentDelegate(int* srcArray, int componentCount, Entity* dstEntities, int* dstCompanionLinkIndices, int* dstArray, int instanceCount, ManagedComponentStore managedComponentStore)
        {
            if (dstCompanionLinkIndices != null)
            {
                var dstCompanionGameObjects = new GameObject[instanceCount];
                for (int i = 0; i < instanceCount; ++i)
                {
                    var companionLink = (CompanionLink)managedComponentStore.GetManagedComponent(dstCompanionLinkIndices[i]);
                    dstCompanionGameObjects[i] = companionLink.Companion;
                    CompanionLink.SetCompanionName(dstEntities[i], dstCompanionGameObjects[i]);
                }

                for (int src = 0; src < componentCount; ++src)
                {
                    var componentType = managedComponentStore.GetManagedComponent(srcArray[src]).GetType();

                    for (int i = 0; i < instanceCount; i++)
                    {
                        var componentInInstance = dstCompanionGameObjects[i].GetComponent(componentType);
                        managedComponentStore.SetManagedComponentValue(dstArray[i], componentInInstance);
                    }
                }
            }
            else
            {
                for (int src = 0; src < componentCount; ++src)
                {
                    var component = managedComponentStore.GetManagedComponent(srcArray[src]);

                    for (int i = 0; i < instanceCount; i++)
                    {
                        managedComponentStore.SetManagedComponentValue(dstArray[i], component);
                    }
                }
            }
        }
        
        static void AssignHybridComponentsToCompanionGameObjectsDelegate(EntityManager entityManager, NativeArray<Entity> entities)
        {
            for (int i = 0; i < entities.Length; ++i)
            {
                var entity = entities[i];
                var companionGameObject = entityManager.GetComponentData<CompanionLink>(entity).Companion;

                var archetypeChunk = entityManager.GetChunk(entities[i]);
                var archetype = archetypeChunk.Archetype.Archetype;

                var types = archetype->Types;
                var firstIndex = archetype->FirstManagedComponent;
                var lastIndex = archetype->ManagedComponentsEnd;

                for (int t = firstIndex; t < lastIndex; ++t)
                {
                    var type = TypeManager.GetTypeInfo(types[t].TypeIndex);

                    if (type.Category != TypeManager.TypeCategory.Class)
                        continue;

                    var hybridComponent = companionGameObject.GetComponent(type.Type);
                    entityManager.SetComponentObject(entity, ComponentType.FromTypeIndex(type.TypeIndex), hybridComponent);
                }
            }

            entityManager.RemoveComponent<CompanionGameObjectActiveSystemState>(entities);
            entityManager.RemoveComponent<EditorCompanionInPreviewSceneTag>(entities);
        }
    }
}
#endif
