using UnityEngine;

namespace Syncity
{
    [System.Serializable]
    public class SingleUnityLayer
    {
        [SerializeField]
        int _layerIndex = 0;

        public int layerIndex
        {
            get { return _layerIndex; }
            set
            {
                if (value > 0 && value < 32)
                {
                    this._layerIndex = value;
                }
            }
        }
 
        public int mask => 1 << _layerIndex;
    }
}