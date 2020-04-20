using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SimViz.Content.MapElements;
using UnityEngine.SimViz.Content.Pipeline;

public class ConvertRnd : MonoBehaviour
{
    public RoadNetworkDescription rnd;
    // Start is called before the first frame update
    void Start()
    {
        RoadNetworkDescriptionToEcsSystem.staticRnd = rnd;
    }
}
