using System;
using System.ComponentModel;

namespace Unity.Core
{
    public readonly struct TimeData
    {
        /// <summary>
        /// The total cumulative elapsed time in seconds.
        /// </summary>
        public readonly double ElapsedTime;

        /// <summary>
        /// The time in seconds since the last time-updating event occurred. (For example, a frame.)
        /// </summary>
        public readonly float DeltaTime;

        /// <summary>
        /// Create a new TimeData struct with the given values.
        /// </summary>
        /// <param name="elapsedTime">Time since the start of time collection.</param>
        /// <param name="deltaTime">Elapsed time since the last time-updating event occurred.</param>
        public TimeData(double elapsedTime, float deltaTime)
        {
            ElapsedTime = elapsedTime;
            DeltaTime = deltaTime;
        }

    #if !UNITY_DOTSPLAYER

        // This member will be deprecated once a native fixed delta time is introduced in dots.
        [EditorBrowsable(EditorBrowsableState.Never)]
        public float fixedDeltaTime => UnityEngine.Time.fixedDeltaTime;

    #endif

    }
}
