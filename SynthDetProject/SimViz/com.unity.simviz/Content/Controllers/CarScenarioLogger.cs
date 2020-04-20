using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UnityEngine.SimViz.Scenarios
{
    public abstract class ScenarioEvent
    {
        protected Dictionary<string, object> m_Payload = new Dictionary<string, object>();

        public int FrameCount { get; set; }
        public float TimeStamp { get; set; }
        public int ScenarioId { get; set; }
        public string Name { get; set; }

        public object this[string name]
        {
            get => m_Payload[name]; 
            set => m_Payload[name] = value;
        }

        protected ScenarioEvent() { }


        protected ScenarioEvent(int scenarioId, string name)
        {
            FrameCount = Time.frameCount;
            TimeStamp = Time.realtimeSinceStartup;
            ScenarioId = scenarioId;
            Name = name;
        }

        public override string ToString()
        {
            return $"[FrameCount={FrameCount}][RealTime(s)={TimeStamp}][ScenarioID={ScenarioId}] {Name}";
        }
    }

    public class BasicScenarioEvent : ScenarioEvent
    {
        public BasicScenarioEvent(int scenarioId, string name)
            : base(scenarioId, name) { }
    }

    public class CarScenarioLogger : Singleton<CarScenarioLogger>
    {
        private int m_BatchId = 0;
        private int m_ScenarioId;
        private StreamWriter m_StreamWriter;
        private bool m_InScenario = false;
        private List<ScenarioEvent> m_MemoryLog = new List<ScenarioEvent>();
        public bool enableFileLogging = true;

        void Awake()
        {
            if (enableFileLogging)
            {
                m_StreamWriter = new StreamWriter(Application.persistentDataPath + "\\CarLog_" + m_BatchId + ".txt");
                m_ScenarioId = 0;
            }
        }

        public void LogScenarioEventMessage(string eventName, string logString = null, IDictionary<string, object> payload = null)
        {

            var evt = new BasicScenarioEvent(m_ScenarioId, eventName);
            if (payload != null)
            {
                foreach (var kvp in payload)
                {
                    evt[kvp.Key] = kvp.Value;
                }
            }

            m_MemoryLog?.Add(evt);
            m_StreamWriter?.WriteLine($"{evt.ToString()}: {logString})");
        }

        public void LogStartCarScenario()
        {
            ++m_ScenarioId;
            LogScenarioEventMessage("StartScenario");
            m_InScenario = true;
        }

        public void LogRouteSelection(ControlPoint currentPoint, ControlPoint towardsPoint)
        {
            var payload = new Dictionary<string, object>()
            {
                ["CurrentPointId"] = currentPoint.Id,
                ["CurrentPointPosition"] = currentPoint.Point,
                ["TowardsPointId"] = towardsPoint.Id,
                ["TowardsPointPosition"] = towardsPoint.Point,
            };

            LogScenarioEventMessage("RouteSelection",
                $"Route Selection at Point {currentPoint.Id} <{currentPoint.Point}> towards Point {towardsPoint.Id} <{towardsPoint.Point}>",
                payload);
        }

        public void LogEndCarScenario()
        {
            LogScenarioEventMessage("EndScenario");
            m_InScenario = false;
        }

        void OnDestroy()
        {
            // If we are destroying the object with an open scenario, log a close scenario event and log a debug message saying so.
            if (m_InScenario)
            {
                LogScenarioEventMessage("EndScenario");
                Debug.LogWarning("Logger was torn down without a scenario end message request");
            }

            m_StreamWriter?.Close();
        }

        public IReadOnlyList<ScenarioEvent> GetMemoryLog()
        {
            return m_MemoryLog;
        }

        public void ClearMemoryLog()
        {
            m_MemoryLog.Clear();
        }
    }
}