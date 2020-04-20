using System;
using System.Text;

using UnityEngine;

namespace Unity.Simulation
{
    /// <summary>
    /// Tracks simulation and wall time, and periodically outputs to log.
    /// </summary>
    [Obsolete("Obsolete msg -> TimeLogger (UnityUpgradable)", true)]
    public static class DXTimeLogger {}

    /// <summary>
    /// Tracks simulation and wall time, and periodically outputs to log.
    /// </summary>
    public static class TimeLogger
    {
        private const float kDefaultLoggingTimeout = 10f;

        /// <summary>
        /// Gets/sets the logging interval in seconds.
        /// </summary>
        public static float loggingIntervalSeconds { get; set; }

        /// <summary>
        /// Enable/disable logging of simulation time.
        /// Simulation time advances when the simulation is running.
        /// </summary>
        public static bool logSimulationTime { get; set; } = true;

        /// <summary>
        /// Enable/disable logging of unscaled simulation time.
        /// Unscaled time is not affected by Time.timeScale.
        /// </summary>
        public static bool logUnscaledSimulationTime { get; set; } = true;

        /// <summary>
        /// Enable/disable logging of wall time.
        /// Wall time advances like time on the wall, i.e. always, independent of simulation state like pause etc.
        /// </summary>
        public static bool logWallTime { get; set; } = true;

        /// <summary>
        /// Enable/disable logging of FPS.
        /// </summary>
        public static bool logFrameTiming { get; set; } = true;

        static float _elapsedTime;
        static uint  _frameCount;

        static StringBuilder _stringBuilder = new StringBuilder(200);

        [RuntimeInitializeOnLoadMethod]
        static void Register()
        {
            Manager.Instance.Tick += Update;
        }

        static TimeLogger()
        {
            loggingIntervalSeconds = Configuration.Instance.SimulationConfig.time_logging_timeout_sec;
            if (loggingIntervalSeconds >= 0)
            {
                loggingIntervalSeconds = loggingIntervalSeconds > 0 ? loggingIntervalSeconds : kDefaultLoggingTimeout;
            }
            _elapsedTime = 0;
            _frameCount = 0;
        }

        static void Append(string name, string value)
        {
            _stringBuilder.Append(name);
            _stringBuilder.Append('(');
            _stringBuilder.Append(value);
            _stringBuilder.Append(") ");
        }

        static void Update(float dt)
        {
            if (loggingIntervalSeconds < float.Epsilon)
                return;

            if (!logWallTime && !logSimulationTime && !logUnscaledSimulationTime && !logFrameTiming)
                return;
                
            ++_frameCount;

            _elapsedTime += Time.deltaTime;

            if (_elapsedTime >= loggingIntervalSeconds)
            {
                _stringBuilder.Clear();

                _stringBuilder.Append("USim Time (secs) : ");

                if (logWallTime)
                {
                    var time = Manager.Instance.WallElapsedTime.ToString("N3");
                    Append("Wall", time);
                }

                if (logSimulationTime)
                {
                    var time = Manager.Instance.SimulationElapsedTime.ToString("N3");
                    Append("Simulation", time);
                }

                if (logUnscaledSimulationTime)
                {
                    var time = Manager.Instance.SimulationElapsedTimeUnscaled.ToString("N3");
                    Append("Unscaled", time);
                }

                if (logFrameTiming)
                {
                    var time = (1.0f / (Manager.Instance.WallElapsedTime / _frameCount)).ToString("N3");
                    Append("FPS", time);
                }

                Log.I(_stringBuilder.ToString());

                while (_elapsedTime >= loggingIntervalSeconds)
                    _elapsedTime -= loggingIntervalSeconds;
                _frameCount = 0;
            }
        }
    }
}
