using System;
using System.IO;

using UnityEngine;

namespace Unity.AI.Simulation
{
    public static class DXHeartbeat
    {
        const float kDefaultHeartbeatTimeoutSeconds = 10;
        static float _heartbeat_timeout_sec = kDefaultHeartbeatTimeoutSeconds;
        static float _heartbeat_elapsed_time_sec = 0;

        [RuntimeInitializeOnLoadMethod]
        static void Register()
        {
            DXManager.Instance.StartNotification += Start;
            DXManager.Instance.Tick += UpdateHeartbeat;
        }

        public static void Start()
        {
            var hbto = Configuration.Instance.SimulationConfig.heartbeat_timeout_sec;
            if (hbto >= 0)
            {
                _heartbeat_timeout_sec = hbto > 0 ? hbto : kDefaultHeartbeatTimeoutSeconds;
            }
        }

        public static void UpdateHeartbeat(float dt)
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
                    var filepath = Path.Combine(DXManager.Instance.GetDirectoryFor(DataCapturePaths.Logs), DXManager.kHeartbeatFileName);
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