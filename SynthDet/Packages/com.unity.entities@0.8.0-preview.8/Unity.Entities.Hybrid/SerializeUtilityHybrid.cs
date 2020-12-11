using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityObject = UnityEngine.Object;

namespace Unity.Entities.Serialization
{
    public static class SerializeUtilityHybrid
    {
        public static void Serialize(EntityManager manager, BinaryWriter writer, out ReferencedUnityObjects objRefs)
        {
            SerializeUtility.SerializeWorld(manager, writer, out var referencedObjects);
            SerializeObjectReferences(manager, writer, (UnityEngine.Object[]) referencedObjects, out objRefs);
        }

        public static void Serialize(EntityManager manager, BinaryWriter writer, out ReferencedUnityObjects objRefs, NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapInfos)
        {
            SerializeUtility.SerializeWorld(manager, writer, out var referencedObjects, entityRemapInfos);
            SerializeObjectReferences(manager, writer, (UnityEngine.Object[]) referencedObjects, out objRefs);
        }

        public static void Deserialize(EntityManager manager, BinaryReader reader, ReferencedUnityObjects objRefs)
        {
            DeserializeObjectReferences(manager, objRefs, "", out var objectReferences);
            var transaction = manager.BeginExclusiveEntityTransaction();
            SerializeUtility.DeserializeWorld(transaction, reader, objectReferences);
            manager.EndExclusiveEntityTransaction();
        }

        public static void SerializeObjectReferences(EntityManager manager, BinaryWriter writer, UnityEngine.Object[] referencedObjects, out ReferencedUnityObjects objRefs)
        {
            objRefs = null;

            if (referencedObjects?.Length > 0)
            {
                objRefs = UnityEngine.ScriptableObject.CreateInstance<ReferencedUnityObjects>();
                objRefs.Array = referencedObjects;
            }
        }

        public static void DeserializeObjectReferences(EntityManager manager, ReferencedUnityObjects objRefs, string debugSceneName, out UnityEngine.Object[] objectReferences)
        {
            objectReferences = objRefs?.Array;

            // NOTE: Object references must not include fake object references, they must be real null.
            // The Unity.Properties deserializer can't handle them correctly.
            // We might want to add support for handling fake null,
            // but it would require tight integration in the deserialize function so that a correct fake null unityengine.object can be constructed on deserialize
            if (objectReferences != null)
            {
#if !UNITY_EDITOR || USE_SUBSCENE_EDITORBUNDLES
                // When using bundles, the Companion GameObjects cannot be directly used (prefabs), so we need to instantiate everything.
                var sourceToInstance = new Dictionary<UnityEngine.GameObject, UnityEngine.GameObject>();
#endif

                for (int i = 0; i != objectReferences.Length; i++)
                {
                    if (objectReferences[i] == null)
                    {
                        objectReferences[i] = null;
                        continue;
                    }

#if !UNITY_EDITOR || USE_SUBSCENE_EDITORBUNDLES
                    if(objectReferences[i] is UnityEngine.GameObject source)
                    {
                        var instance = UnityEngine.GameObject.Instantiate(source);
                        objectReferences[i] = instance;
                        sourceToInstance.Add(source, instance);
                    }
#endif
                }

#if !UNITY_EDITOR || USE_SUBSCENE_EDITORBUNDLES
                for (int i = 0; i != objectReferences.Length; i++)
                {
                    if(objectReferences[i] is UnityEngine.Component component)
                    {
                        objectReferences[i] = sourceToInstance[component.gameObject].GetComponent(component.GetType());
                    }
                }
#endif
            }
        }
    }
}
