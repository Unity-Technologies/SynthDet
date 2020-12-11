using System;
using UnityEngine;

namespace Unity.Simulation.DistributedRendering.Render
{
    public enum MessageType : UInt32
    {
        Unknown,
        StartFrame,
        SetGameObjectState,
        SetTransform,
        PrefabPath,
        EndFrame,
        EndSimulation,
        RequestServerId,
        AnnounceServerId,
        RequestFrame,
        DestroyObject
    }

    public struct TransformProxy
    {
        public Vector3 LocalPosition { get; set; }
        public Vector3 LocalEulerAngles { get; set; }
        public Vector3 LocalScale { get; set; }

        public static TransformProxy FromTransform(Transform source)
        {
            var result = new TransformProxy()
            {
                LocalPosition = source.localPosition,
                LocalEulerAngles = source.localEulerAngles,
                LocalScale = source.localScale,
            };
            return result;
        }

        public void Apply(Transform t)
        {
            t.localPosition = LocalPosition;
            t.localEulerAngles = LocalEulerAngles;
            t.localScale = LocalScale;
        }

        public bool HasChanged(Transform source)
        {
            return !source.localPosition.Equals(LocalPosition) ||
                   !source.localEulerAngles.Equals(LocalEulerAngles) ||
                   !source.localScale.Equals(LocalScale);
        }
    }


    public interface IMessageSerializer
    {
        int ReadInt();
        void Write(int i);

        long ReadLong();
        void Write(long val);

        void Write(bool val);

        uint ReadUInt32();
        void Write(UInt32 val);


        float ReadFloat();

        bool ReadBool();
        void Write(float f);
        string ReadString();
        void Write(string s);

        Vector3 ReadVec3();
        void Write(Vector3 v);

        void ReadTransform(Transform t);
        void Write(Transform t);

        TransformProxy ReadTransformProxy();
        void Write(TransformProxy proxy);

        MessageType ReadMessageType();
        void Write(MessageType messageType);

    }

}
