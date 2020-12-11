using System;
using System.Net;
using System.Net.Sockets;

using UnityEngine;

namespace Unity.Simulation.DistributedRendering
{
    public class Message
    {
        public uint      messageId;
        public IPAddress address;
        public ulong     instanceId;
        public double    arrivalTime;
        public float     timeoutSecs;
        public byte[]    payload;

        public Message(uint messageId, IPAddress address, ulong instanceId, double arrivalTime, float timeoutSecs, byte[] payload)
        {
            this.messageId   = messageId;
            this.address     = address;
            this.instanceId  = instanceId;
            this.arrivalTime = arrivalTime;
            this.timeoutSecs = timeoutSecs;
            this.payload     = payload;
        }

        public void GetPlayload<T>(out T value) where T : struct
        {
            Utils.Read<T>(payload, 0, out value);
        }
    }

    public class OutboundMessage : Message
    {
        public double      startTime;
        public double      lastTime;
        public float       totalTime;
        public int         remainingAcks;
        public IPAddress[] acks;
        public float[]     ackTimes;
        public ClusterManager.CompleteMessageDelegate completion;

        public bool timedOut
        {
            get { return _timedOut; }
        }

        public bool completed
        {
            get { return remainingAcks == 0 || timedOut; }
        }

        public OutboundMessage(uint messageId, IPAddress address, float timeoutSecs, int expectedAcks, ulong instanceId, double time, byte[] payload, ClusterManager.CompleteMessageDelegate completion = null)
            : base(messageId, address, instanceId, 0, timeoutSecs, payload)
        {
            Debug.Assert(messageId   != 0);
            Debug.Assert(timeoutSecs  > 0);

            startTime       = time;
            lastTime        = time;
            remainingAcks   = expectedAcks;
            acks            = new IPAddress[remainingAcks];
            ackTimes        = new float[remainingAcks];
            this.completion = completion;
        }

        public void HandleAck(IPAddress address, double time)
        {
            lock (this)
            {
                if (acks.Length == 0 || _HasAck(address))
                    return;

                for (var i = 0; i < acks.Length; ++i)
                {
                    if (acks[i] == null)
                    {
                        UnityEngine.Debug.Assert(remainingAcks > 0);

                        --remainingAcks;
                        acks[i]     = address;
                        ackTimes[i] = (float)(time - startTime);

                        Log.V($"HandleAck: message {Utils.FourCCToString(messageId)} address {address.ToString()} id {instanceId & 0xffffffff} time {ackTimes[i].ToString(ClusterManager.printFloatPrecision)} remaining {remainingAcks}");
                        return;
                    }
                }
            }
            throw new Exception("Message.HandleAck failed to ack receipt.");
        }

        public void Complete(double time)
        {
            _timedOut = arrivalTime + timeoutSecs < time;
            totalTime = (float)(time - startTime);

            Log.V($"Complete: message {Utils.FourCCToString(messageId)} completed in {totalTime.ToString(ClusterManager.printFloatPrecision)} seconds. timedOut {timedOut.ToString()}");
        }

        // Protected / Private Members

        bool _HasAck(IPAddress address)
        {
            lock (this)
            {
                foreach (var a in acks)
                    if (address.Equals(a))
                        return true;
                return false;
            }
        }

        bool _timedOut;
    }
}