using System;
using UnityEngine;
using UnityEngine.Events;

namespace Syncity.Sensors
{
    [Serializable]
    public class UnityEventVector3 : UnityEvent<Vector3> {}
    
    public class IMU : MonoBehaviour 
    {
        Rigidbody _linkedRigidBody;
        public Rigidbody linkedRigidBody
        {
            get
            {
                if (_linkedRigidBody == null)
                {
                    _linkedRigidBody = GetComponentInParent<Rigidbody>();
                }
                return _linkedRigidBody;
            }
        }

        Vector3 prevVelocity;
        Vector3 prevAngularVelocity;
        void OnEnable()
        {
            if (linkedRigidBody != null)
            {
                prevVelocity = linkedRigidBody.velocity;
                prevAngularVelocity = linkedRigidBody.angularVelocity;
                acceleration = Vector3.zero;
                gyro = Vector3.zero;
            }
        }
        
        Vector3 _acceleration;
        public UnityEventVector3 onAcceleration;
        public Vector3 acceleration
        {
            get { return _acceleration; }
            private set
            {
                if (_acceleration != value)
                {
                    _acceleration = value;
                    onAcceleration?.Invoke(_acceleration);
                }
            }
        }

        Vector3 _gyro;
        public UnityEventVector3 onGyro;
        public Vector3 gyro
        {
            get { return _gyro; }
            set
            {
                if (_gyro != value)
                {
                    _gyro = value;
                    onGyro?.Invoke(_gyro);
                }
            }
        }

        [SerializeField]
        ScriptableObject _accelerationNoiseGenerator;
        public INoiseGenerator<Vector3> accelerationNoiseGenerator
        {
            get { return _accelerationNoiseGenerator as INoiseGenerator<Vector3>; }
            set { _accelerationNoiseGenerator = value as ScriptableObject; }
        }
        [SerializeField]
        ScriptableObject _gyroNoiseGenerator;
        public INoiseGenerator<Vector3> gyroNoiseGenerator
        {
            get { return _gyroNoiseGenerator as INoiseGenerator<Vector3>; }
            set { _gyroNoiseGenerator = value as ScriptableObject; }
        }

        void FixedUpdate()
        {
            if (linkedRigidBody != null)
            {
                var currentVelocity = linkedRigidBody.velocity;
                var currentAngularVelocity = linkedRigidBody.angularVelocity;

                acceleration = (prevVelocity - currentVelocity) / Time.fixedDeltaTime;
                if (accelerationNoiseGenerator != null)
                {
                    acceleration = accelerationNoiseGenerator.Generate(acceleration);
                }

                gyro = (prevAngularVelocity - currentAngularVelocity) / Time.fixedDeltaTime;
                if (gyroNoiseGenerator != null)
                {
                    gyro = gyroNoiseGenerator.Generate(gyro);
                }

                prevVelocity = currentVelocity;
                prevAngularVelocity = currentAngularVelocity;
            }
        }
    }
}
