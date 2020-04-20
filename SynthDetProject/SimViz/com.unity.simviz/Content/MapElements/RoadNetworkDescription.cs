using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace UnityEngine.SimViz.Content.MapElements
{
    internal interface IHasS
    {
        float GetS();
    }

    [Serializable, PreferBinarySerialization]
    public class RoadNetworkDescription : ScriptableObject, ISerializationCallbackReceiver
    {
        private Dictionary<string, int> _roadIdToIndex;
        private Dictionary<string, int> _junctionIdToIndex;
        //private Dictionary<string, int> AllJunctions;

        [NonSerialized]
        public int entityRoadId;

        public Road[] AllRoads => _roadElements;

        public Junction[] AllJunctions => _junctionElements;

        [SerializeField, HideInInspector]
        private Road[] _roadElements;

        [SerializeField,HideInInspector]
        private Junction[] _junctionElements;

        public void OnEnable()
        {
            hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
        }

        public bool HasRoad(string roadId)
        {
            return _roadIdToIndex.ContainsKey(roadId);
        }

        public void OnAfterDeserialize()
        {
            BuildLinkLookUpTables();
        }

        public void OnBeforeSerialize()
        {
        }

        public Junction GetJunctionById(string id)
        {
            return _junctionElements[_junctionIdToIndex[id]];
        }

        public int GetJunctionIndexById(string id)
        {
            return _junctionIdToIndex[id];
        }

        public Road GetRoadById(NativeString64 id)
        {
            return GetRoadById(id.ToString());
        }

        public Road GetRoadById(string id)
        {
            return _roadElements[_roadIdToIndex[id]];
        }

        public int GetRoadIndexById(string id)
        {
            return _roadIdToIndex[id];
        }

        internal void SetRoadsAndJunctions(List<Road> roads, List<Junction> junctions)
        {
            if (_roadElements != default)
            {
                throw new Exception($"{nameof(_roadElements)} already had a reference assigned.");
            }

            if (_junctionElements != default)
            {
                throw new Exception($"{nameof(_junctionElements)} already had a reference assigned.");
            }

            _roadElements = roads.ToArray();
            _junctionElements = junctions.ToArray();
            BuildLinkLookUpTables();
        }

        private void BuildLinkLookUpTables()
        {
            _roadIdToIndex = new Dictionary<string, int>(_roadElements.Length);
            _junctionIdToIndex = new Dictionary<string, int>(_junctionElements.Length);

            for (int i = 0; i < _roadElements.Length; ++i)
            {
                _roadIdToIndex.Add(_roadElements[i].roadId, i);
            }

            for (int i = 0; i < _junctionElements.Length; ++i)
            {
                _junctionIdToIndex.Add(_junctionElements[i].junctionId, i);
            }
        }
    }
}


