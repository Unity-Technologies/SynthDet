using UnityEngine;

namespace Syncity.Sensors
{
    public class GPSOrigin : ScriptableObject
    {
        static GPSOrigin _instance;
        public static GPSOrigin instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<GPSOrigin>(typeof(GPSOrigin).FullName);
                    if(Application.isPlaying && _instance == null)
                    {
                        _instance = CreateInstance<GPSOrigin>();
                    }
                }
                return _instance;
            }
            private set
            {
                if (_instance == value)
                    return;
                if (_instance != null)
                    throw new System.InvalidOperationException(
                        $"Two singletons ({_instance.GetType().FullName}) in the scene!");
                _instance = value;
            }
        }
        public virtual void Awake()
        {
            instance = this;
        }

        public virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        
        public GPSPosition zeroPosition = new GPSPosition(40.452851f, -3.688510f, 425f);
        public float metersPerUnit = 1f;
    }
}