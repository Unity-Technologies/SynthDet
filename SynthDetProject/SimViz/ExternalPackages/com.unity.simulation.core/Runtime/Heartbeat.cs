using System;
using System.IO;

using UnityEngine;

namespace Unity.Simulation
{
    internal static class Heartbeat
    {
        const float kDefaultHeartbeatTimeoutSeconds = 10;
        static float _heartbeat_timeout_sec = kDefaultHeartbeatTimeoutSeconds;
        static float _heartbeat_elapsed_time_sec = 0;

        [RuntimeInitializeOnLoadMethod]
        static void Register()
        {
            Manager.Instance.StartNotification += Start;
            Manager.Instance.Tick += UpdateHeartbeat;
        }

        internal static void Start()
        {
            var hbto = Configuration.Instance.SimulationConfig.heartbeat_timeout_sec;
            if (hbto >= 0)
            {
                _heartbeat_timeout_sec = hbto > 0 ? hbto : kDefaultHeartbeatTimeoutSeconds;
            }
        }

        /// <summary>
        /// Update heartbeat log indicating the progress of the simulation
        /// </summary>
        /// <param name="dt">delta time to be added to the elapsed time.</param>
        internal static void UpdateHeartbeat(float dt)
        {
            // this because heartbeat is independent of time scale.
            dt = Time.unscaledDeltaTime;

            _heartbeat_elapsed_time_sec += dt;
            if (_heartbeat_timeout_sec > 0 && _heartbeat_elapsed_time_sec >= _heartbeat_timeout_sec)
            {
                while (_heartbeat_elapsed_time_sec >= _heartbeat_timeout_sec)
                    _heartbeat_elapsed_time_sec -= _heartbeat_timeout_sec;

                try
                {
                    var filepath = Path.Combine(Manager.Instance.GetDirectoryFor(DataCapturePaths.Logs), Manager.kHeartbeatFileName);
                    using (var writer = File.AppendText(filepath))
                    {
                        writer.Write(Time.timeSinceLevelLoad.ToString("N3") + Environment.NewLine);
                    }
                }
                catch (Exception e)
                {
                    Log.E("UpdateHeartbeat.Write exception : " + e.ToString());
                }
            }
        }
    }
}