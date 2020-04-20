using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SimViz.Scenarios;

public class ReversePointsOrder : MonoBehaviour {

    public WaypointPath source;
    public WaypointPath destination;

    public void ReverseOrder()
    {
        destination.CopySegmentsFrom(source);
        destination.Reverse();
    }
}
