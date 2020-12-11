using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Unity.Simulation.DistributedRendering.Render
{
    public class ClusterTimer : MonoBehaviour
    {
        public long updateIntervalMs = 1000;
        public int maxQueueLength = 120;

        public float AverageTimePerUpdateIntervalMs { get; set; }

        private Queue<long> _times = new Queue<long>();
        private long _queueSum = 0;
        private long _timeUntilUpdate = 0;
        private long _lastTimeMs;

        private readonly Stopwatch _stopwatch = new Stopwatch();

        // Start is called before the first frame update
        void Start()
        {
            _stopwatch.Start();
        }

        void OnDestroy()
        {
            _stopwatch.Stop();
        }

        public void Tick()
        {
            var ms = _stopwatch.ElapsedMilliseconds;
            _timeUntilUpdate -= ms;

            var delta = ms - _lastTimeMs;
            _lastTimeMs = ms;

            while(_times.Count >= maxQueueLength)
            {
                var d = _times.Dequeue();
                _queueSum -= d;
            }

            _times.Enqueue(delta);
            _queueSum += delta;

            if (_timeUntilUpdate <= 0)
            {
                AverageTimePerUpdateIntervalMs = _times.Count * 1000.0f / _queueSum;
                _timeUntilUpdate += updateIntervalMs;
            }
        }


    }

}

