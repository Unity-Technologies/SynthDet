using System.Text;

using UnityEngine;

namespace Unity.AI.Simulation
{ 
    public static class DXTimeLogger
    {
        private const float kDefaultLoggingTimeout = 10f;
        public static float loggingIntervalSeconds    { get; set; }
        public static bool  logSimulationTime         { get; set; } = true;
        public static bool  logUnscaledSimulationTime { get; set; } = true;
        public static bool  logWallTime               { get; set; } = true;
        public static bool  logFrameTiming            { get; set; } = true;

        static float _elapsedTime;
        static uint  _frameCount;

        static StringBuilder _stringBuilder = new StringBuilder(200);

        [RuntimeInitializeOnLoadMethod]
        static void Register()
        {
            DXManager.Instance.Tick += Update;
        }

        static DXTimeLogger()
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
                    var time = DXManager.Instance.WallElapsedTime.ToString("N3");
                    Append("Wall", time);
                }

                if (logSimulationTime)
                {
                    var time = DXManager.Instance.SimulationElapsedTime.ToString("N3");
                    Append("Simulation", time);
                }

                if (logUnscaledSimulationTime)
                {
                    var time = DXManager.Instance.SimulationElapsedTimeUnscaled.ToString("N3");
                    Append("Unscaled", time);
                }

                if (logFrameTiming)
                {
                    var time = (1.0f / (_elapsedTime / _frameCount)).ToString("N3");
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
