using System;
using UnityEngine;

namespace Unity.Simulation.DistributedRendering.Render
{
    /// <summary>
    /// Provides methods for serializing this object's transform hierarchy and creating persistent object IDs
    /// TODO: Move this to a ScriptableObject. Remove debug methods when appropriate.
    /// </summary>
    ///
    public class TransformSerializerBehavior : MonoBehaviour
    {
        public void ResetIDs()
        {
            Debug.Log("Create IDs:\n");
            ResetIDs(gameObject);
            Debug.Log("Create IDs:done.\n");
        }

        private void ResetIDs(GameObject obj)
        {
            GameObjectId state = null;

            if (!obj.TryGetComponent<GameObjectId>(out state))
            {
                state = obj.AddComponent<GameObjectId>();
            }
            
            state.uniqueId = Guid.NewGuid().ToString();

            for (var i = 0; i < obj.transform.childCount; ++i)
            {
                ResetIDs(obj.transform.GetChild(i).gameObject);
            }
        }

        public void PrintIDs()
        {
            Debug.Log("Print IDs:\n");
            PrintIDs(gameObject);
            Debug.Log("Print IDs: done.\n");
        }

        private void PrintIDs(GameObject obj, int indent = 0)
        {
            GameObjectId state;
            if (obj.TryGetComponent(out state))
            {
                var message = "";

                for (var i = 0; i < indent; ++i)
                {
                    message += '\t';
                }

                message += obj.name + '\t' + state.uniqueId + '\n';

                Debug.Log(message);
            }

            for (var i = 0; i < obj.transform.childCount; ++i)
            {
                PrintIDs(obj.transform.GetChild(i).gameObject, indent + 1);
            }
        }

        public void RemoveIDs()
        {
            Debug.Log("Remove IDs:\n");
            RemoveIDs(gameObject);
            Debug.Log("Remove IDs: done.\n");
        }

        private void RemoveIDs(GameObject obj)
        {
            GameObjectId state = null;

            if (obj.TryGetComponent<GameObjectId>(out state))
            {
                Debug.Log("Remove GameObjectState from '" + obj.name + "'\n");
                DestroyImmediate(obj.GetComponent<GameObjectId>());
            }

            for (var i = 0; i < obj.transform.childCount; ++i)
            {
                RemoveIDs(obj.transform.GetChild(i).gameObject);
            }
        }

    }
}


