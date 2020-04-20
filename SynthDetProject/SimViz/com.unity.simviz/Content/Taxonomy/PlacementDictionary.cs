using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SimViz.Content.Pipeline;

namespace UnityEngine.SimViz.Content.Taxonomy
{
    [Serializable]
    public class PlacementDictionary : ScriptableObject, ISerializationCallbackReceiver
    {
        public IReadOnlyDictionary<string, PlacementCategory> placements { get; private set; }
        
        [SerializeField]
        List<string> _keys;
        [SerializeField]
        List<PlacementCategory> _values;
        
        public void SetFromList(IEnumerable<string> names, IEnumerable<PlacementCategory> categories)
        {
            _keys = new List<string>(names);
            _values = new List<PlacementCategory>(categories);
            ToDictionary();
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            ToDictionary();
        }

        void ToDictionary()
        {
            placements = _keys.Zip(_values, (k, v) => new { Key = k, Value = v })
                .ToDictionary(x => x.Key, x => x.Value);
        }
    }
}
