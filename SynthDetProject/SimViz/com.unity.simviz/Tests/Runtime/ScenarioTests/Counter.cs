using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SimViz.Scenarios;

public class Counter : MonoBehaviour
{
    public int Count { get; private set; }

    void Awake()
    {
        Count = 0;
    }

    void Start()
    {
        CarScenarioLogger.Instance.LogScenarioEventMessage("CounterFirstFrame", $"Object {gameObject.name} has started counting");
    }

    void Update()
    {
        Count++;
    }

    void OnDisable()
    {
        CarScenarioLogger.Instance?.LogScenarioEventMessage(
            "CounterStopped",
            $"Object {gameObject.name} has stopped counting at Count={Count}",
            new Dictionary<string, object>()
            {
                ["Count"] = Count
            });
    }
}
